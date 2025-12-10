using UnityEngine;

/// Data structure for level configuration
[CreateAssetMenu(fileName = "NewLevel", menuName = "Game/Level Data")]
public class LevelData : ScriptableObject
{
    #region Inspector Fields

    [Header("Level Information")]
    [Tooltip("Display name of the level")]
    public string LevelName = "Level 1";

    [Tooltip("Level description")]
    [TextArea(2, 4)]
    public string Description = "Defeat all enemy pieces";

    [Tooltip("Level number (for display)")]
    public int LevelNumber = 1;

    [Header("Chess Board Settings")]
    [Tooltip("Radius of the hex grid (1 = center + 6 neighbors)")]
    public int GridRadius = 2;

    [Tooltip("Extra rows added to top/bottom of grid")]
    public int ExtraRows = 1;

    [Tooltip("Size of hex tiles")]
    public float TileSize = 1f;

    [Header("Opponent Spawning")]
    [Tooltip("Number of Basic pawns")]
    public int BasicPawnCount = 2;

    [Tooltip("Number of Handcannon pawns")]
    public int HandcannonCount = 1;

    [Tooltip("Number of Shotgun pawns")]
    public int ShotgunCount = 1;

    [Tooltip("Number of Sniper pawns")]
    public int SniperCount = 0;

    [Tooltip("Allow multiple pawns on same tile")]
    public bool AllowStacking = false;

    [Header("Player Settings")]
    [Tooltip("Player starting HP")]
    public int PlayerStartHP = 2;

    [Tooltip("Player maximum HP")]
    public int PlayerMaxHP = 3;

    [Header("Platform Settings")]
    [Tooltip("Number of floor tiles in Standoff arena")]
    public int StandoffFloorTiles = 6;

    [Tooltip("Which floor tile to build platform above (0-based)")]
    public int PlatformBaseIndex = 2;

    [Tooltip("Number of platform expansions")]
    public int PlatformExpansions = 2;

    [Tooltip("Number of random tiles to add")]
    public int RandomTileCount = 3;

    [Header("Difficulty Settings")]
    [Tooltip("Opponent fire rate (seconds between shots)")]
    public float OpponentFireRate = 1.5f;

    [Tooltip("Detection range for line-of-sight firing")]
    public float DetectionRange = 10f;

    [Header("Visual Settings")]
    [Tooltip("Background music clip")]
    public AudioClip BackgroundMusic;

    [Tooltip("Skybox material")]
    public Material Skybox;

    #endregion

    #region Public Methods

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

    #endregion
}
