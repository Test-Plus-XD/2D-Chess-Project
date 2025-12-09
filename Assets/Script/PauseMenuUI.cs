using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the pause menu UI
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    #region Inspector Fields

    [Header("UI Elements")]
    [Tooltip("Pause menu panel")]
    [SerializeField] private GameObject pauseMenuPanel;

    [Tooltip("Resume button")]
    [SerializeField] private Button resumeButton;

    [Tooltip("Restart button")]
    [SerializeField] private Button restartButton;

    [Tooltip("Main menu button")]
    [SerializeField] private Button mainMenuButton;

    [Tooltip("Title text")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Setup buttons
        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(OnResumeClicked);
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        // Hide initially
        Hide();
    }

    private void Update()
    {
        // ESC key to toggle pause
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (pauseMenuPanel != null && pauseMenuPanel.activeSelf)
            {
                OnResumeClicked();
            }
            else if (GameStateManager.Instance != null &&
                     (GameStateManager.Instance.IsChessMode || GameStateManager.Instance.IsStandoffMode))
            {
                Show();
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Show pause menu
    /// </summary>
    public void Show()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.PauseGame();
        }
    }

    /// <summary>
    /// Hide pause menu
    /// </summary>
    public void Hide()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }
    }

    #endregion

    #region Private Methods - Button Handlers

    private void OnResumeClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        if (showDebug)
        {
            Debug.Log("Resume button clicked");
        }

        Hide();

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ResumeGame();
        }
    }

    private void OnRestartClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        if (showDebug)
        {
            Debug.Log("Restart button clicked");
        }

        Hide();

        // Reload current level
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.ReloadCurrentLevel();
        }

        // Restart game
        if (GameStateManager.Instance != null && LevelManager.Instance != null)
        {
            GameStateManager.Instance.StartGame(LevelManager.Instance.CurrentLevelIndex);
        }
    }

    private void OnMainMenuClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        if (showDebug)
        {
            Debug.Log("Main menu button clicked");
        }

        Hide();

        // Return to main menu
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ReturnToMainMenu();
        }

        // Hide game UI
        GameUI gameUI = FindObjectOfType<GameUI>();
        if (gameUI != null)
        {
            gameUI.Hide();
        }

        // Show main menu
        MainMenuUI mainMenuUI = FindObjectOfType<MainMenuUI>();
        if (mainMenuUI != null)
        {
            mainMenuUI.Show();
        }
    }

    #endregion
}
