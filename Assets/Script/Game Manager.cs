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

    [Header("Current State")]
    [Tooltip("Current game state (viewable in inspector)")]
    // Current game state for state machine management.
    [SerializeField] private GameState currentState = GameState.MainMenu;

    [Header("References")]
    [Tooltip("Reference to the Checkerboard manager")]
    // Turn coordinator for chess mode operations.
    [SerializeField] private Checkerboard checkerboard;
    [Tooltip("Reference to the Platform generator")]
    // Platform generator for standoff mode arena creation.
    [SerializeField] private Platform platformGenerator;
    [Tooltip("Reference to the player controller")]
    // Player controller for mode switching and state management.
    [SerializeField] private PlayerController playerController;

    [Header("Transition Settings")]
    [Tooltip("Delay before transitioning to Standoff mode")]
    // Duration to wait before transitioning from chess to standoff mode.
    [SerializeField] private float standoffTransitionDelay = 1.5f;

    [Header("Standoff Settings")]
    [Tooltip("Minimum opponents remaining to trigger Standoff")]
    // Number of opponents remaining to trigger standoff transition.
    [SerializeField] private int standoffTriggerCount = 1;

    [Header("Level Presets")]
    [Tooltip("Array of level data (3 levels)")]
    // Array of all available level configurations.
    [SerializeField] private LevelData[] levels = new LevelData[3];

    [Header("Current Level")]
    [Tooltip("Currently loaded level (viewable in inspector)")]
    // Currently active level data.
    [SerializeField] private LevelData currentLevelData;
    [Tooltip("Current level index (viewable in inspector)")]
    // Index of the currently loaded level.
    [SerializeField] private int currentLevelIndex = 0;

    [Header("Scene References")]
    [Tooltip("Hex grid generator")]
    // Hex grid generator for chess mode board creation.
    [SerializeField] private HexGridGenerator gridGenerator;
    [Tooltip("Spawner system")]
    // Spawner system for opponent and player pawn creation.
    [SerializeField] private Spawner spawnerSystem;

    [Header("State Events")]
    [Tooltip("Called when game state changes")]
    // Event invoked when the game state changes.
    public UnityEvent<GameState> OnStateChanged;
    [Tooltip("Called when transitioning to Standoff mode")]
    // Event invoked when transitioning to standoff mode.
    public UnityEvent OnStandoffBegin;
    [Tooltip("Called when player wins")]
    // Event invoked when the player achieves victory.
    public UnityEvent OnVictory;
    [Tooltip("Called when player is defeated")]
    // Event invoked when the player is defeated.
    public UnityEvent OnDefeat;

    [Header("Level Events")]
    [Tooltip("Called when a level is loaded")]
    // Event invoked when a new level is loaded.
    public UnityEvent<LevelData> OnLevelLoaded;
    [Tooltip("Called when a level is completed")]
    // Event invoked when a level is completed.
    public UnityEvent<int> OnLevelCompleted;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    // Enable debug logging for game state changes.
    [SerializeField] private bool showDebug = true;

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
        if (gridGenerator == null) gridGenerator = FindFirstObjectByType<HexGridGenerator>();
        if (spawnerSystem == null) spawnerSystem = FindFirstObjectByType<Spawner>();
        if (platformGenerator == null) platformGenerator = FindFirstObjectByType<Platform>();
    }

    private void Start()
    {
        if (checkerboard == null) checkerboard = FindFirstObjectByType<Checkerboard>();
        if (platformGenerator == null) platformGenerator = FindFirstObjectByType<Platform>();
        // Don't find player controller here - it will be spawned later
        // playerController will be found dynamically when needed
    }

    private void Update()
    {
        // Only check standoff condition after a delay to allow spawning to complete
        if (currentState == GameState.ChessMode && !hasTransitionedToStandoff && Time.time > 1f)
        {
            CheckStandoffCondition();
        }
    }

    #endregion

    #region Public Methods - State Management

    /// Transition to a new game state (MainMenu → LevelSelect → ChessMode → Standoff → Victory/Defeat)
    public void SetState(GameState newState)
    {
        // Ignore redundant state changes
        if (currentState == newState) return;

        // Record previous state for transition handling
        GameState previousState = currentState;
        currentState = newState;

        if (showDebug)
        {
            Debug.Log($"Game state changed: {previousState} -> {newState}");
        }

        // Notify listeners of state change
        OnStateChanged?.Invoke(newState);
        // Execute state-specific transition logic
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
            case GameState.MainMenu:
            case GameState.LevelSelect:
                // Play universal menu music
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayMenuMusic();
                }
                break;
            case GameState.ChessMode:
                SetupChessMode();
                // Play Chess mode music from level data
                if (currentLevelData != null && currentLevelData.ChessModeMusic != null && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayMusic(currentLevelData.ChessModeMusic);
                }
                break;
            case GameState.Standoff:
                SetupStandoffMode();
                // Play Standoff mode music from level data
                if (currentLevelData != null && currentLevelData.StandoffModeMusic != null && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayMusic(currentLevelData.StandoffModeMusic);
                }
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
        if (gridGenerator != null) gridGenerator.gameObject.SetActive(true);
        if (platformGenerator != null) platformGenerator.gameObject.SetActive(false);

        // Find player controller if not already assigned
        if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null) playerController.SetStandoffMode(false);
    }

    private void SetupStandoffMode()
    {
        if (checkerboard != null) checkerboard.gameObject.SetActive(false);
        if (gridGenerator != null) gridGenerator.gameObject.SetActive(false);
        if (platformGenerator != null) platformGenerator.gameObject.SetActive(true);

        // Find player controller if not already assigned
        if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null) playerController.SetStandoffMode(true);
        OnStandoffBegin?.Invoke();
    }

    private void CheckStandoffCondition()
    {
        if (checkerboard == null) return;

        int remainingOpponents = checkerboard.GetOpponentControllers().Count;
        // Only trigger standoff if opponents were spawned and now only 1 remains
        // Initial spawn count check prevents triggering before spawning completes
        if (remainingOpponents > 0 && remainingOpponents <= standoffTriggerCount)
        {
            // Ensure we have exactly standoffTriggerCount (1) opponent left
            // This prevents triggering at game start when count is 0
            TriggerStandoff();
        }
    }

    private IEnumerator TransitionToStandoff()
    {
        hasTransitionedToStandoff = true;

        if (showDebug) Debug.Log("Transitioning to Standoff mode...");

        // Convert Basic type to Handcannon if last opponent
        if (checkerboard != null)
        {
            var opponents = checkerboard.GetOpponentControllers();
            if (opponents.Count == 1)
            {
                PawnController lastOpponent = opponents[0];
                if (lastOpponent != null && lastOpponent.aiType == PawnController.AIType.Basic)
                {
                    lastOpponent.ConvertBasicToHandcannon();

                    // Ensure the converted opponent has a weapon system
                    WeaponSystem weaponSystem = lastOpponent.GetComponent<WeaponSystem>();
                    if (weaponSystem == null)
                    {
                        weaponSystem = lastOpponent.gameObject.AddComponent<WeaponSystem>();
                    }
                }
            }
        }

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

        // Use Platform's built-in spawn position calculations.
        // Player spawns at leftmost floor tile, opponent at rightmost.
        // Both spawn one tile height above the highest tile in the arena.
        Vector3 playerSpawnPos = platformGenerator.GetPlayerSpawnPosition();
        Vector3 opponentSpawnPos = platformGenerator.GetOpponentSpawnPosition();

        // Find player controller if not already assigned
        if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();

        if (playerController != null)
        {
            playerController.transform.position = playerSpawnPos;

            if (showDebug) Debug.Log($"Player positioned at {playerSpawnPos}");
        }

        if (checkerboard != null)
        {
            var opponents = checkerboard.GetOpponentControllers();
            if (opponents.Count > 0)
            {
                opponents[0].transform.position = opponentSpawnPos;

                if (showDebug) Debug.Log($"Opponent positioned at {opponentSpawnPos}");
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

        // Music is now handled by HandleStateTransition to support Chess/Standoff mode switching
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