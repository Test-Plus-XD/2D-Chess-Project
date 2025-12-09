using UnityEngine;

/// <summary>
/// Controls time scale for SUPERHOT-style slow motion during Standoff mode
/// </summary>
public class TimeController : MonoBehaviour
{
    #region Singleton

    public static TimeController Instance { get; private set; }

    #endregion

    #region Inspector Fields

    [Header("Time Settings")]
    [Tooltip("Normal time scale (default 1.0)")]
    [SerializeField][Range(0.1f, 2f)] private float normalTimeScale = 1f;

    [Tooltip("Slow motion time scale when player is idle")]
    [SerializeField][Range(0.01f, 1f)] private float slowMotionTimeScale = 0.1f;

    [Tooltip("Minimum movement magnitude to trigger normal time")]
    [SerializeField][Range(0.01f, 1f)] private float movementThreshold = 0.1f;

    [Tooltip("Speed of time scale transitions")]
    [SerializeField] private float transitionSpeed = 5f;

    [Header("Mode Settings")]
    [Tooltip("Enable slow motion effect")]
    [SerializeField] private bool slowMotionEnabled = false;

    [Tooltip("Current time scale (viewable in inspector)")]
    [SerializeField] private float currentTimeScale = 1f;

    [Header("Audio Pitch")]
    [Tooltip("Match audio pitch to time scale")]
    [SerializeField] private bool adjustAudioPitch = true;

    [Tooltip("Minimum audio pitch")]
    [SerializeField][Range(0.1f, 1f)] private float minAudioPitch = 0.3f;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Private Fields

    private float targetTimeScale;
    private bool isPlayerMoving = false;

    #endregion

    #region Properties

    /// <summary>
    /// Check if slow motion is currently enabled
    /// </summary>
    public bool IsSlowMotionEnabled => slowMotionEnabled;

    /// <summary>
    /// Get the current time scale
    /// </summary>
    public float CurrentTimeScale => currentTimeScale;

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

        targetTimeScale = normalTimeScale;
        currentTimeScale = normalTimeScale;
    }

    private void Update()
    {
        if (!slowMotionEnabled)
        {
            currentTimeScale = normalTimeScale;
            Time.timeScale = normalTimeScale;
            return;
        }

        // Determine target time scale based on player movement
        CheckPlayerMovement();
        targetTimeScale = isPlayerMoving ? normalTimeScale : slowMotionTimeScale;

        // Smoothly transition to target time scale
        currentTimeScale = Mathf.Lerp(currentTimeScale, targetTimeScale, Time.unscaledDeltaTime * transitionSpeed);
        Time.timeScale = currentTimeScale;

        // Adjust audio pitch
        if (adjustAudioPitch)
        {
            float pitch = Mathf.Max(minAudioPitch, currentTimeScale);
            AudioListener.pitch = pitch;
        }

        if (showDebug && Mathf.Abs(currentTimeScale - targetTimeScale) > 0.01f)
        {
            Debug.Log($"Time Scale: {currentTimeScale:F2} (Target: {targetTimeScale:F2})");
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Enable or disable slow motion effect
    /// </summary>
    public void SetSlowMotionEnabled(bool enabled)
    {
        slowMotionEnabled = enabled;

        if (!enabled)
        {
            Time.timeScale = normalTimeScale;
            AudioListener.pitch = 1f;
        }

        if (showDebug)
        {
            Debug.Log($"Slow motion {(enabled ? "enabled" : "disabled")}");
        }
    }

    /// <summary>
    /// Set normal time scale value
    /// </summary>
    public void SetNormalTimeScale(float scale)
    {
        normalTimeScale = Mathf.Clamp(scale, 0.1f, 2f);
    }

    /// <summary>
    /// Set slow motion time scale value
    /// </summary>
    public void SetSlowMotionTimeScale(float scale)
    {
        slowMotionTimeScale = Mathf.Clamp(scale, 0.01f, 1f);
    }

    /// <summary>
    /// Temporarily override time scale (for cutscenes, etc.)
    /// </summary>
    public void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        currentTimeScale = scale;
    }

    /// <summary>
    /// Reset time to normal
    /// </summary>
    public void ResetTime()
    {
        Time.timeScale = normalTimeScale;
        currentTimeScale = normalTimeScale;
        AudioListener.pitch = 1f;
    }

    #endregion

    #region Private Methods

    private void CheckPlayerMovement()
    {
        // Check for input from mobile or keyboard
        Vector2 movement = Vector2.zero;

        // Mobile input
        if (MobileInputManager.Instance != null)
        {
            movement = MobileInputManager.Instance.Movement;
        }

        // Keyboard fallback
        if (movement.magnitude < movementThreshold)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            movement = new Vector2(h, v);
        }

        isPlayerMoving = movement.magnitude >= movementThreshold;
    }

    #endregion

    #region Editor Helpers

    private void OnValidate()
    {
        // Ensure slow motion scale is less than normal
        if (slowMotionTimeScale > normalTimeScale)
        {
            slowMotionTimeScale = normalTimeScale * 0.5f;
        }
    }

    #endregion
}
