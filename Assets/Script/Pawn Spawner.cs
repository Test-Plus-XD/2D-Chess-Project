using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Spawn opponent pawns on tiles above the board centre with row-weighted probability.
/// Assign pawn prefabs and spawn counts in the Inspector; counts can be modified by other scripts at runtime.
public class PawnSpawner : MonoBehaviour
{
    // Prefabs for pawn types; assign in Inspector (Pawn, Handcannon, Shotgun, Sniper).
    public GameObject pawnPrefab;
    public GameObject handcannonPrefab;
    public GameObject shotgunPrefab;
    public GameObject sniperPrefab;
    // Spawn counts (modifiable by other scripts).
    public int pawnCount = 0;
    public int handcannonCount = 0;
    public int shotgunCount = 0;
    public int sniperCount = 0;
    // Optional parent for spawned pawns; if null this GameObject will be parent.
    public Transform spawnParent;
    // Optional reference to HexGridGenerator; if null we try to find one.
    public HexGridGenerator gridGenerator;
    // If true, allow multiple pawns on the same tile (stacking). Otherwise prevent duplicate placement.
    public bool allowStacking = false;

    // Public helpers so other scripts can adjust counts easily.
    public void SetPawnCount(int n) { pawnCount = Mathf.Max(0, n); }
    public void AddPawnCount(int delta) { SetPawnCount(pawnCount + delta); }
    public void SetHandcannonCount(int n) { handcannonCount = Mathf.Max(0, n); }
    public void AddHandcannonCount(int delta) { SetHandcannonCount(handcannonCount + delta); }
    public void SetShotgunCount(int n) { shotgunCount = Mathf.Max(0, n); }
    public void AddShotgunCount(int delta) { SetShotgunCount(shotgunCount + delta); }
    public void SetSniperCount(int n) { sniperCount = Mathf.Max(0, n); }
    public void AddSniperCount(int delta) { SetSniperCount(sniperCount + delta); }

    // Small safety wait to let other Start() calls complete.
    private IEnumerator Start()
    {
        // Wait one frame so HexGridGenerator.Start() has a chance to create tiles if it does so in Start()
        yield return new WaitForEndOfFrame();
        SpawnAll();
        yield break;
    }

    // Spawn all configured pawns. Call this after grid generation.
    public void SpawnAll()
    {
        if (gridGenerator == null) gridGenerator = FindFirstObjectByType<HexGridGenerator>();
        if (gridGenerator == null)
        {
            Debug.LogError("[PawnSpawner] No HexGridGenerator found.");
            return;
        }
        if (spawnParent == null) spawnParent = this.transform;

        // Build candidate tile list above board centre with row-weighted probabilities.
        List<TileCandidate> candidates = BuildUpperTileCandidates();
        if (candidates.Count == 0)
        {
            Debug.LogWarning("[PawnSpawner] No upper-side tiles found to spawn pawns.");
        }

        // Keep track of occupied coords to avoid duplicates when allowStacking == false.
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

        // Helper local function to spawn N pawns of given prefab and AI type.
        void SpawnType(GameObject prefab, PawnController.AIType aiType, int count)
        {
            if (prefab == null || count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                Vector2Int chosen = ChooseWeightedTile(candidates, occupied, allowStacking);
                // If no tile available, log and break.
                if (chosen == Vector2Int.zero && candidates.Count == 0)
                {
                    Debug.LogWarning("[PawnSpawner] No tile available for spawning.");
                    break;
                }
                // Instantiate pawn and initialise its PawnController with coords
                GameObject go = Instantiate(prefab, spawnParent);
                // Try to get PawnController or add one
                PawnController pc = go.GetComponent<PawnController>();
                if (pc == null) pc = go.AddComponent<PawnController>();
                pc.gridGenerator = gridGenerator; // assign generator
                pc.aiType = aiType; // set AI type
                pc.SetCoordsAndSnap(chosen.x, chosen.y); // Set q,r and snap to tile centre (very important)
                // Position at tile centre if possible
                Vector3 world;
                if (TryGetTileWorldCentre(chosen.x, chosen.y, out world))
                {
                    go.transform.position = world;
                }
                go.name = $"Opponent {prefab.name}: {chosen.x}_{chosen.y}";
                // Mark occupied (unless stacking allowed)
                if (!allowStacking) occupied.Add(chosen);
            }
        }

        // Spawn each type
        SpawnType(pawnPrefab, PawnController.AIType.Basic, pawnCount);
        SpawnType(handcannonPrefab, PawnController.AIType.Handcannon, handcannonCount);
        SpawnType(shotgunPrefab, PawnController.AIType.Shotgun, shotgunCount);
        SpawnType(sniperPrefab, PawnController.AIType.Sniper, sniperCount);
    }

    // Internal representation of a tile candidate with weight.
    private class TileCandidate { public Vector2Int coordinate; public int r; public float weight; }

    // Build candidate list from tiles whose axial r is above the middle row.
    private List<TileCandidate> BuildUpperTileCandidates()
    {
        List<TileCandidate> list = new List<TileCandidate>();
        // Find tiles by parsing names "Hex_q_r" under the grid parent container.
        Transform parent = gridGenerator.parentContainer == null ? gridGenerator.transform : gridGenerator.parentContainer;
        int minR = int.MaxValue; int maxR = int.MinValue;
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
        // Determine middle row index. We exclude the central row(s) so we only consider tiles above the middle.
        int middleR = Mathf.FloorToInt((minR + maxR) / 2f);
        // Build candidate list for tiles with r > middleR and weight them by (r - middleR + 1).
        foreach (var t in tileTransforms)
        {
            string[] parts = t.name.Split('_');
            if (parts.Length < 3) continue;
            if (!int.TryParse(parts[1], out int qVal)) continue;
            if (!int.TryParse(parts[2], out int rVal)) continue;
            if (rVal <= middleR) continue; // only above middle
            TileCandidate c = new TileCandidate();
            c.coordinate = new Vector2Int(qVal, rVal);
            c.r = rVal;
            // Weight increases linearly with row height above middle so top rows are more likely.
            c.weight = (float)(rVal - middleR + 1);
            list.Add(c);
        }
        return list;
    }

    // Weighted random selection of a tile candidate, optionally avoiding already occupied coords.
    private Vector2Int ChooseWeightedTile(List<TileCandidate> candidates, HashSet<Vector2Int> occupied, bool allowReuse)
    {
        if (candidates == null || candidates.Count == 0) return Vector2Int.zero;
        // Build filtered weighted list
        float sum = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!allowReuse && occupied.Contains(candidates[i].coordinate)) continue;
            sum += candidates[i].weight;
        }
        if (sum <= 0f)
        {
            // Nothing available; if reuse allowed, pick uniform fallback.
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
        // fallback
        return candidates[candidates.Count - 1].coordinate;
    }

    // Try to get tile world centre via PolygonCollider2D bounds centre or transform position.
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
            centre = new Vector3(c.x, c.y, 0f);
            return true;
        }
        centre = tile.position;
        return true;
    }

    #region Public Setters for Level Manager

    /// <summary>
    /// Set pawn counts for all types at once
    /// </summary>
    public void SetPawnCount(int basic, int handcannon, int shotgun, int sniper)
    {
        pawnCount = Mathf.Max(0, basic);
        handcannonCount = Mathf.Max(0, handcannon);
        shotgunCount = Mathf.Max(0, shotgun);
        sniperCount = Mathf.Max(0, sniper);
    }

    /// <summary>
    /// Spawn all pawns (clears existing first)
    /// </summary>
    public void SpawnAllPawns()
    {
        // Clear existing spawned pawns
        ClearSpawnedPawns();

        // Spawn all types
        SpawnType(pawnPrefab, pawnCount, PawnController.AIType.Basic);
        SpawnType(handcannonPrefab, handcannonCount, PawnController.AIType.Handcannon);
        SpawnType(shotgunPrefab, shotgunCount, PawnController.AIType.Shotgun);
        SpawnType(sniperPrefab, sniperCount, PawnController.AIType.Sniper);
    }

    /// <summary>
    /// Clear all spawned pawns
    /// </summary>
    private void ClearSpawnedPawns()
    {
        // Find all opponent pawns and destroy them
        PawnController[] pawns = FindObjectsOfType<PawnController>();
        foreach (var pawn in pawns)
        {
            if (Application.isPlaying)
            {
                Destroy(pawn.gameObject);
            }
            else
            {
                DestroyImmediate(pawn.gameObject);
            }
        }
    }

    #endregion
}