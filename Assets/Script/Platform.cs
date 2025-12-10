using UnityEngine;
using System.Collections.Generic;

/// Generates the Standoff arena using hexagonal tiles with platforms at varying heights
public class Platform : MonoBehaviour
{
    #region Enums

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

    #endregion

    #region Serializable Classes

    [System.Serializable]
    public class TileData
    {
        public Vector3 position;
        public int axialQ;
        public int axialR;
        public TileType tileType;
        public int heightLevel; // 0 = floor, 1+ = platform heights

        public TileData(Vector3 pos, int q, int r, TileType type, int height)
        {
            position = pos;
            axialQ = q;
            axialR = r;
            tileType = type;
            heightLevel = height;
        }
    }

    #endregion

    [Header("Tile Prefabs")]
    [Tooltip("Prefab for floor tiles")]
    // Prefab to instantiate for floor tiles.
    [SerializeField] private GameObject floorTilePrefab;
    [Tooltip("Prefab for platform tiles")]
    // Prefab to instantiate for platform tiles.
    [SerializeField] private GameObject platformTilePrefab;

    [Header("Grid Settings")]
    [Tooltip("Size of hex tiles (distance from top to bottom)")]
    // Size of hexagonal tiles in world units.
    [SerializeField] private float tileSize = 1f;
    [Tooltip("Hex orientation")]
    // Orientation of the hexagonal grid (FlatTop or PointyTop).
    [SerializeField] private HexOrientation hexOrientation = HexOrientation.FlatTop;
    [Tooltip("Rotation offset for tile sprites")]
    // Rotation offset in degrees for tile sprites.
    [SerializeField] private float tileRotationOffset = 0f;

    [Header("Floor Generation")]
    [Tooltip("Number of floor tiles to generate (default 6)")]
    // Number of base floor tiles to generate.
    [SerializeField] private int floorTileCount = 6;
    [Tooltip("Height multiplier for platform elevation")]
    // Multiplier for platform height in world units.
    [SerializeField] private float platformHeightMultiplier = 1f;

    [Header("Platform Generation")]
    [Tooltip("Which floor tile index to build platform above (0-based)")]
    // Index of floor tile to build platform above.
    [SerializeField] private int platformBaseIndex = 2;
    [Tooltip("Number of times to expand the platform")]
    // Number of platform tile expansions.
    [SerializeField] private int platformExpansions = 2;

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

    #region Private Fields

    private List<TileData> allTiles = new List<TileData>();
    private Dictionary<string, TileData> tileDict = new Dictionary<string, TileData>();

    // Direction vectors for hex movement
    private Vector2Int[] hexDirections = new Vector2Int[6];

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Disable the GameObject on start
        gameObject.SetActive(false);
    }

    private void Start()
    {
        InitializeHexDirections();
        if (parentContainer == null)
        {
            GameObject container = new GameObject("StandoffArena");
            parentContainer = container.transform;
        }
        GenerateArena();
    }

    #endregion

    #region Public Methods

    /// Generate the complete Standoff arena
    [ContextMenu("Generate Arena")]
    public void GenerateArena()
    {
        ClearArena();

        // Step 1: Generate floor tiles
        GenerateFloorTiles();

        // Step 2: Generate platform
        GeneratePlatform();

        // Step 3: Mirror tiles from right to left
        MirrorTiles();

        // Step 4: Add random connecting tiles
        AddRandomTiles();

        // Step 5: Instantiate all tiles
        InstantiateTiles();

        if (showDebug)
        {
            Debug.Log($"Arena generated with {allTiles.Count} total tiles");
        }
    }

    /// Clear existing arena
    [ContextMenu("Clear Arena")]
    public void ClearArena()
    {
        allTiles.Clear();
        tileDict.Clear();

        if (parentContainer != null)
        {
            foreach (Transform child in parentContainer)
            {
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

    /// Get tile at specific axial coordinates
    public TileData GetTileAt(int q, int r)
    {
        string key = GetKey(q, r);
        return tileDict.ContainsKey(key) ? tileDict[key] : null;
    }

    /// Get all tiles in the arena
    public List<TileData> GetAllTiles()
    {
        return new List<TileData>(allTiles);
    }

    #endregion

    #region Private Methods - Initialization

    private void InitializeHexDirections()
    {
        // Axial hex directions (flat-top orientation)
        hexDirections[0] = new Vector2Int(1, 0);   // Right
        hexDirections[1] = new Vector2Int(1, -1);  // Top-right
        hexDirections[2] = new Vector2Int(0, -1);  // Top-left
        hexDirections[3] = new Vector2Int(-1, 0);  // Left
        hexDirections[4] = new Vector2Int(-1, 1);  // Bottom-left
        hexDirections[5] = new Vector2Int(0, 1);   // Bottom-right
    }

    #endregion

    #region Private Methods - Floor Generation

    private void GenerateFloorTiles()
    {
        // First tile at (0,0,0) in world space
        Vector3 startPos = Vector3.zero;
        AddTile(startPos, 0, 0, TileType.Floor, 0);

        int lastQ = 0;
        int lastR = 0;
        int consecutiveDirection = 0;
        int lastDirection = -1;

        // Generate remaining floor tiles
        for (int i = 1; i < floorTileCount; i++)
        {
            // Choose direction: 1=top-right, 5=bottom-right
            int direction;

            if (consecutiveDirection >= 2)
            {
                // Force switch direction
                direction = (lastDirection == 1) ? 5 : 1;
                consecutiveDirection = 1;
            }
            else
            {
                // Random choice
                direction = Random.value > 0.5f ? 1 : 5;

                if (direction == lastDirection)
                {
                    consecutiveDirection++;
                }
                else
                {
                    consecutiveDirection = 1;
                }
            }

            lastDirection = direction;

            // Apply direction
            Vector2Int offset = hexDirections[direction];
            int newQ = lastQ + offset.x;
            int newR = lastR + offset.y;

            Vector3 newPos = AxialToWorld(newQ, newR, 0);
            AddTile(newPos, newQ, newR, TileType.Floor, 0);

            lastQ = newQ;
            lastR = newR;
        }
    }

    #endregion

    #region Private Methods - Platform Generation

    private void GeneratePlatform()
    {
        if (allTiles.Count <= platformBaseIndex)
        {
            Debug.LogWarning($"Not enough floor tiles to build platform at index {platformBaseIndex}");
            return;
        }

        TileData baseTile = allTiles[platformBaseIndex];
        int platformHeight = 2;

        // Spawn initial platform tile 2 levels above base
        int startQ = baseTile.axialQ;
        int startR = baseTile.axialR;
        Vector3 platformPos = AxialToWorld(startQ, startR, platformHeight);
        AddTile(platformPos, startQ, startR, TileType.Platform, platformHeight);

        // Track platform tiles for expansion
        List<TileData> platformTiles = new List<TileData> { allTiles[allTiles.Count - 1] };

        // Expand platform
        for (int i = 0; i < platformExpansions; i++)
        {
            // Choose random direction (top-right or bottom-right)
            int direction = Random.value > 0.5f ? 1 : 5;

            // Expand from last added platform tile
            TileData lastPlatform = platformTiles[platformTiles.Count - 1];
            Vector2Int offset = hexDirections[direction];
            int newQ = lastPlatform.axialQ + offset.x;
            int newR = lastPlatform.axialR + offset.y;

            // Check if tile already exists at this position and height
            string key = GetKey(newQ, newR, platformHeight);
            if (!tileDict.ContainsKey(key))
            {
                Vector3 newPos = AxialToWorld(newQ, newR, platformHeight);
                TileData newTile = AddTile(newPos, newQ, newR, TileType.Platform, platformHeight);
                platformTiles.Add(newTile);
            }
        }
    }

    #endregion

    #region Private Methods - Mirroring

    private void MirrorTiles()
    {
        // Copy all current tiles (to avoid modifying list during iteration)
        List<TileData> tilesToMirror = new List<TileData>(allTiles);

        foreach (TileData tile in tilesToMirror)
        {
            // Mirror across Q axis (Q becomes -Q)
            int mirrorQ = -tile.axialQ;
            int mirrorR = tile.axialR;

            // Skip if this is the center tile or already mirrored
            if (mirrorQ == tile.axialQ) continue;

            string key = GetKey(mirrorQ, mirrorR, tile.heightLevel);
            if (!tileDict.ContainsKey(key))
            {
                Vector3 mirrorPos = AxialToWorld(mirrorQ, mirrorR, tile.heightLevel);
                AddTile(mirrorPos, mirrorQ, mirrorR, tile.tileType, tile.heightLevel);
            }
        }
    }

    #endregion

    #region Private Methods - Random Tiles

    private void AddRandomTiles()
    {
        int tilesAdded = 0;
        int maxAttempts = randomTileCount * 10; // Prevent infinite loops
        int attempts = 0;

        while (tilesAdded < randomTileCount && attempts < maxAttempts)
        {
            attempts++;

            // Pick random existing tile
            TileData randomTile = allTiles[Random.Range(0, allTiles.Count)];

            // Pick random direction
            int direction = Random.Range(0, 6);
            Vector2Int offset = hexDirections[direction];

            int newQ = randomTile.axialQ + offset.x;
            int newR = randomTile.axialR + offset.y;
            int newHeight = randomTile.heightLevel; // Same height as source tile

            // Check if tile doesn't already exist
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
            Debug.Log($"Added {tilesAdded} random tiles in {attempts} attempts");
        }
    }

    #endregion

    #region Private Methods - Tile Management

    private TileData AddTile(Vector3 position, int q, int r, TileType type, int height)
    {
        TileData tile = new TileData(position, q, r, type, height);
        allTiles.Add(tile);

        string key = GetKey(q, r, height);
        tileDict[key] = tile;

        return tile;
    }

    private void InstantiateTiles()
    {
        foreach (TileData tile in allTiles)
        {
            GameObject prefab = tile.tileType == TileType.Floor ? floorTilePrefab : platformTilePrefab;

            if (prefab == null)
            {
                Debug.LogWarning($"Missing prefab for tile type: {tile.tileType}");
                continue;
            }

            GameObject tileObj = Instantiate(prefab, tile.position, Quaternion.Euler(0, 0, tileRotationOffset), parentContainer);
            tileObj.name = $"{tile.tileType}_H{tile.heightLevel}_{tile.axialQ}_{tile.axialR}";
        }
    }

    private string GetKey(int q, int r, int height = 0)
    {
        return $"{q}_{r}_{height}";
    }

    #endregion

    #region Private Methods - Coordinate Conversion

    private Vector3 AxialToWorld(int q, int r, int heightLevel)
    {
        float x, y;

        if (hexOrientation == HexOrientation.FlatTop)
        {
            // Flat-top hex
            float width = tileSize * Mathf.Sqrt(3f);
            x = width * (q + r / 2f);
            y = tileSize * 0.75f * r;
        }
        else
        {
            // Pointy-top hex
            float height = tileSize * Mathf.Sqrt(3f);
            x = tileSize * 0.75f * q;
            y = height * (r + q / 2f);
        }

        // Add height offset
        float zOffset = heightLevel * tileSize * platformHeightMultiplier;

        return new Vector3(x, y + zOffset, 0);
    }

    #endregion

    #region Editor Helpers

    private void OnDrawGizmos()
    {
        if (!showDebug) return;

        // Draw tile positions
        Gizmos.color = Color.cyan;
        foreach (TileData tile in allTiles)
        {
            Gizmos.DrawWireSphere(tile.position, tileSize * 0.2f);
        }
    }

    #endregion
}
