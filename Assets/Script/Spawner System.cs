using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Unified spawning system for both player and opponent pawns.
// Consolidates PlayerSpawner and PawnSpawner functionality.
public class SpawnerSystem : MonoBehaviour
{
    #region Inspector Fields - References

    [Header("Grid Reference")]
    [Tooltip("Reference to the HexGridGenerator")]
    public HexGridGenerator gridGenerator;

    [Tooltip("Reference to the Checkerboard")]
    public Checkerboard checkerboard;

    #endregion

    #region Inspector Fields - Player Spawning

    [Header("Player Spawning")]
    [Tooltip("Player pawn prefab")]
    public GameObject playerPawnPrefab;

    [Tooltip("Parent for player pawn")]
    public Transform playerPawnParent;

    [Tooltip("Vertical offset for player spawn")]
    public float playerVerticalOffset = 0f;

    #endregion

    #region Inspector Fields - Opponent Spawning

    [Header("Opponent Prefabs")]
    [Tooltip("Basic pawn prefab")]
    public GameObject pawnPrefab;

    [Tooltip("Handcannon pawn prefab")]
    public GameObject handcannonPrefab;

    [Tooltip("Shotgun pawn prefab")]
    public GameObject shotgunPrefab;

    [Tooltip("Sniper pawn prefab")]
    public GameObject sniperPrefab;

    [Header("Opponent Spawn Counts")]
    [Tooltip("Number of Basic pawns")]
    public int pawnCount = 0;

    [Tooltip("Number of Handcannon pawns")]
    public int handcannonCount = 0;

    [Tooltip("Number of Shotgun pawns")]
    public int shotgunCount = 0;

    [Tooltip("Number of Sniper pawns")]
    public int sniperCount = 0;

    [Header("Opponent Spawning Settings")]
    [Tooltip("Parent for spawned opponent pawns")]
    public Transform opponentSpawnParent;

    [Tooltip("Allow multiple pawns on same tile")]
    public bool allowStacking = false;

    #endregion

    #region Inspector Fields - General

    [Header("General Settings")]
    [Tooltip("Allow scene-wide collider search")]
    public bool allowSceneWideColliderSearch = true;

    #endregion

    #region Private Fields

    private class TileCandidate
    {
        public Vector2Int coordinate;
        public int r;
        public float weight;
    }

    #endregion

    #region Unity Lifecycle

    private IEnumerator Start()
    {
        if (gridGenerator == null) gridGenerator = FindFirstObjectByType<HexGridGenerator>();
        if (checkerboard == null) checkerboard = FindFirstObjectByType<Checkerboard>();

        yield return new WaitForEndOfFrame();

        SpawnAll();
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
            Debug.LogError("[SpawnerSystem] No HexGridGenerator found.");
            return;
        }

        if (checkerboard == null)
        {
            Debug.LogError("[SpawnerSystem] No Checkerboard found.");
            return;
        }

        if (playerPawnPrefab == null)
        {
            Debug.LogWarning("[SpawnerSystem] No player pawn prefab assigned.");
            return;
        }

        Transform tileParent = gridGenerator.parentContainer == null
            ? gridGenerator.transform
            : gridGenerator.parentContainer;

        List<PolygonCollider2D> colliders = new List<PolygonCollider2D>();
        for (int i = 0; i < tileParent.childCount; i++)
        {
            Transform child = tileParent.GetChild(i);
            PolygonCollider2D pc = child.GetComponent<PolygonCollider2D>();
            if (pc != null) colliders.Add(pc);
        }

        if (colliders.Count == 0 && allowSceneWideColliderSearch)
        {
            PolygonCollider2D[] all = Object.FindObjectsByType<PolygonCollider2D>(FindObjectsSortMode.None);
            foreach (var pc in all)
            {
                if (pc.gameObject.name.StartsWith("Hex_")) colliders.Add(pc);
            }
        }

        if (colliders.Count == 0)
        {
            Debug.LogWarning("[SpawnerSystem] No tiles found for player spawn.");
            SpawnPlayerUsingTransformScan(tileParent);
            return;
        }

        // Find bottom-right tile
        float bestBottom = float.PositiveInfinity;
        foreach (var pc in colliders)
        {
            float bMinY = pc.bounds.min.y;
            if (bMinY < bestBottom) bestBottom = bMinY;
        }

        const float eps = 0.0001f;
        PolygonCollider2D chosenCollider = null;
        float bestCenterX = float.NegativeInfinity;
        foreach (var pc in colliders)
        {
            if (pc.bounds.min.y <= bestBottom + eps)
            {
                float cx = pc.bounds.center.x;
                if (cx > bestCenterX)
                {
                    bestCenterX = cx;
                    chosenCollider = pc;
                }
            }
        }

        if (chosenCollider == null)
        {
            Debug.LogError("[SpawnerSystem] Failed to find tile for player spawn.");
            return;
        }

        Vector3 spawnPos = new Vector3(
            chosenCollider.bounds.center.x,
            chosenCollider.bounds.center.y + playerVerticalOffset,
            0f
        );

        GameObject playerPawn = playerPawnParent == null
            ? Instantiate(playerPawnPrefab, spawnPos, Quaternion.identity)
            : Instantiate(playerPawnPrefab, spawnPos, Quaternion.identity, playerPawnParent);

        string name = chosenCollider.gameObject.name;
        string[] parts = name.Split('_');
        int q = 0, r = 0;
        if (parts.Length >= 3 && int.TryParse(parts[1], out q) && int.TryParse(parts[2], out r))
        {
            PlayerController pc = playerPawn.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.Initialise(q, r, gridGenerator, checkerboard);
            }
        }
    }

    public void SpawnOpponents()
    {
        if (gridGenerator == null)
        {
            gridGenerator = FindFirstObjectByType<HexGridGenerator>();
            if (gridGenerator == null)
            {
                Debug.LogError("[SpawnerSystem] No HexGridGenerator found.");
                return;
            }
        }

        if (opponentSpawnParent == null) opponentSpawnParent = this.transform;

        List<TileCandidate> candidates = BuildUpperTileCandidates();
        if (candidates.Count == 0)
        {
            Debug.LogWarning("[SpawnerSystem] No upper tiles found for opponent spawning.");
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

    public void SetPawnCount(int n) { pawnCount = Mathf.Max(0, n); }
    public void SetHandcannonCount(int n) { handcannonCount = Mathf.Max(0, n); }
    public void SetShotgunCount(int n) { shotgunCount = Mathf.Max(0, n); }
    public void SetSniperCount(int n) { sniperCount = Mathf.Max(0, n); }

    public void ClearAllPawns()
    {
        // Clear player
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (Application.isPlaying) Destroy(p.gameObject);
            else DestroyImmediate(p.gameObject);
        }

        // Clear opponents
        PawnController[] pawns = FindObjectsOfType<PawnController>();
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
            Debug.LogError("[SpawnerSystem] Fallback transform scan failed.");
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
            PlayerController pc = playerPawn.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.Initialise(q, r, gridGenerator, checkerboard);
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
                Debug.LogWarning("[SpawnerSystem] No tile available for spawning.");
                break;
            }

            GameObject go = Instantiate(prefab, opponentSpawnParent);

            PawnController pc = go.GetComponent<PawnController>();
            if (pc == null) pc = go.AddComponent<PawnController>();

            pc.gridGenerator = gridGenerator;
            pc.aiType = aiType;
            pc.SetCoordsAndSnap(chosen.x, chosen.y);

            Vector3 world;
            if (TryGetTileWorldCentre(chosen.x, chosen.y, out world))
            {
                go.transform.position = world;
            }

            go.name = $"Opponent {prefab.name}: {chosen.x}_{chosen.y}";

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

        PolygonCollider2D pc = tile.GetComponent<PolygonCollider2D>();
        if (pc != null)
        {
            Vector3 c = pc.bounds.center;
            centre = new Vector3(c.x, c.y, 0f);
            return true;
        }

        centre = tile.position;
        return true;
    }

    #endregion
}
