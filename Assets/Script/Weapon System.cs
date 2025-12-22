using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Unified weapon system handling shooting, projectiles, and gun aiming.
// Consolidates Firearm, Projectile, and GunAiming functionality.
public class WeaponSystem : MonoBehaviour
{
    #region Enums

    public enum FireMode
    {
        Manual,
        OnLineOfSight,
        TrackPlayer,
        Timed
    }

    public enum ProjectileType
    {
        Single,
        Spread,
        Beam
    }

    #endregion

    [Header("Firearm Settings")]
    [Tooltip("The fire mode this firearm uses")]
    [SerializeField] private FireMode fireMode = FireMode.TrackPlayer;
    [Tooltip("The type of projectile this firearm shoots")]
    [SerializeField] private ProjectileType projectileType = ProjectileType.Single;
    [Tooltip("Damage dealt per bullet")]
    [SerializeField] private int damage = 1;
    [Tooltip("Time between shots in seconds (Standoff mode)")]
    [SerializeField] private float fireInterval = 3f;
    [Tooltip("Firing delay before shot (Standoff mode)")]
    [SerializeField] private float firingDelay = 0.5f;
    [Tooltip("Maximum range of bullets in world units")]
    [SerializeField] private float maxRange = 15f;
    [Tooltip("Speed of projectiles")]
    [SerializeField] private float projectileSpeed = 10f;
    [Tooltip("Angular velocity for tracking player (degrees/second)")]
    [SerializeField] private float trackingAngularVelocity = 90f;

    [Header("Spread Settings")]
    [Tooltip("Number of bullets in spread shot")]
    [SerializeField] private int spreadCount = 3;
    [Tooltip("Angle between spread bullets in degrees")]
    [SerializeField] private float spreadAngle = 30f;

    [Header("Line of Sight Settings")]
    [Tooltip("Detection range for line of sight mode")]
    [SerializeField] private float detectionRange = 10f;
    [Tooltip("Layer mask for detecting targets")]
    [SerializeField] private LayerMask targetLayer;
    [Tooltip("Layer mask for obstacles blocking line of sight")]
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Projectile Visuals")]
    [Tooltip("Projectile prefab to spawn")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("Muzzle flash effect (optional)")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [Tooltip("Rotation and position offset for projectile and muzzle flash in degrees (adjusts orientation)")]
    [SerializeField] private float fireOffset = 0f;
    [Tooltip("Duration muzzle flash stays visible before auto-destroy")]
    [SerializeField] private float muzzleFlashDuration = 0.1f;
    [Tooltip("Point where projectiles and muzzle flash spawn")]
    [SerializeField] private Transform firePoint;

    [Header("Gun Aiming")]
    [Tooltip("The gun transform to rotate")]
    [SerializeField] private Transform gunTransform;
    [Tooltip("Gun offset from pawn center")]
    [SerializeField] private Vector2 gunOffset = new Vector2(0.2f, 0f);

    [Header("Audio & Animation")]
    [Tooltip("Sound played when firing")]
    [SerializeField] private AudioClip fireSound;
    [Tooltip("Volume of fire sound")]
    [SerializeField][Range(0f, 1f)] private float fireVolume = 0.7f;
    [Tooltip("Animator for gun animations")]
    [SerializeField] private Animator gunAnimator;
    [Tooltip("Name of fire animation trigger")]
    [SerializeField] private string fireAnimationTrigger = "Fire";

    [Header("Recoil (Standoff Mode)")]
    [Tooltip("Enable physical recoil when shooting")]
    [SerializeField] private bool enableRecoil = true;
    [Tooltip("Recoil force magnitude")]
    [SerializeField] private float recoilForce = 3f;

    [Header("Debug")]
    [Tooltip("Show debug lines")]
    [SerializeField] private bool showDebug = false;
    [Tooltip("Use Debug.DrawLine as fallback for targeting visualization")]
    [SerializeField] private bool useDebugLineFallback = false;

    [Header("Targeting Visualization")]
    [Tooltip("Enable targeting visualization")]
    [SerializeField] private bool enableTargetingVisualization = true;
    [Tooltip("Chess Mode: Color to blink tiles")]
    [SerializeField] private Color chessTileBlinkColor = new Color(1f, 0f, 0f, 0.5f);
    [Tooltip("Chess Mode: Blink interval in seconds")]
    [SerializeField] private float blinkInterval = 0.5f;
    [Tooltip("Chess Mode: Maximum blinks before stopping (0 = infinite)")]
    [SerializeField] private int maxBlinkCount = 2;
    [Tooltip("Chess Mode: Maximum range in tiles to visualize")]
    [SerializeField] private int maxTileRange = 10;
    [Tooltip("Standoff Mode: Color of aiming line")]
    [SerializeField] private Color standoffLineColor = Color.red;
    [Tooltip("Standoff Mode: Width of aiming line")]
    [SerializeField] private float lineWidth = 0.05f;
    [Tooltip("Standoff Mode: Line material (leave null for default)")]
    [SerializeField] private Material lineMaterial;

    #region Private Fields

    private float lastFireTime;
    private AudioSource audioSource;
    private Transform playerTransform;
    private bool isInStandoffMode = false;
    private float currentAimAngle = 0f; // Angle in degrees
    private float targetAimAngle = 0f;
    private float lastShotTime = -999f;
    private Rigidbody2D rigidBody;
    private PawnController pawnController;
    private PawnController.Modifier currentModifier = PawnController.Modifier.None;

    // Standoff firing state
    private float intervalStartTime = 0f;
    private bool isInFiringDelay = false;
    private float firingDelayStartTime = 0f;
    private float lockedAimAngle = 0f;

    // Gun rotation freeze/unfreeze for animation
    private bool isGunRotationFrozen = false;
    private float gunUnfreezeTime = 0f;
    private const float GUN_ANIMATION_DURATION = 1f;

    // Targeting visualization state
    private LineRenderer lineRenderer;
    private List<GameObject> targetedTiles = new List<GameObject>();
    private Dictionary<SpriteRenderer, Color> originalTileColors = new Dictionary<SpriteRenderer, Color>();
    private Coroutine blinkCoroutine;
    private HexGridGenerator gridGenerator;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        rigidBody = GetComponent<Rigidbody2D>();
        pawnController = GetComponent<PawnController>();

        if (firePoint == null)
        {
            GameObject firePointObj = new GameObject("FirePoint");
            firePointObj.transform.SetParent(transform);
            firePointObj.transform.localPosition = Vector3.zero;
            firePoint = firePointObj.transform;
        }

        if (gunTransform == null)
        {
            GameObject gunObj = new GameObject("Gun");
            gunObj.transform.SetParent(transform);
            gunObj.transform.localPosition = gunOffset;
            gunTransform = gunObj.transform;
        }

        // Initialize targeting visualization
        if (enableTargetingVisualization)
        {
            InitializeTargetingVisualization();
        }
    }

    private void Start()
    {
        FindPlayer();
        gridGenerator = FindFirstObjectByType<HexGridGenerator>();
        lastFireTime = -fireInterval;

        // Calculate initial aim on spawn (Chess mode step 0)
        if (!isInStandoffMode)
        {
            RecalculateAim();
        }
    }

    private void Update()
    {
        // Don't update weapons when game is paused
        if (IsGamePaused()) return;
        
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        // Only update aim continuously in Standoff mode
        // In Chess mode, aim is calculated explicitly via RecalculateAim()
        if (isInStandoffMode)
        {
            UpdateAimDirection();
        }

        ApplyGunRotation();
        HandleFireModes();

        // Update targeting visualization
        if (enableTargetingVisualization)
        {
            UpdateTargetingVisualization();
        }
    }

    private void OnDisable()
    {
        ClearChessVisualization();
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        // Ensure we restore any tiles we were affecting when this weapon system is destroyed
        if (originalTileColors.Count > 0)
        {
            foreach (var kvp in originalTileColors)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.color = kvp.Value;
                }
            }
            originalTileColors.Clear();
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebug || firePoint == null) return;

        Vector2 aimDir = GetAimDirectionVector();
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(firePoint.position, aimDir * maxRange);

        if (fireMode == FireMode.OnLineOfSight)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }

        // Draw targeting visualization in Scene view for Standoff mode
        if (isInStandoffMode && enableTargetingVisualization)
        {
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + new Vector3(aimDir.x * maxRange, aimDir.y * maxRange, 0f);

            // Perform raycast to find actual end point
            RaycastHit2D hit = Physics2D.Raycast(startPos, aimDir, maxRange);
            if (hit.collider != null)
            {
                endPos = new Vector3(hit.point.x, hit.point.y, startPos.z);
            }

            Gizmos.color = standoffLineColor;
            Gizmos.DrawLine(startPos, endPos);
            
            // Draw small sphere at end point
            Gizmos.DrawWireSphere(endPos, 0.1f);
        }
    }

    #endregion

    #region Public Methods

    public void ManualFire()
    {
        // Don't fire when game is paused
        if (IsGamePaused()) return;
        
        if (Time.time >= lastFireTime + GetAdjustedFireInterval())
        {
            Fire();
        }
    }

    public void SetStandoffMode(bool standoffMode)
    {
        isInStandoffMode = standoffMode;

        if (standoffMode)
        {
            intervalStartTime = Time.time;
            isInFiringDelay = false;

            if (enableTargetingVisualization)
            {
                ClearChessVisualization();
                if (lineRenderer != null)
                {
                    lineRenderer.enabled = true;
                    // Refresh line renderer settings when entering standoff mode
                    RefreshLineRendererSettings();
                }
            }
        }
        else
        {
            if (enableTargetingVisualization && lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
        }
    }

    /// Refresh line renderer settings to ensure proper visibility
    private void RefreshLineRendererSettings()
    {
        if (lineRenderer == null) return;
        
        // Find main camera for proper positioning
        Camera mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindFirstObjectByType<Camera>();
        
        if (mainCamera != null)
        {
            // Adjust sorting order based on camera type
            if (mainCamera.orthographic)
            {
                // For orthographic cameras, use high sorting order
                lineRenderer.sortingOrder = 1000;
            }
            else
            {
                // For perspective cameras, position closer to camera
                lineRenderer.sortingOrder = 100;
            }
        }
        
        // Ensure material and color settings are applied
        if (lineRenderer.material != null)
        {
            lineRenderer.material.color = standoffLineColor;
        }
        
        lineRenderer.startColor = standoffLineColor;
        lineRenderer.endColor = standoffLineColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        
        if (showDebug)
        {
            Debug.Log($"[WeaponSystem] Refreshed line renderer settings - Sorting Order: {lineRenderer.sortingOrder}, Color: {standoffLineColor}");
        }
    }

    public void SetFireMode(FireMode mode)
    {
        fireMode = mode;
    }

    public float GetTimeToNextShot()
    {
        float timeSinceLastShot = Time.time - lastFireTime;
        return Mathf.Max(0f, GetAdjustedFireInterval() - timeSinceLastShot);
    }

    public Vector2 GetAimDirection()
    {
        return GetAimDirectionVector();
    }

    public void OnShotFired()
    {
        lastShotTime = Time.time;
    }

    public void ApplyModifier(PawnController.Modifier modifier)
    {
        currentModifier = modifier;
    }

    public void FireChessMode()
    {
        // Don't fire when game is paused
        if (IsGamePaused()) return;
        
        if (pawnController == null) return;
        if (pawnController.aiType == PawnController.AIType.Basic) return;
        Fire();
    }

    public void RecalculateAim()
    {
        if (playerTransform == null) return;
        UpdateAimDirection();
    }

    public void ClearTargetingVisualization()
    {
        if (enableTargetingVisualization)
        {
            ClearChessVisualization();
        }
    }

    public void RestoreAllTilesToOriginalColor()
    {
        if (gridGenerator == null || gridGenerator.parentContainer == null) return;

        // Restore all tiles to original white color (Color.white with alpha 255)
        Color originalColor = new Color(1f, 1f, 1f, 1f); // White with full alpha

        foreach (Transform child in gridGenerator.parentContainer)
        {
            SpriteRenderer spriteRenderer = child.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }

        // Clear any stored original colors since we're resetting everything
        originalTileColors.Clear();
    }

    // Static method to restore all tiles to original color - can be called from anywhere
    public static void RestoreAllTilesGlobally()
    {
        HexGridGenerator grid = FindFirstObjectByType<HexGridGenerator>();
        if (grid == null || grid.parentContainer == null) return;

        // Restore all tiles to original white color (Color.white with alpha 255)
        Color originalColor = new Color(1f, 1f, 1f, 1f); // White with full alpha

        foreach (Transform child in grid.parentContainer)
        {
            SpriteRenderer spriteRenderer = child.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }
    }

    // Public method to enable debug mode for testing direction calculations
    public void EnableDebugMode(bool enable)
    {
        showDebug = enable;
        if (showDebug) Debug.Log("[WeaponSystem] Debug mode enabled - direction calculations will be logged");
    }

    /// Public method to adjust targeting visualization settings at runtime
    public void UpdateTargetingVisualizationSettings(Color newLineColor, float newLineWidth, Material newMaterial = null)
    {
        standoffLineColor = newLineColor;
        lineWidth = newLineWidth;
        
        if (newMaterial != null)
        {
            lineMaterial = newMaterial;
        }
        
        if (lineRenderer != null)
        {
            lineRenderer.startColor = standoffLineColor;
            lineRenderer.endColor = standoffLineColor;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            
            if (newMaterial != null)
            {
                lineRenderer.material = newMaterial;
            }
            else if (lineRenderer.material != null)
            {
                lineRenderer.material.color = standoffLineColor;
            }
        }
        
        if (showDebug)
        {
            Debug.Log($"[WeaponSystem] Updated targeting visualization - Color: {newLineColor}, Width: {newLineWidth}");
        }
    }

    #endregion

    #region Private Methods - Pause Detection
    
    private bool IsGamePaused()
    {
        bool paused = Time.timeScale == 0f;
        if (paused && showDebug)
        {
            Debug.Log("[WeaponSystem] Firing blocked - game is paused");
        }
        return paused;
    }
    
    #endregion

    #region Private Methods - Initialization

    private void FindPlayer()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    #endregion

    #region Private Methods - Aiming

    private void UpdateAimDirection()
    {
        // Don't update aim direction when game is paused
        if (IsGamePaused()) return;
        
        if (playerTransform == null) return;

        if (isInStandoffMode)
        {
            UpdateStandoffAiming();
        }
        else
        {
            UpdateChessAiming();
        }
    }

    private void UpdateChessAiming()
    {
        // In Chess mode, find the best hex direction angle
        int bestHexIndex = GetBestAlignedHexDirection();
        targetAimAngle = GetHexDirectionAngle(bestHexIndex);
        currentAimAngle = targetAimAngle;

        if (showDebug)
        {
            Debug.Log($"[WeaponSystem] Chess aim: hex index {bestHexIndex}, angle {currentAimAngle}°");
        }
    }

    private void UpdateStandoffAiming()
    {
        // Don't update standoff aiming when game is paused
        if (IsGamePaused()) return;
        
        float adjustedInterval = GetAdjustedFireInterval();
        float adjustedDelay = GetAdjustedFiringDelay();
        float timeSinceIntervalStart = Time.time - intervalStartTime;

        if (!isInFiringDelay && timeSinceIntervalStart >= adjustedInterval)
        {
            isInFiringDelay = true;
            firingDelayStartTime = Time.time;
            lockedAimAngle = currentAimAngle;

            if (showDebug)
            {
                Debug.Log($"[WeaponSystem] Entering firing delay. Locked aim at {lockedAimAngle}°");
            }
        }

        if (isInFiringDelay)
        {
            float timeSinceDelayStart = Time.time - firingDelayStartTime;
            currentAimAngle = lockedAimAngle;

            if (timeSinceDelayStart >= adjustedDelay)
            {
                Fire();
                isInFiringDelay = false;
                intervalStartTime = Time.time;

                if (showDebug)
                {
                    Debug.Log($"[WeaponSystem] Fired! Restarting interval.");
                }
            }
        }
        else
        {
            // Calculate target angle to player
            Vector2 toPlayer = (playerTransform.position - transform.position).normalized;
            targetAimAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;

            // Reflexive modifier: instant tracking
            if (pawnController != null && pawnController.ShouldFixGunOnPlayer())
            {
                currentAimAngle = targetAimAngle;
            }
            else
            {
                // Normal tracking with angular velocity
                float maxRotationThisFrame = trackingAngularVelocity * Time.deltaTime;
                float angleDiff = Mathf.DeltaAngle(currentAimAngle, targetAimAngle);
                float rotationAmount = Mathf.Clamp(angleDiff, -maxRotationThisFrame, maxRotationThisFrame);
                currentAimAngle += rotationAmount;
            }
        }
    }

    // Get the world-space angle for a hex direction index
    private float GetHexDirectionAngle(int hexIndex)
    {
        // Hex directions for flat-top grid (prefabs point RIGHT by default):
        // PlayerController.HEX_DIR_Q/R indices in flat-top mode:
        // 0=(1,0)=TopRight, 1=(1,-1)=BottomRight, 2=(0,-1)=Bottom, 3=(-1,0)=BottomLeft, 4=(-1,1)=TopLeft, 5=(0,1)=Top
        //
        // World-space angles for flat-top grid:
        // Top=90°, TopRight=30°, BottomRight=-30°, Bottom=-90°, BottomLeft=-150°(+X180), TopLeft=-210°(+X180)
        switch (hexIndex)
        {
            case 0: return 30f;      // (1,0) TopRight → 30°
            case 1: return -30f;     // (1,-1) BottomRight → -30°
            case 2: return -90f;     // (0,-1) Bottom → -90°
            case 3: return -150f;    // (-1,0) BottomLeft → -150° with X-flip
            case 4: return -210f;    // (-1,1) TopLeft → -210° with X-flip
            case 5: return 90f;      // (0,1) Top → 90°
            default: return -90f;    // Default to bottom direction instead of top
        }
    }

    // Convert aim angle to direction vector
    private Vector2 GetAimDirectionVector()
    {
        float rad = currentAimAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void ApplyGunRotation()
    {
        if (gunTransform == null) return;

        if (isGunRotationFrozen)
        {
            if (Time.time >= gunUnfreezeTime)
            {
                isGunRotationFrozen = false;
            }
            else
            {
                return;
            }
        }

        float zAngle = currentAimAngle;
        float xRotation = 0f;
        float yRotation = 0f;

        // Determine flip based on mode
        if (isInStandoffMode)
        {
            // Standoff: flip on Y-axis when aiming left and inverse Z rotation
            if (zAngle > 90f || zAngle < -90f)
            {
                xRotation = 180f;
                zAngle = -zAngle;
            }
        }
        else
        {
            // Chess: flip on X-axis for bottom-left (-150°) and top-left (-210°) and inverse Z rotation
            if (zAngle == -150f || zAngle == -210f)
            {
                xRotation = 180f;
                zAngle = -zAngle;
            }
        }

        gunTransform.rotation = Quaternion.Euler(xRotation, yRotation, zAngle);

        if (showDebug)
        {
            Debug.Log($"[WeaponSystem] Gun rotation: Z={zAngle:F1}°, X={xRotation}°, Y={yRotation}°");
        }
    }

    // Find the best aligned hex direction using line-of-sight scoring
    private int GetBestAlignedHexDirection()
    {
        if (pawnController == null || pawnController.gridGenerator == null)
        {
            if (showDebug) Debug.LogWarning("[WeaponSystem] No PawnController or grid generator - defaulting to bottom direction");
            return 2; // Default to bottom direction (index 2 = -90°)
        }

        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        if (playerController == null)
        {
            if (showDebug) Debug.LogWarning("[WeaponSystem] No PlayerController found - defaulting to bottom direction");
            return 2; // Default to bottom direction (index 2 = -90°)
        }

        int pawnQ = pawnController.q;
        int pawnR = pawnController.r;
        List<Vector2Int> playerArea = playerController.GetPlayerArea();

        int bestIndex = 2; // Default to bottom direction instead of 0 (TopRight)
        int bestScore = -1;

        // Calculate scores for all directions
        for (int dirIndex = 0; dirIndex < 6; dirIndex++)
        {
            int score = 0;
            int currentQ = pawnQ;
            int currentR = pawnR;

            for (int step = 1; step <= 999; step++)
            {
                currentQ += PlayerController.HEX_DIR_Q[dirIndex];
                currentR += PlayerController.HEX_DIR_R[dirIndex];
                Vector2Int currentTile = new Vector2Int(currentQ, currentR);

                if (playerArea.Contains(currentTile))
                {
                    score += (1000 - step);
                }
            }

            if (showDebug)
            {
                Debug.Log($"[WeaponSystem] Direction {dirIndex}: Score = {score}");
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = dirIndex;
            }
        }

        // If no direction has a positive score (no line of sight to player), default to bottom
        if (bestScore <= 0)
        {
            if (showDebug) Debug.Log("[WeaponSystem] No clear line of sight to player - defaulting to bottom direction");
            bestIndex = 2; // Bottom direction
        }

        // Debug mode: Calculate direction twice to help identify issues
        if (showDebug)
        {
            Debug.Log($"[WeaponSystem] First calculation - Best hex direction: {bestIndex} with score {bestScore}");
            
            // Second calculation for debug verification
            int secondBestIndex = 2; // Default to bottom for second calculation too
            int secondBestScore = -1;
            
            for (int dirIndex = 0; dirIndex < 6; dirIndex++)
            {
                int score = 0;
                int currentQ = pawnQ;
                int currentR = pawnR;

                for (int step = 1; step <= 999; step++)
                {
                    currentQ += PlayerController.HEX_DIR_Q[dirIndex];
                    currentR += PlayerController.HEX_DIR_R[dirIndex];
                    Vector2Int currentTile = new Vector2Int(currentQ, currentR);

                    if (playerArea.Contains(currentTile))
                    {
                        score += (1000 - step);
                    }
                }

                if (score > secondBestScore)
                {
                    secondBestScore = score;
                    secondBestIndex = dirIndex;
                }
            }
            
            // Apply same fallback logic for second calculation
            if (secondBestScore <= 0)
            {
                secondBestIndex = 2; // Bottom direction
            }
            
            Debug.Log($"[WeaponSystem] Second calculation - Best hex direction: {secondBestIndex} with score {secondBestScore}");
            
            if (bestIndex != secondBestIndex)
            {
                Debug.LogWarning($"[WeaponSystem] Direction calculation mismatch! First: {bestIndex}, Second: {secondBestIndex}");
            }
        }

        if (showDebug)
        {
            Debug.Log($"[WeaponSystem] Final result - Best hex direction: {bestIndex} with score {bestScore}");
        }

        return bestIndex;
    }

    #endregion

    #region Private Methods - Firing

    private void HandleFireModes()
    {
        // Don't handle fire modes when game is paused
        if (IsGamePaused()) return;
        
        switch (fireMode)
        {
            case FireMode.Timed:
                if (Time.time >= lastFireTime + fireInterval)
                {
                    Fire();
                }
                break;

            case FireMode.OnLineOfSight:
                if (Time.time >= lastFireTime + fireInterval && HasLineOfSight())
                {
                    Fire();
                }
                break;

            case FireMode.TrackPlayer:
                if (Time.time >= lastFireTime + fireInterval)
                {
                    Fire();
                }
                break;
        }
    }

    private bool HasLineOfSight()
    {
        if (playerTransform == null) return false;

        Vector2 toPlayer = playerTransform.position - transform.position;
        float distance = toPlayer.magnitude;

        if (distance > detectionRange) return false;

        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            toPlayer.normalized,
            distance,
            obstacleLayer
        );

        if (showDebug)
        {
            Debug.DrawRay(transform.position, toPlayer, hit.collider != null ? Color.red : Color.green);
        }

        return hit.collider == null;
    }

    private void Fire()
    {
        // Don't fire when game is paused
        if (IsGamePaused()) return;
        
        lastFireTime = Time.time;
        lastShotTime = Time.time;

        isGunRotationFrozen = true;
        gunUnfreezeTime = Time.time + GUN_ANIMATION_DURATION;

        if (fireSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(fireSound, fireVolume);
        }

        if (gunAnimator != null && !string.IsNullOrEmpty(fireAnimationTrigger))
        {
            gunAnimator.SetTrigger(fireAnimationTrigger);
        }

        // Spawn muzzle flash
        if (muzzleFlashPrefab != null && firePoint != null)
        {
            float zAngle = currentAimAngle + fireOffset;
            float xRotation = (currentAimAngle > 90f && currentAimAngle < 270f) ? 180f : 0f;
            Quaternion flashRotation = Quaternion.Euler(xRotation, 0f, zAngle);
            GameObject flash = Instantiate(muzzleFlashPrefab, firePoint.position, flashRotation);
            Destroy(flash, muzzleFlashDuration);
        }

        // Apply recoil in Standoff mode
        if (isInStandoffMode && enableRecoil && rigidBody != null)
        {
            ApplyRecoil();
        }

        // Fire based on AI type
        if (pawnController != null)
        {
            switch (pawnController.aiType)
            {
                case PawnController.AIType.Basic:
                    SpawnProjectile(0f, damage: 1);
                    break;

                case PawnController.AIType.Handcannon:
                    SpawnProjectile(0f, damage: 1);
                    break;

                case PawnController.AIType.Shotgun:
                    // Shotgun: 3 bullets at 0°, +60°, -60° in BOTH stages
                    SpawnProjectile(0f, damage: 1);
                    SpawnProjectile(65f, damage: 1);
                    SpawnProjectile(-65f, damage: 1);
                    break;

                case PawnController.AIType.Sniper:
                    SpawnProjectile(0f, damage: 2, piercing: true);
                    break;
            }
        }
        else
        {
            // Fallback
            switch (projectileType)
            {
                case ProjectileType.Single:
                    SpawnProjectile(0f);
                    break;

                case ProjectileType.Spread:
                    FireSpread();
                    break;

                case ProjectileType.Beam:
                    FireBeam();
                    break;
            }
        }
    }

    private float GetAdjustedFireInterval()
    {
        if (pawnController == null) return fireInterval;
        return fireInterval * pawnController.GetFireIntervalMultiplier();
    }

    private float GetAdjustedFiringDelay()
    {
        if (pawnController == null) return firingDelay;
        return firingDelay * pawnController.GetFiringDelayMultiplier();
    }

    private void ApplyRecoil()
    {
        if (rigidBody == null) return;
        Vector2 aimDir = GetAimDirectionVector();
        Vector2 recoilDirection = -aimDir.normalized;
        rigidBody.AddForce(recoilDirection * recoilForce, ForceMode2D.Impulse);
    }

    private void FireSpread()
    {
        float startAngle = -spreadAngle * (spreadCount - 1) / 2f;

        for (int i = 0; i < spreadCount; i++)
        {
            float angle = startAngle + (spreadAngle * i);
            SpawnProjectile(angle);
        }
    }

    private void FireBeam()
    {
        Vector2 aimDir = GetAimDirectionVector();
        RaycastHit2D hit = Physics2D.Raycast(
            firePoint.position,
            aimDir,
            maxRange,
            targetLayer | obstacleLayer
        );

        float beamDistance = hit.collider != null ? hit.distance : maxRange;

        if (hit.collider != null)
        {
            PawnHealth playerHealth = hit.collider.GetComponent<PawnHealth>();
            if (playerHealth != null && playerHealth.pawnType == PawnHealth.PawnType.Player)
            {
                playerHealth.TakeDamage(damage, gameObject.name);
            }
        }

        if (projectilePrefab != null)
        {
            GameObject beam = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
            beam.transform.right = aimDir;
            beam.transform.localScale = new Vector3(beamDistance, 1f, 1f);
            Destroy(beam, 0.2f);
        }
    }

    // Spawn projectile with angle offset relative to current aim angle
    private void SpawnProjectile(float angleOffset, int damage = -1, bool piercing = false)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Projectile prefab not assigned!");
            return;
        }

        if (firePoint == null)
        {
            Debug.LogWarning("FirePoint not assigned!");
            return;
        }

        // Calculate final angle: current aim angle + offset + fire offset
        float finalAngle = currentAimAngle + angleOffset + fireOffset;

        // Calculate bullet direction vector using final angle
        float dirRad = finalAngle * Mathf.Deg2Rad;
        Vector2 bulletDirection = new Vector2(Mathf.Cos(dirRad), Mathf.Sin(dirRad));

        // Determine X-axis flip based on final angle
        float xRotation = 0f;
        if (isInStandoffMode)
        {
            // Standoff: flip when aiming left (angle > 90 or < -90)
            if (finalAngle > 90f || finalAngle < -90f)
            {
                xRotation = 180f;
            }
        }
        else
        {
            // Chess: flip for bottom-left (-150°) and top-left (-210°)
            // Use base angle to determine flip, not the offset angle
            if (currentAimAngle == -150f || currentAimAngle == -210f)
            {
                xRotation = 180f;
            }
        }

        // Create projectile with correct rotation
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        projectile.transform.rotation = Quaternion.Euler(xRotation, 0f, finalAngle);

        ProjectileBehavior proj = projectile.GetComponent<ProjectileBehavior>();
        if (proj == null)
        {
            proj = projectile.AddComponent<ProjectileBehavior>();
        }

        int finalDamage = damage < 0 ? this.damage : damage;
        bool onlyDamagePlayer = pawnController != null && pawnController.BulletsOnlyDamagePlayer();

        proj.Initialize(bulletDirection, projectileSpeed, maxRange, finalDamage, gameObject, piercing, onlyDamagePlayer);

        if (showDebug)
        {
            Debug.Log($"[WeaponSystem] Spawned projectile: angle={finalAngle:F1}°, offset={angleOffset}°, X-flip={xRotation}°, dir={bulletDirection}");
        }
    }

    #endregion

    #region Targeting Visualization

    private void InitializeTargetingVisualization()
    {
        Debug.Log($"[WeaponSystem] Initializing targeting visualization for {gameObject.name} at position {transform.position}");
        
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.material = lineMaterial != null ? lineMaterial : CreateDefaultLineMaterial();
        lineRenderer.startColor = standoffLineColor;
        lineRenderer.endColor = standoffLineColor;
        lineRenderer.enabled = false;
        
        // Set sorting layer and order to ensure visibility
        lineRenderer.sortingLayerName = "Default";
        lineRenderer.sortingOrder = 100; // High sorting order to render on top
        
        // Use world space and set to render closer to camera
        lineRenderer.useWorldSpace = true;
        
        // Find main camera to position line closer
        Camera mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindFirstObjectByType<Camera>();
        
        if (mainCamera != null)
        {
            // Position the line slightly closer to the camera than other objects
            float cameraZ = mainCamera.transform.position.z;
            float lineZ = cameraZ + 1f; // Move 1 unit closer to camera
            
            // Note: We don't set lineRenderer.transform.position here because that would move the entire pawn!
            // Instead, we'll use the lineZ value when setting line positions in UpdateStandoffVisualization
        }
        
        Debug.Log($"[WeaponSystem] Targeting visualization initialized for {gameObject.name}, pawn position remains at {transform.position}");
    }

    private Material CreateDefaultLineMaterial()
    {
        // Try to find a suitable shader for line rendering
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = standoffLineColor;
        
        // Set rendering properties to ensure visibility
        mat.SetInt("_ZWrite", 0); // Disable depth writing
        mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always); // Always render on top
        
        // Enable transparency if the color has alpha
        if (standoffLineColor.a < 1f)
        {
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        
        return mat;
    }

    private void UpdateTargetingVisualization()
    {
        if (isInStandoffMode)
        {
            UpdateStandoffVisualization();
        }
        else
        {
            UpdateChessVisualization();
        }
    }

    private void UpdateChessVisualization()
    {
        if (pawnController == null || gridGenerator == null || playerTransform == null)
        {
            return;
        }

        List<GameObject> newTargetedTiles = GetTilesInFiringDirection();

        if (!TileListsMatch(targetedTiles, newTargetedTiles))
        {
            ClearChessVisualization();
            targetedTiles = newTargetedTiles;

            if (targetedTiles.Count > 0)
            {
                if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
                blinkCoroutine = StartCoroutine(BlinkTilesCoroutine());
            }
        }
    }

    private List<GameObject> GetTilesInFiringDirection()
    {
        List<GameObject> tiles = new List<GameObject>();

        if (pawnController == null || gridGenerator == null) return tiles;

        int pawnQ = pawnController.q;
        int pawnR = pawnController.r;
        int hexIndex = GetBestAlignedHexDirection();

        if (hexIndex < 0) return tiles;

        bool isShotgun = pawnController.aiType == PawnController.AIType.Shotgun;

        if (isShotgun)
        {
            // Shotgun: 3 directions
            int[] directions = { hexIndex, (hexIndex + 1) % 6, (hexIndex + 5) % 6 };

            // First, add the pawn's own tile (starting point)
            GameObject pawnTile = FindTileAtCoordinates(pawnQ, pawnR);
            if (pawnTile != null && !tiles.Contains(pawnTile))
            {
                tiles.Add(pawnTile);
            }

            foreach (int dirIndex in directions)
            {
                int currentQ = pawnQ;
                int currentR = pawnR;

                for (int step = 1; step <= maxTileRange; step++)
                {
                    currentQ += PlayerController.HEX_DIR_Q[dirIndex];
                    currentR += PlayerController.HEX_DIR_R[dirIndex];

                    GameObject tile = FindTileAtCoordinates(currentQ, currentR);
                    if (tile != null && !tiles.Contains(tile))
                    {
                        tiles.Add(tile);
                    }
                }
            }

            if (showDebug)
            {
                Debug.Log($"[WeaponSystem] Shotgun targeting {tiles.Count} tiles in 3 directions (including pawn tile)");
            }
        }
        else
        {
            // Single direction - include pawn's tile as starting point
            int currentQ = pawnQ;
            int currentR = pawnR;

            // Add the pawn's own tile first (step 0)
            GameObject pawnTile = FindTileAtCoordinates(currentQ, currentR);
            if (pawnTile != null)
            {
                tiles.Add(pawnTile);
            }

            // Then add tiles in the firing direction
            for (int step = 1; step <= maxTileRange; step++)
            {
                currentQ += PlayerController.HEX_DIR_Q[hexIndex];
                currentR += PlayerController.HEX_DIR_R[hexIndex];

                GameObject tile = FindTileAtCoordinates(currentQ, currentR);
                if (tile != null)
                {
                    tiles.Add(tile);
                }
            }

            if (showDebug)
            {
                Debug.Log($"[WeaponSystem] Targeting {tiles.Count} tiles in direction {hexIndex} (including pawn tile)");
            }
        }

        return tiles;
    }

    private GameObject FindTileAtCoordinates(int q, int r)
    {
        if (gridGenerator == null || gridGenerator.parentContainer == null) return null;

        string targetName = $"Hex_{q}_{r}";

        foreach (Transform child in gridGenerator.parentContainer)
        {
            if (child.name == targetName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private IEnumerator BlinkTilesCoroutine()
    {
        foreach (GameObject tile in targetedTiles)
        {
            SpriteRenderer spriteRenderer = tile.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null && !originalTileColors.ContainsKey(spriteRenderer))
            {
                originalTileColors[spriteRenderer] = spriteRenderer.color;
            }
        }

        bool isBlinkOn = false;
        int blinkCounter = 0;

        while (true)
        {
            isBlinkOn = !isBlinkOn;

            if (!isBlinkOn)
            {
                blinkCounter++;

                if (maxBlinkCount > 0 && blinkCounter >= maxBlinkCount)
                {
                    foreach (var kvp in originalTileColors)
                    {
                        if (kvp.Key != null)
                        {
                            kvp.Key.color = kvp.Value;
                        }
                    }
                    yield break;
                }
            }

            foreach (GameObject tile in targetedTiles)
            {
                SpriteRenderer spriteRenderer = tile.GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    if (isBlinkOn)
                    {
                        spriteRenderer.color = chessTileBlinkColor;
                    }
                    else
                    {
                        if (originalTileColors.TryGetValue(spriteRenderer, out Color originalColor))
                        {
                            spriteRenderer.color = originalColor;
                        }
                    }
                }
            }

            yield return new WaitForSeconds(blinkInterval);
        }
    }

    private void ClearChessVisualization()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        foreach (var kvp in originalTileColors)
        {
            if (kvp.Key != null)
            {
                kvp.Key.color = kvp.Value;
            }
        }

        originalTileColors.Clear();
        targetedTiles.Clear();
    }

    private bool TileListsMatch(List<GameObject> list1, List<GameObject> list2)
    {
        if (list1.Count != list2.Count) return false;

        for (int i = 0; i < list1.Count; i++)
        {
            if (list1[i] != list2[i]) return false;
        }

        return true;
    }

    private void UpdateStandoffVisualization()
    {
        if (lineRenderer == null || !lineRenderer.enabled) 
        {
            // If LineRenderer is not available, use Debug.DrawLine as fallback
            if (useDebugLineFallback)
            {
                DrawDebugTargetingLine();
            }
            return;
        }

        // Get camera reference for proper Z positioning
        Camera mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindFirstObjectByType<Camera>();
        
        float lineZ = 0f; // Default Z position
        if (mainCamera != null)
        {
            // Position line closer to camera than other objects
            lineZ = mainCamera.transform.position.z + 1f;
        }

        Vector3 startPos = new Vector3(transform.position.x, transform.position.y, lineZ);
        Vector2 aimDir = GetAimDirectionVector();
        Vector3 endPos = startPos + new Vector3(aimDir.x * maxRange, aimDir.y * maxRange, 0f);

        // Perform raycast to find actual end point (obstacles, walls, etc.)
        RaycastHit2D hit = Physics2D.Raycast(transform.position, aimDir, maxRange);
        if (hit.collider != null)
        {
            endPos = new Vector3(hit.point.x, hit.point.y, lineZ);
        }

        // Set line positions
        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, endPos);
        
        // Ensure line renderer properties are maintained
        lineRenderer.startColor = standoffLineColor;
        lineRenderer.endColor = standoffLineColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        
        // Debug visualization (can be removed in production)
        if (showDebug)
        {
            Debug.DrawLine(startPos, endPos, standoffLineColor, 0.1f);
        }
        
        // Additional fallback debug line if enabled
        if (useDebugLineFallback)
        {
            DrawDebugTargetingLine();
        }
    }

    /// Draw targeting line using Debug.DrawLine as a fallback visualization method
    private void DrawDebugTargetingLine()
    {
        Vector3 startPos = transform.position;
        Vector2 aimDir = GetAimDirectionVector();
        Vector3 endPos = startPos + new Vector3(aimDir.x * maxRange, aimDir.y * maxRange, 0f);

        // Perform raycast to find actual end point
        RaycastHit2D hit = Physics2D.Raycast(startPos, aimDir, maxRange);
        if (hit.collider != null)
        {
            endPos = new Vector3(hit.point.x, hit.point.y, startPos.z);
        }

        // Draw the line (visible in Scene view and with Gizmos enabled)
        Debug.DrawLine(startPos, endPos, standoffLineColor, 0.1f);
        
        if (showDebug)
        {
            Debug.Log($"[WeaponSystem] Debug targeting line: {startPos} -> {endPos}");
        }
    }

    #endregion
}

/// Projectile behavior component (nested for consolidation).
/// Handles bullet movement, damage, piercing, and collision detection.
[RequireComponent(typeof(Rigidbody2D))]
public class ProjectileBehavior : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private float maxRange;
    private int damage;
    private GameObject sourceObject;
    private Vector2 startPosition;
    private Rigidbody2D rigidBody;
    private bool isInitialized = false;
    private bool isPiercing = false;
    private bool onlyDamagePlayer = false;
    private int pierceCount = 0;
    private int maxPierceCount = 1;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody2D>();
        if (rigidBody != null)
        {
            rigidBody.gravityScale = 0f;
        }
    }

    private void Start()
    {
        if (sourceObject != null)
        {
            Collider2D projectileCollider = GetComponent<Collider2D>();
            Collider2D[] sourceColliders = sourceObject.GetComponents<Collider2D>();

            if (projectileCollider != null)
            {
                foreach (Collider2D sourceCollider in sourceColliders)
                {
                    if (sourceCollider != null)
                    {
                        Physics2D.IgnoreCollision(projectileCollider, sourceCollider, true);
                    }
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isInitialized) return;

        rigidBody.linearVelocity = direction * speed;

        float traveledDistance = Vector2.Distance(startPosition, transform.position);
        if (traveledDistance >= maxRange)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (sourceObject != null && collision.gameObject == sourceObject)
        {
            return;
        }

        PawnHealth pawnHealth = collision.GetComponent<PawnHealth>();

        if (pawnHealth != null)
        {
            if (onlyDamagePlayer)
            {
                if (pawnHealth.pawnType == PawnHealth.PawnType.Player)
                {
                    string sourceName = sourceObject != null ? sourceObject.name : "Unknown";
                    pawnHealth.TakeDamage(damage, sourceName);
                }
                else
                {
                    return;
                }
            }
            else
            {
                string sourceName = sourceObject != null ? sourceObject.name : "Unknown";
                pawnHealth.TakeDamage(damage, sourceName);
            }

            if (isPiercing && pierceCount < maxPierceCount)
            {
                pierceCount++;
                damage = 1;
                return;
            }

            Destroy(gameObject);
            return;
        }

        if (collision.CompareTag("Tile") || collision.CompareTag("Wall") || collision.CompareTag("Obstacle"))
        {
            Destroy(gameObject);
        }
    }

    public void Initialize(Vector2 moveDirection, float moveSpeed, float range, int damageAmount, GameObject source, bool piercing = false, bool playerOnly = false)
    {
        direction = moveDirection.normalized;
        speed = moveSpeed;
        maxRange = range;
        damage = damageAmount;
        sourceObject = source;
        startPosition = transform.position;
        isPiercing = piercing;
        onlyDamagePlayer = playerOnly;
        isInitialized = true;
    }
}