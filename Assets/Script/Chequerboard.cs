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
    public PlayerController playerController;
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
    // The method is safeguarded so multiple calls while opponents are moving are ignored.
    public void OnPlayerMoved()
    {
        // Prevent re-triggering opponents' turn if they're already taking their turn
        if (!playerTurn) return;
        // Start the opponent turn sequence as a coroutine
        StartCoroutine(OpponentsTurnRoutine());
    }

    // Coroutine that orchestrates all opponents' moves in sequence
    // Each opponent gets a turn to: fire → move (1 or 2 times depending on Fleet modifier) → yield to next
    // Then Reflexive modifiers recalculate aim, and control returns to player
    private IEnumerator OpponentsTurnRoutine()
    {
        // Lock player input while opponents are moving
        playerTurn = false;
        // Ensure opponent list is fresh (in case pawns were spawned since last refresh)
        RefreshOpponents();
        // Reset Moved flags before starting so each opponent's Moved flag starts false
        foreach (var opp in opponents) if (opp != null) opp.ResetMovedFlag();

        // Read authoritative player coords at the start of opponent phase (get current position)
        if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
        int playerQ = (playerController != null) ? playerController.q : 0;
        int playerR = (playerController != null) ? playerController.r : 0;
        Debug.Log($"[Checkerboard] Opponents turn starting. Player at {playerQ}_{playerR}. Opponent count={opponents.Count}");

        // Iterate over a snapshot to prevent issues if opponents are destroyed during movement
        var snapshot = new List<PawnController>(opponents);
        // Build occupied set from current opponent positions (prevents 2 opponents choosing same tile)
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        foreach (var p in snapshot)
        {
            if (p == null) continue;
            occupied.Add(new Vector2Int(p.q, p.r));
        }

        foreach (var opp in snapshot)
        {
            if (opp == null) continue;

            // Fire weapon at turn start (each opponent type has unique firing pattern)
            WeaponSystem weaponSystem = opp.GetComponent<WeaponSystem>();
            if (weaponSystem != null && opp.aiType != PawnController.AIType.Basic)
            {
                // Fire once per turn (Handcannon/Shotgun/Sniper, not Basic)
                weaponSystem.FireChessMode();
                yield return new WaitForSeconds(0.2f); // Small visual delay after firing
            }

            // Fleet modifier grants extra move: move twice but only shoot once (already shot above)
            int movesThisTurn = opp.HasExtraMove() ? 2 : 1;

            for (int moveIndex = 0; moveIndex < movesThisTurn; moveIndex++)
            {
                // Free current pawn position so it can rechoose or move to a new tile
                occupied.Remove(new Vector2Int(opp.q, opp.r));
                // Ask pawn AI to choose best move target while respecting occupied tiles
                int tgtQ, tgtR;
                bool chosen = opp.ChooseMoveTarget(playerQ, playerR, occupied, out tgtQ, out tgtR);
                if (!chosen)
                {
                    // No valid move available (all neighbors occupied) - pawn skips remaining moves
                    occupied.Add(new Vector2Int(opp.q, opp.r));
                    break; // Stop extra moves if no valid target
                }
                // Reserve target so subsequent opponents don't choose this tile
                occupied.Add(new Vector2Int(tgtQ, tgtR));
                // Execute movement coroutine (smooth animation + capture check)
                bool started = opp.ExecuteAIMoveTo(tgtQ, tgtR);
                if (!started)
                {
                    // Move failed for some reason - free the reservation
                    occupied.Remove(new Vector2Int(tgtQ, tgtR));
                    occupied.Add(new Vector2Int(opp.q, opp.r));
                    break; // Stop extra moves if move failed
                }
                // Wait for pawn to complete movement animation (with timeout to prevent hangs)
                float timer = 0f; float timeout = 2f;
                while (!opp.Moved && timer < timeout)
                {
                    timer += Time.deltaTime;
                    yield return null;
                }
                // Reset Moved flag for next move in Fleet turn
                if (moveIndex < movesThisTurn - 1)
                {
                    opp.ResetMovedFlag();
                }
                // Small delay between moves for visual pacing
                if (opponentMoveDelay > 0f) yield return new WaitForSeconds(opponentMoveDelay);
            }
        }

        // After all opponents move, handle Reflexive modifier (recalculate aim at new player position)
        // Reflexive gives opponent tactical advantage by allowing mid-turn aim adjustment
        foreach (var opp in opponents)
        {
            if (opp == null) continue;
            if (opp.ShouldRecalculateAimAfterPlayerMove())
            {
                WeaponSystem weapon = opp.GetComponent<WeaponSystem>();
                if (weapon != null)
                {
                    // Recalculate aiming direction now that player has moved
                    weapon.RecalculateAim();
                }
            }
        }
        // Opponents done - unlock player input for next turn
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