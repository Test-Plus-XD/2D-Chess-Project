using UnityEngine;
using System.Collections;

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

    #region Inspector Fields - Firearm Settings

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

    #endregion

    #region Inspector Fields - Spread Settings

    [Header("Spread Settings")]
    [Tooltip("Number of bullets in spread shot")]
    [SerializeField] private int spreadCount = 3;

    [Tooltip("Angle between spread bullets in degrees")]
    [SerializeField] private float spreadAngle = 30f;

    #endregion

    #region Inspector Fields - Line of Sight

    [Header("Line of Sight Settings")]
    [Tooltip("Detection range for line of sight mode")]
    [SerializeField] private float detectionRange = 10f;

    [Tooltip("Layer mask for detecting targets")]
    [SerializeField] private LayerMask targetLayer;

    [Tooltip("Layer mask for obstacles blocking line of sight")]
    [SerializeField] private LayerMask obstacleLayer;

    #endregion

    #region Inspector Fields - Visuals

    [Header("Projectile Visuals")]
    [Tooltip("Projectile prefab to spawn")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Muzzle flash effect (optional)")]
    [SerializeField] private GameObject muzzleFlashPrefab;

    [Tooltip("Point where projectiles spawn")]
    [SerializeField] private Transform firePoint;

    #endregion

    #region Inspector Fields - Gun Aiming

    [Header("Gun Aiming")]
    [Tooltip("The gun transform to rotate")]
    [SerializeField] private Transform gunTransform;

    [Tooltip("Gun offset from pawn center")]
    [SerializeField] private Vector2 gunOffset = new Vector2(0.2f, 0f);

    #endregion

    #region Inspector Fields - Audio & Animation

    [Header("Audio & Animation")]
    [Tooltip("Sound played when firing")]
    [SerializeField] private AudioClip fireSound;

    [Tooltip("Volume of fire sound")]
    [SerializeField][Range(0f, 1f)] private float fireVolume = 0.7f;

    [Tooltip("Animator for gun animations")]
    [SerializeField] private Animator gunAnimator;

    [Tooltip("Name of fire animation trigger")]
    [SerializeField] private string fireAnimationTrigger = "Fire";

    #endregion

    #region Inspector Fields - Recoil

    [Header("Recoil (Standoff Mode)")]
    [Tooltip("Enable physical recoil when shooting")]
    [SerializeField] private bool enableRecoil = true;

    [Tooltip("Recoil force magnitude")]
    [SerializeField] private float recoilForce = 3f;

    #endregion

    #region Inspector Fields - Debug

    [Header("Debug")]
    [Tooltip("Show debug lines")]
    [SerializeField] private bool showDebug = false;

    #endregion

    #region Private Fields

    private float lastFireTime;
    private AudioSource audioSource;
    private Transform playerTransform;
    private bool isInStandoffMode = false;
    private Vector2 currentAimDirection = Vector2.right;
    private Vector2 targetAimDirection = Vector2.right;
    private float lastShotTime = -999f;
    private Rigidbody2D rigidBody;
    private PawnController pawnController;
    private Vector2[] hexDirections = new Vector2[6];
    private PawnController.Modifier currentModifier = PawnController.Modifier.None;

    // Standoff firing state
    private float intervalStartTime = 0f;
    private bool isInFiringDelay = false;
    private float firingDelayStartTime = 0f;
    private Vector2 lockedAimDirection = Vector2.right;

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

        InitializeHexDirections();

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
    }

    private void Start()
    {
        FindPlayer();
        lastFireTime = -fireInterval;
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        UpdateAimDirection();
        ApplyGunRotation();
        HandleFireModes();
    }

    private void OnDrawGizmos()
    {
        if (!showDebug || firePoint == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(firePoint.position, currentAimDirection * maxRange);

        if (fireMode == FireMode.OnLineOfSight)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
    }

    #endregion

    #region Public Methods

    public void ManualFire()
    {
        if (Time.time >= lastFireTime + GetAdjustedFireInterval())
        {
            Fire();
        }
    }

    public void SetAimDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
        {
            currentAimDirection = direction.normalized;
        }
    }

    public void SetStandoffMode(bool standoffMode)
    {
        isInStandoffMode = standoffMode;

        if (standoffMode)
        {
            // Initialize standoff firing state
            intervalStartTime = Time.time;
            isInFiringDelay = false;
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
        return currentAimDirection;
    }

    public void OnShotFired()
    {
        lastShotTime = Time.time;
    }

    /// Apply modifier effects to weapon system
    public void ApplyModifier(PawnController.Modifier modifier)
    {
        currentModifier = modifier;
    }

    /// Fire once in Chess mode (called when turn starts)
    public void FireChessMode()
    {
        if (pawnController == null) return;

        // Basic pawns don't shoot in chess mode
        if (pawnController.aiType == PawnController.AIType.Basic) return;

        Fire();
    }

    /// Recalculate aim direction (for Reflexive modifier after player moves)
    public void RecalculateAim()
    {
        if (playerTransform == null) return;
        Vector2 toPlayer = (playerTransform.position - transform.position).normalized;
        currentAimDirection = GetNearestHexDirection(toPlayer);
    }

    #endregion

    #region Private Methods - Initialization

    private void InitializeHexDirections()
    {
        float angle = 0f;
        for (int i = 0; i < 6; i++)
        {
            float rad = angle * Mathf.Deg2Rad;
            hexDirections[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            angle += 60f;
        }
    }

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
        if (playerTransform == null) return;

        Vector2 toPlayer = (playerTransform.position - transform.position).normalized;

        if (isInStandoffMode)
        {
            UpdateStandoffAiming(toPlayer);
        }
        else
        {
            UpdateChessAiming(toPlayer);
        }
    }

    private void UpdateChessAiming(Vector2 toPlayer)
    {
        targetAimDirection = GetNearestHexDirection(toPlayer);
        currentAimDirection = targetAimDirection;
    }

    private void UpdateStandoffAiming(Vector2 toPlayer)
    {
        // Get fire interval and delay with modifier adjustments (e.g., Confrontational -25%, Observant -50%)
        float adjustedInterval = GetAdjustedFireInterval();
        float adjustedDelay = GetAdjustedFiringDelay();
        float timeSinceIntervalStart = Time.time - intervalStartTime;

        // Check if we should enter firing delay phase (after fire interval has elapsed)
        if (!isInFiringDelay && timeSinceIntervalStart >= adjustedInterval)
        {
            // Lock current aim direction to prevent tracking during delay
            isInFiringDelay = true;
            firingDelayStartTime = Time.time;
            lockedAimDirection = currentAimDirection;

            if (showDebug)
            {
                Debug.Log($"[WeaponSystem] Entering firing delay. Locked aim at {lockedAimDirection}");
            }
        }

        // If in firing delay phase (stop tracking, prepare to fire)
        if (isInFiringDelay)
        {
            float timeSinceDelayStart = Time.time - firingDelayStartTime;

            // Hold position and aim frozen during delay period
            currentAimDirection = lockedAimDirection; // FROZEN AIM

            // Fire after delay completes
            if (timeSinceDelayStart >= adjustedDelay)
            {
                Fire(); // SHOOT!

                // Reset to tracking phase for next cycle
                isInFiringDelay = false;
                intervalStartTime = Time.time; // RESTART INTERVAL

                if (showDebug)
                {
                    Debug.Log($"[WeaponSystem] Fired! Restarting interval.");
                }
            }
        }
        else
        {
            // Tracking phase: Follow player with angular velocity until interval expires
            targetAimDirection = toPlayer;

            // Reflexive modifier: Gun fixed on player (instant tracking, no lerp)
            if (pawnController != null && pawnController.ShouldFixGunOnPlayer())
            {
                // Lock instantly to target (matches "Reflexive" modifier instant tracking)
                currentAimDirection = targetAimDirection;
            }
            else
            {
                // Normal tracking with angular velocity (allows for slow gun turning)
                // Calculate maximum rotation allowed this frame based on angular velocity
                float maxRotationThisFrame = trackingAngularVelocity * Time.deltaTime;
                // Convert current and target directions to angles for proper interpolation
                float currentAngle = Mathf.Atan2(currentAimDirection.y, currentAimDirection.x) * Mathf.Rad2Deg;
                float targetAngle = Mathf.Atan2(targetAimDirection.y, targetAimDirection.x) * Mathf.Rad2Deg;
                // Use DeltaAngle to get shortest rotation path (handles 359→1 degree wrap)
                float angleDiff = Mathf.DeltaAngle(currentAngle, targetAngle);
                // Clamp rotation to max allowed this frame
                float rotationAmount = Mathf.Clamp(angleDiff, -maxRotationThisFrame, maxRotationThisFrame);
                // Apply rotation and convert back to direction vector
                float newAngle = currentAngle + rotationAmount;
                currentAimDirection = new Vector2(Mathf.Cos(newAngle * Mathf.Deg2Rad), Mathf.Sin(newAngle * Mathf.Deg2Rad));
            }
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

    private void ApplyGunRotation()
    {
        if (gunTransform == null) return;

        float angle = Mathf.Atan2(currentAimDirection.y, currentAimDirection.x) * Mathf.Rad2Deg;
        gunTransform.rotation = Quaternion.Euler(0f, 0f, angle);

        Vector3 scale = gunTransform.localScale;
        if (currentAimDirection.x < 0)
        {
            scale.y = -Mathf.Abs(scale.y);
        }
        else
        {
            scale.y = Mathf.Abs(scale.y);
        }
        gunTransform.localScale = scale;
    }

    #endregion

    #region Private Methods - Firing

    private void HandleFireModes()
    {
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
        // Record fire time for cooldown tracking
        lastFireTime = Time.time;
        lastShotTime = Time.time;

        // Play firing sound effect
        if (fireSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(fireSound, fireVolume);
        }

        // Trigger gun animation (e.g., recoil animation)
        if (gunAnimator != null && !string.IsNullOrEmpty(fireAnimationTrigger))
        {
            gunAnimator.SetTrigger(fireAnimationTrigger);
        }

        // Spawn muzzle flash visual effect (auto-destroy after 0.1s)
        if (muzzleFlashPrefab != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, firePoint.position, Quaternion.identity);
            Destroy(flash, 0.1f);
        }

        // Apply physical recoil in Standoff mode (pushes pawn backward)
        if (isInStandoffMode && enableRecoil && rigidBody != null)
        {
            ApplyRecoil();
        }

        // Fire based on AI type (each type has unique firing pattern)
        if (pawnController != null)
        {
            switch (pawnController.aiType)
            {
                case PawnController.AIType.Basic:
                    // Basic doesn't shoot normally (but spawns projectile if converted to Handcannon)
                    SpawnProjectile(currentAimDirection);
                    break;

                case PawnController.AIType.Handcannon:
                    // Handcannon: Single bullet, 1 damage (mid-range specialist)
                    SpawnProjectile(currentAimDirection, damage: 1);
                    break;

                case PawnController.AIType.Shotgun:
                    // Shotgun: 3 bullets in spread pattern (0°, +60°, -60°), 1 damage each
                    SpawnProjectile(currentAimDirection, damage: 1);
                    SpawnProjectile(RotateVector(currentAimDirection, 60f), damage: 1);
                    SpawnProjectile(RotateVector(currentAimDirection, -60f), damage: 1);
                    break;

                case PawnController.AIType.Sniper:
                    // Sniper: Single bullet, 2 damage, pierces once for additional 1 damage (high damage, piercing)
                    SpawnProjectile(currentAimDirection, damage: 2, piercing: true);
                    break;
            }
        }
        else
        {
            // Fallback: Fire based on projectile type if no pawn controller found
            switch (projectileType)
            {
                case ProjectileType.Single:
                    SpawnProjectile(currentAimDirection);
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

    /// Get adjusted fire interval based on modifier applied to the pawn
    private float GetAdjustedFireInterval()
    {
        if (pawnController == null) return fireInterval;
        // Apply modifier multiplier (e.g., Confrontational = 0.75x => 25% faster firing)
        return fireInterval * pawnController.GetFireIntervalMultiplier();
        // Example: Confrontational modifier = 0.75x (25% reduced interval = fires more frequently)
    }

    /// Get adjusted firing delay based on modifier applied to the pawn
    private float GetAdjustedFiringDelay()
    {
        if (pawnController == null) return firingDelay;
        // Apply modifier multiplier (e.g., Observant = 0.5x => 50% faster, Reflexive = 0.75x => 25% faster)
        return firingDelay * pawnController.GetFiringDelayMultiplier();
        // Example: Observant modifier = 0.5x (0.5s delay → 0.25s delay, fires sooner)
    }

    private void ApplyRecoil()
    {
        if (rigidBody == null) return;
        Vector2 recoilDirection = -currentAimDirection.normalized;
        rigidBody.AddForce(recoilDirection * recoilForce, ForceMode2D.Impulse);
    }

    private void FireSpread()
    {
        float startAngle = -spreadAngle * (spreadCount - 1) / 2f;

        for (int i = 0; i < spreadCount; i++)
        {
            float angle = startAngle + (spreadAngle * i);
            Vector2 direction = RotateVector(currentAimDirection, angle);
            SpawnProjectile(direction);
        }
    }

    private void FireBeam()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            firePoint.position,
            currentAimDirection,
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
            beam.transform.right = currentAimDirection;
            beam.transform.localScale = new Vector3(beamDistance, 1f, 1f);
            Destroy(beam, 0.2f);
        }
    }

    private void SpawnProjectile(Vector2 direction, int damage = -1, bool piercing = false)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Projectile prefab not assigned!");
            return;
        }

        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        projectile.transform.rotation = Quaternion.Euler(0, 0, angle);

        ProjectileBehavior proj = projectile.GetComponent<ProjectileBehavior>();
        if (proj == null)
        {
            proj = projectile.AddComponent<ProjectileBehavior>();
        }

        int finalDamage = damage < 0 ? this.damage : damage;
        bool onlyDamagePlayer = pawnController != null && pawnController.BulletsOnlyDamagePlayer();

        proj.Initialize(direction, projectileSpeed, maxRange, finalDamage, gameObject.name, piercing, onlyDamagePlayer);
    }

    private Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(
            v.x * cos - v.y * sin,
            v.x * sin + v.y * cos
        );
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
    private string sourceTag;
    private Vector2 startPosition;
    private Rigidbody2D rigidBody;
    private bool isInitialized = false;
    private bool isPiercing = false;
    private bool onlyDamagePlayer = false;
    private int pierceCount = 0;
    private int maxPierceCount = 1; // Sniper bullets pierce once

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody2D>();
        if (rigidBody != null)
        {
            rigidBody.gravityScale = 0f;
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
        PawnHealth pawnHealth = collision.GetComponent<PawnHealth>();

        // Damage pawns
        if (pawnHealth != null)
        {
            // Observant modifier: Only damage player
            if (onlyDamagePlayer)
            {
                if (pawnHealth.pawnType == PawnHealth.PawnType.Player)
                {
                    pawnHealth.TakeDamage(damage, sourceTag);
                }
                else
                {
                    // Don't damage or destroy bullet when hitting opponent
                    return;
                }
            }
            else
            {
                // Normal: Damage both sides
                pawnHealth.TakeDamage(damage, sourceTag);
            }

            // Handle piercing
            if (isPiercing && pierceCount < maxPierceCount)
            {
                pierceCount++;
                // Reduce damage after first pierce (Sniper: 2 damage first hit, 1 damage second)
                damage = 1;
                return; // Don't destroy bullet yet
            }

            // Destroy bullet after hitting (or after max pierces)
            Destroy(gameObject);
            return;
        }

        // Destroy on hitting tiles/walls/obstacles
        if (collision.CompareTag("Tile") || collision.CompareTag("Wall") || collision.CompareTag("Obstacle"))
        {
            Destroy(gameObject);
        }
    }

    public void Initialize(Vector2 moveDirection, float moveSpeed, float range, int damageAmount, string source, bool piercing = false, bool playerOnly = false)
    {
        direction = moveDirection.normalized;
        speed = moveSpeed;
        maxRange = range;
        damage = damageAmount;
        sourceTag = source;
        startPosition = transform.position;
        isPiercing = piercing;
        onlyDamagePlayer = playerOnly;
        isInitialized = true;
    }
}
