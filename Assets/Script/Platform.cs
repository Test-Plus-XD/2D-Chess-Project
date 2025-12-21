using UnityEngine;
using System.Collections.Generic;

/// Generates the Standoff arena using hexagonal tiles with platforms at varying heights.
/// Algorithm:
/// 1. Spawn first floor tile at (0,0,0) world space
/// 2. Extend floor by spawning tiles in top-right or bottom-right direction (cannot choose same 3x in a row)
/// 3. Choose a floor tile and spawn a platform 2 tiles high above it
/// 4. Expand the platform in top-right or bottom-right direction
/// 5. Mirror all tiles from right to left
/// 6. Add random connecting tiles
public class Platform : MonoBehaviour
{
    public enum HexOrientation
    {
        FlatTop,
        PointyTop
    }

    public enum TileType
    {
        Floor,
        Platform
    }

    [System.Serializable]
    public class TileData
    {
        public Vector3 position;
        public int axialQ;
        public int axialR;
        public TileType tileType;
        public int heightLevel;

        public TileData(Vector3 pos, int q, int r, TileType type, int height)
        {
            position = pos;
            axialQ = q;
            axialR = r;
            tileType = type;
            heightLevel = height;
        }
    }

    [Header("Tile Prefabs")]
    [Tooltip("Prefab for floor tiles")]
    // Prefab to instantiate for floor tiles.
    [SerializeField] private GameObject floorTilePrefab;
    [Tooltip("Prefab for platform tiles")]
    // Prefab to instantiate for platform tiles.
    [SerializeField] private GameObject platformTilePrefab;

    [Header("Grid Settings")]
    [Tooltip("Size of hex tiles (distance from center to flat edge for FlatTop)")]
    // Size of hexagonal tiles in world units.
    [SerializeField] private float tileSize = 1f;
    [Tooltip("Hex orientation")]
    // Orientation of the hexagonal grid (FlatTop or PointyTop).
    [SerializeField] private HexOrientation hexOrientation = HexOrientation.FlatTop;
    [Tooltip("Rotation offset for tile sprites in degrees")]
    // Rotation offset in degrees for tile sprites.
    [SerializeField] private float tileRotationOffset = 0f;

    [Header("Floor Generation")]
    [Tooltip("Number of floor tile expansions after the first tile (e.g., 5 means 6 total floor tiles)")]
    // Number of floor tile expansions after the initial tile.
    [SerializeField] private int floorExpansionCount = 5;
    [Tooltip("Maximum consecutive same-direction choices allowed")]
    // Maximum times the same direction can be chosen consecutively.
    [SerializeField] private int maxConsecutiveSameDirection = 2;

    [Header("Platform Generation")]
    [Tooltip("Height level for platforms (2 = two tiles above floor)")]
    // Height level for platform tiles (in tile units).
    [SerializeField] private int platformHeightLevel = 2;
    [Tooltip("Height multiplier for platform elevation in world units")]
    // Multiplier applied to height level for world position.
    [SerializeField] private float platformHeightMultiplier = 1f;
    [Tooltip("Which floor tile index to build platform above (0-based)")]
    // Index of floor tile to build platform above.
    [SerializeField] private int platformBaseIndex = 2;
    [Tooltip("Number of times to expand the platform")]
    // Number of platform tile expansions.
    [SerializeField] private int platformExpansionCount = 2;

    [Header("Random Tiles")]
    [Tooltip("Number of random tiles to attach to the arena")]
    // Number of random connecting tiles to add.
    [SerializeField] private int randomTileCount = 3;

    [Header("Organization")]
    [Tooltip("Parent container for spawned tiles")]
    // Parent transform for all spawned tiles.
    [SerializeField] private Transform parentContainer;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    // Enable debug logging for arena generation.
    [SerializeField] private bool showDebug = true;

    // Cached square root of 3 for hex calculations.
    private readonly float SQRT_3 = Mathf.Sqrt(3f);
    // List of all generated tiles.
    private List<TileData> allTiles = new List<TileData>();
    // Dictionary for quick tile lookup by position key.
    private Dictionary<string, TileData> tileDict = new Dictionary<string, TileData>();
    // Floor tiles generated before mirroring (right side only).
    private List<TileData> rightSideFloorTiles = new List<TileData>();
    // Platform tiles generated before mirroring (right side only).
    private List<TileData> rightSidePlatformTiles = new List<TileData>();

    // Hex direction vectors for FlatTop orientation.
    // Index: 0=Right, 1=TopRight, 2=TopLeft, 3=Left, 4=BottomLeft, 5=BottomRight
    private readonly Vector2Int[] flatTopDirections = new Vector2Int[]
    {
        new Vector2Int(1, 0),    // 0: Right
        new Vector2Int(1, -1),   // 1: Top-Right (right and up in world space)
        new Vector2Int(0, -1),   // 2: Top-Left
        new Vector2Int(-1, 0),   // 3: Left
        new Vector2Int(-1, 1),   // 4: Bottom-Left
        new Vector2Int(0, 1)     // 5: Bottom-Right (right and down in world space)
    };

    // Hex direction vectors for PointyTop orientation.
    private readonly Vector2Int[] pointyTopDirections = new Vector2Int[]
    {
        new Vector2Int(1, 0),    // 0: Right
        new Vector2Int(0, -1),   // 1: Top-Right
        new Vector2Int(-1, -1),  // 2: Top-Left
        new Vector2Int(-1, 0),   // 3: Left
        new Vector2Int(0, 1),    // 4: Bottom-Left
        new Vector2Int(1, 1)     // 5: Bottom-Right
    };

    private void Awake()
    {
        // Disable the GameObject on start so it only activates in Standoff mode.
        gameObject.SetActive(false);
    }

    private void Start()
    {
        if (parentContainer == null)
        {
            GameObject container = new GameObject("StandoffArena");
            parentContainer = container.transform;
        }
        GenerateArena();
    }

    /// Generate the complete Standoff arena.
    [ContextMenu("Generate Arena")]
    public void GenerateArena()
    {
        ClearArena();

        // Step 1: Generate floor tiles on the right side.
        GenerateFloorTiles();

        // Step 2: Generate platform above a selected floor tile.
        GeneratePlatform();

        // Step 3: Mirror all tiles from right to left.
        MirrorTiles();

        // Step 4: Add random connecting tiles.
        AddRandomTiles();

        // Step 5: Instantiate all tiles.
        InstantiateTiles();

        if (showDebug)
        {
            Debug.Log($"[Platform] Arena generated with {allTiles.Count} total tiles");
        }
    }

    /// Clear existing arena tiles.
    [ContextMenu("Clear Arena")]
    public void ClearArena()
    {
        allTiles.Clear();
        tileDict.Clear();
        rightSideFloorTiles.Clear();
        rightSidePlatformTiles.Clear();

        if (parentContainer != null)
        {
            for (int i = parentContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = parentContainer.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }

    /// Get tile at specific axial coordinates and height.
    public TileData GetTileAt(int q, int r, int height = 0)
    {
        string key = GetKey(q, r, height);
        return tileDict.ContainsKey(key) ? tileDict[key] : null;
    }

    /// Get all tiles in the arena.
    public List<TileData> GetAllTiles()
    {
        return new List<TileData>(allTiles);
    }

    /// Get the spawn position for the player (leftmost floor tile, above highest tile).
    public Vector3 GetPlayerSpawnPosition()
    {
        return CalculateSpawnPosition(findLeftmost: true);
    }

    /// Get the spawn position for the opponent (rightmost floor tile, above highest tile).
    public Vector3 GetOpponentSpawnPosition()
    {
        return CalculateSpawnPosition(findLeftmost: false);
    }

    /// Get the current tile size.
    public float GetTileSize()
    {
        return tileSize;
    }

    /// Set the tile size (for level configuration).
    public void SetTileSize(float newSize)
    {
        tileSize = Mathf.Max(0.1f, newSize);
    }

    /// Calculate spawn position for player or opponent.
    private Vector3 CalculateSpawnPosition(bool findLeftmost)
    {
        // Find floor tiles (height level 0).
        List<TileData> floorTiles = new List<TileData>();
        foreach (var tile in allTiles)
        {
            if (tile.heightLevel == 0)
            {
                floorTiles.Add(tile);
            }
        }

        if (floorTiles.Count == 0)
        {
            Debug.LogWarning("[Platform] No floor tiles found for spawn position.");
            return Vector3.zero;
        }

        // Find leftmost or rightmost floor tile.
        TileData targetTile = floorTiles[0];
        foreach (var tile in floorTiles)
        {
            if (findLeftmost)
            {
                if (tile.position.x < targetTile.position.x)
                {
                    targetTile = tile;
                }
            }
            else
            {
                if (tile.position.x > targetTile.position.x)
                {
                    targetTile = tile;
                }
            }
        }

        // Find the highest Y position among all tiles.
        float highestY = float.NegativeInfinity;
        foreach (var tile in allTiles)
        {
            if (tile.position.y > highestY)
            {
                highestY = tile.position.y;
            }
        }

        // Spawn position: X from target tile, Y = highest tile + one tile height.
        Vector3 spawnPos = new Vector3(
            targetTile.position.x,
            highestY + tileSize,
            0f
        );

        if (showDebug)
        {
            string side = findLeftmost ? "Player (left)" : "Opponent (right)";
            Debug.Log($"[Platform] {side} spawn at {spawnPos} (target tile: q={targetTile.axialQ}, r={targetTile.axialR})");
        }

        return spawnPos;
    }

    /// Generate floor tiles starting at (0,0,0) and expanding to the right.
    private void GenerateFloorTiles()
    {
        // First tile at world origin with axial coordinates (0, 0).
        Vector3 startPos = Vector3.zero;
        TileData firstTile = AddTile(startPos, 0, 0, TileType.Floor, 0);
        rightSideFloorTiles.Add(firstTile);

        int currentQ = 0;
        int currentR = 0;
        int consecutiveSameDirection = 0;
        int lastDirectionIndex = -1;

        // Direction indices for floor expansion (both go right, alternating up/down).
        // For FlatTop: (1, 0) goes right+up in world, (1, -1) goes right+down in world.
        int topRightDirIndex = 0;     // (1, 0) - goes right and up in world space
        int bottomRightDirIndex = 1;  // (1, -1) - goes right and down in world space

        for (int i = 0; i < floorExpansionCount; i++)
        {
            // Choose direction: prefer alternating, but random within constraint.
            int chosenDirIndex;

            if (consecutiveSameDirection >= maxConsecutiveSameDirection)
            {
                // Force switch direction.
                chosenDirIndex = (lastDirectionIndex == topRightDirIndex) ? bottomRightDirIndex : topRightDirIndex;
                consecutiveSameDirection = 1;
            }
            else
            {
                // Random choice between top-right and bottom-right.
                chosenDirIndex = (Random.value > 0.5f) ? topRightDirIndex : bottomRightDirIndex;

                if (chosenDirIndex == lastDirectionIndex)
                {
                    consecutiveSameDirection++;
                }
                else
                {
                    consecutiveSameDirection = 1;
                }
            }

            lastDirectionIndex = chosenDirIndex;

            // Apply direction offset.
            Vector2Int dirOffset = GetDirections()[chosenDirIndex];
            int newQ = currentQ + dirOffset.x;
            int newR = currentR + dirOffset.y;

            Vector3 newPos = AxialToWorld(newQ, newR, 0);
            TileData newTile = AddTile(newPos, newQ, newR, TileType.Floor, 0);
            rightSideFloorTiles.Add(newTile);

            currentQ = newQ;
            currentR = newR;
        }

        if (showDebug)
        {
            Debug.Log($"[Platform] Generated {rightSideFloorTiles.Count} floor tiles");
        }
    }

    /// Generate platform tiles above a selected floor tile.
    private void GeneratePlatform()
    {
        if (rightSideFloorTiles.Count <= platformBaseIndex)
        {
            Debug.LogWarning($"[Platform] Not enough floor tiles to build platform at index {platformBaseIndex}");
            return;
        }

        TileData baseTile = rightSideFloorTiles[platformBaseIndex];

        // Spawn initial platform tile at platformHeightLevel above base.
        int startQ = baseTile.axialQ;
        int startR = baseTile.axialR;
        Vector3 platformPos = AxialToWorld(startQ, startR, platformHeightLevel);
        TileData firstPlatformTile = AddTile(platformPos, startQ, startR, TileType.Platform, platformHeightLevel);
        rightSidePlatformTiles.Add(firstPlatformTile);

        // Direction indices for expansion (same as floor: top-right and bottom-right).
        int topRightDirIndex = 0;
        int bottomRightDirIndex = 1;

        int currentQ = startQ;
        int currentR = startR;

        // Expand platform.
        for (int i = 0; i < platformExpansionCount; i++)
        {
            // Random choice between top-right and bottom-right for expansion.
            int dirIndex = (Random.value > 0.5f) ? topRightDirIndex : bottomRightDirIndex;
            Vector2Int dirOffset = GetDirections()[dirIndex];

            int newQ = currentQ + dirOffset.x;
            int newR = currentR + dirOffset.y;

            // Check if tile already exists at this position and height.
            string key = GetKey(newQ, newR, platformHeightLevel);
            if (!tileDict.ContainsKey(key))
            {
                Vector3 newPos = AxialToWorld(newQ, newR, platformHeightLevel);
                TileData newTile = AddTile(newPos, newQ, newR, TileType.Platform, platformHeightLevel);
                rightSidePlatformTiles.Add(newTile);
            }

            currentQ = newQ;
            currentR = newR;
        }

        if (showDebug)
        {
            Debug.Log($"[Platform] Generated {rightSidePlatformTiles.Count} platform tiles above floor index {platformBaseIndex}");
        }
    }

    /// Mirror all tiles from right to left across the vertical axis.
    private void MirrorTiles()
    {
        // Collect tiles to mirror (all current tiles except those at x=0 or already have a mirror).
        List<TileData> tilesToMirror = new List<TileData>(allTiles);

        int mirroredCount = 0;
        foreach (TileData tile in tilesToMirror)
        {
            // Calculate mirrored axial coordinates.
            // For FlatTop: x = SQRT_3/2 * tileSize * q, y = tileSize * (r + q/2)
            // To mirror x -> -x, we need: -q for q, but y must remain the same.
            // y' = tileSize * (r' + q'/2) = tileSize * (r + q/2)
            // With q' = -q: r' + (-q)/2 = r + q/2 -> r' = r + q
            int mirrorQ = -tile.axialQ;
            int mirrorR = tile.axialR + tile.axialQ;

            // Skip if this is at the mirror axis (q=0 becomes q=0).
            if (mirrorQ == tile.axialQ && mirrorR == tile.axialR) continue;

            string key = GetKey(mirrorQ, mirrorR, tile.heightLevel);
            if (!tileDict.ContainsKey(key))
            {
                Vector3 mirrorPos = AxialToWorld(mirrorQ, mirrorR, tile.heightLevel);
                AddTile(mirrorPos, mirrorQ, mirrorR, tile.tileType, tile.heightLevel);
                mirroredCount++;
            }
        }

        if (showDebug)
        {
            Debug.Log($"[Platform] Mirrored {mirroredCount} tiles from right to left");
        }
    }

    /// Add random connecting tiles to the arena.
    private void AddRandomTiles()
    {
        int tilesAdded = 0;
        int maxAttempts = randomTileCount * 20;
        int attempts = 0;

        Vector2Int[] directions = GetDirections();

        while (tilesAdded < randomTileCount && attempts < maxAttempts)
        {
            attempts++;

            // Pick a random existing tile.
            TileData randomTile = allTiles[Random.Range(0, allTiles.Count)];

            // Pick a random direction (any of the 6).
            int dirIndex = Random.Range(0, 6);
            Vector2Int dirOffset = directions[dirIndex];

            int newQ = randomTile.axialQ + dirOffset.x;
            int newR = randomTile.axialR + dirOffset.y;
            int newHeight = randomTile.heightLevel;

            // Check if tile doesn't already exist at this position.
            string key = GetKey(newQ, newR, newHeight);
            if (!tileDict.ContainsKey(key))
            {
                Vector3 newPos = AxialToWorld(newQ, newR, newHeight);
                TileType type = newHeight == 0 ? TileType.Floor : TileType.Platform;
                AddTile(newPos, newQ, newR, type, newHeight);
                tilesAdded++;
            }
        }

        if (showDebug)
        {
            Debug.Log($"[Platform] Added {tilesAdded} random tiles in {attempts} attempts");
        }
    }

    /// Add a tile to the arena and register it in the dictionary.
    private TileData AddTile(Vector3 position, int q, int r, TileType type, int height)
    {
        TileData tile = new TileData(position, q, r, type, height);
        allTiles.Add(tile);

        string key = GetKey(q, r, height);
        tileDict[key] = tile;

        return tile;
    }

    /// Instantiate all tiles as GameObjects.
    private void InstantiateTiles()
    {
        foreach (TileData tile in allTiles)
        {
            GameObject prefab = tile.tileType == TileType.Floor ? floorTilePrefab : platformTilePrefab;

            if (prefab == null)
            {
                Debug.LogWarning($"[Platform] Missing prefab for tile type: {tile.tileType}");
                continue;
            }

            GameObject tileObj = Instantiate(prefab, tile.position, Quaternion.Euler(0, 0, tileRotationOffset), parentContainer);
            tileObj.name = $"{tile.tileType}_H{tile.heightLevel}_{tile.axialQ}_{tile.axialR}";

            // Copy tag and layer from prefab to ensure platform tiles have proper collision settings
            tileObj.tag = prefab.tag;
            tileObj.layer = prefab.layer;
        }
    }

    /// Get a unique key for a tile based on its coordinates and height.
    private string GetKey(int q, int r, int height)
    {
        return $"{q}_{r}_{height}";
    }

    /// Get the direction vectors based on current hex orientation.
    private Vector2Int[] GetDirections()
    {
        return hexOrientation == HexOrientation.FlatTop ? flatTopDirections : pointyTopDirections;
    }

    /// Convert axial coordinates to world position.
    private Vector3 AxialToWorld(int q, int r, int heightLevel)
    {
        float x, y;

        if (hexOrientation == HexOrientation.FlatTop)
        {
            // FlatTop hex formula (matching HexGridGenerator).
            // x = tileSize * (SQRT_3 / 2) * q
            // y = tileSize * (r + q / 2)
            x = tileSize * (SQRT_3 / 2f) * q;
            y = tileSize * (r + q / 2f);
        }
        else
        {
            // PointyTop hex formula (matching HexGridGenerator).
            // x = tileSize * (SQRT_3 / 2) * (q + r / 2)
            // y = tileSize * (3 / 4) * r
            x = tileSize * (SQRT_3 / 2f) * (q + r / 2f);
            y = tileSize * 0.75f * r;
        }

        // Add height offset in Y direction.
        float heightOffset = heightLevel * tileSize * platformHeightMultiplier;

        return new Vector3(x, y + heightOffset, 0f);
    }

    private void OnDrawGizmos()
    {
        if (!showDebug) return;

        // Draw tile positions.
        Gizmos.color = Color.cyan;
        foreach (TileData tile in allTiles)
        {
            Gizmos.DrawWireSphere(tile.position, tileSize * 0.2f);
        }

        // Draw spawn positions.
        if (allTiles.Count > 0)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(GetPlayerSpawnPosition(), tileSize * 0.3f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(GetOpponentSpawnPosition(), tileSize * 0.3f);
        }
    }
}
