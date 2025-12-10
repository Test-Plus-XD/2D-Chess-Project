using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// Unified input system handling mobile and desktop input.
// Consolidates MobileInputManager and VirtualJoystick functionality.
public class InputSystem : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    #region Singleton

    public static InputSystem Instance { get; private set; }

    #endregion

    #region Inspector Fields - Joystick

    [Header("Joystick Components")]
    [Tooltip("The background image of the joystick")]
    [SerializeField] private RectTransform joystickBackground;

    [Tooltip("The handle that moves within the joystick")]
    [SerializeField] private RectTransform joystickHandle;

    [Header("Joystick Settings")]
    [Tooltip("Maximum distance the handle can move from center")]
    [SerializeField] private float handleRange = 50f;

    [Tooltip("Minimum input magnitude to register (0-1)")]
    [SerializeField][Range(0f, 1f)] private float deadZone = 0.1f;

    [Tooltip("Show/hide joystick when not in use")]
    [SerializeField] private bool dynamicJoystick = true;

    [Tooltip("Opacity when joystick is idle")]
    [SerializeField][Range(0f, 1f)] private float idleOpacity = 0.3f;

    [Tooltip("Opacity when joystick is active")]
    [SerializeField][Range(0f, 1f)] private float activeOpacity = 1f;

    #endregion

    #region Inspector Fields - Jump Button

    [Header("Jump Button")]
    [Tooltip("Jump button")]
    [SerializeField] private Button jumpButton;

    #endregion

    #region Inspector Fields - Settings

    [Header("Settings")]
    [Tooltip("Enable mobile controls")]
    [SerializeField] private bool enableMobileControls = true;

    [Tooltip("Auto-detect platform (enable on mobile, disable on desktop)")]
    [SerializeField] private bool autoDetectPlatform = true;

    #endregion

    #region Inspector Fields - Events

    [Header("Events")]
    [Tooltip("Called when jump button is pressed")]
    public UnityEvent OnJumpPressed;

    #endregion

    #region Inspector Fields - Debug

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Private Fields

    private Vector2 inputVector;
    private CanvasGroup canvasGroup;
    private bool isJoystickActive = false;
    private bool jumpInputThisFrame = false;

    #endregion

    #region Properties

    public Vector2 Direction => inputVector.magnitude > deadZone ? inputVector : Vector2.zero;
    public float Horizontal => Direction.x;
    public float Vertical => Direction.y;
    public Vector2 Movement => enableMobileControls ? Direction : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    public bool JumpPressed => jumpInputThisFrame;
    public bool IsJoystickActive => isJoystickActive;

    #endregion

    #region Unity Lifecycle

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
        // Keyboard jump input
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            jumpInputThisFrame = true;
        }

        // Desktop fallback using keyboard
        if (!isJoystickActive && !enableMobileControls)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            if (h != 0 || v != 0)
            {
                inputVector = new Vector2(h, v).normalized;
            }
            else
            {
                inputVector = Vector2.zero;
            }
        }
    }

    private void LateUpdate()
    {
        jumpInputThisFrame = false;
    }

    #endregion

    #region Event Handlers - Joystick

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

    #endregion

    #region Public Methods

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

    #endregion
}
