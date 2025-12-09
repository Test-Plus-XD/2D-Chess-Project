using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the in-game UI (HUD)
/// </summary>
public class GameUI : MonoBehaviour
{
    #region Inspector Fields

    [Header("UI Panels")]
    [Tooltip("Main game UI panel")]
    [SerializeField] private GameObject gameUIPanel;

    [Header("Player Info")]
    [Tooltip("Player HP text")]
    [SerializeField] private TextMeshProUGUI hpText;

    [Tooltip("Player HP bar (optional)")]
    [SerializeField] private Slider hpBar;

    [Tooltip("Heart icons container (optional)")]
    [SerializeField] private Transform heartIconsContainer;

    [Tooltip("Heart icon prefab")]
    [SerializeField] private GameObject heartIconPrefab;

    [Header("Game Info")]
    [Tooltip("Current level text")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Tooltip("Opponents remaining text")]
    [SerializeField] private TextMeshProUGUI opponentsText;

    [Tooltip("Game mode text (Chess/Standoff)")]
    [SerializeField] private TextMeshProUGUI gameModeText;

    [Header("Controls")]
    [Tooltip("Pause button")]
    [SerializeField] private Button pauseButton;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Private Fields

    private PlayerPawn playerPawn;
    private Checkerboard checkerboard;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Setup pause button
        if (pauseButton != null)
        {
            pauseButton.onClick.AddListener(OnPauseClicked);
        }

        // Find references
        playerPawn = FindObjectOfType<PlayerPawn>();
        checkerboard = FindObjectOfType<Checkerboard>();

        // Subscribe to player HP changes
        if (playerPawn != null)
        {
            playerPawn.OnHPChanged.AddListener(OnPlayerHPChanged);
        }

        // Hide initially
        Hide();
    }

    private void Update()
    {
        // Update UI elements
        UpdateOpponentsCount();
        UpdateGameMode();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Show game UI
    /// </summary>
    public void Show()
    {
        if (gameUIPanel != null)
        {
            gameUIPanel.SetActive(true);
        }

        RefreshUI();
    }

    /// <summary>
    /// Hide game UI
    /// </summary>
    public void Hide()
    {
        if (gameUIPanel != null)
        {
            gameUIPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Refresh all UI elements
    /// </summary>
    public void RefreshUI()
    {
        UpdatePlayerHP();
        UpdateLevelInfo();
        UpdateOpponentsCount();
        UpdateGameMode();
    }

    #endregion

    #region Private Methods - UI Updates

    private void UpdatePlayerHP()
    {
        if (playerPawn == null)
        {
            playerPawn = FindObjectOfType<PlayerPawn>();
            if (playerPawn != null)
            {
                playerPawn.OnHPChanged.AddListener(OnPlayerHPChanged);
            }
        }

        if (playerPawn == null) return;

        int currentHP = playerPawn.GetCurrentHP();
        int maxHP = playerPawn.GetMaxHP();

        // Update HP text
        if (hpText != null)
        {
            hpText.text = $"HP: {currentHP}/{maxHP}";
        }

        // Update HP bar
        if (hpBar != null)
        {
            hpBar.maxValue = maxHP;
            hpBar.value = currentHP;
        }

        // Update heart icons
        UpdateHeartIcons(currentHP, maxHP);
    }

    private void UpdateHeartIcons(int currentHP, int maxHP)
    {
        if (heartIconsContainer == null || heartIconPrefab == null) return;

        // Clear existing hearts
        foreach (Transform child in heartIconsContainer)
        {
            Destroy(child.gameObject);
        }

        // Create heart icons
        for (int i = 0; i < maxHP; i++)
        {
            GameObject heart = Instantiate(heartIconPrefab, heartIconsContainer);
            Image heartImage = heart.GetComponent<Image>();

            if (heartImage != null)
            {
                // Full heart if HP remaining, empty heart otherwise
                heartImage.color = i < currentHP ? Color.red : new Color(0.3f, 0.3f, 0.3f);
            }
        }
    }

    private void UpdateLevelInfo()
    {
        if (levelText != null && LevelManager.Instance != null)
        {
            LevelData currentLevel = LevelManager.Instance.CurrentLevel;
            if (currentLevel != null)
            {
                levelText.text = $"Level {LevelManager.Instance.CurrentLevelIndex + 1}: {currentLevel.LevelName}";
            }
        }
    }

    private void UpdateOpponentsCount()
    {
        if (opponentsText != null)
        {
            if (checkerboard == null)
            {
                checkerboard = FindObjectOfType<Checkerboard>();
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
        if (gameModeText != null && GameStateManager.Instance != null)
        {
            string mode = GameStateManager.Instance.IsStandoffMode ? "STANDOFF" : "CHESS";
            gameModeText.text = mode;

            // Change color for Standoff mode
            if (GameStateManager.Instance.IsStandoffMode)
            {
                gameModeText.color = Color.red;
            }
            else
            {
                gameModeText.color = Color.white;
            }
        }
    }

    #endregion

    #region Private Methods - Event Handlers

    private void OnPlayerHPChanged(int newHP)
    {
        UpdatePlayerHP();

        if (showDebug)
        {
            Debug.Log($"Player HP changed to {newHP}");
        }
    }

    private void OnPauseClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.PauseGame();
        }

        // Show pause menu
        PauseMenuUI pauseMenuUI = FindObjectOfType<PauseMenuUI>();
        if (pauseMenuUI != null)
        {
            pauseMenuUI.Show();
        }

        if (showDebug)
        {
            Debug.Log("Game paused");
        }
    }

    #endregion
}
