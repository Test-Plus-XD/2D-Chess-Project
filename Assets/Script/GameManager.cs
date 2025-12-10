using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

// Unified game manager handling game state, level loading, and progression.
// Consolidates GameStateManager and LevelManager functionality.
public class GameManager : MonoBehaviour
{
    #region Singleton

    public static GameManager Instance { get; private set; }

    #endregion

    #region Enums

    public enum GameState
    {
        MainMenu,
        LevelSelect,
        ChessMode,
        Standoff,
        Victory,
        Defeat,
        Paused
    }

    #endregion

    #region Inspector Fields - State Management

    [Header("Current State")]
    [Tooltip("Current game state (viewable in inspector)")]
    [SerializeField] private GameState currentState = GameState.MainMenu;

    [Header("References")]
    [Tooltip("Reference to the Checkerboard manager")]
    [SerializeField] private Checkerboard checkerboard;

    [Tooltip("Reference to the Platform generator")]
    [SerializeField] private Platform platformGenerator;

    [Tooltip("Reference to the player controller")]
    [SerializeField] private PlayerController playerController;

    [Header("Transition Settings")]
    [Tooltip("Delay before transitioning to Standoff mode")]
    [SerializeField] private float standoffTransitionDelay = 1.5f;

    [Tooltip("Duration of transition fade effect")]
    [SerializeField] private float transitionDuration = 1f;

    [Header("Standoff Settings")]
    [Tooltip("Minimum opponents remaining to trigger Standoff")]
    [SerializeField] private int standoffTriggerCount = 1;

    #endregion

    #region Inspector Fields - Level Management

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

    [Tooltip("Spawner system")]
    [SerializeField] private SpawnerSystem spawnerSystem;

    #endregion

    #region Inspector Fields - Events

    [Header("State Events")]
    [Tooltip("Called when game state changes")]
    public UnityEvent<GameState> OnStateChanged;

    [Tooltip("Called when transitioning to Standoff mode")]
    public UnityEvent OnStandoffBegin;

    [Tooltip("Called when player wins")]
    public UnityEvent OnVictory;

    [Tooltip("Called when player is defeated")]
    public UnityEvent OnDefeat;

    [Header("Level Events")]
    [Tooltip("Called when a level is loaded")]
    public UnityEvent<LevelData> OnLevelLoaded;

    [Tooltip("Called when a level is completed")]
    public UnityEvent<int> OnLevelCompleted;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = true;

    #endregion

    #region Private Fields

    private bool hasTransitionedToStandoff = false;

    #endregion

    #region Properties - State

    public GameState CurrentState => currentState;
    public bool IsChessMode => currentState == GameState.ChessMode;
    public bool IsStandoffMode => currentState == GameState.Standoff;

    #endregion

    #region Properties - Level

    public LevelData CurrentLevel => currentLevelData;
    public int CurrentLevelIndex => currentLevelIndex;
    public int TotalLevels => levels.Length;
    public bool HasNextLevel => currentLevelIndex < levels.Length - 1;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Auto-find references
        if (gridGenerator == null) gridGenerator = FindObjectOfType<HexGridGenerator>();
        if (spawnerSystem == null) spawnerSystem = FindObjectOfType<SpawnerSystem>();
        if (platformGenerator == null) platformGenerator = FindObjectOfType<Platform>();
    }

    private void Start()
    {
        if (checkerboard == null) checkerboard = FindObjectOfType<Checkerboard>();
        if (platformGenerator == null) platformGenerator = FindObjectOfType<Platform>();
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
    }

    private void Update()
    {
        if (currentState == GameState.ChessMode && !hasTransitionedToStandoff)
        {
            CheckStandoffCondition();
        }
    }

    #endregion

    #region Public Methods - State Management

    public void SetState(GameState newState)
    {
        if (currentState == newState) return;

        GameState previousState = currentState;
        currentState = newState;

        if (showDebug)
        {
            Debug.Log($"Game state changed: {previousState} -> {newState}");
        }

        OnStateChanged?.Invoke(newState);
        HandleStateTransition(previousState, newState);
    }

    public void StartGame(int levelIndex)
    {
        currentLevelIndex = levelIndex;
        hasTransitionedToStandoff = false;

        if (showDebug)
        {
            Debug.Log($"Starting game - Level {levelIndex}");
        }

        SetState(GameState.ChessMode);
    }

    public void TriggerVictory()
    {
        SetState(GameState.Victory);
        OnVictory?.Invoke();

        if (showDebug) Debug.Log("Victory!");
    }

    public void TriggerDefeat()
    {
        SetState(GameState.Defeat);
        OnDefeat?.Invoke();

        if (showDebug) Debug.Log("Defeat!");
    }

    public void PauseGame()
    {
        SetState(GameState.Paused);
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        SetState(hasTransitionedToStandoff ? GameState.Standoff : GameState.ChessMode);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SetState(GameState.MainMenu);
    }

    public void OpenLevelSelect()
    {
        SetState(GameState.LevelSelect);
    }

    public void TriggerStandoff()
    {
        if (hasTransitionedToStandoff) return;
        StartCoroutine(TransitionToStandoff());
    }

    #endregion

    #region Public Methods - Level Management

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

    public void LoadNextLevel()
    {
        if (HasNextLevel)
        {
            LoadLevel(currentLevelIndex + 1);
        }
        else if (showDebug)
        {
            Debug.Log("No more levels available");
        }
    }

    public void ReloadCurrentLevel()
    {
        LoadLevel(currentLevelIndex);
    }

    public void CompleteLevel()
    {
        if (showDebug) Debug.Log($"Level {currentLevelIndex} completed!");
        OnLevelCompleted?.Invoke(currentLevelIndex);
    }

    public LevelData GetLevel(int index)
    {
        if (index >= 0 && index < levels.Length) return levels[index];
        return null;
    }

    public LevelData[] GetAllLevels()
    {
        return levels;
    }

    #endregion

    #region Private Methods - State Transitions

    private void HandleStateTransition(GameState from, GameState to)
    {
        switch (from)
        {
            case GameState.Paused:
                Time.timeScale = 1f;
                break;
        }

        switch (to)
        {
            case GameState.ChessMode:
                SetupChessMode();
                break;
            case GameState.Standoff:
                SetupStandoffMode();
                break;
            case GameState.Victory:
            case GameState.Defeat:
                Time.timeScale = 1f;
                break;
        }
    }

    private void SetupChessMode()
    {
        if (checkerboard != null) checkerboard.gameObject.SetActive(true);
        if (platformGenerator != null) platformGenerator.gameObject.SetActive(false);
        if (playerController != null) playerController.SetStandoffMode(false);
    }

    private void SetupStandoffMode()
    {
        if (checkerboard != null) checkerboard.gameObject.SetActive(false);
        if (platformGenerator != null) platformGenerator.gameObject.SetActive(true);
        if (playerController != null) playerController.SetStandoffMode(true);
        OnStandoffBegin?.Invoke();
    }

    private void CheckStandoffCondition()
    {
        if (checkerboard == null) return;

        int remainingOpponents = checkerboard.GetOpponentControllers().Count;
        if (remainingOpponents <= standoffTriggerCount)
        {
            TriggerStandoff();
        }
    }

    private IEnumerator TransitionToStandoff()
    {
        hasTransitionedToStandoff = true;

        if (showDebug) Debug.Log("Transitioning to Standoff mode...");

        yield return new WaitForSeconds(standoffTransitionDelay);

        SetState(GameState.Standoff);

        if (platformGenerator != null)
        {
            platformGenerator.GenerateArena();
        }

        PositionPawnsForStandoff();
    }

    private void PositionPawnsForStandoff()
    {
        if (platformGenerator == null) return;

        List<Platform.TileData> floorTiles = new List<Platform.TileData>();
        foreach (var tile in platformGenerator.GetAllTiles())
        {
            if (tile.heightLevel == 0) floorTiles.Add(tile);
        }

        if (floorTiles.Count < 2) return;

        Platform.TileData leftMostTile = floorTiles[0];
        Platform.TileData rightMostTile = floorTiles[0];

        foreach (var tile in floorTiles)
        {
            if (tile.position.x < leftMostTile.position.x) leftMostTile = tile;
            if (tile.position.x > rightMostTile.position.x) rightMostTile = tile;
        }

        float tileHeight = currentLevelData != null ? currentLevelData.TileSize : 1f;

        if (playerController != null)
        {
            Vector3 playerPos = leftMostTile.position;
            playerPos.y += tileHeight;
            playerController.transform.position = playerPos;

            if (showDebug) Debug.Log($"Player positioned at {playerPos}");
        }

        if (checkerboard != null)
        {
            var opponents = checkerboard.GetOpponentControllers();
            if (opponents.Count > 0)
            {
                Vector3 opponentPos = rightMostTile.position;
                opponentPos.y += tileHeight;
                opponents[0].transform.position = opponentPos;

                if (showDebug) Debug.Log($"Opponent positioned at {opponentPos}");
            }
        }
    }

    #endregion

    #region Private Methods - Level Settings

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

        // Configure spawner
        if (spawnerSystem != null)
        {
            spawnerSystem.SetPawnCounts(
                currentLevelData.BasicPawnCount,
                currentLevelData.HandcannonCount,
                currentLevelData.ShotgunCount,
                currentLevelData.SniperCount
            );
            spawnerSystem.SpawnAll();
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
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.Skybox;
            }
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
                Debug.LogWarning($"Level {i} is null!");
            else if (!levels[i].IsValid())
                Debug.LogWarning($"Level {i} ({levels[i].LevelName}) has invalid data!");
            else
                Debug.Log($"Level {i} ({levels[i].LevelName}) is valid");
        }
    }

    #endregion
}
