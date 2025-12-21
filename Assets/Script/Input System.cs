using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// Unified input system handling mobile and desktop input.
// Uses DynamicJoystick from 3rd party Joystick Pack for mobile controls.
public class InputSystem : MonoBehaviour
{
    // Singleton instance for easy access.
    public static InputSystem Instance { get; private set; }

    [Header("Joystick Components")]
    [Tooltip("DynamicJoystick component for mobile controls")]
    // DynamicJoystick component from 3rd party Joystick Pack.
    [SerializeField] public DynamicJoystick dynamicJoystick;

    [Header("Jump Button")]
    [Tooltip("Jump button")]
    // UI button that triggers the jump action.
    [SerializeField] public Button jumpButton;

    [Header("Settings")]
    [Tooltip("Enable mobile controls")]
    // Whether to enable mobile touch-based controls.
    [SerializeField] public bool enableMobileControls = true;
    [Tooltip("Auto-detect platform (enable on mobile, disable on desktop)")]
    // Automatically detect if running on mobile platform.
    [SerializeField] public bool autoDetectPlatform = true;

    [Header("Events")]
    [Tooltip("Called when jump button is pressed")]
    // Event invoked when the jump button is pressed.
    public UnityEvent OnJumpPressed;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    // Enable debug logging for input events.
    [SerializeField] public bool showDebug = false;

    // Whether jump input was pressed this frame.
    private bool jumpInputThisFrame = false;

    // Direction input from joystick or keyboard.
    public Vector2 Direction
    {
        get
        {
            if (dynamicJoystick != null && dynamicJoystick.Direction.magnitude > 0.01f)
                return dynamicJoystick.Direction;

            // Keyboard fallback
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (h != 0 || v != 0)
                return new Vector2(h, v).normalized;

            return Vector2.zero;
        }
    }

    // Horizontal component of direction.
    public float Horizontal => Direction.x;
    // Vertical component of direction.
    public float Vertical => Direction.y;
    // Movement from either mobile or desktop input.
    public Vector2 Movement => Direction;
    // Whether jump button was pressed.
    public bool JumpPressed => jumpInputThisFrame;
    // Whether joystick is currently active (has input).
    public bool IsJoystickActive => dynamicJoystick != null && dynamicJoystick.Direction.magnitude > 0.01f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (autoDetectPlatform)
        {
#if UNITY_ANDROID || UNITY_IOS
            enableMobileControls = true;
#else
            enableMobileControls = Application.isMobilePlatform;
#endif
        }

        if (jumpButton != null)
        {
            jumpButton.onClick.AddListener(OnJumpButtonPressed);
        }
    }

    private void Start()
    {
        SetMobileControlsVisibility(enableMobileControls);
    }

    private void Update()
    {
        // Keyboard jump input (always available regardless of mobile controls)
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            jumpInputThisFrame = true;
        }
    }

    private void LateUpdate()
    {
        jumpInputThisFrame = false;
    }

    public void SetMobileControls(bool enabled)
    {
        enableMobileControls = enabled;
        SetMobileControlsVisibility(enabled);
    }

    public void SetMobileControlsVisibility(bool visible)
    {
        if (dynamicJoystick != null)
        {
            dynamicJoystick.gameObject.SetActive(visible);
        }

        if (jumpButton != null)
        {
            jumpButton.gameObject.SetActive(visible);
        }
    }

    public void OnJumpButtonPressed()
    {
        jumpInputThisFrame = true;
        OnJumpPressed?.Invoke();

        if (showDebug)
        {
            Debug.Log("Jump button pressed");
        }
    }
}