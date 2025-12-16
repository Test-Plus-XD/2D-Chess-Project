using UnityEngine;

/// Data structure for level configuration
[CreateAssetMenu(fileName = "NewLevel", menuName = "Game/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Level Information")]
    [Tooltip("Display name of the level")]
    // Name displayed in UI for this level.
    public string LevelName = "Level 1";
    [Tooltip("Level description")]
    [TextArea(2, 4)]
    // Description shown in level select menu.
    public string Description = "Defeat all enemy pieces";
    [Tooltip("Level number (for display)")]
    // Numeric identifier for this level.
    public int LevelNumber = 1;

    [Header("Chess Board Settings")]
    [Tooltip("Radius of the hex grid (1 = center + 6 neighbors)")]
    // Radius of the hexagonal grid determines board size.
    public int GridRadius = 2;
    [Tooltip("Extra rows added to top/bottom of grid")]
    // Additional rows to extend the grid vertically.
    public int ExtraRows = 1;
    [Tooltip("Size of hex tiles")]
    // Size of each hexagonal tile in world units.
    public float TileSize = 1f;

    [Header("Opponent Spawning")]
    [Tooltip("Number of Basic pawns")]
    // Number of Basic type opponent pawns to spawn.
    public int BasicPawnCount = 2;
    [Tooltip("Number of Handcannon pawns")]
    // Number of Handcannon type opponent pawns to spawn.
    public int HandcannonCount = 1;
    [Tooltip("Number of Shotgun pawns")]
    // Number of Shotgun type opponent pawns to spawn.
    public int ShotgunCount = 1;
    [Tooltip("Number of Sniper pawns")]
    // Number of Sniper type opponent pawns to spawn.
    public int SniperCount = 0;
    [Tooltip("Allow multiple pawns on same tile")]
    // Whether multiple pawns can occupy the same tile.
    public bool AllowStacking = false;

    [Header("Player Settings")]
    [Tooltip("Player starting HP")]
    // Player's initial health points at level start.
    public int PlayerStartHP = 2;
    [Tooltip("Player maximum HP")]
    // Maximum health points the player can have.
    public int PlayerMaxHP = 3;

    [Header("Platform Settings")]
    [Tooltip("Number of floor tiles in Standoff arena")]
    // Number of floor tiles to generate in Standoff mode.
    public int StandoffFloorTiles = 6;
    [Tooltip("Which floor tile to build platform above (0-based)")]
    // Index of floor tile to build the platform above.
    public int PlatformBaseIndex = 2;
    [Tooltip("Number of platform expansions")]
    // Number of times to expand the platform structure.
    public int PlatformExpansions = 2;
    [Tooltip("Number of random tiles to add")]
    // Number of random connecting tiles to add to arena.
    public int RandomTileCount = 3;

    [Header("Difficulty Settings")]
    [Tooltip("Opponent fire rate (seconds between shots)")]
    // Time in seconds between opponent shots.
    public float OpponentFireRate = 1.5f;
    [Tooltip("Detection range for line-of-sight firing")]
    // Maximum detection range for opponent line-of-sight.
    public float DetectionRange = 10f;

    [Header("Visual Settings")]
    [Tooltip("Background music clip for Chess mode")]
    // Music track to play during Chess mode for this level.
    public AudioClip ChessModeMusic;
    [Tooltip("Background music clip for Standoff mode")]
    // Music track to play during Standoff mode for this level.
    public AudioClip StandoffModeMusic;
    [Tooltip("Skybox material")]
    // Skybox material for the level's background.
    public Material Skybox;

    /// Get total opponent count for this level
    public int GetTotalOpponentCount()
    {
        return BasicPawnCount + HandcannonCount + ShotgunCount + SniperCount;
    }

    /// Validate level data
    public bool IsValid()
    {
        if (GridRadius < 1) return false;
        if (TileSize <= 0) return false;
        if (GetTotalOpponentCount() <= 0) return false;
        if (PlayerMaxHP <= 0) return false;
        return true;
    }
}