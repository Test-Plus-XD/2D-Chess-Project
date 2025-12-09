using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the main menu UI
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    #region Inspector Fields

    [Header("UI Elements")]
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
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Settings")]
    [Tooltip("Default level to start (0-based)")]
    [SerializeField] private int defaultStartLevel = 0;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Setup button listeners
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayClicked);
        }

        if (levelSelectButton != null)
        {
            levelSelectButton.onClick.AddListener(OnLevelSelectClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnQuitClicked);
        }

        // Show main menu
        Show();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Show main menu
    /// </summary>
    public void Show()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetState(GameStateManager.GameState.MainMenu);
        }
    }

    /// <summary>
    /// Hide main menu
    /// </summary>
    public void Hide()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }
    }

    #endregion

    #region Private Methods - Button Handlers

    private void OnPlayClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        if (showDebug)
        {
            Debug.Log("Play button clicked");
        }

        // Start game at default level
        StartGame(defaultStartLevel);
    }

    private void OnLevelSelectClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        if (showDebug)
        {
            Debug.Log("Level select button clicked");
        }

        // Show level select screen
        Hide();

        LevelSelectUI levelSelectUI = FindObjectOfType<LevelSelectUI>();
        if (levelSelectUI != null)
        {
            levelSelectUI.Show();
        }
    }

    private void OnSettingsClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        if (showDebug)
        {
            Debug.Log("Settings button clicked");
        }

        // Show settings screen (to be implemented)
        SettingsUI settingsUI = FindObjectOfType<SettingsUI>();
        if (settingsUI != null)
        {
            settingsUI.Show();
        }
    }

    private void OnQuitClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        if (showDebug)
        {
            Debug.Log("Quit button clicked");
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void StartGame(int levelIndex)
    {
        Hide();

        // Load level
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.LoadLevel(levelIndex);
        }

        // Start game
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.StartGame(levelIndex);
        }

        // Show game UI
        GameUI gameUI = FindObjectOfType<GameUI>();
        if (gameUI != null)
        {
            gameUI.Show();
        }
    }

    #endregion
}
