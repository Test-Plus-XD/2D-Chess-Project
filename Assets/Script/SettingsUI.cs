using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the settings menu UI
/// </summary>
public class SettingsUI : MonoBehaviour
{
    #region Inspector Fields

    [Header("UI Elements")]
    [Tooltip("Settings panel")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Audio Controls")]
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

    [Header("Controls")]
    [Tooltip("Back button")]
    [SerializeField] private Button backButton;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Setup sliders
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

        // Setup back button
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackClicked);
        }

        // Load current volumes
        LoadSettings();

        // Hide initially
        Hide();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Show settings menu
    /// </summary>
    public void Show()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }

        LoadSettings();
    }

    /// <summary>
    /// Hide settings menu
    /// </summary>
    public void Hide()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    #endregion

    #region Private Methods

    private void LoadSettings()
    {
        if (AudioManager.Instance == null) return;

        // Load volumes
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

    #endregion
}
