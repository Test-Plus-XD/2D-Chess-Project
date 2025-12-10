using UnityEngine;

/// Controls time scale for SUPERHOT-style slow motion during Standoff mode
public class TimeController : MonoBehaviour
{
    // Singleton instance for easy access.
    public static TimeController Instance { get; private set; }

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

    // Target time scale for smooth transition.
    private float targetTimeScale;
    // Whether the player is currently moving.
    private bool isPlayerMoving = false;
    // Cached array of all audio sources in the scene.
    private AudioSource[] audioSources;

    /// Check if slow motion is currently enabled
    public bool IsSlowMotionEnabled => slowMotionEnabled;
    /// Get the current time scale
    public float CurrentTimeScale => currentTimeScale;

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
        audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
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
            AdjustAudioPitch(pitch);
        }

        if (showDebug && Mathf.Abs(currentTimeScale - targetTimeScale) > 0.01f)
        {
            Debug.Log($"Time Scale: {currentTimeScale:F2} (Target: {targetTimeScale:F2})");
        }
    }

    /// Enable or disable slow motion effect
    public void SetSlowMotionEnabled(bool enabled)
    {
        slowMotionEnabled = enabled;

        if (!enabled)
        {
            Time.timeScale = normalTimeScale;
            AdjustAudioPitch(1f);
        }

        if (showDebug)
        {
            Debug.Log($"Slow motion {(enabled ? "enabled" : "disabled")}");
        }
    }

    /// Set normal time scale value
    public void SetNormalTimeScale(float scale)
    {
        normalTimeScale = Mathf.Clamp(scale, 0.1f, 2f);
    }

    /// Set slow motion time scale value
    public void SetSlowMotionTimeScale(float scale)
    {
        slowMotionTimeScale = Mathf.Clamp(scale, 0.01f, 1f);
    }

    /// Temporarily override time scale (for cutscenes, etc.)
    public void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        currentTimeScale = scale;
    }

    /// Reset time to normal
    public void ResetTime()
    {
        Time.timeScale = normalTimeScale;
        currentTimeScale = normalTimeScale;
        AdjustAudioPitch(1f);
    }

    private void CheckPlayerMovement()
    {
        // Check for input from mobile or keyboard
        Vector2 movement = Vector2.zero;

        // Input system (unified mobile/desktop)
        if (InputSystem.Instance != null)
        {
            movement = InputSystem.Instance.Movement;
        }
        else
        {
            // Keyboard fallback if InputSystem not available
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            movement = new Vector2(h, v);
        }

        isPlayerMoving = movement.magnitude >= movementThreshold;
    }

    private void AdjustAudioPitch(float pitch)
    {
        // Refresh audio sources list if empty
        if (audioSources == null || audioSources.Length == 0)
        {
            audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        }

        // Adjust pitch for all audio sources
        foreach (var audioSource in audioSources)
        {
            if (audioSource != null)
            {
                audioSource.pitch = pitch;
            }
        }
    }

    private void OnValidate()
    {
        // Ensure slow motion scale is less than normal
        if (slowMotionTimeScale > normalTimeScale)
        {
            slowMotionTimeScale = normalTimeScale * 0.5f;
        }
    }
}
