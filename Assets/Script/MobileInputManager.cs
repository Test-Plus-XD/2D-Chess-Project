using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Manages mobile input including virtual joystick and buttons
/// </summary>
public class MobileInputManager : MonoBehaviour
{
    #region Singleton

    public static MobileInputManager Instance { get; private set; }

    #endregion

    #region Inspector Fields

    [Header("Input Components")]
    [Tooltip("Reference to the virtual joystick")]
    [SerializeField] private VirtualJoystick virtualJoystick;

    [Tooltip("Jump button")]
    [SerializeField] private Button jumpButton;

    [Header("Settings")]
    [Tooltip("Enable mobile controls")]
    [SerializeField] private bool enableMobileControls = true;

    [Tooltip("Auto-detect platform (enable on mobile, disable on desktop)")]
    [SerializeField] private bool autoDetectPlatform = true;

    [Header("Events")]
    [Tooltip("Called when jump button is pressed")]
    public UnityEvent OnJumpPressed;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Private Fields

    private bool jumpInputThisFrame = false;

    #endregion

    #region Properties

    /// <summary>
    /// Get horizontal input from joystick or keyboard
    /// </summary>
    public float Horizontal
    {
        get
        {
            if (enableMobileControls && virtualJoystick != null)
            {
                return virtualJoystick.Horizontal;
            }
            return Input.GetAxisRaw("Horizontal");
        }
    }

    /// <summary>
    /// Get vertical input from joystick or keyboard
    /// </summary>
    public float Vertical
    {
        get
        {
            if (enableMobileControls && virtualJoystick != null)
            {
                return virtualJoystick.Vertical;
            }
            return Input.GetAxisRaw("Vertical");
        }
    }

    /// <summary>
    /// Get movement direction vector
    /// </summary>
    public Vector2 Movement
    {
        get
        {
            if (enableMobileControls && virtualJoystick != null)
            {
                return virtualJoystick.Direction;
            }
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }
    }

    /// <summary>
    /// Check if jump was pressed this frame
    /// </summary>
    public bool JumpPressed => jumpInputThisFrame;

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

        // Auto-detect platform
        if (autoDetectPlatform)
        {
#if UNITY_ANDROID || UNITY_IOS
            enableMobileControls = true;
#else
            enableMobileControls = Application.isMobilePlatform;
#endif
        }

        // Setup jump button
        if (jumpButton != null)
        {
            jumpButton.onClick.AddListener(OnJumpButtonPressed);
        }
    }

    private void Start()
    {
        // Show/hide mobile controls based on platform
        SetMobileControlsVisibility(enableMobileControls);
    }

    private void Update()
    {
        // Check for keyboard jump input (fallback)
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            jumpInputThisFrame = true;
        }
    }

    private void LateUpdate()
    {
        // Reset jump input at end of frame
        jumpInputThisFrame = false;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Enable or disable mobile controls
    /// </summary>
    public void SetMobileControls(bool enabled)
    {
        enableMobileControls = enabled;
        SetMobileControlsVisibility(enabled);
    }

    /// <summary>
    /// Show or hide mobile UI elements
    /// </summary>
    public void SetMobileControlsVisibility(bool visible)
    {
        if (virtualJoystick != null)
        {
            virtualJoystick.gameObject.SetActive(visible);
        }

        if (jumpButton != null)
        {
            jumpButton.gameObject.SetActive(visible);
        }
    }

    #endregion

    #region Private Methods

    private void OnJumpButtonPressed()
    {
        jumpInputThisFrame = true;
        OnJumpPressed?.Invoke();

        if (showDebug)
        {
            Debug.Log("Jump button pressed");
        }
    }

    #endregion
}
