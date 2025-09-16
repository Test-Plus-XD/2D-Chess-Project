using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Per-pawn AI behaviour, combined weighting logic and an executable AI move.
/// Each pawn tracks its own axial coordinates q,r and sets public bool Moved when finished.
public class PawnController : MonoBehaviour
{
    // AI types available for this pawn.
    public enum AIType { Basic, Handcannon, Shotgun, Sniper }
    // Placeholder modifier for future extension.
    public enum Modifier { None }

    // Public fields.
    public AIType aiType = AIType.Basic;
    public Modifier modifier = Modifier.None;
    public HexGridGenerator gridGenerator;
    public float aiMoveDuration = 0.12f; // Duration of AI movement animation.
    public bool Moved = false; // Set true after pawn completes its AI move.
    public int q, r; // Axial coordinates tracked by this pawn.

    // Neighbour axial deltas (must match your project's convention).
    private readonly int[] dirQ = { 1, 1, 0, -1, -1, 0 };
    private readonly int[] dirR = { 0, -1, -1, 0, 1, 1 };

    // Keep the original prefab/type label for nicer names (optional).
    private string typeLabel = "";

    // Data-holder for neighbour candidates.
    private class Candidate { public int q, r, index; public float weight = 1f; public int distToPlayer; }

    // Notify Checkerboard when this pawn is created/destroyed.
    private void Start()
    {
        // Attempt to pick a grid generator if not assigned.
        if (gridGenerator == null) gridGenerator = FindFirstObjectByType<HexGridGenerator>();
        // Lightweight label for naming.
        typeLabel = this.gameObject.name.Split('(')[0].Trim();
        if (Checkerboard.Instance != null) Checkerboard.Instance.RegisterOpponent(this);
        Debug.Log($"[PawnController] Spawned {typeLabel} at {q}_{r}");
    }
    private void OnDestroy() { if (Checkerboard.Instance != null) Checkerboard.Instance.DeregisterOpponent(this); }

    // Choose a neighbor target using this pawn's q,r and the given player's q,r.
    // Returns true and out coords if a target was chosen.
    // Respects occupied/reserved tiles, occupied may be null; player tile (playerQ,playerR) is always allowed (so capture works).
    public bool ChooseMoveTarget(int playerQ, int playerR, HashSet<Vector2Int> occupied, out int outQ, out int outR)
    {
        outQ = 0; outR = 0;
        if (gridGenerator == null)
        {
            Debug.LogWarning("[PawnController] ChooseMoveTarget: gridGenerator is null.");
            return false;
        }

        // Build neighbour candidates that actually exist as tiles and exclude the player's tile.
        List<Candidate> candidates = new List<Candidate>();
        for (int i = 0; i < 6; i++)
        {
            int nq = q + dirQ[i];
            int nr = r + dirR[i];
            //if (nq == playerQ && nr == playerR) continue; // Exclude the player's tile explicitly so opponents never try to occupy the player.
            if (!TileExists(nq, nr)) continue;
            Candidate c = new Candidate() { q = nq, r = nr, index = i };
            candidates.Add(c);
        }
        if (candidates.Count == 0) return false;
        // Compute distances to player for all candidates (axial distance via cube coords).
        foreach (var c in candidates) c.distToPlayer = AxialDistance(c.q, c.r, playerQ, playerR);
        // Apply the single combined weight logic for this pawn's AI type.
        ApplyCombinedWeights(candidates, aiType);

        // Debug: log candidate list and weights for transparency.
        string debugLine = $"[PawnController] {typeLabel} @{q}_{r} Candidates:";
        foreach (var c in candidates) debugLine += $" ({c.q}_{c.r}:w={c.weight},d={c.distToPlayer},idx={c.index})";
        Debug.Log(debugLine);

        // Remove candidates that are occupied by other opponents, unless it's the player's tile (allow capture).
        List<Candidate> allowed = new List<Candidate>();
        foreach (var c in candidates)
        {
            var coord = new Vector2Int(c.q, c.r);
            bool isPlayerTile = (c.q == playerQ && c.r == playerR);
            if (occupied != null && occupied.Contains(coord) && !isPlayerTile) continue;
            if (c.weight <= 0f) continue;
            allowed.Add(c);
        }
        if (allowed.Count == 0) return false;
        // Weighted random selection among allowed.
        float sum = 0f;
        foreach (var c in allowed) sum += c.weight;
        float pick = Random.Range(0f, sum);
        float acc = 0f;
        foreach (var c in allowed)
        {
            acc += c.weight;
            if (pick <= acc)
            {
                outQ = c.q; outR = c.r;
                Debug.Log($"[PawnController] {typeLabel} @{q}_{r} chose target {outQ}_{outR} (pick={pick:F3} sum={sum:F3}).");
                return true;
            }
        }
        // Fallback
        outQ = allowed[allowed.Count - 1].q; outR = allowed[allowed.Count - 1].r;
        Debug.Log($"[PawnController] {typeLabel} @{q}_{r} fallback chose {outQ}_{outR}.");
        return true;
    }

    // Combined weight application for all AI types in one place.
    private void ApplyCombinedWeights(List<Candidate> candidates, AIType type)
    {
        if (candidates == null || candidates.Count == 0) return;
        // Find min/max distances among candidates.
        int minDist = int.MaxValue; int maxDist = int.MinValue;
        foreach (var c in candidates) { minDist = Mathf.Min(minDist, c.distToPlayer); maxDist = Mathf.Max(maxDist, c.distToPlayer); }

        switch (type)
        {
            case AIType.Basic:
                // Allowed only bottom, bottom-right and bottom-left relative to pawn.
                // Determine allowed directions by comparing dirQ/dirR (robust against index reordering).
                foreach (var c in candidates) c.weight = 0f;
                foreach (var c in candidates)
                {
                    int dq = c.q - q; int dr = c.r - r;
                    // bottom = (0,-1); bottom-left = (-1,0); bottom-right = (1,-1)
                    if ((dq == 0 && dr == -1) || (dq == -1 && dr == 0) || (dq == 1 && dr == -1))
                    {
                        c.weight = 1f;
                    }
                }
                // Give extra weight to the allowed tile(s) that are closest to the player.
                float bestDist = float.PositiveInfinity;
                foreach (var c in candidates) if (c.weight > 0f) bestDist = Mathf.Min(bestDist, c.distToPlayer);
                foreach (var c in candidates) if (c.weight > 0f && c.distToPlayer == bestDist) c.weight = 5f;
                break;

            case AIType.Handcannon:
                // All 6 allowed; closest to player has weight 3, others 1.
                foreach (var c in candidates) c.weight = (c.distToPlayer == minDist) ? 3f : 1f;
                break;

            case AIType.Shotgun:
                // All 6 allowed; closest weight 3, farthest weight 0, others weight1.
                foreach (var c in candidates)
                {
                    if (c.distToPlayer == minDist) c.weight = 3f;
                    else if (c.distToPlayer == maxDist) c.weight = 0f;
                    else c.weight = 1f;
                }
                break;

            case AIType.Sniper:
                // All 6 allowed; farthest weight 4, closest weight 1, others weight 2.
                foreach (var c in candidates)
                {
                    if (c.distToPlayer == maxDist) c.weight = 4f;
                    else if (c.distToPlayer == minDist) c.weight = 1f;
                    else c.weight = 2f;
                }
                break;
        }
    }

    // Execute this pawn's AI move using the pawn's own q,r and the player's q,r.
    // Returns true if a move was started.
    public bool ExecuteAIMoveTo(int targetQ, int targetR)
    {
        if (gridGenerator == null)
        {
            Debug.LogWarning("[PawnController] ExecuteAIMoveTo: gridGenerator is null.");
            return false;
        }
        Vector3 world;
        if (!TryGetTileWorldCentre(targetQ, targetR, out world)) return false;
        // Start coroutine to move and update q,r after movement.
        StartCoroutine(AIMoveCoroutine(world, targetQ, targetR));
        return true;
    }

    // Coroutine that moves the pawn and updates its tracked q,r when complete.
    private IEnumerator AIMoveCoroutine(Vector3 targetWorld, int targetQ, int targetR)
    {
        Moved = false;
        int fromQ = q; int fromR = r;
        Vector3 start = transform.position;
        float t = 0f;
        if ((targetWorld - start).sqrMagnitude < 0.0001f)
        {
            transform.position = targetWorld;
            q = targetQ; r = targetR; Moved = true; UpdateNameWithCoords();
            Debug.Log($"[PawnController] {typeLabel} moved (instant) {fromQ}_{fromR} -> {q}_{r}");
            yield break;
        }
        while (t < aiMoveDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / aiMoveDuration);
            transform.position = Vector3.Lerp(start, targetWorld, a);
            yield return null;
        }

        transform.position = targetWorld;
        q = targetQ; r = targetR; Moved = true; UpdateNameWithCoords();

        // After this pawn finishes moving, tries to capture player.
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            PlayerPawn playerPawn = player.GetComponent<PlayerPawn>();
            PlayerController playerController = player.GetComponent<PlayerController>();
            if (playerPawn != null && playerController.q == q && playerController.r == r)
            {
                // Deal damage equal to this Player's MaxHP (OpponentPawn component).
                playerPawn.TakeDamage(playerPawn.MaxHP, $"Opponent_{aiType}");
            }
        }

        Debug.Log($"[PawnController] {typeLabel} moved {fromQ}_{fromR} -> {q}_{r}");
    }

    // Set initial coordinates and optionally reposition pawn to tile centre.
    public void SetCoordsAndSnap(int startQ, int startR)
    {
        q = startQ; r = startR;
        Vector3 world;
        if (TryGetTileWorldCentre(q, r, out world)) transform.position = world;
        UpdateNameWithCoords();
        Debug.Log($"[PawnController] {typeLabel} initialised at {q}_{r}");
    }

    // Utility: update GameObject name to include axial coords for debugging.
    private void UpdateNameWithCoords()
    {
        // Use aiType for readable label
        this.gameObject.name = $"Opponent {aiType}: {q}_{r}";
    }

    // Utility: check tile existence under the generator parent container.
    private bool TileExists(int qa, int ra)
    {
        Transform parent = gridGenerator.parentContainer == null ? gridGenerator.transform : gridGenerator.parentContainer;
        return parent.Find($"Hex_{qa}_{ra}") != null;
    }

    // Utility: get tile world centre using PolygonCollider2D if available.
    private bool TryGetTileWorldCentre(int qa, int ra, out Vector3 centre)
    {
        centre = Vector3.zero;
        Transform parent = gridGenerator.parentContainer == null ? gridGenerator.transform : gridGenerator.parentContainer;
        Transform tile = parent.Find($"Hex_{qa}_{ra}");
        if (tile == null) return false;
        PolygonCollider2D pc = tile.GetComponent<PolygonCollider2D>();
        if (pc != null)
        {
            Vector3 c = pc.bounds.center;
            centre = new Vector3(c.x, c.y, transform.position.z);
            return true;
        }
        centre = tile.position; return true;
    }

    // Axial distance using cube coords: distance = max(|dx|, |dy|, |dz|).
    private int AxialDistance(int q1, int r1, int q2, int r2)
    {
        int x1 = q1; int z1 = r1; int y1 = -x1 - z1;
        int x2 = q2; int z2 = r2; int y2 = -x2 - z2;
        return Mathf.Max(Mathf.Abs(x1 - x2), Mathf.Abs(y1 - y2), Mathf.Abs(z1 - z2));
    }

    // Helper to reset Moved (used by Checkerboard between turns).
    public void ResetMovedFlag() { Moved = false; }
}