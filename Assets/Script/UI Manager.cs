using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// Unified UI management system handling all game screens with automatic panel initialisation.
/// Consolidates MainMenuUI, LevelSelectUI, GameUI, PauseMenuUI, FinishMenuUI, and SettingsUI.
/// All panels are automatically configured on startup, eliminating manual scene setup requirements.
public class UIManager : MonoBehaviour
{
    #region Singleton

    public static UIManager Instance { get; private set; }

    #endregion

    #region Inspector Fields - Main Menu

    [Header("Main Menu")]
    [Tooltip("Main menu panel")]
    [SerializeField] private GameObject mainMenuPanel;
    [Tooltip("Play button")]
    [SerializeField] private Button playButton;
    [Tooltip("Settings button")]
    [SerializeField] private Button settingsButton;
    [Tooltip("Quit button")]
    [SerializeField] private Button quitButton;

    #endregion

    #region Inspector Fields - Level Select

    [Header("Level Select")]
    [Tooltip("Level select panel")]
    [SerializeField] private GameObject levelSelectPanel;
    [Tooltip("Level button prefab")]
    [SerializeField] private GameObject levelButtonPrefab;
    [Tooltip("Container for level buttons (should have HorizontalLayoutGroup)")]
    [SerializeField] private Transform levelButtonContainer;
    [Tooltip("Back button (level select)")]
    [SerializeField] private Button levelSelectBackButton;
    [Tooltip("Level select title")]
    [SerializeField] private TextMeshProUGUI levelSelectTitleText;
    [Tooltip("ScrollRect for swipeable level selection (optional)")]
    [SerializeField] private UnityEngine.UI.ScrollRect levelSelectScrollRect;
    [Tooltip("Spacing between level buttons")]
    [SerializeField] private float levelButtonSpacing = 50f;
    [Tooltip("Base size for level buttons")]
    [SerializeField] private float levelButtonSize = 100f;
    [Tooltip("Scale multiplier for center button")]
    [SerializeField] private float centerButtonScale = 1.2f;

    #endregion

    #region Inspector Fields - Game UI

    [Header("Game UI (HUD)")]
    [Tooltip("Main game UI panel")]
    [SerializeField] private GameObject gameUIPanel;
    [Tooltip("Player HP text")]
    [SerializeField] private TextMeshProUGUI hpText;
    [Tooltip("Player HP bar")]
    [SerializeField] private Slider hpBar;
    [Tooltip("Heart icons container")]
    [SerializeField] private Transform heartIconsContainer;
    [Tooltip("Heart icon prefab")]
    [SerializeField] private GameObject heartIconPrefab;
    [Tooltip("Current level text")]
    [SerializeField] private TextMeshProUGUI levelText;
    [Tooltip("Opponents remaining text")]
    [SerializeField] private TextMeshProUGUI opponentsText;
    [Tooltip("Game mode text")]
    [SerializeField] private TextMeshProUGUI gameModeText;
    [Tooltip("Pause button")]
    [SerializeField] private Button pauseButton;

    [Header("Turn Indicator")]
    [Tooltip("Text showing whose turn it is (Your Turn / Opponent Turn)")]
    [SerializeField] private TextMeshProUGUI turnIndicatorText;
    [Tooltip("Color for player's turn")]
    [SerializeField] private Color playerTurnColor = new Color(0.2f, 0.8f, 0.2f); // Green
    [Tooltip("Color for opponent's turn")]
    [SerializeField] private Color opponentTurnColor = new Color(0.8f, 0.2f, 0.2f); // Red

    [Header("Announcer System")]
    [Tooltip("Announcer panel positioned at top-right, animates left when triggered")]
    [SerializeField] private GameObject announcerPanel;
    [Tooltip("Announcer text for displaying messages")]
    [SerializeField] private TextMeshProUGUI announcerText;
    [Tooltip("Animation slide distance (percentage of screen width, 0.25 = 25%)")]
    [SerializeField] private float announcerSlideDistance = 0.25f;
    [Tooltip("Animation slide-in duration")]
    [SerializeField] private float announcerSlideInDuration = 0.3f;
    [Tooltip("Duration to display message before fading")]
    [SerializeField] private float announcerDisplayDuration = 2.0f;
    [Tooltip("Animation fade-out duration")]
    [SerializeField] private float announcerFadeOutDuration = 0.5f;
    [Tooltip("Highlight color for bracketed text (vibrant orange)")]
    [SerializeField] private Color announcerHighlightColor = new Color(1f, 0.5f, 0f); // Vibrant orange

    [Header("Mobile Controls")]
    [Tooltip("Mobile controls container (joystick and jump button for Standoff mode)")]
    // Container for mobile controls (joystick and jump button) shown in Standoff mode.
    [SerializeField] private GameObject mobileControlsPanel;

    #endregion

    #region Inspector Fields - Pause Menu

    [Header("Pause Menu")]
    [Tooltip("Pause menu panel")]
    [SerializeField] private GameObject pauseMenuPanel;
    [Tooltip("Resume button")]
    [SerializeField] private Button resumeButton;
    [Tooltip("Restart button")]
    [SerializeField] private Button restartButton;
    [Tooltip("Main menu button (pause)")]
    [SerializeField] private Button pauseMainMenuButton;
    [Tooltip("Pause title text")]
    [SerializeField] private TextMeshProUGUI pauseTitleText;

    #endregion

    #region Inspector Fields - Finish Menu

    [Header("Victory Panel")]
    [Tooltip("Victory panel")]
    [SerializeField] private GameObject victoryPanel;
    [Tooltip("Victory title text")]
    [SerializeField] private TextMeshProUGUI victoryTitleText;
    [Tooltip("Victory message text")]
    [SerializeField] private TextMeshProUGUI victoryMessageText;
    [Tooltip("Next level button")]
    [SerializeField] private Button nextLevelButton;
    [Tooltip("Victory main menu button")]
    [SerializeField] private Button victoryMainMenuButton;

    [Header("Defeat Panel")]
    [Tooltip("Defeat panel")]
    [SerializeField] private GameObject defeatPanel;
    [Tooltip("Defeat title text")]
    [SerializeField] private TextMeshProUGUI defeatTitleText;
    [Tooltip("Defeat message text")]
    [SerializeField] private TextMeshProUGUI defeatMessageText;
    [Tooltip("Retry button")]
    [SerializeField] private Button retryButton;
    [Tooltip("Defeat main menu button")]
    [SerializeField] private Button defeatMainMenuButton;

    #endregion

    #region Inspector Fields - Settings

    [Header("Settings")]
    [Tooltip("Settings panel")]
    [SerializeField] private GameObject settingsPanel;
    [Tooltip("Master volume slider")]
    [SerializeField] private Slider masterVolumeSlider;
    [Tooltip("Music volume slider")]
    [SerializeField] private Slider musicVolumeSlider;
    [Tooltip("SFX volume slider")]
    [SerializeField] private Slider sfxVolumeSlider;
    [Tooltip("Master volume text")]
    [SerializeField] private TextMeshProUGUI masterVolumeText;
    [Tooltip("Music volume text")]
    [SerializeField] private TextMeshProUGUI musicVolumeText;
    [Tooltip("SFX volume text")]
    [SerializeField] private TextMeshProUGUI sfxVolumeText;
    [Tooltip("Settings back button")]
    [SerializeField] private Button settingsBackButton;

    #endregion

    #region Inspector Fields - Debug

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Private Fields

    private List<GameObject> levelButtons = new List<GameObject>();
    private PawnHealth playerHealth;
    private Checkerboard checkerboard;
    private bool isInitialised = false;
    private Coroutine announcerCoroutine;
    private RectTransform announcerRectTransform;
    private CanvasGroup announcerCanvasGroup;
    private Vector2 announcerStartPosition;
    private bool isPlayerTurn = true;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton pattern ensures only one UI Manager exists
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Force all panels to hidden state before Start() is called on any object
        InitialisePanelStates();
    }

    private void Start()
    {
        // Button listeners are established for all interactive UI elements
        SetupAllButtons();
        // Volume sliders are configured with their change callbacks
        SetupSliders();

        // Player health reference is located for HP tracking
        playerHealth = FindFirstObjectByType<PawnHealth>();
        // Checkerboard reference is located for opponent counting
        checkerboard = FindFirstObjectByType<Checkerboard>();

        // Player health change event is subscribed to for real-time HP updates
        if (playerHealth != null)
        {
            playerHealth.OnHPChanged.AddListener(OnPlayerHPChanged);
        }

        // Game Manager events are subscribed to for victory/defeat handling
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnVictory.AddListener(ShowVictory);
            GameManager.Instance.OnDefeat.AddListener(ShowDefeat);
            GameManager.Instance.OnStateChanged.AddListener(OnGameStateChanged);
        }

        // Level selection buttons are dynamically generated from available levels
        GenerateLevelButtons();

        // Initialize announcer system
        InitializeAnnouncer();

        // Main menu is displayed as the initial screen
        ShowMainMenu();

        // Initialisation is marked as complete
        isInitialised = true;
    }

    /// Initialize the announcer panel for animations.
    private void InitializeAnnouncer()
    {
        if (announcerPanel != null)
        {
            // Get or add RectTransform for position animation
            announcerRectTransform = announcerPanel.GetComponent<RectTransform>();

            // Get or add CanvasGroup for fade animation
            announcerCanvasGroup = announcerPanel.GetComponent<CanvasGroup>();
            if (announcerCanvasGroup == null)
            {
                announcerCanvasGroup = announcerPanel.AddComponent<CanvasGroup>();
            }

            // Store initial position for animation calculations
            if (announcerRectTransform != null)
            {
                announcerStartPosition = announcerRectTransform.anchoredPosition;
            }

            // Hide announcer initially
            announcerPanel.SetActive(false);
        }
    }

    private void Update()
    {
        // Game UI is continuously updated during active gameplay
        UpdateGameUI();

        // ESC key toggles pause menu when in gameplay states
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (pauseMenuPanel != null && pauseMenuPanel.activeSelf)
            {
                OnResumeClicked();
            }
            else if (GameManager.Instance != null &&
                     (GameManager.Instance.IsChessMode || GameManager.Instance.IsStandoffMode))
            {
                ShowPauseMenu();
            }
        }
    }

    #endregion

    #region Initialisation Methods

    /// All UI panels are forcibly set to inactive state to ensure clean startup.
    /// This method is called in Awake() before any other initialisation occurs.
    private void InitialisePanelStates()
    {
        // All panels are explicitly disabled to ensure no overlap on startup
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (gameUIPanel != null) gameUIPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mobileControlsPanel != null) mobileControlsPanel.SetActive(false);

        if (showDebug) Debug.Log("[UIManager] All panels initialised to inactive state");
    }

    #endregion

    #region Setup Methods

    private void SetupAllButtons()
    {
        // Main Menu button listeners are registered
        if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        // Level Select button listener is registered
        if (levelSelectBackButton != null) levelSelectBackButton.onClick.AddListener(OnLevelSelectBackClicked);

        // Game UI button listener is registered
        if (pauseButton != null) pauseButton.onClick.AddListener(OnPauseClicked);

        // Pause Menu button listeners are registered
        if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
        if (pauseMainMenuButton != null) pauseMainMenuButton.onClick.AddListener(OnMainMenuClicked);

        // Victory screen button listeners are registered
        if (nextLevelButton != null) nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        if (victoryMainMenuButton != null) victoryMainMenuButton.onClick.AddListener(OnMainMenuClicked);

        // Defeat screen button listeners are registered
        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (defeatMainMenuButton != null) defeatMainMenuButton.onClick.AddListener(OnMainMenuClicked);

        // Settings button listener is registered
        if (settingsBackButton != null) settingsBackButton.onClick.AddListener(OnSettingsBackClicked);
    }

    private void SetupSliders()
    {
        // Volume slider change callbacks are registered
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }
    }

    private void GenerateLevelButtons()
    {
        // Level buttons cannot be generated without required references
        if (GameManager.Instance == null || levelButtonContainer == null || levelButtonPrefab == null)
        {
            if (showDebug) Debug.LogWarning("[UIManager] Cannot generate level buttons: missing references");
            return;
        }

        // Existing level buttons are destroyed before regeneration
        foreach (var button in levelButtons)
        {
            if (button != null) Destroy(button);
        }
        levelButtons.Clear();

        // Get level count from Game Manager
        int levelCount = GameManager.Instance.TotalLevels;
        if (levelCount == 0)
        {
            if (showDebug) Debug.LogWarning("[UIManager] No levels found in Game Manager");
            return;
        }

        // Level buttons are instantiated in sequential order (1, 2, 3, ...)
        LevelData[] levels = GameManager.Instance.GetAllLevels();

        // Calculate centre index for scaling (center button is larger)
        int centreIndex = levelCount / 2;

        // Generate buttons in order from left to right
        for (int i = 0; i < levelCount; i++)
        {
            if (levels[i] != null)
            {
                bool isCentre = (i == centreIndex);
                GameObject button = CreateLevelButton(levels[i], i, isCentre);
                levelButtons.Add(button);

                // Set sibling index to maintain visual order
                button.transform.SetSiblingIndex(i);
            }
        }

        if (showDebug) Debug.Log($"[UIManager] Generated {levelButtons.Count} level buttons (center at index {centreIndex})");
    }

    /// Create a level button with optional centre scaling
    private GameObject CreateLevelButton(LevelData levelData, int levelIndex, bool isCentre)
    {
        // Button GameObject is instantiated from prefab
        GameObject buttonObject = Instantiate(levelButtonPrefab, levelButtonContainer);

        // Button component and text are retrieved
        Button button = buttonObject.GetComponent<Button>();
        TextMeshProUGUI buttonText = buttonObject.GetComponentInChildren<TextMeshProUGUI>();

        // Button text is set to display level number only (e.g., "1", "2", "3")
        if (buttonText != null)
        {
            buttonText.text = $"{levelIndex + 1}";
        }

        // Centre button is scaled larger (1.2x scale)
        if (isCentre)
        {
            buttonObject.transform.localScale = Vector3.one * 1.2f;
        }

        // Button click listener is registered with captured level index
        if (button != null)
        {
            button.onClick.AddListener(() => OnLevelButtonClicked(levelIndex));
        }

        return buttonObject;
    }

    #endregion

    #region Public Methods - Show/Hide Screens

    /// Main menu is displayed and all other panels are hidden.
    /// Game state is set to MainMenu.
    public void ShowMainMenu()
    {
        // All panels are hidden before showing main menu
        HideAllPanels();

        // Main menu panel is activated
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

        // Game state is transitioned to MainMenu
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameManager.GameState.MainMenu);
        }

        if (showDebug) Debug.Log("[UIManager] Main menu displayed");
    }

    /// Level select screen is displayed and all other panels are hidden.
    /// Game state is set to LevelSelect.
    public void ShowLevelSelect()
    {
        // All panels are hidden before showing level select
        HideAllPanels();

        // Level select panel is activated
        if (levelSelectPanel != null) levelSelectPanel.SetActive(true);

        // Game state is transitioned to LevelSelect
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameManager.GameState.LevelSelect);
        }

        if (showDebug) Debug.Log("[UIManager] Level select displayed");
    }

    /// Game HUD is displayed and all other panels are hidden.
    /// UI elements are refreshed with current game data.
    public void ShowGameUI()
    {
        // All panels are hidden before showing game UI
        HideAllPanels();

        // Game UI panel is activated
        if (gameUIPanel != null) gameUIPanel.SetActive(true);

        // All game UI elements are updated with current values
        RefreshGameUI();

        if (showDebug) Debug.Log("[UIManager] Game UI displayed");
    }

    /// Pause menu is displayed over the game UI without hiding it.
    /// Game state is set to Paused and time scale is set to 0.
    public void ShowPauseMenu()
    {
        // Pause menu panel is activated (overlays game UI)
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);

        // Game is paused via Game Manager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PauseGame();
        }

        if (showDebug) Debug.Log("[UIManager] Pause menu displayed");
    }

    /// Victory screen is displayed and all other panels are hidden.
    /// Victory audio is played and next level button visibility is updated.
    public void ShowVictory()
    {
        // All panels are hidden before showing victory screen
        HideAllPanels();

        // Victory panel is activated
        if (victoryPanel != null) victoryPanel.SetActive(true);

        // Victory audio is played
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayVictory();
        }

        // Victory text is updated with level information
        UpdateVictoryText();

        // Next level button visibility is set based on level availability
        if (nextLevelButton != null && GameManager.Instance != null)
        {
            nextLevelButton.gameObject.SetActive(GameManager.Instance.HasNextLevel);
        }

        if (showDebug) Debug.Log("[UIManager] Victory screen displayed");
    }

    /// Defeat screen is displayed and all other panels are hidden.
    /// Defeat audio is played and retry message is shown.
    public void ShowDefeat()
    {
        // All panels are hidden before showing defeat screen
        HideAllPanels();

        // Defeat panel is activated
        if (defeatPanel != null) defeatPanel.SetActive(true);

        // Defeat audio is played
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayDefeat();
        }

        // Defeat text is updated
        UpdateDefeatText();

        if (showDebug) Debug.Log("[UIManager] Defeat screen displayed");
    }

    /// Settings screen is displayed and all other panels are hidden.
    /// Current audio settings are loaded into the sliders.
    public void ShowSettings()
    {
        // All panels are hidden before showing settings
        HideAllPanels();

        // Settings panel is activated
        if (settingsPanel != null) settingsPanel.SetActive(true);

        // Current audio settings are loaded
        LoadSettings();

        if (showDebug) Debug.Log("[UIManager] Settings displayed");
    }

    /// All UI panels are deactivated to prepare for showing a specific panel.
    /// This method ensures clean transitions between UI states.
    public void HideAllPanels()
    {
        // Each panel is explicitly deactivated
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (gameUIPanel != null) gameUIPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    #endregion

    #region Game State Handler

    /// Game state changes are handled by showing the appropriate UI panel.
    /// This method ensures UI stays synchronised with game state.
    private void OnGameStateChanged(GameManager.GameState newState)
    {
        // Initialisation check prevents premature state handling
        if (!isInitialised) return;

        // Appropriate UI panel is displayed based on new game state
        switch (newState)
        {
            case GameManager.GameState.MainMenu:
                if (showDebug) Debug.Log("[UIManager] State changed to MainMenu");
                // Main menu is already shown by explicit calls
                break;
            case GameManager.GameState.LevelSelect:
                if (showDebug) Debug.Log("[UIManager] State changed to LevelSelect");
                // Level select is already shown by explicit calls
                break;
            case GameManager.GameState.ChessMode:
            case GameManager.GameState.Standoff:
                // Game UI is shown for both gameplay states
                if (showDebug) Debug.Log($"[UIManager] State changed to {newState}");
                if (gameUIPanel != null && !gameUIPanel.activeSelf)
                {
                    ShowGameUI();
                }
                break;
            case GameManager.GameState.Victory:
                if (showDebug) Debug.Log("[UIManager] State changed to Victory");
                // Victory screen is shown by GameManager callback
                break;
            case GameManager.GameState.Defeat:
                if (showDebug) Debug.Log("[UIManager] State changed to Defeat");
                // Defeat screen is shown by GameManager callback
                break;
            case GameManager.GameState.Paused:
                if (showDebug) Debug.Log("[UIManager] State changed to Paused");
                // Pause menu is shown by explicit pause calls
                break;
        }
    }

    #endregion

    #region Button Handlers - Main Menu

    private void OnPlayClicked()
    {
        // Button click sound is played
        PlayButtonSound();
        // Level select screen is shown for player to choose level
        ShowLevelSelect();

        if (showDebug) Debug.Log("[UIManager] Play button clicked - showing level select");
    }

    private void OnSettingsClicked()
    {
        // Button click sound is played
        PlayButtonSound();
        // Settings screen is shown
        ShowSettings();

        if (showDebug) Debug.Log("[UIManager] Settings button clicked");
    }

    private void OnQuitClicked()
    {
        // Button click sound is played
        PlayButtonSound();

        if (showDebug) Debug.Log("[UIManager] Quit button clicked");

        // Application is quit (editor or standalone build)
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Button Handlers - Level Select

    private void OnLevelButtonClicked(int levelIndex)
    {
        // Button click sound is played
        PlayButtonSound();
        // Selected level is started
        StartGame(levelIndex);

        if (showDebug) Debug.Log($"[UIManager] Level {levelIndex} button clicked");
    }

    private void OnLevelSelectBackClicked()
    {
        // Button click sound is played
        PlayButtonSound();
        // Main menu is shown
        ShowMainMenu();

        if (showDebug) Debug.Log("[UIManager] Level select back button clicked");
    }

    #endregion

    #region Button Handlers - Pause Menu

    private void OnPauseClicked()
    {
        // Button click sound is played
        PlayButtonSound();

        // Game is paused via Game Manager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PauseGame();
        }

        // Pause menu is shown
        ShowPauseMenu();

        if (showDebug) Debug.Log("[UIManager] Pause button clicked");
    }

    private void OnResumeClicked()
    {
        // Button click sound is played
        PlayButtonSound();

        // Pause menu panel is hidden
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        // Game is resumed via Game Manager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResumeGame();
        }

        if (showDebug) Debug.Log("[UIManager] Resume button clicked");
    }

    private void OnRestartClicked()
    {
        // Button click sound is played
        PlayButtonSound();

        // Pause menu is hidden
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        // Current level is reloaded and started
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReloadCurrentLevel();
            GameManager.Instance.StartGame(GameManager.Instance.CurrentLevelIndex);
        }

        // Game UI is shown
        ShowGameUI();

        if (showDebug) Debug.Log("[UIManager] Restart button clicked");
    }

    private void OnMainMenuClicked()
    {
        // Button click sound is played
        PlayButtonSound();

        // Game state is returned to main menu
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToMainMenu();
        }

        // Main menu is shown
        ShowMainMenu();

        if (showDebug) Debug.Log("[UIManager] Main menu button clicked");
    }

    #endregion

    #region Button Handlers - Finish Menu

    private void OnNextLevelClicked()
    {
        // Button click sound is played
        PlayButtonSound();

        // Next level is loaded and started
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadNextLevel();
            GameManager.Instance.StartGame(GameManager.Instance.CurrentLevelIndex);
        }

        // Game UI is shown
        ShowGameUI();

        if (showDebug) Debug.Log("[UIManager] Next level button clicked");
    }

    private void OnRetryClicked()
    {
        // Button click sound is played
        PlayButtonSound();

        // Current level is reloaded and restarted
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReloadCurrentLevel();
            GameManager.Instance.StartGame(GameManager.Instance.CurrentLevelIndex);
        }

        // Game UI is shown
        ShowGameUI();

        if (showDebug) Debug.Log("[UIManager] Retry button clicked");
    }

    #endregion

    #region Button Handlers - Settings

    private void OnSettingsBackClicked()
    {
        // Button click sound is played
        PlayButtonSound();
        // Main menu is shown
        ShowMainMenu();

        if (showDebug) Debug.Log("[UIManager] Settings back button clicked");
    }

    private void OnMasterVolumeChanged(float value)
    {
        // Master volume is updated via Audio Manager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(value);
        }
        // Volume display texts are updated
        UpdateVolumeTexts();
    }

    private void OnMusicVolumeChanged(float value)
    {
        // Music volume is updated via Audio Manager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }
        // Volume display texts are updated
        UpdateVolumeTexts();
    }

    private void OnSFXVolumeChanged(float value)
    {
        // SFX volume is updated via Audio Manager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }
        // Volume display texts are updated
        UpdateVolumeTexts();
    }

    #endregion

    #region Private Methods - Game Logic

    /// Specified level is loaded and game is started with appropriate UI.
    private void StartGame(int levelIndex)
    {
        // Level is loaded and game is started via Game Manager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadLevel(levelIndex);
            GameManager.Instance.StartGame(levelIndex);
        }

        // Game UI is shown for gameplay
        ShowGameUI();

        if (showDebug) Debug.Log($"[UIManager] Started game with level {levelIndex}");
    }

    #endregion

    #region Private Methods - UI Updates

    /// Game UI elements are continuously updated during active gameplay.
    /// Opponent count and game mode displays are refreshed each frame.
    private void UpdateGameUI()
    {
        // UI updates are skipped if game UI panel is not visible
        if (gameUIPanel == null || !gameUIPanel.activeSelf) return;

        // Dynamic UI elements are updated
        UpdateOpponentsCount();
        UpdateGameMode();
    }

    /// All game UI elements are refreshed with current game state.
    /// This method is called when game UI is first shown.
    private void RefreshGameUI()
    {
        // All game UI components are updated with current values
        UpdatePlayerHP();
        UpdateLevelInfo();
        UpdateOpponentsCount();
        UpdateGameMode();

        if (showDebug) Debug.Log("[UIManager] Game UI refreshed");
    }

    /// Player HP display is updated when health changes.
    private void OnPlayerHPChanged(int newHP)
    {
        // Player HP display is refreshed
        UpdatePlayerHP();

        if (showDebug) Debug.Log($"[UIManager] Player HP changed to {newHP}");
    }

    /// Player HP text, bar, and heart icons are updated with current health values.
    private void UpdatePlayerHP()
    {
        // Player health reference is reacquired if lost
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PawnHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnHPChanged.AddListener(OnPlayerHPChanged);
            }
        }

        // Update is skipped if player health is unavailable
        if (playerHealth == null) return;

        // Current and maximum HP values are retrieved
        int currentHP = playerHealth.GetCurrentHP();
        int maxHP = playerHealth.GetMaxHP();

        // HP text display is updated
        if (hpText != null)
        {
            hpText.text = $"HP: {currentHP}/{maxHP}";
        }

        // HP bar slider is updated
        if (hpBar != null)
        {
            hpBar.maxValue = maxHP;
            hpBar.value = currentHP;
        }

        // Heart icons are updated to reflect current HP
        UpdateHeartIcons(currentHP, maxHP);
    }

    /// Heart icons are generated to visually represent player health.
    /// Filled hearts represent current HP, empty hearts represent lost HP.
    private void UpdateHeartIcons(int currentHP, int maxHP)
    {
        // Update is skipped if heart container or prefab is missing
        if (heartIconsContainer == null || heartIconPrefab == null) return;

        // Existing heart icons are destroyed before regeneration
        foreach (Transform child in heartIconsContainer)
        {
            Destroy(child.gameObject);
        }

        // Heart icon is instantiated for each maximum HP point
        for (int i = 0; i < maxHP; i++)
        {
            GameObject heart = Instantiate(heartIconPrefab, heartIconsContainer);
            Image heartImage = heart.GetComponent<Image>();

            // Heart colour indicates whether HP point is filled or empty
            if (heartImage != null)
            {
                heartImage.color = i < currentHP ? Color.red : new Color(0.3f, 0.3f, 0.3f);
            }
        }
    }

    /// Current level name and number are displayed in the game UI.
    private void UpdateLevelInfo()
    {
        // Level display is updated if Game Manager and level text exist
        if (levelText != null && GameManager.Instance != null)
        {
            // Display only level number (e.g., "Level 1")
            levelText.text = $"Level {GameManager.Instance.CurrentLevelIndex + 1}";
        }
    }

    /// Remaining opponent count is displayed in the game UI.
    private void UpdateOpponentsCount()
    {
        // Opponent count display is updated if opponents text exists
        if (opponentsText != null)
        {
            // Checkerboard reference is reacquired if lost
            if (checkerboard == null)
            {
                checkerboard = FindFirstObjectByType<Checkerboard>();
            }

            // Opponent count is retrieved and displayed
            if (checkerboard != null)
            {
                int count = checkerboard.GetOpponentControllers().Count;
                opponentsText.text = $"Enemies: {count}";
            }
        }
    }

    /// Current game mode (Chess or Standoff) is displayed with appropriate styling.
    private void UpdateGameMode()
    {
        // Game mode display is updated if mode text and Game Manager exist
        if (gameModeText != null && GameManager.Instance != null)
        {
            // Mode text and colour are set based on current game state
            string mode = GameManager.Instance.IsStandoffMode ? "STANDOFF" : "CHESS";
            gameModeText.text = mode;

            // Red colour indicates standoff mode, white indicates chess mode
            gameModeText.color = GameManager.Instance.IsStandoffMode ? Color.red : Color.white;

            // Show/hide mobile controls based on game mode
            // Mobile controls (joystick and jump button) are only needed in Standoff mode
            if (mobileControlsPanel != null)
            {
                mobileControlsPanel.SetActive(GameManager.Instance.IsStandoffMode);
            }
        }
    }

    /// Victory screen text is updated with current level completion information.
    private void UpdateVictoryText()
    {
        // Update is skipped if Game Manager is unavailable
        if (GameManager.Instance == null) return;

        // Current level data is retrieved
        LevelData currentLevel = GameManager.Instance.CurrentLevel;
        if (currentLevel == null) return;

        // Victory title is set
        if (victoryTitleText != null)
        {
            victoryTitleText.text = "VICTORY!";
        }

        // Victory message includes completed level name
        if (victoryMessageText != null)
        {
            victoryMessageText.text = $"You completed {currentLevel.LevelName}!";
        }
    }

    /// Defeat screen text is updated with retry encouragement message.
    private void UpdateDefeatText()
    {
        // Defeat title is set
        if (defeatTitleText != null)
        {
            defeatTitleText.text = "DEFEAT";
        }

        // Defeat message encourages player to retry
        if (defeatMessageText != null)
        {
            defeatMessageText.text = "Try again!";
        }
    }

    /// Current audio settings are loaded from Audio Manager into volume sliders.
    private void LoadSettings()
    {
        // Settings loading is skipped if Audio Manager is unavailable
        if (AudioManager.Instance == null) return;

        // Master volume slider is set to current value
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = AudioManager.Instance.GetMasterVolume();
        }

        // Music volume slider is set to current value
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = AudioManager.Instance.GetMusicVolume();
        }

        // SFX volume slider is set to current value
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = AudioManager.Instance.GetSFXVolume();
        }

        // Volume percentage texts are updated
        UpdateVolumeTexts();
    }

    /// Volume percentage labels are updated to reflect current slider values.
    private void UpdateVolumeTexts()
    {
        // Master volume text is updated if both text and slider exist
        if (masterVolumeText != null && masterVolumeSlider != null)
        {
            masterVolumeText.text = $"{Mathf.RoundToInt(masterVolumeSlider.value * 100)}%";
        }

        // Music volume text is updated if both text and slider exist
        if (musicVolumeText != null && musicVolumeSlider != null)
        {
            musicVolumeText.text = $"{Mathf.RoundToInt(musicVolumeSlider.value * 100)}%";
        }

        // SFX volume text is updated if both text and slider exist
        if (sfxVolumeText != null && sfxVolumeSlider != null)
        {
            sfxVolumeText.text = $"{Mathf.RoundToInt(sfxVolumeSlider.value * 100)}%";
        }
    }

    /// Button click sound effect is played via Audio Manager.
    private void PlayButtonSound()
    {
        // Sound effect is played if Audio Manager is available
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
    }

    #endregion

    #region Turn Indicator

    /// Update turn indicator text to show whose turn it is.
    /// Call this from Checkerboard when turn changes.
    public void SetTurnIndicator(bool isPlayersTurn)
    {
        isPlayerTurn = isPlayersTurn;
        UpdateTurnIndicator();
    }

    /// Update the turn indicator display.
    private void UpdateTurnIndicator()
    {
        if (turnIndicatorText == null) return;

        // Only show turn indicator in Chess mode
        if (GameManager.Instance == null || !GameManager.Instance.IsChessMode)
        {
            turnIndicatorText.gameObject.SetActive(false);
            return;
        }

        turnIndicatorText.gameObject.SetActive(true);

        if (isPlayerTurn)
        {
            turnIndicatorText.text = "Your Turn";
            turnIndicatorText.color = playerTurnColor;
        }
        else
        {
            turnIndicatorText.text = "Opponent Turn";
            turnIndicatorText.color = opponentTurnColor;
        }
    }

    #endregion

    #region Announcer System

    /// Show an announcement message with slide-in animation and fade-out.
    /// Text in square brackets will be highlighted in orange.
    /// Example: ShowAnnouncement("[Sniper] pawn has been captured.");
    public void ShowAnnouncement(string message)
    {
        if (announcerPanel == null || announcerText == null) return;

        // Stop any existing announcement
        if (announcerCoroutine != null)
        {
            StopCoroutine(announcerCoroutine);
        }

        // Start new announcement
        announcerCoroutine = StartCoroutine(AnnouncementCoroutine(message));
    }

    /// Display opponent death message.
    /// Usage: ShowOpponentDeathMessage(PawnController.AIType.Sniper);
    public void ShowOpponentDeathMessage(PawnController.AIType aiType)
    {
        ShowAnnouncement($"[{aiType}] pawn has been captured.");
    }

    /// Display damage taken message.
    /// Usage: ShowDamageTakenMessage(PawnController.AIType.Shotgun, 2, 1);
    public void ShowDamageTakenMessage(PawnController.AIType aiType, int damage, int remainingHP)
    {
        ShowAnnouncement($"[{aiType}] pawn dealt [{damage}] damage to your pawn, your pawn has [{remainingHP}] HP left.");
    }

    /// Display stage change message (entering Standoff mode).
    /// Usage: ShowStageChangeMessage(PawnController.AIType.Handcannon);
    public void ShowStageChangeMessage(PawnController.AIType aiType)
    {
        ShowAnnouncement($"Down to one opponent pawn, you are now in a duel with [{aiType}] pawn.");
    }

    /// Coroutine handling the announcement animation: slide-in, display, fade-out.
    private System.Collections.IEnumerator AnnouncementCoroutine(string message)
    {
        // Format message with highlighted text (replace [text] with orange colored text)
        string formattedMessage = FormatAnnouncementText(message);
        announcerText.text = formattedMessage;

        // Reset state
        announcerPanel.SetActive(true);
        if (announcerCanvasGroup != null)
        {
            announcerCanvasGroup.alpha = 1f;
        }

        // Calculate slide distance in pixels (based on screen width percentage)
        float slidePixels = Screen.width * announcerSlideDistance;
        if (slidePixels < 100f) slidePixels = 500f; // Fallback to 500px minimum

        // Set initial position (off-screen to the right)
        Vector2 startPos = announcerStartPosition + new Vector2(slidePixels, 0f);
        Vector2 endPos = announcerStartPosition;

        if (announcerRectTransform != null)
        {
            announcerRectTransform.anchoredPosition = startPos;
        }

        // Slide-in animation
        float elapsed = 0f;
        while (elapsed < announcerSlideInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / announcerSlideInDuration);
            // Ease out cubic for smooth deceleration
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            if (announcerRectTransform != null)
            {
                announcerRectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            }

            yield return null;
        }

        // Ensure final position
        if (announcerRectTransform != null)
        {
            announcerRectTransform.anchoredPosition = endPos;
        }

        // Display duration
        yield return new WaitForSecondsRealtime(announcerDisplayDuration);

        // Fade-out animation
        elapsed = 0f;
        while (elapsed < announcerFadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / announcerFadeOutDuration);

            if (announcerCanvasGroup != null)
            {
                announcerCanvasGroup.alpha = 1f - t;
            }

            yield return null;
        }

        // Hide panel
        announcerPanel.SetActive(false);
        announcerCoroutine = null;
    }

    /// Format announcement text with TMP rich text tags for highlighting.
    /// Converts [text] to <color=#FF8000>text</color> (vibrant orange).
    private string FormatAnnouncementText(string input)
    {
        // Convert Color to hex string for TMP
        string hexColor = ColorUtility.ToHtmlStringRGB(announcerHighlightColor);

        // Replace [text] with <color=#hex>text</color>
        string result = System.Text.RegularExpressions.Regex.Replace(
            input,
            @"\[([^\]]+)\]",
            $"<color=#{hexColor}>$1</color>"
        );

        return result;
    }

    #endregion
}