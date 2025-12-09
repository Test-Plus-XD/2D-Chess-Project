using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages the level selection UI
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    #region Inspector Fields

    [Header("UI Elements")]
    [Tooltip("Level select panel")]
    [SerializeField] private GameObject levelSelectPanel;

    [Tooltip("Level button prefab")]
    [SerializeField] private GameObject levelButtonPrefab;

    [Tooltip("Container for level buttons")]
    [SerializeField] private Transform levelButtonContainer;

    [Tooltip("Back button")]
    [SerializeField] private Button backButton;

    [Tooltip("Title text")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Private Fields

    private List<GameObject> levelButtons = new List<GameObject>();

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Setup back button
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackClicked);
        }

        // Generate level buttons
        GenerateLevelButtons();

        // Hide initially
        Hide();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Show level select screen
    /// </summary>
    public void Show()
    {
        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(true);
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetState(GameStateManager.GameState.LevelSelect);
        }

        RefreshLevelButtons();
    }

    /// <summary>
    /// Hide level select screen
    /// </summary>
    public void Hide()
    {
        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(false);
        }
    }

    #endregion

    #region Private Methods

    private void GenerateLevelButtons()
    {
        if (LevelManager.Instance == null || levelButtonContainer == null || levelButtonPrefab == null)
        {
            return;
        }

        // Clear existing buttons
        foreach (var button in levelButtons)
        {
            Destroy(button);
        }
        levelButtons.Clear();

        // Create buttons for each level
        LevelData[] levels = LevelManager.Instance.GetAllLevels();
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] == null) continue;

            GameObject buttonObj = Instantiate(levelButtonPrefab, levelButtonContainer);
            levelButtons.Add(buttonObj);

            // Setup button
            Button button = buttonObj.GetComponent<Button>();
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();

            if (buttonText != null)
            {
                buttonText.text = $"Level {i + 1}\n{levels[i].LevelName}";
            }

            // Add click listener
            int levelIndex = i; // Capture for closure
            if (button != null)
            {
                button.onClick.AddListener(() => OnLevelButtonClicked(levelIndex));
            }
        }
    }

    private void RefreshLevelButtons()
    {
        // Update button states (e.g., locked/unlocked)
        // For now, all levels are unlocked
    }

    private void OnLevelButtonClicked(int levelIndex)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        if (showDebug)
        {
            Debug.Log($"Level {levelIndex} button clicked");
        }

        StartLevel(levelIndex);
    }

    private void OnBackClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        Hide();

        MainMenuUI mainMenuUI = FindObjectOfType<MainMenuUI>();
        if (mainMenuUI != null)
        {
            mainMenuUI.Show();
        }
    }

    private void StartLevel(int levelIndex)
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
