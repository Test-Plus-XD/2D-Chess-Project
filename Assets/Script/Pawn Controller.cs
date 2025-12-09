using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Per-pawn AI behaviour in both Chess and Standoff modes.
/// Chess mode: Weighted movement on hex grid based on AI type.
/// Standoff mode: 2D platformer AI with jumping and shooting.
[RequireComponent(typeof(Rigidbody2D))]
public class PawnController : MonoBehaviour
{
    #region Enums and Types
    // AI types available for this pawn.
    public enum AIType { Basic, Handcannon, Shotgun, Sniper }
    // Placeholder modifier for future extension.
    public enum Modifier { None }
    #endregion

    #region Chess Mode Fields
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
    #endregion

    #region Standoff Mode Fields
    [Header("Standoff Mode Settings")]
    [Tooltip("Movement speed in Standoff mode")]
    public float standoffMoveSpeed = 3f;
    [Tooltip("Jump force for AI")]
    public float jumpForce = 8f;
    [Tooltip("Ground check distance")]
    public float groundCheckDistance = 0.1f;
    [Tooltip("Layer mask for ground detection")]
    public LayerMask groundLayer;
    [Tooltip("Decision update interval (seconds)")]
    public float aiThinkInterval = 0.5f;

    // Standoff mode state
    private bool isStandoffMode = false;
    private Rigidbody2D rb;
    private bool isGrounded = false;
    private float lastThinkTime = 0f;
    private float currentMoveDirection = 0f; // -1 = left, 0 = none, 1 = right
    private Transform playerTransform;
    private Firearm firearm;
    private GunAiming gunAiming;
    private SpriteRenderer spriteRenderer;
    #endregion

    // Data-holder for neighbour candidates.
    private class Candidate { public int q, r, index; public float weight = 1f; public int distToPlayer; }

    #region Unity Lifecycle

    // Notify Checkerboard when this pawn is created/destroyed.
    private void Start()
    {
        // Attempt to pick a grid generator if not assigned.
        if (gridGenerator == null) gridGenerator = FindFirstObjectByType<HexGridGenerator>();

        // Get components
        rb = GetComponent<Rigidbody2D>();
        firearm = GetComponent<Firearm>();
        gunAiming = GetComponent<GunAiming>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Configure Rigidbody2D for Chess mode by default
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.gravityScale = 0f;
        }

        // Lightweight label for naming.
        typeLabel = this.gameObject.name.Split('(')[0].Trim();
        if (Checkerboard.Instance != null) Checkerboard.Instance.RegisterOpponent(this);
        Debug.Log($"[PawnController] Spawned {typeLabel} at {q}_{r}");

        // Find player
        FindPlayer();
    }

    private void Update()
    {
        if (isStandoffMode)
        {
            UpdateStandoffMode();
        }
    }

    private void FixedUpdate()
    {
        if (isStandoffMode)
        {
            FixedUpdateStandoffMode();
        }
    }

    private void OnDestroy()
    {
        if (Checkerboard.Instance != null) Checkerboard.Instance.DeregisterOpponent(this);
    }

    #endregion

    #region Mode Switching

    /// <summary>
    /// Switch between Chess and Standoff modes
    /// </summary>
    public void SetStandoffMode(bool standoffMode)
    {
        isStandoffMode = standoffMode;

        if (rb == null) rb = GetComponent<Rigidbody2D>();

        if (isStandoffMode)
        {
            // Configure for Standoff mode (platformer physics)
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.gravityScale = 2f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            // Configure firearm and gun aiming
            if (firearm != null)
            {
                firearm.SetStandoffMode(true);
            }
            if (gunAiming != null)
            {
                gunAiming.SetStandoffMode(true);
            }
        }
        else
        {
            // Configure for Chess mode (kinematic movement)
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.gravityScale = 0f;
                rb.velocity = Vector2.zero;
            }

            // Configure firearm and gun aiming
            if (firearm != null)
            {
                firearm.SetStandoffMode(false);
            }
            if (gunAiming != null)
            {
                gunAiming.SetStandoffMode(false);
            }
        }
    }

    #endregion

    #region Standoff Mode AI

    private void FindPlayer()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void UpdateStandoffMode()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        // Check ground status
        CheckGroundStatus();

        // AI thinking at intervals
        if (Time.time >= lastThinkTime + aiThinkInterval)
        {
            lastThinkTime = Time.time;
            MakeStandoffDecision();
        }

        // Sprite flipping
        if (spriteRenderer != null && Mathf.Abs(currentMoveDirection) > 0.1f)
        {
            spriteRenderer.flipX = currentMoveDirection < 0;
        }
    }

    private void FixedUpdateStandoffMode()
    {
        if (rb == null) return;

        // Apply horizontal movement
        rb.velocity = new Vector2(currentMoveDirection * standoffMoveSpeed, rb.velocity.y);
    }

    private void MakeStandoffDecision()
    {
        if (playerTransform == null) return;

        Vector2 toPlayer = playerTransform.position - transform.position;
        float distance = toPlayer.magnitude;

        // Decide movement and jumping based on AI type (matches Chess mode personalities)
        switch (aiType)
        {
            case AIType.Basic:
            case AIType.Shotgun:
                // Aggressive: Always move toward player (matches Chess mode behavior)
                if (distance > 1.5f)
                {
                    currentMoveDirection = Mathf.Sign(toPlayer.x);
                    TryJumpIfObstacle();
                }
                else
                {
                    currentMoveDirection = 0f;
                }
                break;

            case AIType.Handcannon:
                // Mid-range: Maintain 2-4 unit distance (matches Chess mode preference)
                if (distance > 4f)
                {
                    // Too far, move closer
                    currentMoveDirection = Mathf.Sign(toPlayer.x);
                    TryJumpIfObstacle();
                }
                else if (distance < 2f)
                {
                    // Too close, back up
                    currentMoveDirection = -Mathf.Sign(toPlayer.x);
                    TryJumpIfObstacle();
                }
                else
                {
                    // In optimal range, stop moving
                    currentMoveDirection = 0f;
                }
                break;

            case AIType.Sniper:
                // Long-range: Keep distance, move away if too close (matches Chess mode behavior)
                if (distance < 6f)
                {
                    // Too close, retreat
                    currentMoveDirection = -Mathf.Sign(toPlayer.x);
                    TryJumpIfObstacle();
                }
                else
                {
                    // Far enough, stop moving
                    currentMoveDirection = 0f;
                }
                break;
        }
    }

    private void CheckGroundStatus()
    {
        if (rb == null) return;

        // Raycast downward to check for ground
        Vector2 position = transform.position;
        RaycastHit2D hit = Physics2D.Raycast(position, Vector2.down, groundCheckDistance, groundLayer);

        isGrounded = hit.collider != null;
    }

    private void TryJumpIfObstacle()
    {
        if (!isGrounded || rb == null) return;

        // Check for obstacle ahead
        Vector2 position = transform.position;
        Vector2 checkDirection = new Vector2(currentMoveDirection, 0f);
        RaycastHit2D hit = Physics2D.Raycast(position, checkDirection, 1f, groundLayer);

        // If obstacle detected, try to jump
        if (hit.collider != null)
        {
            // Check if obstacle is jumpable (not too high)
            if (hit.point.y - position.y < 2f)
            {
                rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            }
        }

        // Also jump if at edge (to avoid falling)
        Vector2 edgeCheck = position + new Vector2(currentMoveDirection * 0.5f, -0.5f);
        RaycastHit2D edgeHit = Physics2D.Raycast(edgeCheck, Vector2.down, 1f, groundLayer);

        if (edgeHit.collider == null)
        {
            // No ground ahead, stop moving or jump gap
            float gapDistance = 2f; // Max jumpable gap
            Vector2 farCheck = position + new Vector2(currentMoveDirection * gapDistance, -0.5f);
            RaycastHit2D farHit = Physics2D.Raycast(farCheck, Vector2.down, 2f, groundLayer);

            if (farHit.collider != null)
            {
                // Ground exists after gap, jump it
                rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            }
            else
            {
                // No ground ahead, don't move forward
                currentMoveDirection = 0f;
            }
        }
    }

    #endregion

    #region Chess Mode Methods (Existing)

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
        ApplyCombinedWeights(candidates, aiType, playerQ, playerR);

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
    private void ApplyCombinedWeights(List<Candidate> candidates, AIType type, int playerQ, int playerR)
    {
        if (candidates == null || candidates.Count == 0) return;
        // Find min/max distances among candidates.
        int minDist = int.MaxValue; int maxDist = int.MinValue;
        foreach (var c in candidates) { minDist = Mathf.Min(minDist, c.distToPlayer); maxDist = Mathf.Max(maxDist, c.distToPlayer); }

        switch (type)
        {
            case AIType.Basic:
                // Like chess pawns: only move forward (down in world space), never backward (up)
                // Allowed only bottom 3 directions, and must not move upward in world space (y)
                foreach (var c in candidates) c.weight = 0f;

                // Get current tile world position
                Vector3 currentWorldPos;
                if (!TryGetTileWorldCentre(q, r, out currentWorldPos))
                {
                    // Fallback to allowing all 3 directions if can't get world pos
                    foreach (var c in candidates)
                    {
                        int dq = c.q - q; int dr = c.r - r;
                        if ((dq == 0 && dr == -1) || (dq == -1 && dr == 0) || (dq == 1 && dr == -1))
                        {
                            c.weight = 1f;
                        }
                    }
                    break;
                }

                foreach (var c in candidates)
                {
                    // Get candidate tile world position
                    Vector3 candidateWorldPos;
                    if (!TryGetTileWorldCentre(c.q, c.r, out candidateWorldPos))
                    {
                        c.weight = 0f;
                        continue;
                    }

                    // Block any move that increases y (moving backward/upward in world space)
                    if (candidateWorldPos.y > currentWorldPos.y)
                    {
                        c.weight = 0f;
                        continue;
                    }

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
                // Aggressive toward player with directional preferences
                // 4 weight: toward player (closest)
                // 3 weight: top-right and top-left
                // 2 weight: bottom-right and bottom-left
                // 1 weight: farthest from player or other directions
                foreach (var c in candidates)
                {
                    // Determine hex direction index by calculating offset
                    int dq = c.q - q;
                    int dr = c.r - r;
                    int dirIndex = -1;
                    for (int i = 0; i < 6; i++)
                    {
                        if (dirQ[i] == dq && dirR[i] == dr)
                        {
                            dirIndex = i;
                            break;
                        }
                    }

                    // Assign weights with priority
                    if (c.distToPlayer == minDist)
                    {
                        c.weight = 4f; // Closest to player (highest priority)
                    }
                    else if (c.distToPlayer == maxDist)
                    {
                        c.weight = 1f; // Farthest from player (lowest priority)
                    }
                    else if (dirIndex == 1 || dirIndex == 2) // Top-right (1,-1) or top-left (0,-1)
                    {
                        c.weight = 3f;
                    }
                    else if (dirIndex == 4 || dirIndex == 5) // Bottom-left (-1,1) or bottom-right (0,1)
                    {
                        c.weight = 2f;
                    }
                    else // Right (1,0) or left (-1,0)
                    {
                        c.weight = 1f;
                    }
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

    #endregion
}