using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages the overall game state and transitions between Chess and Standoff modes
/// </summary>
public class GameStateManager : MonoBehaviour
{
    #region Singleton

    public static GameStateManager Instance { get; private set; }

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

    #region Inspector Fields

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

    [Header("Events")]
    [Tooltip("Called when game state changes")]
    public UnityEvent<GameState> OnStateChanged;

    [Tooltip("Called when transitioning to Standoff mode")]
    public UnityEvent OnStandoffBegin;

    [Tooltip("Called when player wins")]
    public UnityEvent OnVictory;

    [Tooltip("Called when player is defeated")]
    public UnityEvent OnDefeat;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = true;

    #endregion

    #region Private Fields

    private bool hasTransitionedToStandoff = false;
    private int currentLevel = 0;

    #endregion

    #region Properties

    /// <summary>
    /// Get the current game state
    /// </summary>
    public GameState CurrentState => currentState;

    /// <summary>
    /// Check if game is in Chess mode
    /// </summary>
    public bool IsChessMode => currentState == GameState.ChessMode;

    /// <summary>
    /// Check if game is in Standoff mode
    /// </summary>
    public bool IsStandoffMode => currentState == GameState.Standoff;

    /// <summary>
    /// Get current level index
    /// </summary>
    public int CurrentLevel => currentLevel;

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
    }

    private void Start()
    {
        // Initialize references if not set
        if (checkerboard == null)
        {
            checkerboard = FindObjectOfType<Checkerboard>();
        }

        if (platformGenerator == null)
        {
            platformGenerator = FindObjectOfType<Platform>();
        }

        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }
    }

    private void Update()
    {
        // Check for Standoff transition during Chess mode
        if (currentState == GameState.ChessMode && !hasTransitionedToStandoff)
        {
            CheckStandoffCondition();
        }
    }

    #endregion

    #region Public Methods - State Management

    /// <summary>
    /// Change the game state
    /// </summary>
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

        // Handle state transitions
        HandleStateTransition(previousState, newState);
    }

    /// <summary>
    /// Start a new game at specified level
    /// </summary>
    public void StartGame(int levelIndex)
    {
        currentLevel = levelIndex;
        hasTransitionedToStandoff = false;

        if (showDebug)
        {
            Debug.Log($"Starting game - Level {levelIndex}");
        }

        SetState(GameState.ChessMode);
    }

    /// <summary>
    /// Trigger victory condition
    /// </summary>
    public void TriggerVictory()
    {
        SetState(GameState.Victory);
        OnVictory?.Invoke();

        if (showDebug)
        {
            Debug.Log("Victory!");
        }
    }

    /// <summary>
    /// Trigger defeat condition
    /// </summary>
    public void TriggerDefeat()
    {
        SetState(GameState.Defeat);
        OnDefeat?.Invoke();

        if (showDebug)
        {
            Debug.Log("Defeat!");
        }
    }

    /// <summary>
    /// Pause the game
    /// </summary>
    public void PauseGame()
    {
        SetState(GameState.Paused);
        Time.timeScale = 0f;
    }

    /// <summary>
    /// Resume the game
    /// </summary>
    public void ResumeGame()
    {
        Time.timeScale = 1f;

        // Return to previous gameplay state
        if (hasTransitionedToStandoff)
        {
            SetState(GameState.Standoff);
        }
        else
        {
            SetState(GameState.ChessMode);
        }
    }

    /// <summary>
    /// Return to main menu
    /// </summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SetState(GameState.MainMenu);

        // Optionally load main menu scene
        // UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    /// <summary>
    /// Go to level select
    /// </summary>
    public void OpenLevelSelect()
    {
        SetState(GameState.LevelSelect);
    }

    #endregion

    #region Public Methods - Standoff

    /// <summary>
    /// Manually trigger Standoff mode
    /// </summary>
    public void TriggerStandoff()
    {
        if (hasTransitionedToStandoff) return;

        StartCoroutine(TransitionToStandoff());
    }

    #endregion

    #region Private Methods - State Transitions

    private void HandleStateTransition(GameState from, GameState to)
    {
        // Exit previous state
        switch (from)
        {
            case GameState.Paused:
                Time.timeScale = 1f;
                break;
        }

        // Enter new state
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
        // Enable chess mode components
        if (checkerboard != null)
        {
            checkerboard.gameObject.SetActive(true);
        }

        // Disable standoff components
        if (platformGenerator != null)
        {
            platformGenerator.gameObject.SetActive(false);
        }

        // Notify player controller
        if (playerController != null)
        {
            playerController.SetStandoffMode(false);
        }
    }

    private void SetupStandoffMode()
    {
        // Disable chess mode components
        if (checkerboard != null)
        {
            checkerboard.gameObject.SetActive(false);
        }

        // Enable standoff components
        if (platformGenerator != null)
        {
            platformGenerator.gameObject.SetActive(true);
        }

        // Notify player controller
        if (playerController != null)
        {
            playerController.SetStandoffMode(true);
        }

        OnStandoffBegin?.Invoke();
    }

    #endregion

    #region Private Methods - Standoff Trigger

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

        if (showDebug)
        {
            Debug.Log("Transitioning to Standoff mode...");
        }

        // Wait for delay
        yield return new WaitForSeconds(standoffTransitionDelay);

        // Transition effect could go here (fade, camera move, etc.)

        // Change state
        SetState(GameState.Standoff);

        // Generate platform arena
        if (platformGenerator != null)
        {
            platformGenerator.GenerateArena();
        }

        // Position player and remaining opponent(s)
        PositionPawnsForStandoff();
    }

    private void PositionPawnsForStandoff()
    {
        // Get all tiles from platform
        if (platformGenerator == null) return;

        List<Platform.TileData> floorTiles = new List<Platform.TileData>();
        foreach (var tile in platformGenerator.GetAllTiles())
        {
            if (tile.heightLevel == 0) // Floor tiles only
            {
                floorTiles.Add(tile);
            }
        }

        if (floorTiles.Count < 2) return;

        // Find left-most and right-most floor tiles by x-coordinate
        Platform.TileData leftMostTile = floorTiles[0];
        Platform.TileData rightMostTile = floorTiles[0];

        foreach (var tile in floorTiles)
        {
            if (tile.position.x < leftMostTile.position.x)
            {
                leftMostTile = tile;
            }
            if (tile.position.x > rightMostTile.position.x)
            {
                rightMostTile = tile;
            }
        }

        // Get tile height from LevelManager if available, otherwise use default
        float tileHeight = 1f;
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
        {
            tileHeight = LevelManager.Instance.CurrentLevel.TileSize;
        }

        // Position player above middle of left-most tile, one tile height above
        if (playerController != null)
        {
            Vector3 playerPos = leftMostTile.position;
            playerPos.y += tileHeight; // One tile height above
            playerController.transform.position = playerPos;

            if (showDebug)
            {
                Debug.Log($"Player positioned at {playerPos} (left-most tile)");
            }
        }

        // Position opponent above middle of right-most tile, one tile height above
        if (checkerboard != null)
        {
            var opponents = checkerboard.GetOpponentControllers();
            if (opponents.Count > 0)
            {
                Vector3 opponentPos = rightMostTile.position;
                opponentPos.y += tileHeight; // One tile height above
                opponents[0].transform.position = opponentPos;

                if (showDebug)
                {
                    Debug.Log($"Opponent positioned at {opponentPos} (right-most tile)");
                }
            }
        }
    }

    #endregion
}
