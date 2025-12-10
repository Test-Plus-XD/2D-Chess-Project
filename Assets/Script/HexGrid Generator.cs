using System.Collections.Generic;
using UnityEngine;

/// Generate a hex grid with selectable orientation (PointyTop or FlatTop).
/// Attach to an empty GameObject and assign HexPrefab (sprite should match chosen orientation visually).
public class HexGridGenerator : MonoBehaviour
{
    // Prefab for each hex tile; should be a GameObject with a SpriteRenderer (2D) or a mesh.
    public GameObject hexPrefab;
    // Radius of the hex grid: 1 => centre + 6 neighbours; 2 => centre + 18 neighbours.
    public int radius = 1;
    // Extra axial rows to add on top and bottom (extends r/z range by this amount).
    public int extraRow = 1;
    // Size measured as top-to-bottom distance of the sprite in world units.
    public float tileSize = 1f;
    // Rotation to apply to each tile in degrees if the sprite is visually tilted in the art asset.
    public float rotationOffsetDegrees = 0f;
    // Parent transform where tiles will be placed; if null, this GameObject is used.
    public Transform parentContainer;
    // When true, AutoSetTileSizeFromPrefab() will attempt to measure sprite bounds and set tileSize automatically.
    public bool autoSetTileSizeFromPrefab = false;
    // Orientation selection to control whether Top/Bottom neighbours exist.
    public Orientation hexOrientation = Orientation.FlatTop;
    // Change row expanding mode.
    public ExtraRowMode extraRowMode = ExtraRowMode.FullWidth;
    // Internal sqrt(3) cached for fast position calculation.
    private readonly float sqrt3 = Mathf.Sqrt(3f);

    // Orientation enum for clearer Inspector control.
    public enum Orientation
    {
        // Pointy-topped hex (vertex on top). Vertical spacing = 2 * hexRadius.
        PointyTop,
        // Flat-topped hex (flat side on top). Vertical spacing includes direct top/bottom neighbours.
        FlatTop
    }
    public enum ExtraRowMode { FullWidth, Tapered };

    // Generate grid on Start when playing.
    void Start()
    {
        if (parentContainer == null) parentContainer = this.transform;
        if (autoSetTileSizeFromPrefab) AutoSetTileSizeFromPrefab();
        GenerateGrid();
    }

    // Public method to generate grid; can be called externally.
    public void GenerateGrid()
    {
        if (hexPrefab == null)
        {
            Debug.LogError("[HexGridGenerator] No hexPrefab assigned.");
            return;
        }
        if (parentContainer == null) parentContainer = this.transform;
        ClearExistingTiles();

        // Ensure extraRow is non-negative to avoid surprising behaviour.
        extraRow = Mathf.Max(0, extraRow);

        // HashSet of placed axial coordinates for fast lookup and to avoid duplicates.
        HashSet<Vector2Int> placed = new HashSet<Vector2Int>();

        // 1) Generate the core hex (classic radius-limited diamond) and record coords.
        int currentTopR = int.MinValue; // track highest r generated so far
        for (int r = -radius; r <= radius; r++)
        {
            int qMin = Mathf.Max(-radius, -r - radius); // algebraic bound for axial q given fixed r in the core
            int qMax = Mathf.Min(radius, -r + radius);  // symmetric upper bound
            for (int q = qMin; q <= qMax; q++)
            {
                placed.Add(new Vector2Int(q, r));
                Vector2 localPos = AxialToLocal(q, r);
                GameObject go = Instantiate(hexPrefab, parentContainer);
                go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, rotationOffsetDegrees);
                go.name = $"Hex_{q}_{r}";
            }
            if (r > currentTopR) currentTopR = r; // update top row tracker
        }

        // 2) Extra-row spawning using explicit directional walks from the visually-centred top tile.
        // Choose bottom-left and bottom-right deltas depending on orientation.
        Vector2Int bottomLeftDelta;
        Vector2Int bottomRightDelta;
        if (hexOrientation == Orientation.FlatTop)
        {
            // For FlatTop axial system used in this script:
            // bottom-left (visual) relative to a tile at (q,r) is (-1, 0).
            // bottom-right (visual) relative to a tile at (q,r) is (1, -1).
            bottomLeftDelta = new Vector2Int(-1, 0);
            bottomRightDelta = new Vector2Int(1, -1);
        }
        else
        {
            // For PointyTop axial system (common convention used here):
            // bottom-left (visual) relative to a tile at (q,r) is (-1, 1).
            // bottom-right (visual) relative to a tile at (q,r) is (0, 1).
            bottomLeftDelta = new Vector2Int(-1, 1);
            bottomRightDelta = new Vector2Int(0, 1);
        }

        // Repeat the anchored expansion extraRow times.
        for (int t = 1; t <= extraRow; t++)
        {
            // Find the current top row (maximum r).
            int topR = int.MinValue;
            foreach (var p in placed) if (p.y > topR) topR = p.y;
            if (topR == int.MinValue) break; // nothing placed, abort

            // Gather q values for tiles on the top row.
            List<int> topQs = new List<int>();
            foreach (var p in placed) if (p.y == topR) topQs.Add(p.x);
            if (topQs.Count == 0) break; // safety

            // Choose the visually centred q by minimising absolute local X (grid local centre assumed at 0).
            // This ensures we pick the tile that sits visually in the middle of the top row.
            topQs.Sort();
            int bestCentreQ = topQs[0];
            float bestDist = Mathf.Abs(AxialToLocal(bestCentreQ, topR).x - 0f);
            for (int i = 1; i < topQs.Count; i++)
            {
                int q = topQs[i];
                float dist = Mathf.Abs(AxialToLocal(q, topR).x - 0f);
                // Tie-breaker: prefer the tile with larger q (rightward) when distances are equal.
                if (dist < bestDist || (Mathf.Approximately(dist, bestDist) && q > bestCentreQ))
                {
                    bestDist = dist;
                    bestCentreQ = q;
                }
            }

            // Place the new top centre tile one row above the current top row.
            int topNewR = topR + 1;
            Vector2Int topCoord = new Vector2Int(bestCentreQ, topNewR);
            if (!placed.Contains(topCoord))
            {
                placed.Add(topCoord);
                Vector2 lp = AxialToLocal(topCoord.x, topCoord.y);
                GameObject go = Instantiate(hexPrefab, parentContainer);
                go.transform.localPosition = new Vector3(lp.x, lp.y, 0f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, rotationOffsetDegrees);
                go.name = $"Hex_{topCoord.x}_{topCoord.y}";
            }

            // Spawn bottom-left chain by repeatedly applying bottomLeftDelta, radius steps.
            Vector2Int currLeft = topCoord;
            for (int i = 1; i <= radius; i++)
            {
                currLeft += bottomLeftDelta; // move to next bottom-left
                if (placed.Contains(currLeft)) continue; // skip duplicates if present
                placed.Add(currLeft);
                Vector2 lp = AxialToLocal(currLeft.x, currLeft.y);
                GameObject go = Instantiate(hexPrefab, parentContainer);
                go.transform.localPosition = new Vector3(lp.x, lp.y, 0f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, rotationOffsetDegrees);
                go.name = $"Hex_{currLeft.x}_{currLeft.y}";
            }

            // Spawn bottom-right chain by repeatedly applying bottomRightDelta, radius steps.
            Vector2Int currRight = topCoord;
            for (int i = 1; i <= radius; i++)
            {
                currRight += bottomRightDelta; // move to next bottom-right
                if (placed.Contains(currRight)) continue; // skip duplicates if present
                placed.Add(currRight);
                Vector2 lp = AxialToLocal(currRight.x, currRight.y);
                GameObject go = Instantiate(hexPrefab, parentContainer);
                go.transform.localPosition = new Vector3(lp.x, lp.y, 0f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, rotationOffsetDegrees);
                go.name = $"Hex_{currRight.x}_{currRight.y}";
            }
            // Promote the newly-added top row to be the current top for the next iteration.
            currentTopR = topNewR;
        }
    }

    // Convert axial coordinates (q, r) to local 2D position so tiles are placed in parentContainer local-space.
    // This returns the local offset; parentContainer position and rotation will then move/rotate the tiles.
    private Vector2 AxialToLocal(int q, int r)
    {
        if (hexOrientation == Orientation.PointyTop)
        {
            // Pointy topped formula
            // x = (sqrt3/2 * tileSize) * (q + r/2)
            // y = (3/4 * tileSize) * r
            float x = tileSize * (sqrt3 / 2f) * (q + r / 2f);
            float y = tileSize * 0.75f * r;
            return new Vector2(x, y);
        }
        else
        {
            // Flat topped formula
            // x = (sqrt3/2 * tileSize) * q
            // y = tileSize * (r + q/2)
            float x = tileSize * (sqrt3 / 2f) * q;
            float y = tileSize * (r + q / 2f);
            return new Vector2(x, y);
        }
    }

    // Remove previously generated tiles (children of parentContainer).
    private void ClearExistingTiles()
    {
#if UNITY_EDITOR
        // In the Editor, use DestroyImmediate for immediate cleanup while not playing.
        if (!Application.isPlaying)
        {
            for (int i = parentContainer.childCount - 1; i >= 0; i--)
            {
                var child = parentContainer.GetChild(i);
                DestroyImmediate(child.gameObject);
            }
            return;
        }
#endif
        // In Play mode use Destroy
        for (int i = parentContainer.childCount - 1; i >= 0; i--)
        {
            var child = parentContainer.GetChild(i);
            Destroy(child.gameObject);
        }
    }

    // Attempt to measure the prefab's sprite vertical size and set tileSize so top-to-bottom = measured height.
    public void AutoSetTileSizeFromPrefab()
    {
        if (hexPrefab == null)
        {
            Debug.LogWarning("[HexGridGenerator] AutoSetTileSizeFromPrefab failed: hexPrefab is null.");
            return;
        }
        SpriteRenderer sr = hexPrefab.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = hexPrefab.GetComponentInChildren<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogWarning("[HexGridGenerator] AutoSetTileSizeFromPrefab failed: no SpriteRenderer found on prefab.");
                return;
            }
        }
        float spriteHeight = sr.bounds.size.y / hexPrefab.transform.localScale.y; // Remove prefab scale to get base sprite units
        tileSize = spriteHeight; // Set tileSize so top-bottom distance equals sprite height
        Debug.Log($"[HexGridGenerator] tileSize auto-set to {tileSize:F3} from prefab sprite bounds.");
    }

    // Add a convenient context menu function to re-generate the grid in the Editor.
    [ContextMenu("Generate Grid (Editor)")]
    private void ContextMenuGenerate()
    {
        if (parentContainer == null) parentContainer = this.transform;
        if (autoSetTileSizeFromPrefab) AutoSetTileSizeFromPrefab();
        GenerateGrid();
    }

    #region Public Setters for Level Manager

    /// Set the grid radius
    public void SetRadius(int newRadius)
    {
        radius = Mathf.Max(1, newRadius);
    }

    /// Set extra rows
    public void SetExtraRow(int newExtraRow)
    {
        extraRow = Mathf.Max(0, newExtraRow);
    }

    /// Set tile size
    public void SetTileSize(float newTileSize)
    {
        tileSize = Mathf.Max(0.1f, newTileSize);
    }

    /// Set hex orientation
    public void SetOrientation(Orientation newOrientation)
    {
        hexOrientation = newOrientation;
    }

    #endregion
}