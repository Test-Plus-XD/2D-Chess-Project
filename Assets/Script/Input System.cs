using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// Unified input system handling mobile and desktop input.
// Consolidates MobileInputManager and VirtualJoystick functionality.
public class InputSystem : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    // Singleton instance for easy access.
    public static InputSystem Instance { get; private set; }

    [Header("Joystick Components")]
    [Tooltip("The background image of the joystick")]
    // Background image of the joystick control.
    [SerializeField] public RectTransform joystickBackground;
    [Tooltip("The handle that moves within the joystick")]
    // Handle that moves within the joystick bounds.
    [SerializeField] public RectTransform joystickHandle;

    [Header("Joystick Settings")]
    [Tooltip("Maximum distance the handle can move from center")]
    // Maximum distance in pixels the joystick handle can move from center.
    [SerializeField] public float handleRange = 50f;
    [Tooltip("Minimum input magnitude to register (0-1)")]
    // Minimum joystick input magnitude to register as valid input.
    [SerializeField][Range(0f, 1f)] public float deadZone = 0.1f;
    [Tooltip("Show/hide joystick when not in use")]
    // Whether to show/hide joystick based on activity.
    [SerializeField] public bool dynamicJoystick = true;
    [Tooltip("Opacity when joystick is idle")]
    // Opacity of joystick when not in use.
    [SerializeField][Range(0f, 1f)] public float idleOpacity = 0.3f;
    [Tooltip("Opacity when joystick is active")]
    // Opacity of joystick when being used.
    [SerializeField][Range(0f, 1f)] public float activeOpacity = 1f;

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

    // Current input direction from joystick or keyboard.
    private Vector2 inputVector;
    // Canvas group for opacity control.
    private CanvasGroup canvasGroup;
    // Whether the joystick is currently active.
    private bool isJoystickActive = false;
    // Whether jump input was pressed this frame.
    private bool jumpInputThisFrame = false;

    // Direction input clamped by dead zone.
    public Vector2 Direction => inputVector.magnitude > deadZone ? inputVector : Vector2.zero;
    // Horizontal component of direction.
    public float Horizontal => Direction.x;
    // Vertical component of direction.
    public float Vertical => Direction.y;
    // Movement from either mobile or desktop input.
    public Vector2 Movement => enableMobileControls ? Direction : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    // Whether jump button was pressed.
    public bool JumpPressed => jumpInputThisFrame;
    // Whether joystick is currently active.
    public bool IsJoystickActive => isJoystickActive;

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

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null && joystickBackground != null)
        {
            canvasGroup = joystickBackground.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = joystickBackground.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void Start()
    {
        SetMobileControlsVisibility(enableMobileControls);

        if (dynamicJoystick)
        {
            SetOpacity(idleOpacity);
        }
        else
        {
            SetOpacity(activeOpacity);
        }

        ResetJoystick();
    }

    private void Update()
    {
        // Keyboard jump input (always available regardless of mobile controls)
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            jumpInputThisFrame = true;
        }

        // Keyboard input (always available as fallback)
        // If joystick is not active, use keyboard input
        if (!isJoystickActive)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            if (h != 0 || v != 0)
            {
                inputVector = new Vector2(h, v).normalized;
            }
            else if (!isJoystickActive)
            {
                // Only reset if joystick is also not active
                inputVector = Vector2.zero;
            }
        }
    }

    private void LateUpdate()
    {
        jumpInputThisFrame = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isJoystickActive = true;
        OnDrag(eventData);

        if (dynamicJoystick)
        {
            SetOpacity(activeOpacity);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isJoystickActive = false;
        ResetJoystick();

        if (dynamicJoystick)
        {
            SetOpacity(idleOpacity);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (joystickBackground == null) return;

        Vector2 position;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground,
            eventData.position,
            eventData.pressEventCamera,
            out position))
        {
            Vector2 direction = position / (joystickBackground.sizeDelta / 2f);
            inputVector = direction.magnitude > 1f ? direction.normalized : direction;

            if (joystickHandle != null)
            {
                joystickHandle.anchoredPosition = inputVector * handleRange;
            }

            if (showDebug)
            {
                Debug.Log($"Joystick Input: {inputVector}");
            }
        }
    }

    public void SetMobileControls(bool enabled)
    {
        enableMobileControls = enabled;
        SetMobileControlsVisibility(enabled);
    }

    public void SetMobileControlsVisibility(bool visible)
    {
        if (joystickBackground != null)
        {
            joystickBackground.gameObject.SetActive(visible);
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

    private void ResetJoystick()
    {
        inputVector = Vector2.zero;
        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = Vector2.zero;
        }
    }

    private void SetOpacity(float opacity)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = opacity;
        }
    }
}