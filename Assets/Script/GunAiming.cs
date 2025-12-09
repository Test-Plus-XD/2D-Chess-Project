using UnityEngine;

/// <summary>
/// Handles gun rotation and aiming for opponent pawns in both Chess and Standoff modes
/// </summary>
public class GunAiming : MonoBehaviour
{
    #region Inspector Fields

    [Header("Gun Settings")]
    [Tooltip("The gun transform to rotate (single gun sprite)")]
    [SerializeField] private Transform gunTransform;

    [Tooltip("Gun offset from pawn center")]
    [SerializeField] private Vector2 gunOffset = new Vector2(0.2f, 0f);

    [Header("Chess Mode Settings")]
    [Tooltip("Update aim direction when AI thinks (turn-based)")]
    [SerializeField] private bool updateOnAITurn = true;

    [Header("Standoff Mode Settings")]
    [Tooltip("Aim tracking speed (lower = more delay)")]
    [SerializeField][Range(1f, 20f)] private float aimTrackingSpeed = 10f;

    [Tooltip("Time to stop tracking before shooting")]
    [SerializeField] private float stopTrackingBeforeShotTime = 0.5f;

    [Tooltip("Smoothing for rotation")]
    [SerializeField] private bool smoothRotation = true;

    [Header("Debug")]
    [Tooltip("Show debug lines")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Private Fields

    private Transform playerTransform;
    private PawnController pawnController;
    private Firearm firearm;
    private bool isStandoffMode = false;
    private Vector2 currentAimDirection = Vector2.right;
    private Vector2 targetAimDirection = Vector2.right;
    private float lastShotTime = -999f;

    // Hex direction vectors (6 directions, flat-top orientation)
    private Vector2[] hexDirections = new Vector2[6];

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        pawnController = GetComponent<PawnController>();
        firearm = GetComponent<Firearm>();

        // Initialize hex directions (flat-top)
        InitializeHexDirections();

        // Create gun transform if not assigned
        if (gunTransform == null)
        {
            GameObject gunObj = new GameObject("Gun");
            gunObj.transform.SetParent(transform);
            gunObj.transform.localPosition = gunOffset;
            gunTransform = gunObj.transform;
        }
    }

    private void Start()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        if (isStandoffMode)
        {
            UpdateStandoffAiming();
        }
        else
        {
            UpdateChessAiming();
        }

        // Apply rotation to gun
        ApplyGunRotation();
    }

    private void OnDrawGizmos()
    {
        if (!showDebug || gunTransform == null) return;

        // Draw aim direction
        Gizmos.color = Color.red;
        Gizmos.DrawRay(gunTransform.position, currentAimDirection * 2f);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Switch between Chess and Standoff modes
    /// </summary>
    public void SetStandoffMode(bool standoffMode)
    {
        isStandoffMode = standoffMode;
    }

    /// <summary>
    /// Get current aim direction
    /// </summary>
    public Vector2 GetAimDirection()
    {
        return currentAimDirection;
    }

    /// <summary>
    /// Notify when shot was fired (for timing)
    /// </summary>
    public void OnShotFired()
    {
        lastShotTime = Time.time;
    }

    #endregion

    #region Private Methods - Initialization

    private void InitializeHexDirections()
    {
        // Flat-top hex directions (angles: 0°, 60°, 120°, 180°, 240°, 300°)
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            hexDirections[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }

    private void FindPlayer()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    #endregion

    #region Private Methods - Chess Mode Aiming

    private void UpdateChessAiming()
    {
        if (playerTransform == null) return;

        // In Chess mode, aim in the hex direction closest to player
        Vector2 toPlayer = (playerTransform.position - transform.position).normalized;
        targetAimDirection = GetNearestHexDirection(toPlayer);

        // Update immediately (turn-based, no smoothing needed)
        currentAimDirection = targetAimDirection;

        // Notify firearm of aim direction
        if (firearm != null)
        {
            firearm.SetAimDirection(currentAimDirection);
        }
    }

    private Vector2 GetNearestHexDirection(Vector2 targetDirection)
    {
        int bestIndex = 0;
        float bestDot = -1f;

        for (int i = 0; i < hexDirections.Length; i++)
        {
            float dot = Vector2.Dot(targetDirection, hexDirections[i]);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestIndex = i;
            }
        }

        return hexDirections[bestIndex];
    }

    #endregion

    #region Private Methods - Standoff Mode Aiming

    private void UpdateStandoffAiming()
    {
        if (playerTransform == null) return;

        // Check if we should stop tracking (0.5s before shot)
        bool shouldStopTracking = false;
        if (firearm != null)
        {
            float timeSinceLastShot = Time.time - lastShotTime;
            float timeToNextShot = firearm.GetTimeToNextShot();

            // If next shot is imminent (within 0.5s), stop tracking
            if (timeToNextShot <= stopTrackingBeforeShotTime && timeToNextShot > 0f)
            {
                shouldStopTracking = true;
            }
        }

        if (!shouldStopTracking)
        {
            // Continuously track player with delay
            Vector2 toPlayer = (playerTransform.position - transform.position).normalized;
            targetAimDirection = toPlayer;

            // Smooth transition to target
            if (smoothRotation)
            {
                currentAimDirection = Vector2.Lerp(
                    currentAimDirection,
                    targetAimDirection,
                    Time.deltaTime * aimTrackingSpeed
                ).normalized;
            }
            else
            {
                currentAimDirection = targetAimDirection;
            }
        }
        // else: Stop tracking, keep current aim direction

        // Notify firearm of aim direction
        if (firearm != null)
        {
            firearm.SetAimDirection(currentAimDirection);
        }

        if (showDebug && shouldStopTracking)
        {
            Debug.Log($"[GunAiming] Stopped tracking - preparing to shoot");
        }
    }

    #endregion

    #region Private Methods - Gun Rotation

    private void ApplyGunRotation()
    {
        if (gunTransform == null) return;

        // Calculate rotation angle from aim direction
        float angle = Mathf.Atan2(currentAimDirection.y, currentAimDirection.x) * Mathf.Rad2Deg;

        // Apply rotation
        gunTransform.rotation = Quaternion.Euler(0f, 0f, angle);

        // Flip gun sprite if aiming left
        Vector3 scale = gunTransform.localScale;
        if (currentAimDirection.x < 0)
        {
            scale.y = -Mathf.Abs(scale.y); // Flip vertically when aiming left
        }
        else
        {
            scale.y = Mathf.Abs(scale.y); // Normal when aiming right
        }
        gunTransform.localScale = scale;
    }

    #endregion
}
