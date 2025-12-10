using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Unified UI management system handling all game screens.
// Consolidates MainMenuUI, LevelSelectUI, GameUI, PauseMenuUI, FinishMenuUI, and SettingsUI.
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

    [Tooltip("Level select button")]
    [SerializeField] private Button levelSelectButton;

    [Tooltip("Settings button")]
    [SerializeField] private Button settingsButton;

    [Tooltip("Quit button")]
    [SerializeField] private Button quitButton;

    [Tooltip("Title text")]
    [SerializeField] private TextMeshProUGUI mainMenuTitleText;

    [Tooltip("Default level to start (0-based)")]
    [SerializeField] private int defaultStartLevel = 0;

    #endregion

    #region Inspector Fields - Level Select

    [Header("Level Select")]
    [Tooltip("Level select panel")]
    [SerializeField] private GameObject levelSelectPanel;

    [Tooltip("Level button prefab")]
    [SerializeField] private GameObject levelButtonPrefab;

    [Tooltip("Container for level buttons")]
    [SerializeField] private Transform levelButtonContainer;

    [Tooltip("Back button (level select)")]
    [SerializeField] private Button levelSelectBackButton;

    [Tooltip("Level select title")]
    [SerializeField] private TextMeshProUGUI levelSelectTitleText;

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
    }

    private void Start()
    {
        SetupAllButtons();
        SetupSliders();

        playerHealth = FindFirstObjectByType<PawnHealth>();
        checkerboard = FindFirstObjectByType<Checkerboard>();

        if (playerHealth != null)
        {
            playerHealth.OnHPChanged.AddListener(OnPlayerHPChanged);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnVictory.AddListener(ShowVictory);
            GameManager.Instance.OnDefeat.AddListener(ShowDefeat);
        }

        GenerateLevelButtons();
        HideAllPanels();
        ShowMainMenu();
    }

    private void Update()
    {
        UpdateGameUI();

        // ESC key to toggle pause
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

    #region Setup Methods

    private void SetupAllButtons()
    {
        // Main Menu
        if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
        if (levelSelectButton != null) levelSelectButton.onClick.AddListener(OnLevelSelectClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        // Level Select
        if (levelSelectBackButton != null) levelSelectBackButton.onClick.AddListener(OnLevelSelectBackClicked);

        // Game UI
        if (pauseButton != null) pauseButton.onClick.AddListener(OnPauseClicked);

        // Pause Menu
        if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
        if (pauseMainMenuButton != null) pauseMainMenuButton.onClick.AddListener(OnMainMenuClicked);

        // Victory
        if (nextLevelButton != null) nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        if (victoryMainMenuButton != null) victoryMainMenuButton.onClick.AddListener(OnMainMenuClicked);

        // Defeat
        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (defeatMainMenuButton != null) defeatMainMenuButton.onClick.AddListener(OnMainMenuClicked);

        // Settings
        if (settingsBackButton != null) settingsBackButton.onClick.AddListener(OnSettingsBackClicked);
    }

    private void SetupSliders()
    {
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
        if (GameManager.Instance == null || levelButtonContainer == null || levelButtonPrefab == null)
        {
            return;
        }

        foreach (var button in levelButtons)
        {
            Destroy(button);
        }
        levelButtons.Clear();

        LevelData[] levels = GameManager.Instance.GetAllLevels();
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] == null) continue;

            GameObject buttonObj = Instantiate(levelButtonPrefab, levelButtonContainer);
            levelButtons.Add(buttonObj);

            Button button = buttonObj.GetComponent<Button>();
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();

            if (buttonText != null)
            {
                buttonText.text = $"Level {i + 1}\n{levels[i].LevelName}";
            }

            int levelIndex = i;
            if (button != null)
            {
                button.onClick.AddListener(() => OnLevelButtonClicked(levelIndex));
            }
        }
    }

    #endregion

    #region Public Methods - Show/Hide Screens

    public void ShowMainMenu()
    {
        HideAllPanels();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameManager.GameState.MainMenu);
        }
    }

    public void ShowLevelSelect()
    {
        HideAllPanels();
        if (levelSelectPanel != null) levelSelectPanel.SetActive(true);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameManager.GameState.LevelSelect);
        }
    }

    public void ShowGameUI()
    {
        HideAllPanels();
        if (gameUIPanel != null) gameUIPanel.SetActive(true);
        RefreshGameUI();
    }

    public void ShowPauseMenu()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PauseGame();
        }
    }

    public void ShowVictory()
    {
        HideAllPanels();
        if (victoryPanel != null) victoryPanel.SetActive(true);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayVictory();
        }

        UpdateVictoryText();

        if (nextLevelButton != null && GameManager.Instance != null)
        {
            nextLevelButton.gameObject.SetActive(GameManager.Instance.HasNextLevel);
        }

        if (showDebug) Debug.Log("Victory screen shown");
    }

    public void ShowDefeat()
    {
        HideAllPanels();
        if (defeatPanel != null) defeatPanel.SetActive(true);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayDefeat();
        }

        UpdateDefeatText();

        if (showDebug) Debug.Log("Defeat screen shown");
    }

    public void ShowSettings()
    {
        HideAllPanels();
        if (settingsPanel != null) settingsPanel.SetActive(true);
        LoadSettings();
    }

    public void HideAllPanels()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (gameUIPanel != null) gameUIPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    #endregion

    #region Button Handlers - Main Menu

    private void OnPlayClicked()
    {
        PlayButtonSound();
        StartGame(defaultStartLevel);
    }

    private void OnLevelSelectClicked()
    {
        PlayButtonSound();
        ShowLevelSelect();
    }

    private void OnSettingsClicked()
    {
        PlayButtonSound();
        ShowSettings();
    }

    private void OnQuitClicked()
    {
        PlayButtonSound();

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
        PlayButtonSound();
        if (showDebug) Debug.Log($"Level {levelIndex} button clicked");
        StartGame(levelIndex);
    }

    private void OnLevelSelectBackClicked()
    {
        PlayButtonSound();
        ShowMainMenu();
    }

    #endregion

    #region Button Handlers - Pause Menu

    private void OnPauseClicked()
    {
        PlayButtonSound();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PauseGame();
        }

        ShowPauseMenu();

        if (showDebug) Debug.Log("Game paused");
    }

    private void OnResumeClicked()
    {
        PlayButtonSound();

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResumeGame();
        }

        if (showDebug) Debug.Log("Resume button clicked");
    }

    private void OnRestartClicked()
    {
        PlayButtonSound();

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReloadCurrentLevel();
            GameManager.Instance.StartGame(GameManager.Instance.CurrentLevelIndex);
        }

        if (showDebug) Debug.Log("Restart button clicked");
    }

    private void OnMainMenuClicked()
    {
        PlayButtonSound();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToMainMenu();
        }

        ShowMainMenu();

        if (showDebug) Debug.Log("Main menu button clicked");
    }

    #endregion

    #region Button Handlers - Finish Menu

    private void OnNextLevelClicked()
    {
        PlayButtonSound();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadNextLevel();
            GameManager.Instance.StartGame(GameManager.Instance.CurrentLevelIndex);
        }

        ShowGameUI();

        if (showDebug) Debug.Log("Next level button clicked");
    }

    private void OnRetryClicked()
    {
        PlayButtonSound();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReloadCurrentLevel();
            GameManager.Instance.StartGame(GameManager.Instance.CurrentLevelIndex);
        }

        ShowGameUI();

        if (showDebug) Debug.Log("Retry button clicked");
    }

    #endregion

    #region Button Handlers - Settings

    private void OnSettingsBackClicked()
    {
        PlayButtonSound();
        ShowMainMenu();
    }

    private void OnMasterVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(value);
        }
        UpdateVolumeTexts();
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }
        UpdateVolumeTexts();
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }
        UpdateVolumeTexts();
    }

    #endregion

    #region Private Methods - Game Logic

    private void StartGame(int levelIndex)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadLevel(levelIndex);
            GameManager.Instance.StartGame(levelIndex);
        }

        ShowGameUI();
    }

    #endregion

    #region Private Methods - UI Updates

    private void UpdateGameUI()
    {
        if (gameUIPanel == null || !gameUIPanel.activeSelf) return;

        UpdateOpponentsCount();
        UpdateGameMode();
    }

    private void RefreshGameUI()
    {
        UpdatePlayerHP();
        UpdateLevelInfo();
        UpdateOpponentsCount();
        UpdateGameMode();
    }

    private void OnPlayerHPChanged(int newHP)
    {
        UpdatePlayerHP();
        if (showDebug) Debug.Log($"Player HP changed to {newHP}");
    }

    private void UpdatePlayerHP()
    {
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PawnHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnHPChanged.AddListener(OnPlayerHPChanged);
            }
        }

        if (playerHealth == null) return;

        int currentHP = playerHealth.GetCurrentHP();
        int maxHP = playerHealth.GetMaxHP();

        if (hpText != null)
        {
            hpText.text = $"HP: {currentHP}/{maxHP}";
        }

        if (hpBar != null)
        {
            hpBar.maxValue = maxHP;
            hpBar.value = currentHP;
        }

        UpdateHeartIcons(currentHP, maxHP);
    }

    private void UpdateHeartIcons(int currentHP, int maxHP)
    {
        if (heartIconsContainer == null || heartIconPrefab == null) return;

        foreach (Transform child in heartIconsContainer)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < maxHP; i++)
        {
            GameObject heart = Instantiate(heartIconPrefab, heartIconsContainer);
            Image heartImage = heart.GetComponent<Image>();

            if (heartImage != null)
            {
                heartImage.color = i < currentHP ? Color.red : new Color(0.3f, 0.3f, 0.3f);
            }
        }
    }

    private void UpdateLevelInfo()
    {
        if (levelText != null && GameManager.Instance != null)
        {
            LevelData currentLevel = GameManager.Instance.CurrentLevel;
            if (currentLevel != null)
            {
                levelText.text = $"Level {GameManager.Instance.CurrentLevelIndex + 1}: {currentLevel.LevelName}";
            }
        }
    }

    private void UpdateOpponentsCount()
    {
        if (opponentsText != null)
        {
            if (checkerboard == null)
            {
                checkerboard = FindFirstObjectByType<Checkerboard>();
            }

            if (checkerboard != null)
            {
                int count = checkerboard.GetOpponentControllers().Count;
                opponentsText.text = $"Enemies: {count}";
            }
        }
    }

    private void UpdateGameMode()
    {
        if (gameModeText != null && GameManager.Instance != null)
        {
            string mode = GameManager.Instance.IsStandoffMode ? "STANDOFF" : "CHESS";
            gameModeText.text = mode;

            gameModeText.color = GameManager.Instance.IsStandoffMode ? Color.red : Color.white;
        }
    }

    private void UpdateVictoryText()
    {
        if (GameManager.Instance == null) return;

        LevelData currentLevel = GameManager.Instance.CurrentLevel;
        if (currentLevel == null) return;

        if (victoryTitleText != null)
        {
            victoryTitleText.text = "VICTORY!";
        }

        if (victoryMessageText != null)
        {
            victoryMessageText.text = $"You completed {currentLevel.LevelName}!";
        }
    }

    private void UpdateDefeatText()
    {
        if (defeatTitleText != null)
        {
            defeatTitleText.text = "DEFEAT";
        }

        if (defeatMessageText != null)
        {
            defeatMessageText.text = "Try again!";
        }
    }

    private void LoadSettings()
    {
        if (AudioManager.Instance == null) return;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = AudioManager.Instance.GetMasterVolume();
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = AudioManager.Instance.GetMusicVolume();
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = AudioManager.Instance.GetSFXVolume();
        }

        UpdateVolumeTexts();
    }

    private void UpdateVolumeTexts()
    {
        if (masterVolumeText != null && masterVolumeSlider != null)
        {
            masterVolumeText.text = $"{Mathf.RoundToInt(masterVolumeSlider.value * 100)}%";
        }

        if (musicVolumeText != null && musicVolumeSlider != null)
        {
            musicVolumeText.text = $"{Mathf.RoundToInt(musicVolumeSlider.value * 100)}%";
        }

        if (sfxVolumeText != null && sfxVolumeSlider != null)
        {
            sfxVolumeText.text = $"{Mathf.RoundToInt(sfxVolumeSlider.value * 100)}%";
        }
    }

    private void PlayButtonSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
    }

    #endregion
}
