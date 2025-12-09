using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the victory/defeat screen UI
/// </summary>
public class FinishMenuUI : MonoBehaviour
{
    #region Inspector Fields

    [Header("UI Panels")]
    [Tooltip("Victory panel")]
    [SerializeField] private GameObject victoryPanel;

    [Tooltip("Defeat panel")]
    [SerializeField] private GameObject defeatPanel;

    [Header("Victory UI")]
    [Tooltip("Victory title text")]
    [SerializeField] private TextMeshProUGUI victoryTitleText;

    [Tooltip("Victory message text")]
    [SerializeField] private TextMeshProUGUI victoryMessageText;

    [Tooltip("Next level button")]
    [SerializeField] private Button nextLevelButton;

    [Tooltip("Victory main menu button")]
    [SerializeField] private Button victoryMainMenuButton;

    [Header("Defeat UI")]
    [Tooltip("Defeat title text")]
    [SerializeField] private TextMeshProUGUI defeatTitleText;

    [Tooltip("Defeat message text")]
    [SerializeField] private TextMeshProUGUI defeatMessageText;

    [Tooltip("Retry button")]
    [SerializeField] private Button retryButton;

    [Tooltip("Defeat main menu button")]
    [SerializeField] private Button defeatMainMenuButton;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Setup victory buttons
        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        }

        if (victoryMainMenuButton != null)
        {
            victoryMainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        // Setup defeat buttons
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(OnRetryClicked);
        }

        if (defeatMainMenuButton != null)
        {
            defeatMainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        // Subscribe to game state events
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnVictory.AddListener(ShowVictory);
            GameStateManager.Instance.OnDefeat.AddListener(ShowDefeat);
        }

        // Hide initially
        HideAll();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Show victory screen
    /// </summary>
    public void ShowVictory()
    {
        HideAll();

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        // Play victory sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayVictory();
        }

        // Update victory text
        UpdateVictoryText();

        // Check if there's a next level
        if (nextLevelButton != null && LevelManager.Instance != null)
        {
            nextLevelButton.gameObject.SetActive(LevelManager.Instance.HasNextLevel);
        }

        if (showDebug)
        {
            Debug.Log("Victory screen shown");
        }
    }

    /// <summary>
    /// Show defeat screen
    /// </summary>
    public void ShowDefeat()
    {
        HideAll();

        if (defeatPanel != null)
        {
            defeatPanel.SetActive(true);
        }

        // Play defeat sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayDefeat();
        }

        // Update defeat text
        UpdateDefeatText();

        if (showDebug)
        {
            Debug.Log("Defeat screen shown");
        }
    }

    /// <summary>
    /// Hide all finish screens
    /// </summary>
    public void HideAll()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }

        if (defeatPanel != null)
        {
            defeatPanel.SetActive(false);
        }
    }

    #endregion

    #region Private Methods - UI Updates

    private void UpdateVictoryText()
    {
        if (LevelManager.Instance == null) return;

        LevelData currentLevel = LevelManager.Instance.CurrentLevel;
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

    #endregion

    #region Private Methods - Button Handlers

    private void OnNextLevelClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        if (showDebug)
        {
            Debug.Log("Next level button clicked");
        }

        HideAll();

        // Load next level
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.LoadNextLevel();
        }

        // Start next level
        if (GameStateManager.Instance != null && LevelManager.Instance != null)
        {
            GameStateManager.Instance.StartGame(LevelManager.Instance.CurrentLevelIndex);
        }

        // Show game UI
        GameUI gameUI = FindObjectOfType<GameUI>();
        if (gameUI != null)
        {
            gameUI.Show();
        }
    }

    private void OnRetryClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        if (showDebug)
        {
            Debug.Log("Retry button clicked");
        }

        HideAll();

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

        // Show game UI
        GameUI gameUI = FindObjectOfType<GameUI>();
        if (gameUI != null)
        {
            gameUI.Show();
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

        HideAll();

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
