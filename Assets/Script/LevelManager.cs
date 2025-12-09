using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// Manages level loading, configuration, and progression
/// </summary>
public class LevelManager : MonoBehaviour
{
    #region Singleton

    public static LevelManager Instance { get; private set; }

    #endregion

    #region Inspector Fields

    [Header("Level Presets")]
    [Tooltip("Array of level data (3 levels)")]
    [SerializeField] private LevelData[] levels = new LevelData[3];

    [Header("Current Level")]
    [Tooltip("Currently loaded level (viewable in inspector)")]
    [SerializeField] private LevelData currentLevelData;

    [Tooltip("Current level index (viewable in inspector)")]
    [SerializeField] private int currentLevelIndex = 0;

    [Header("Scene References")]
    [Tooltip("Hex grid generator")]
    [SerializeField] private HexGridGenerator gridGenerator;

    [Tooltip("Pawn spawner")]
    [SerializeField] private PawnSpawner pawnSpawner;

    [Tooltip("Player spawner")]
    [SerializeField] private PlayerSpawner playerSpawner;

    [Tooltip("Platform generator")]
    [SerializeField] private Platform platformGenerator;

    [Header("Events")]
    [Tooltip("Called when a level is loaded")]
    public UnityEvent<LevelData> OnLevelLoaded;

    [Tooltip("Called when a level is completed")]
    public UnityEvent<int> OnLevelCompleted;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = true;

    #endregion

    #region Properties

    /// <summary>
    /// Get current level data
    /// </summary>
    public LevelData CurrentLevel => currentLevelData;

    /// <summary>
    /// Get current level index
    /// </summary>
    public int CurrentLevelIndex => currentLevelIndex;

    /// <summary>
    /// Get total number of levels
    /// </summary>
    public int TotalLevels => levels.Length;

    /// <summary>
    /// Check if there are more levels to play
    /// </summary>
    public bool HasNextLevel => currentLevelIndex < levels.Length - 1;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Auto-find references if not set
        if (gridGenerator == null) gridGenerator = FindObjectOfType<HexGridGenerator>();
        if (pawnSpawner == null) pawnSpawner = FindObjectOfType<PawnSpawner>();
        if (playerSpawner == null) playerSpawner = FindObjectOfType<PlayerSpawner>();
        if (platformGenerator == null) platformGenerator = FindObjectOfType<Platform>();
    }

    #endregion

    #region Public Methods - Level Loading

    /// <summary>
    /// Load a level by index
    /// </summary>
    public void LoadLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= levels.Length)
        {
            Debug.LogError($"Invalid level index: {levelIndex}");
            return;
        }

        currentLevelIndex = levelIndex;
        currentLevelData = levels[levelIndex];

        if (currentLevelData == null)
        {
            Debug.LogError($"Level data is null at index {levelIndex}");
            return;
        }

        if (!currentLevelData.IsValid())
        {
            Debug.LogError($"Level data is invalid at index {levelIndex}");
            return;
        }

        if (showDebug)
        {
            Debug.Log($"Loading Level {levelIndex}: {currentLevelData.LevelName}");
        }

        ApplyLevelSettings();
        OnLevelLoaded?.Invoke(currentLevelData);
    }

    /// <summary>
    /// Load next level
    /// </summary>
    public void LoadNextLevel()
    {
        if (HasNextLevel)
        {
            LoadLevel(currentLevelIndex + 1);
        }
        else
        {
            if (showDebug)
            {
                Debug.Log("No more levels available");
            }
        }
    }

    /// <summary>
    /// Reload current level
    /// </summary>
    public void ReloadCurrentLevel()
    {
        LoadLevel(currentLevelIndex);
    }

    /// <summary>
    /// Complete current level
    /// </summary>
    public void CompleteLevel()
    {
        if (showDebug)
        {
            Debug.Log($"Level {currentLevelIndex} completed!");
        }

        OnLevelCompleted?.Invoke(currentLevelIndex);
    }

    #endregion

    #region Public Methods - Level Access

    /// <summary>
    /// Get level data by index
    /// </summary>
    public LevelData GetLevel(int index)
    {
        if (index >= 0 && index < levels.Length)
        {
            return levels[index];
        }
        return null;
    }

    /// <summary>
    /// Get all levels
    /// </summary>
    public LevelData[] GetAllLevels()
    {
        return levels;
    }

    #endregion

    #region Private Methods

    private void ApplyLevelSettings()
    {
        if (currentLevelData == null) return;

        // Configure hex grid
        if (gridGenerator != null)
        {
            gridGenerator.SetRadius(currentLevelData.GridRadius);
            gridGenerator.SetExtraRow(currentLevelData.ExtraRows);
            gridGenerator.SetTileSize(currentLevelData.TileSize);
            gridGenerator.GenerateGrid();
        }

        // Configure pawn spawner
        if (pawnSpawner != null)
        {
            pawnSpawner.SetPawnCount(
                currentLevelData.BasicPawnCount,
                currentLevelData.HandcannonCount,
                currentLevelData.ShotgunCount,
                currentLevelData.SniperCount
            );
            pawnSpawner.SpawnAllPawns();
        }

        // Configure player
        if (playerSpawner != null)
        {
            playerSpawner.SpawnPlayer();

            // Set player HP
            PlayerPawn playerPawn = FindObjectOfType<PlayerPawn>();
            if (playerPawn != null)
            {
                playerPawn.SetMaxHP(currentLevelData.PlayerMaxHP);
                playerPawn.SetHP(currentLevelData.PlayerStartHP);
            }
        }

        // Configure platform (but don't generate yet - wait for Standoff)
        if (platformGenerator != null)
        {
            // Platform settings will be applied when transitioning to Standoff
        }

        // Apply visual settings
        ApplyVisualSettings();
    }

    private void ApplyVisualSettings()
    {
        // Set skybox
        if (currentLevelData.Skybox != null)
        {
            RenderSettings.skybox = currentLevelData.Skybox;
        }

        // Play background music
        if (currentLevelData.BackgroundMusic != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMusic(currentLevelData.BackgroundMusic);
        }
    }

    #endregion

    #region Editor Helpers

    [ContextMenu("Validate All Levels")]
    private void ValidateAllLevels()
    {
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] == null)
            {
                Debug.LogWarning($"Level {i} is null!");
            }
            else if (!levels[i].IsValid())
            {
                Debug.LogWarning($"Level {i} ({levels[i].LevelName}) has invalid data!");
            }
            else
            {
                Debug.Log($"Level {i} ({levels[i].LevelName}) is valid");
            }
        }
    }

    #endregion
}
