using UnityEngine;

/// Controls time scale for SUPERHOT-style slow motion during Standoff mode
public class TimeController : MonoBehaviour
{
    // Singleton instance for easy access.
    public static TimeController Instance { get; private set; }

    [Header("Time Settings")]
    [Tooltip("Normal time scale (default 1.0)")]
    // Time scale when player is moving (normal speed).
    [SerializeField][Range(0.1f, 2f)] private float normalTimeScale = 1f;
    [Tooltip("Slow motion time scale when player is idle")]
    // Time scale when player is idle (slow motion).
    [SerializeField][Range(0.01f, 1f)] private float slowMotionTimeScale = 0.1f;
    [Tooltip("Minimum movement magnitude to trigger normal time")]
    // Minimum player movement magnitude to trigger normal time scale.
    [SerializeField][Range(0.01f, 1f)] private float movementThreshold = 0.1f;
    [Tooltip("Speed of time scale transitions")]
    // Speed at which time scale transitions between normal and slow motion.
    [SerializeField] private float transitionSpeed = 5f;

    [Header("Mode Settings")]
    [Tooltip("Enable slow motion effect")]
    // Whether the slow motion effect is currently enabled.
    [SerializeField] private bool slowMotionEnabled = false;
    [Tooltip("Current time scale (viewable in inspector)")]
    // Current time scale applied to the game.
    [SerializeField] private float currentTimeScale = 1f;

    [Header("Audio Pitch")]
    [Tooltip("Match audio pitch to time scale (affects all audio including BGM)")]
    // Whether to adjust audio pitch to match the time scale.
    [SerializeField] private bool adjustAudioPitch = true;
    [Tooltip("Minimum audio pitch")]
    // Minimum pitch to prevent audio from becoming inaudible.
    [SerializeField][Range(0.1f, 1f)] private float minAudioPitch = 0.3f;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    // Enable debug logging for time scale changes.
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
            currentTimeScale = normalTimeScale;
            targetTimeScale = normalTimeScale;
            // Reset audio pitch to normal when disabling slow motion
            if (adjustAudioPitch)
            {
                AdjustAudioPitch(1f);
            }
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
        targetTimeScale = normalTimeScale;
        slowMotionEnabled = false;
        if (adjustAudioPitch)
        {
            AdjustAudioPitch(1f);
        }
    }

    private void CheckPlayerMovement()
    {
        // Check for input from mobile or keyboard
        Vector2 movement = Vector2.zero;
        bool hasJumpInput = false;

        // Input system (unified mobile/desktop)
        if (InputSystem.Instance != null)
        {
            movement = InputSystem.Instance.Movement;
            hasJumpInput = InputSystem.Instance.JumpPressed;
        }
        else
        {
            // Keyboard fallback if InputSystem not available
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            movement = new Vector2(h, v);
            hasJumpInput = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        }

        // Consider any movement or jump input as player activity
        isPlayerMoving = movement.magnitude >= movementThreshold || hasJumpInput;
    }

    private void AdjustAudioPitch(float pitch)
    {
        // Refresh audio sources list if empty
        if (audioSources == null || audioSources.Length == 0)
        {
            audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        }

        // Adjust pitch for all audio sources (including BGM)
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