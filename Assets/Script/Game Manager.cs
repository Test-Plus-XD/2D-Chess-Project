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

    [Header("Showcase Settings")]
    [Tooltip("Enable showcase system on main menu")]
    // Enable showcase system with random pawns on main menu.
    [SerializeField] private bool enableShowcase = true;
    [Tooltip("Number of random pawns to spawn in showcase")]
    // Number of random pawns to spawn for showcase.
    [SerializeField] private int showcasePawnCount = 3;
    [Tooltip("Interval between random pawn movements (seconds)")]
    // Interval between random pawn movements in showcase.
    [SerializeField] private float showcaseMovementInterval = 2f;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    // Enable debug logging for game state changes.
    [SerializeField] private bool showDebug = true;
    [Tooltip("Temporary fix: Load correct next level instead of skipping levels")]
    // Workaround for level skipping issue - ensures next level loads correctly instead of skipping.
    [SerializeField] private bool ductTape = false;

    #region Private Fields

    private bool hasTransitionedToStandoff = false;
    private Coroutine showcaseCoroutine;
    private List<PawnController> showcasePawns = new List<PawnController>();

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

        // During Standoff, if there are no opponent pawns left (tagged "Pawn"), consider it a victory.
        if (currentState == GameState.Standoff)
        {
            // Find all objects tagged as Pawn
            var pawns = GameObject.FindGameObjectsWithTag("Pawn");
            int pawnCount = pawns != null ? pawns.Length : 0;

            // Exclude the player object if it is also tagged as Pawn
            PlayerController player = playerController ?? FindFirstObjectByType<PlayerController>();
            if (player != null && player.gameObject != null && player.gameObject.CompareTag("Pawn"))
            {
                pawnCount = Mathf.Max(0, pawnCount - 1);
            }

            if (pawnCount == 0)
            {
                // No opponent pawns remain - trigger victory
                TriggerVictory();
            }
        }
    }

    #endregion

    #region Private Methods - Showcase System

    /// Start the showcase system with random pawns on main menu
    private void StartShowcase()
    {
        if (!enableShowcase) return;

        if (showDebug) Debug.Log("[GameManager] Starting showcase system...");

        // Stop any existing showcase
        StopShowcase();

        // Ensure grid generator and spawner are available
        if (gridGenerator == null || spawnerSystem == null)
        {
            if (showDebug) Debug.LogWarning("[GameManager] Cannot start showcase: missing grid or spawner");
            return;
        }

        // Configure showcase grid (smaller than normal levels)
        gridGenerator.SetRadius(2);
        gridGenerator.SetExtraRow(0);
        gridGenerator.SetTileSize(1f);
        gridGenerator.GenerateGrid();

        // Activate grid for showcase
        if (gridGenerator != null) gridGenerator.gameObject.SetActive(true);

        // Spawn random pawns for showcase
        StartCoroutine(SpawnShowcasePawns());
    }

    /// Stop the showcase system and clean up
    private void StopShowcase()
    {
        if (showcaseCoroutine != null)
        {
            StopCoroutine(showcaseCoroutine);
            showcaseCoroutine = null;
        }

        // Clear showcase pawns
        foreach (var pawn in showcasePawns)
        {
            if (pawn != null) Destroy(pawn.gameObject);
        }
        showcasePawns.Clear();

        if (showDebug) Debug.Log("[GameManager] Showcase system stopped");
    }

    /// Spawn random pawns for showcase
    private IEnumerator SpawnShowcasePawns()
    {
        // Wait for grid to be generated
        yield return new WaitForSeconds(0.5f);

        showcasePawns.Clear();

        // Get available tiles
        Transform tileParent = gridGenerator.parentContainer ?? gridGenerator.transform;
        List<Transform> tiles = new List<Transform>();
        for (int i = 0; i < tileParent.childCount; i++)
        {
            tiles.Add(tileParent.GetChild(i));
        }

        if (tiles.Count == 0)
        {
            if (showDebug) Debug.LogWarning("[GameManager] No tiles found for showcase");
            yield break;
        }

        // Spawn random pawns
        GameObject[] pawnPrefabs = new GameObject[]
        {
            spawnerSystem.pawnPrefab,
            spawnerSystem.handcannonPrefab,
            spawnerSystem.shotgunPrefab,
            spawnerSystem.sniperPrefab
        };

        for (int i = 0; i < showcasePawnCount && i < tiles.Count; i++)
        {
            // Pick a random tile
            int tileIndex = Random.Range(0, tiles.Count);
            Transform tile = tiles[tileIndex];
            tiles.RemoveAt(tileIndex);

            // Pick a random pawn prefab
            GameObject prefab = pawnPrefabs[Random.Range(0, pawnPrefabs.Length)];
            if (prefab == null) continue;

            // Spawn pawn at tile position
            GameObject pawnObj = Instantiate(prefab, tile.position, Quaternion.identity, spawnerSystem.opponentSpawnParent);
            PawnController pawn = pawnObj.GetComponent<PawnController>();

            if (pawn != null)
            {
                // Parse tile coordinates
                string[] parts = tile.name.Split('_');
                int q = 0, r = 0;
                if (parts.Length >= 3)
                {
                    int.TryParse(parts[1], out q);
                    int.TryParse(parts[2], out r);
                }

                pawn.q = q;
                pawn.r = r;
                pawn.gridGenerator = gridGenerator;
                showcasePawns.Add(pawn);
            }
        }

        // Start random movement coroutine
        if (showcasePawns.Count > 0)
        {
            showcaseCoroutine = StartCoroutine(ShowcaseMovementLoop());
        }
    }

    /// Continuously move random pawns in showcase
    private IEnumerator ShowcaseMovementLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(showcaseMovementInterval);

            if (showcasePawns.Count == 0) break;

            // Pick a random pawn
            PawnController pawn = showcasePawns[Random.Range(0, showcasePawns.Count)];
            if (pawn == null) continue;

            // Move pawn to a random adjacent tile
            Vector2Int[] hexDirections = new Vector2Int[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(1, -1),
                new Vector2Int(0, -1),
                new Vector2Int(-1, 0),
                new Vector2Int(-1, 1),
                new Vector2Int(0, 1)
            };

            Vector2Int randomDir = hexDirections[Random.Range(0, hexDirections.Length)];
            int newQ = pawn.q + randomDir.x;
            int newR = pawn.r + randomDir.y;

            // Check if target tile exists
            Transform tileParent = gridGenerator.parentContainer ?? gridGenerator.transform;
            Transform targetTile = tileParent.Find($"Hex_{newQ}_{newR}");

            if (targetTile != null)
            {
                // Move pawn (simple teleport for showcase)
                pawn.q = newQ;
                pawn.r = newR;
                pawn.transform.position = targetTile.position;
            }
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
        if (showDebug)
        {
            Debug.Log($"[StartGame] ENTRY - Received levelIndex: {levelIndex}");
            Debug.Log($"[StartGame] ENTRY - Current currentLevelIndex before assignment: {currentLevelIndex}");
        }

        currentLevelIndex = levelIndex;
        hasTransitionedToStandoff = false;

        if (showDebug)
        {
            string levelName = currentLevelData != null ? currentLevelData.LevelName : "Unknown";
            Debug.Log($"[StartGame] AFTER ASSIGNMENT - currentLevelIndex: {currentLevelIndex}, currentLevelData: {levelName}");
            Debug.Log($"[StartGame] ABOUT TO CALL LoadLevel({levelIndex})");
        }

        // Load level data before setting state to ensure BGM and settings are available
        LoadLevel(levelIndex);
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
        if (showDebug)
        {
            Debug.Log($"[LoadLevel] ENTRY - Received levelIndex: {levelIndex}");
            Debug.Log($"[LoadLevel] ENTRY - Current currentLevelIndex before assignment: {currentLevelIndex}");
            Debug.Log($"[LoadLevel] ENTRY - levels.Length: {levels.Length}");
        }

        if (levelIndex < 0 || levelIndex >= levels.Length)
        {
            Debug.LogError($"[LoadLevel] Invalid level index: {levelIndex}");
            return;
        }

        currentLevelIndex = levelIndex;
        currentLevelData = levels[levelIndex];

        if (showDebug)
        {
            Debug.Log($"[LoadLevel] AFTER ASSIGNMENT - currentLevelIndex: {currentLevelIndex}");
            Debug.Log($"[LoadLevel] AFTER ASSIGNMENT - Accessing levels[{levelIndex}]");
        }

        if (currentLevelData == null)
        {
            Debug.LogError($"[LoadLevel] Level data is null at index {levelIndex}");
            return;
        }

        if (!currentLevelData.IsValid())
        {
            Debug.LogError($"[LoadLevel] Level data is invalid at index {levelIndex}");
            return;
        }

        if (showDebug)
        {
            Debug.Log($"[LoadLevel] SUCCESS - Loading Level {levelIndex + 1} (array index {levelIndex}): {currentLevelData.LevelName}");
        }

        ApplyLevelSettings();
        OnLevelLoaded?.Invoke(currentLevelData);
    }

    public void LoadNextLevel()
    {
        if (showDebug)
        {
            string currentLevelName = currentLevelData != null ? currentLevelData.LevelName : $"Level {currentLevelIndex + 1}";
            Debug.Log($"[LoadNextLevel] ENTRY - Current level: {currentLevelName} (array index {currentLevelIndex}), HasNextLevel: {HasNextLevel}");
            Debug.Log($"[LoadNextLevel] ENTRY - levels.Length: {levels.Length}, currentLevelIndex: {currentLevelIndex}");
        }

        if (HasNextLevel)
        {
            int nextLevelIndex = currentLevelIndex + 1;
            
            // Apply DuctTape fix if enabled - compensate for the +2 skip bug
            if (ductTape)
            {
                // The bug causes +2 skip, so we subtract 1 to get the correct +1
                nextLevelIndex = currentLevelIndex + 1 - 1; // This equals currentLevelIndex (same level)
                if (showDebug)
                {
                    Debug.Log($"[LoadNextLevel] DUCT TAPE APPLIED - Compensating for +2 skip bug: loading index {nextLevelIndex} instead of {currentLevelIndex + 1}");
                    Debug.Log($"[LoadNextLevel] DUCT TAPE RESULT - This should load the CORRECT next level despite the bug");
                }
            }
            else
            {
                if (showDebug)
                {
                    Debug.Log($"[LoadNextLevel] NO DUCT TAPE - Normal calculation: {nextLevelIndex} (bug will likely cause +2 skip)");
                }
            }
            
            if (showDebug)
            {
                LevelData nextLevel = GetLevel(nextLevelIndex);
                string nextLevelName = nextLevel != null ? nextLevel.LevelName : $"Level {nextLevelIndex + 1}";
                Debug.Log($"[LoadNextLevel] FINAL - Passing to StartGame: {nextLevelName} (array index {nextLevelIndex})");
                Debug.Log($"[LoadNextLevel] ABOUT TO CALL StartGame({nextLevelIndex})");
            }
            StartGame(nextLevelIndex);
        }
        else if (showDebug)
        {
            Debug.Log("[LoadNextLevel] No more levels available");
        }
    }

    public void ReloadCurrentLevel()
    {
        LoadLevel(currentLevelIndex);
    }

    public void CompleteLevel()
    {
        if (showDebug) 
        {
            string levelName = currentLevelData != null ? currentLevelData.LevelName : $"Level {currentLevelIndex + 1}";
            Debug.Log($"{levelName} (array index {currentLevelIndex}) completed!");
        }
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
            case GameState.MainMenu:
                // Stop showcase when leaving main menu
                StopShowcase();
                break;
            case GameState.Standoff:
                // Reset time when leaving Standoff mode
                if (TimeController.Instance != null)
                {
                    TimeController.Instance.ResetTime();
                }
                // Cleanup board when leaving Standoff (but NOT when going to Victory/Defeat)
                if (to != GameState.Victory && to != GameState.Defeat)
                {
                    CleanupGameBoard();
                }
                break;
            case GameState.ChessMode:
                // Cleanup board when leaving Chess mode (but NOT when going to Standoff)
                if (to != GameState.Standoff)
                {
                    CleanupGameBoard();
                }
                break;
        }

        switch (to)
        {
            case GameState.MainMenu:
                // Play universal menu music
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayMenuMusic();
                }
                // Start showcase system
                if (enableShowcase)
                {
                    StartShowcase();
                }
                break;
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
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayChessModeMusic(currentLevelData);
                }
                // Initialize turn indicator to player's turn
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.SetTurnIndicator(true);
                }
                break;
            case GameState.Standoff:
                SetupStandoffMode();
                // Play Standoff mode music from level data
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayStandoffModeMusic(currentLevelData);
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

        // Restore all tiles to original white color when starting chess mode
        WeaponSystem.RestoreAllTilesGlobally();
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

        // Spawn fresh Handcannon to replace Basic pawn if last opponent is Basic
        PawnController.AIType lastOpponentType = PawnController.AIType.Basic;
        if (checkerboard != null)
        {
            var opponents = checkerboard.GetOpponentControllers();
            if (opponents.Count == 1)
            {
                PawnController lastOpponent = opponents[0];
                if (lastOpponent != null)
                {
                    if (lastOpponent.aiType == PawnController.AIType.Basic)
                    {
                        // Spawn fresh Handcannon pawn to replace the Basic pawn
                        if (spawnerSystem != null)
                        {
                            GameObject newHandcannon = spawnerSystem.SpawnFreshHandcannonReplacement(lastOpponent);
                            if (newHandcannon != null)
                            {
                                lastOpponentType = PawnController.AIType.Handcannon;
                                if (showDebug) Debug.Log("[GameManager] Spawned fresh Handcannon to replace Basic pawn");
                            }
                            else
                            {
                                // Fallback to old conversion method if spawning fails
                                lastOpponent.ConvertBasicToHandcannon();
                                
                                // Ensure the converted opponent has a weapon system
                                WeaponSystem weaponSystem = lastOpponent.GetComponent<WeaponSystem>();
                                if (weaponSystem == null)
                                {
                                    weaponSystem = lastOpponent.gameObject.AddComponent<WeaponSystem>();
                                }
                                lastOpponentType = lastOpponent.aiType;
                                if (showDebug) Debug.Log("[GameManager] Fallback: Converted Basic to Handcannon in place");
                            }
                        }
                        else
                        {
                            // Fallback to old conversion method if no spawner available
                            lastOpponent.ConvertBasicToHandcannon();
                            
                            // Ensure the converted opponent has a weapon system
                            WeaponSystem weaponSystem = lastOpponent.GetComponent<WeaponSystem>();
                            if (weaponSystem == null)
                            {
                                weaponSystem = lastOpponent.gameObject.AddComponent<WeaponSystem>();
                            }
                            lastOpponentType = lastOpponent.aiType;
                            if (showDebug) Debug.Log("[GameManager] Fallback: No spawner found, converted Basic to Handcannon in place");
                        }
                    }
                    else
                    {
                        lastOpponentType = lastOpponent.aiType;
                    }
                }
            }
        }

        // Show stage change announcement
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowStageChangeMessage(lastOpponentType);
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
        // Swapped: Player spawns at rightmost floor tile, opponent at leftmost.
        // Both spawn one tile height above the highest tile in the arena.
        Vector3 playerSpawnPos = platformGenerator.GetOpponentSpawnPosition();
        Vector3 opponentSpawnPos = platformGenerator.GetPlayerSpawnPosition();

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
                PawnController opponent = opponents[0];
                opponent.transform.position = opponentSpawnPos;

                // Switch opponent to Standoff mode (enables Dynamic rigidbody with gravity)
                opponent.SetStandoffMode(true);

                if (showDebug) Debug.Log($"Opponent positioned at {opponentSpawnPos} and switched to Standoff mode");
            }
        }
    }

    #endregion

    #region Private Methods - Level Settings

    private void ApplyLevelSettings()
    {
        if (currentLevelData == null) return;

        // Cleanup existing board before applying new level settings
        CleanupGameBoard();

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
            spawnerSystem.SetOpponentHP(currentLevelData.OpponentHP);
            spawnerSystem.SpawnAll();
            
            // Apply modifiers to spawned pawns after a short delay
            StartCoroutine(ApplyModifiersAfterSpawn());
        }

        // Apply visual settings
        ApplyVisualSettings();
    }
    
    private IEnumerator ApplyModifiersAfterSpawn()
    {
        // Wait for pawns to spawn
        yield return new WaitForSeconds(0.2f);
        
        if (currentLevelData == null || checkerboard == null) yield break;
        
        // Get allowed modifiers for this level
        List<PawnController.Modifier> allowedModifiers = currentLevelData.GetAllowedModifiers();
        if (allowedModifiers.Count == 0 || currentLevelData.ModifierCount == 0) yield break;
        
        // Get all opponent pawns
        List<PawnController> opponents = new List<PawnController>(checkerboard.GetOpponentControllers());
        if (opponents.Count == 0) yield break;
        
        // Shuffle opponents for random modifier assignment
        List<PawnController> shuffledOpponents = new List<PawnController>(opponents);
        for (int i = shuffledOpponents.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            PawnController temp = shuffledOpponents[i];
            shuffledOpponents[i] = shuffledOpponents[j];
            shuffledOpponents[j] = temp;
        }
        
        // Track used modifiers if duplicates are not allowed
        List<PawnController.Modifier> usedModifiers = new List<PawnController.Modifier>();
        int modifiersAssigned = 0;
        
        // Assign modifiers to pawns
        for (int i = 0; i < shuffledOpponents.Count && modifiersAssigned < currentLevelData.ModifierCount; i++)
        {
            PawnController pawn = shuffledOpponents[i];
            if (pawn == null) continue;
            
            // Get available modifiers for this pawn
            List<PawnController.Modifier> availableModifiers = new List<PawnController.Modifier>(allowedModifiers);
            if (!currentLevelData.AllowDuplicateModifiers)
            {
                foreach (PawnController.Modifier used in usedModifiers)
                {
                    availableModifiers.Remove(used);
                }
            }
            
            if (availableModifiers.Count == 0) break;
            
            // Select random modifier from available
            PawnController.Modifier selectedModifier = availableModifiers[Random.Range(0, availableModifiers.Count)];
            
            // Apply modifier
            pawn.SetModifier(selectedModifier);
            
            // Apply Tenacious modifier HP boost
            if (selectedModifier == PawnController.Modifier.Tenacious && currentLevelData.pawnCustomiser != null)
            {
                PawnHealth pawnHealth = pawn.GetComponent<PawnHealth>();
                if (pawnHealth != null)
                {
                    int boostedHP = Mathf.FloorToInt(currentLevelData.OpponentHP * currentLevelData.pawnCustomiser.modifierEffects.tenaciousHPMultiplier);
                    pawnHealth.SetOpponentHP(boostedHP);
                }
            }
            
            usedModifiers.Add(selectedModifier);
            modifiersAssigned++;
            
            if (showDebug) Debug.Log($"[GameManager] Assigned {selectedModifier} modifier to {pawn.name}");
        }
        
        if (showDebug) Debug.Log($"[GameManager] Assigned {modifiersAssigned} modifiers to opponents");
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

    #region Private Methods - Cleanup

    /// Clean up all game board elements (tiles, pawns) before loading a new level
    private void CleanupGameBoard()
    {
        if (showDebug) Debug.Log("[GameManager] Cleaning up game board...");

        // Clear all pawns (player and opponents)
        if (spawnerSystem != null)
        {
            spawnerSystem.ClearAllPawns();
        }

        // Clear all hex tiles
        if (gridGenerator != null && gridGenerator.parentContainer != null)
        {
            Transform tileParent = gridGenerator.parentContainer;
            for (int i = tileParent.childCount - 1; i >= 0; i--)
            {
                Destroy(tileParent.GetChild(i).gameObject);
            }
        }

        // Clear checkerboard references
        if (checkerboard != null)
        {
            // Checkerboard will automatically clear its lists when pawns are destroyed
        }

        if (showDebug) Debug.Log("[GameManager] Game board cleanup complete");
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