using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
/// Coordinates turns between the player and opponent pawns.
/// Caches opponent PawnController instances and the player's PlayerController instance.
/// Call Checkerboard.Instance.OnPlayerMoved(playerQ, playerR) after the player's move completes.
public class Checkerboard : MonoBehaviour
{
    // Singleton instance for easy access.
    public static Checkerboard Instance { get; private set; }
    // Cached list of opponent PawnController instances.
    private List<PawnController> opponents = new List<PawnController>();
    // Cached reference to the player's PlayerController (optional).
    private PlayerController playerController;
    // If true the player may act; set to false while opponents are moving.
    private bool playerTurn = true;
    // Delay between opponent moves for visual pacing.
    public float opponentMoveDelay = 0.12f;
    // If true the Checkerboard will automatically discover pawns at Start.
    public bool autoDiscover = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this.gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (autoDiscover) RefreshOpponents();
        if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
    }

    // Refresh the cached opponent list (call after spawning new opponent pawns).
    public void RefreshOpponents()
    {
        opponents.Clear();
        var found = FindObjectsByType<PawnController>(FindObjectsSortMode.InstanceID);
        foreach (var p in found) if (p != null) opponents.Add(p);
    }

    // Register an opponent (call from PawnSpawner when you spawn a pawn).
    public void RegisterOpponent(PawnController polygonCollider)
    {
        if (polygonCollider == null) return;
        if (!opponents.Contains(polygonCollider)) opponents.Add(polygonCollider);
    }

    // De-register an opponent (call from PawnController.OnDestroy()).
    public void DeregisterOpponent(PawnController polygonCollider)
    {
        if (polygonCollider == null) return;
        opponents.Remove(polygonCollider);
    }

    // Registration for the PlayerController.
    public void RegisterPlayer(PlayerController polygonCollider) { playerController = polygonCollider; }

    // Returns true if it is currently the player's turn.
    public bool IsPlayerTurn() { return playerTurn; }

    // Main entry: call this after the player finishes a move to trigger opponents' turn.
    // The method is safe-guarded so multiple calls while opponents are moving are ignored.
    public void OnPlayerMoved()
    {
        if (!playerTurn) return; // Ignore if opponents are already moving
        StartCoroutine(OpponentsTurnRoutine());
    }

    // Coroutine that asks each opponent to move once (in sequence) and then returns control to player.
    // Opponents turn routine now reads player's coords directly from the registered PlayerController
    //private IEnumerator OpponentsTurnRoutine(int playerQ, int playerR)
    private IEnumerator OpponentsTurnRoutine()
    {
        playerTurn = false; // Block player actions while opponents move
        // Ensure opponent list is fresh (in case pawns were spawned since last refresh).
        RefreshOpponents();
        // Reset Moved flags before starting.
        foreach (var opp in opponents) if (opp != null) opp.ResetMovedFlag();

        // Read authoritative player coords at the start of the opponent phase
        if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
        int playerQ = (playerController != null) ? playerController.q : 0;
        int playerR = (playerController != null) ? playerController.r : 0;
        Debug.Log($"[Checkerboard] Opponents turn starting. Player at {playerQ}_{playerR}. Opponent count={opponents.Count}");

        // Iterate over a snapshot so modifications(like captures) don’t break the loop
        var snapshot = new List<PawnController>(opponents);
        // Build initial occupied set from current opponent positions so two opponents don't aim for the same tile.
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        foreach (var p in snapshot)
        {
            if (p == null) continue;
            occupied.Add(new Vector2Int(p.q, p.r));
        }

        foreach (var opp in snapshot)
        {
            if (opp == null) continue;
            // Make this pawn free to choose (remove its current pos so it can rechoose it or move).
            occupied.Remove(new Vector2Int(opp.q, opp.r));
            // Ask pawn to choose a target while blocking occupied tiles.
            int tgtQ, tgtR;
            bool chosen = opp.ChooseMoveTarget(playerQ, playerR, occupied, out tgtQ, out tgtR);
            if (!chosen)
            {
                // No valid target (all neighbors blocked); re-reserve the pawn's current tile to avoid others taking it.
                occupied.Add(new Vector2Int(opp.q, opp.r));
                continue;
            }
            // Reserve target so subsequent opponents can't pick it.
            occupied.Add(new Vector2Int(tgtQ, tgtR));
            // Tell the pawn to move to the chosen reserved target.
            bool started = opp.ExecuteAIMoveTo(tgtQ, tgtR);
            if (!started)
            {
                // If move failed for some reason, free the reservation so others might take it.
                occupied.Remove(new Vector2Int(tgtQ, tgtR));
                occupied.Add(new Vector2Int(opp.q, opp.r));
                continue;
            }
            // Wait for the pawn to complete its move (same as your old code).
            float timer = 0f; float timeout = 2f;
            while (!opp.Moved && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }
            // Small delay for visuals.
            if (opponentMoveDelay > 0f) yield return new WaitForSeconds(opponentMoveDelay);
        }
        // Opponents done, allow player to act again.
        playerTurn = true;
    }

    // Return a copy of current opponent axial coordinates as Vector2Int list.
    public List<Vector2Int> GetOpponentCoords()
    {
        List<Vector2Int> coords = new List<Vector2Int>();
        for (int i = opponents.Count - 1; i >= 0; i--)
        {
            var p = opponents[i];
            if (p == null) continue;
            coords.Add(new Vector2Int(p.q, p.r));
        }
        return coords;
    }

    // Return the list of opponent PawnController references (read-only copy).
    public IReadOnlyList<PawnController> GetOpponentControllers()
    {
        return opponents.AsReadOnly();
    }

    // Try to parse axial coords from object name "..._q_r". Returns (0,0) if parsing fails.
    private Vector2Int ParseAxialFromName(string objName)
    {
        if (string.IsNullOrEmpty(objName)) return Vector2Int.zero;
        string[] parts = objName.Split('_');
        if (parts.Length >= 3)
        {
            if (int.TryParse(parts[parts.Length - 2], out int q) && int.TryParse(parts[parts.Length - 1], out int r))
            {
                return new Vector2Int(q, r);
            }
        }
        return Vector2Int.zero;
    }
}