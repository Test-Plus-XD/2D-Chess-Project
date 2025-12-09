using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Virtual joystick for mobile control in Standoff mode
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    #region Inspector Fields

    [Header("Joystick Components")]
    [Tooltip("The background image of the joystick")]
    [SerializeField] private RectTransform joystickBackground;

    [Tooltip("The handle that moves within the joystick")]
    [SerializeField] private RectTransform joystickHandle;

    [Header("Settings")]
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

    [Header("Debug")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Private Fields

    private Vector2 inputVector;
    private CanvasGroup canvasGroup;
    private Camera mainCamera;
    private bool isActive = false;

    #endregion

    #region Properties

    /// <summary>
    /// Get the current input direction (normalized)
    /// </summary>
    public Vector2 Direction => inputVector.magnitude > deadZone ? inputVector : Vector2.zero;

    /// <summary>
    /// Get the horizontal input (-1 to 1)
    /// </summary>
    public float Horizontal => Direction.x;

    /// <summary>
    /// Get the vertical input (-1 to 1)
    /// </summary>
    public float Vertical => Direction.y;

    /// <summary>
    /// Check if joystick is currently being used
    /// </summary>
    public bool IsActive => isActive;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Setup canvas group for opacity control
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        mainCamera = Camera.main;
    }

    private void Start()
    {
        // Initialize joystick
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
        // Desktop fallback using keyboard
        if (!isActive && Application.isEditor)
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

    #endregion

    #region Event Handlers

    public void OnPointerDown(PointerEventData eventData)
    {
        isActive = true;
        OnDrag(eventData);

        if (dynamicJoystick)
        {
            SetOpacity(activeOpacity);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isActive = false;
        ResetJoystick();

        if (dynamicJoystick)
        {
            SetOpacity(idleOpacity);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 position;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground,
            eventData.position,
            eventData.pressEventCamera,
            out position))
        {
            // Normalize position
            Vector2 direction = position / (joystickBackground.sizeDelta / 2f);

            // Clamp to circular boundary
            inputVector = direction.magnitude > 1f ? direction.normalized : direction;

            // Update handle position
            joystickHandle.anchoredPosition = inputVector * handleRange;

            if (showDebug)
            {
                Debug.Log($"Joystick Input: {inputVector}");
            }
        }
    }

    #endregion

    #region Private Methods

    private void ResetJoystick()
    {
        inputVector = Vector2.zero;
        joystickHandle.anchoredPosition = Vector2.zero;
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
