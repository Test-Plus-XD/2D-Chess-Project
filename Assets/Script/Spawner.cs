using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Unified spawning system for both player and opponent pawns.
// Consolidates PlayerSpawner and PawnSpawner functionality.
public class Spawner : MonoBehaviour
{
    [Header("Grid Reference")]
    [Tooltip("Reference to the HexGridGenerator")]
    // Hex grid generator for board layout and tile management.
    public HexGridGenerator gridGenerator;
    [Tooltip("Reference to the Checkerboard")]
    // Turn coordinator for player/opponent registration.
    public Checkerboard checkerboard;

    [Header("Player Spawning")]
    [Tooltip("Player pawn prefab")]
    // Prefab to instantiate for the player pawn.
    public GameObject playerPawnPrefab;
    [Tooltip("Parent for player pawn")]
    // Parent transform for the spawned player pawn.
    public Transform playerPawnParent;
    [Tooltip("Vertical offset for player spawn")]
    // Vertical offset adjustment for player spawn position.
    public float playerVerticalOffset = 0f;

    [Header("Opponent Prefabs")]
    [Tooltip("Basic pawn prefab")]
    // Prefab for basic opponent pawns.
    public GameObject pawnPrefab;
    [Tooltip("Handcannon pawn prefab")]
    // Prefab for handcannon opponent pawns.
    public GameObject handcannonPrefab;
    [Tooltip("Shotgun pawn prefab")]
    // Prefab for shotgun opponent pawns.
    public GameObject shotgunPrefab;
    [Tooltip("Sniper pawn prefab")]
    // Prefab for sniper opponent pawns.
    public GameObject sniperPrefab;

    [Header("Opponent Spawn Counts")]
    [Tooltip("Number of Basic pawns")]
    // Number of basic pawns to spawn.
    public int pawnCount = 0;
    [Tooltip("Number of Handcannon pawns")]
    // Number of handcannon pawns to spawn.
    public int handcannonCount = 0;
    [Tooltip("Number of Shotgun pawns")]
    // Number of shotgun pawns to spawn.
    public int shotgunCount = 0;
    [Tooltip("Number of Sniper pawns")]
    // Number of sniper pawns to spawn.
    public int sniperCount = 0;

    [Header("Opponent Spawning Settings")]
    [Tooltip("Parent for spawned opponent pawns")]
    // Parent transform for all spawned opponent pawns.
    public Transform opponentSpawnParent;
    [Tooltip("Allow multiple pawns on same tile")]
    // Whether multiple pawns can spawn on the same tile.
    public bool allowStacking = false;

    [Header("General Settings")]
    [Tooltip("Allow scene-wide collider search")]
    // Whether to search entire scene for colliders if needed.
    public bool allowSceneWideColliderSearch = true;

    #region Private Fields

    private class TileCandidate
    {
        public Vector2Int coordinate;
        public int r;
        public float weight;
    }

    private int opponentHP = 1;

    #endregion

    #region Unity Lifecycle

    private IEnumerator Start()
    {
        if (gridGenerator == null) gridGenerator = FindFirstObjectByType<HexGridGenerator>();
        if (checkerboard == null) checkerboard = FindFirstObjectByType<Checkerboard>();

        // Wait for grid to be generated (ensure tiles exist before spawning)
        yield return new WaitForEndOfFrame();

        // Wait until the grid has generated tiles (check parent container has children)
        int waitFrames = 0;
        const int maxWaitFrames = 60; // Maximum frames to wait
        Transform tileParent = gridGenerator != null
            ? (gridGenerator.parentContainer ?? gridGenerator.transform)
            : null;

        while (tileParent != null && tileParent.childCount == 0 && waitFrames < maxWaitFrames)
        {
            yield return null;
            waitFrames++;
        }

        // Only spawn if not being managed by GameManager (GameManager will call SpawnAll directly)
        if (GameManager.Instance == null || GameManager.Instance.CurrentState == GameManager.GameState.ChessMode)
        {
            SpawnAll();
        }
    }

    #endregion

    #region Public Methods - Spawning

    public void SpawnAll()
    {
        SpawnPlayer();
        SpawnOpponents();
    }

    public void SpawnPlayer()
    {
        if (gridGenerator == null)
        {
            Debug.LogError("[Spawner] No HexGridGenerator found.");
            return;
        }

        if (checkerboard == null)
        {
            Debug.LogError("[Spawner] No Checkerboard found.");
            return;
        }

        if (playerPawnPrefab == null)
        {
            Debug.LogWarning("[Spawner] No player pawn prefab assigned.");
            return;
        }

        Transform tileParent = gridGenerator.parentContainer == null
            ? gridGenerator.transform
            : gridGenerator.parentContainer;

        List<PolygonCollider2D> colliders = new List<PolygonCollider2D>();
        for (int i = 0; i < tileParent.childCount; i++)
        {
            Transform child = tileParent.GetChild(i);
            PolygonCollider2D polygonCollider = child.GetComponent<PolygonCollider2D>();
            if (polygonCollider != null) colliders.Add(polygonCollider);
        }

        if (colliders.Count == 0 && allowSceneWideColliderSearch)
        {
            PolygonCollider2D[] all = Object.FindObjectsByType<PolygonCollider2D>(FindObjectsSortMode.None);
            foreach (var polygonCollider in all)
            {
                if (polygonCollider.gameObject.name.StartsWith("Hex_")) colliders.Add(polygonCollider);
            }
        }

        if (colliders.Count == 0)
        {
            Debug.LogWarning("[Spawner] No tiles found for player spawn.");
            SpawnPlayerUsingTransformScan(tileParent);
            return;
        }

        // Find bottom-right tile
        float bestBottom = float.PositiveInfinity;
        foreach (var polygonCollider in colliders)
        {
            float bMinY = polygonCollider.bounds.min.y;
            if (bMinY < bestBottom) bestBottom = bMinY;
        }

        const float eps = 0.0001f;
        PolygonCollider2D chosenCollider = null;
        float bestCenterX = float.NegativeInfinity;
        foreach (var polygonCollider in colliders)
        {
            if (polygonCollider.bounds.min.y <= bestBottom + eps)
            {
                float cx = polygonCollider.bounds.center.x;
                if (cx > bestCenterX)
                {
                    bestCenterX = cx;
                    chosenCollider = polygonCollider;
                }
            }
        }

        if (chosenCollider == null)
        {
            Debug.LogError("[Spawner] Failed to find tile for player spawn.");
            return;
        }

        Vector3 spawnPos = new Vector3(
            chosenCollider.bounds.center.x,
            chosenCollider.bounds.center.y + playerVerticalOffset,
            0f
        );

        // Parse tile coordinates before instantiation
        string tileName = chosenCollider.gameObject.name;
        string[] parts = tileName.Split('_');
        int q = 0, r = 0;
        if (parts.Length >= 3)
        {
            int.TryParse(parts[1], out q);
            int.TryParse(parts[2], out r);
        }

        // Instantiate player at correct position immediately
        GameObject playerPawn = playerPawnParent == null
            ? Instantiate(playerPawnPrefab, spawnPos, Quaternion.identity)
            : Instantiate(playerPawnPrefab, spawnPos, Quaternion.identity, playerPawnParent);

        // Initialize player controller with coordinates
        PlayerController playerController = playerPawn.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.Initialise(q, r, gridGenerator, checkerboard);
        }

        Debug.Log($"[Spawner] Player spawned at tile {q}_{r} position {spawnPos}");
    }

    public void SpawnOpponents()
    {
        if (gridGenerator == null)
        {
            gridGenerator = FindFirstObjectByType<HexGridGenerator>();
            if (gridGenerator == null)
            {
                Debug.LogError("[Spawner] No HexGridGenerator found.");
                return;
            }
        }

        if (opponentSpawnParent == null) opponentSpawnParent = this.transform;

        List<TileCandidate> candidates = BuildUpperTileCandidates();
        if (candidates.Count == 0)
        {
            Debug.LogWarning("[Spawner] No upper tiles found for opponent spawning.");
        }

        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

        SpawnType(pawnPrefab, PawnController.AIType.Basic, pawnCount, candidates, occupied);
        SpawnType(handcannonPrefab, PawnController.AIType.Handcannon, handcannonCount, candidates, occupied);
        SpawnType(shotgunPrefab, PawnController.AIType.Shotgun, shotgunCount, candidates, occupied);
        SpawnType(sniperPrefab, PawnController.AIType.Sniper, sniperCount, candidates, occupied);
    }

    #endregion

    #region Public Methods - Configuration

    public void SetPawnCounts(int basic, int handcannon, int shotgun, int sniper)
    {
        pawnCount = Mathf.Max(0, basic);
        handcannonCount = Mathf.Max(0, handcannon);
        shotgunCount = Mathf.Max(0, shotgun);
        sniperCount = Mathf.Max(0, sniper);
    }

    public void SetOpponentHP(int hp)
    {
        opponentHP = Mathf.Max(1, hp);
    }

    public void SetPawnCount(int n) { pawnCount = Mathf.Max(0, n); }
    public void SetHandcannonCount(int n) { handcannonCount = Mathf.Max(0, n); }
    public void SetShotgunCount(int n) { shotgunCount = Mathf.Max(0, n); }
    public void SetSniperCount(int n) { sniperCount = Mathf.Max(0, n); }

    public void ClearAllPawns()
    {
        // Clear player
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (Application.isPlaying) Destroy(p.gameObject);
            else DestroyImmediate(p.gameObject);
        }

        // Clear opponents
        PawnController[] pawns = FindObjectsByType<PawnController>(FindObjectsSortMode.None);
        foreach (var pawn in pawns)
        {
            if (Application.isPlaying) Destroy(pawn.gameObject);
            else DestroyImmediate(pawn.gameObject);
        }
    }

    #endregion

    #region Private Methods - Player Spawning

    private void SpawnPlayerUsingTransformScan(Transform tileParent)
    {
        Transform chosen = null;
        float minY = float.PositiveInfinity;
        float bestX = float.NegativeInfinity;

        for (int i = 0; i < tileParent.childCount; i++)
        {
            Transform t = tileParent.GetChild(i);
            Vector3 pos = t.position;
            if (pos.y < minY - 0.0001f)
            {
                minY = pos.y;
                bestX = pos.x;
                chosen = t;
            }
            else if (Mathf.Approximately(pos.y, minY))
            {
                if (pos.x > bestX)
                {
                    bestX = pos.x;
                    chosen = t;
                }
            }
        }

        if (chosen == null)
        {
            Debug.LogError("[Spawner] Fallback transform scan failed.");
            return;
        }

        Vector3 spawnPos = chosen.position + new Vector3(0f, playerVerticalOffset, 0f);
        GameObject playerPawn = playerPawnParent == null
            ? Instantiate(playerPawnPrefab, spawnPos, Quaternion.identity)
            : Instantiate(playerPawnPrefab, spawnPos, Quaternion.identity, playerPawnParent);

        string[] parts = chosen.name.Split('_');
        int q = 0, r = 0;
        if (parts.Length >= 3 && int.TryParse(parts[1], out q) && int.TryParse(parts[2], out r))
        {
            PlayerController playerController = playerPawn.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.Initialise(q, r, gridGenerator, checkerboard);
            }
        }
    }

    #endregion

    #region Private Methods - Opponent Spawning

    private void SpawnType(GameObject prefab, PawnController.AIType aiType, int count,
        List<TileCandidate> candidates, HashSet<Vector2Int> occupied)
    {
        if (prefab == null || count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            Vector2Int chosen = ChooseWeightedTile(candidates, occupied, allowStacking);

            if (chosen == Vector2Int.zero && candidates.Count == 0)
            {
                Debug.LogWarning("[Spawner] No tile available for spawning.");
                break;
            }

            // Get tile world position FIRST before instantiating
            Vector3 spawnPosition = Vector3.zero;
            if (!TryGetTileWorldCentre(chosen.x, chosen.y, out spawnPosition))
            {
                Debug.LogWarning($"[Spawner] Could not find tile position for {chosen.x}_{chosen.y}, using origin.");
            }

            // Instantiate at the correct position immediately
            GameObject gameObject = Instantiate(prefab, spawnPosition, Quaternion.identity, opponentSpawnParent);

            PawnController pawnController = gameObject.GetComponent<PawnController>();
            if (pawnController == null) pawnController = gameObject.AddComponent<PawnController>();

            pawnController.gridGenerator = gridGenerator;
            pawnController.aiType = aiType;
            // Set coords without snapping since we already positioned correctly
            pawnController.q = chosen.x;
            pawnController.r = chosen.y;

            // Initialize opponent HP from the configured opponentHP value
            PawnHealth pawnHealth = gameObject.GetComponent<PawnHealth>();
            if (pawnHealth == null) pawnHealth = gameObject.AddComponent<PawnHealth>();
            pawnHealth.SetOpponentHP(opponentHP);

            gameObject.name = $"Opponent {prefab.name}: {chosen.x}_{chosen.y}";

            if (!allowStacking) occupied.Add(chosen);
        }
    }

    private List<TileCandidate> BuildUpperTileCandidates()
    {
        List<TileCandidate> list = new List<TileCandidate>();

        Transform parent = gridGenerator.parentContainer == null
            ? gridGenerator.transform
            : gridGenerator.parentContainer;

        int minR = int.MaxValue;
        int maxR = int.MinValue;
        List<Transform> tileTransforms = new List<Transform>();

        for (int i = 0; i < parent.childCount; i++)
        {
            var t = parent.GetChild(i);
            tileTransforms.Add(t);
            string[] parts = t.name.Split('_');
            if (parts.Length >= 3 && int.TryParse(parts[2], out int rVal))
            {
                minR = Mathf.Min(minR, rVal);
                maxR = Mathf.Max(maxR, rVal);
            }
        }

        if (tileTransforms.Count == 0) return list;

        int middleR = Mathf.FloorToInt((minR + maxR) / 2f);

        foreach (var t in tileTransforms)
        {
            string[] parts = t.name.Split('_');
            if (parts.Length < 3) continue;
            if (!int.TryParse(parts[1], out int qVal)) continue;
            if (!int.TryParse(parts[2], out int rVal)) continue;
            if (rVal <= middleR) continue;

            TileCandidate c = new TileCandidate();
            c.coordinate = new Vector2Int(qVal, rVal);
            c.r = rVal;
            c.weight = (float)(rVal - middleR + 1);
            list.Add(c);
        }

        return list;
    }

    private Vector2Int ChooseWeightedTile(List<TileCandidate> candidates, HashSet<Vector2Int> occupied, bool allowReuse)
    {
        if (candidates == null || candidates.Count == 0) return Vector2Int.zero;

        float sum = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!allowReuse && occupied.Contains(candidates[i].coordinate)) continue;
            sum += candidates[i].weight;
        }

        if (sum <= 0f)
        {
            if (allowReuse)
            {
                int idx = Random.Range(0, candidates.Count);
                return candidates[idx].coordinate;
            }
            return Vector2Int.zero;
        }

        float pick = Random.Range(0f, sum);
        float acc = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (!allowReuse && occupied.Contains(candidates[i].coordinate)) continue;
            acc += candidates[i].weight;
            if (pick <= acc) return candidates[i].coordinate;
        }

        return candidates[candidates.Count - 1].coordinate;
    }

    private bool TryGetTileWorldCentre(int qa, int ra, out Vector3 centre)
    {
        centre = Vector3.zero;
        Transform parent = gridGenerator.parentContainer == null
            ? gridGenerator.transform
            : gridGenerator.parentContainer;

        Transform tile = parent.Find($"Hex_{qa}_{ra}");
        if (tile == null) return false;

        PolygonCollider2D polygonCollider = tile.GetComponent<PolygonCollider2D>();
        if (polygonCollider != null)
        {
            Vector3 c = polygonCollider.bounds.center;
            centre = new Vector3(c.x, c.y, 0f);
            return true;
        }

        centre = tile.position;
        return true;
    }

    #endregion
}
