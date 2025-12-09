using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles shooting mechanics for opponent pawns in both Chess and Standoff stages.
/// Supports multiple fire modes and modifiers.
/// </summary>
public class Firearm : MonoBehaviour
{
    #region Enums

    public enum FireMode
    {
        Manual,             // Fires on command
        OnLineOfSight,      // Fires when piece enters line of sight
        TrackPlayer,        // Fires in direction closest to player
        Timed               // Fires at regular intervals
    }

    public enum ProjectileType
    {
        Single,             // Single bullet
        Spread,             // Multiple bullets in a cone
        Beam                // Continuous beam
    }

    #endregion

    #region Inspector Fields

    [Header("Firearm Settings")]
    [Tooltip("The fire mode this firearm uses")]
    [SerializeField] private FireMode fireMode = FireMode.TrackPlayer;

    [Tooltip("The type of projectile this firearm shoots")]
    [SerializeField] private ProjectileType projectileType = ProjectileType.Single;

    [Tooltip("Damage dealt per bullet")]
    [SerializeField] private int damage = 1;

    [Tooltip("Time between shots in seconds")]
    [SerializeField] private float fireRate = 1.5f;

    [Tooltip("Maximum range of bullets in world units")]
    [SerializeField] private float maxRange = 15f;

    [Tooltip("Speed of projectiles")]
    [SerializeField] private float projectileSpeed = 10f;

    [Header("Spread Settings (for Spread projectile type)")]
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

    [Tooltip("Point where projectiles spawn")]
    [SerializeField] private Transform firePoint;

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
    [Tooltip("Show debug lines for line of sight")]
    [SerializeField] private bool showDebugLines = true;

    #endregion

    #region Private Fields

    private float lastFireTime;
    private AudioSource audioSource;
    private Transform playerTransform;
    private bool isInStandoffMode = false;
    private Vector2 currentAimDirection = Vector2.right;
    private Rigidbody2D rb;
    private GunAiming gunAiming;

    // Hex direction vectors (for chess mode)
    private Vector2[] hexDirections = new Vector2[6];

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Get components
        rb = GetComponent<Rigidbody2D>();
        gunAiming = GetComponent<GunAiming>();

        // Initialize hex directions
        InitializeHexDirections();

        // Create fire point if not assigned
        if (firePoint == null)
        {
            GameObject firePointObj = new GameObject("FirePoint");
            firePointObj.transform.SetParent(transform);
            firePointObj.transform.localPosition = Vector3.zero;
            firePoint = firePointObj.transform;
        }
    }

    private void Start()
    {
        // Find player
        FindPlayer();

        // Initialize fire timer
        lastFireTime = -fireRate;
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        // Update aim direction based on fire mode
        UpdateAimDirection();

        // Handle fire modes
        switch (fireMode)
        {
            case FireMode.Timed:
                if (Time.time >= lastFireTime + fireRate)
                {
                    Fire();
                }
                break;

            case FireMode.OnLineOfSight:
                if (Time.time >= lastFireTime + fireRate && HasLineOfSight())
                {
                    Fire();
                }
                break;

            case FireMode.TrackPlayer:
                if (Time.time >= lastFireTime + fireRate)
                {
                    Fire();
                }
                break;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugLines || firePoint == null) return;

        // Draw aim direction
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(firePoint.position, currentAimDirection * maxRange);

        // Draw detection range for line of sight mode
        if (fireMode == FireMode.OnLineOfSight)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Manually trigger a fire action
    /// </summary>
    public void ManualFire()
    {
        if (Time.time >= lastFireTime + fireRate)
        {
            Fire();
        }
    }

    /// <summary>
    /// Set the aim direction manually (for Standoff mode)
    /// </summary>
    public void SetAimDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
        {
            currentAimDirection = direction.normalized;
        }
    }

    /// <summary>
    /// Switch between Chess and Standoff modes
    /// </summary>
    public void SetStandoffMode(bool standoffMode)
    {
        isInStandoffMode = standoffMode;
    }

    /// <summary>
    /// Set fire mode at runtime
    /// </summary>
    public void SetFireMode(FireMode mode)
    {
        fireMode = mode;
    }

    /// <summary>
    /// Get time until next shot is ready
    /// </summary>
    public float GetTimeToNextShot()
    {
        float timeSinceLastShot = Time.time - lastFireTime;
        return Mathf.Max(0f, fireRate - timeSinceLastShot);
    }

    #endregion

    #region Private Methods

    private void InitializeHexDirections()
    {
        // Flat-top hex directions (adjust for pointy-top if needed)
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
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void UpdateAimDirection()
    {
        if (playerTransform == null) return;

        Vector2 toPlayer = (playerTransform.position - transform.position).normalized;

        if (isInStandoffMode)
        {
            // In Standoff mode, aim directly at player
            currentAimDirection = toPlayer;
        }
        else
        {
            // In Chess mode, snap to nearest hex direction
            currentAimDirection = GetNearestHexDirection(toPlayer);
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

    private bool HasLineOfSight()
    {
        if (playerTransform == null) return false;

        Vector2 toPlayer = playerTransform.position - transform.position;
        float distance = toPlayer.magnitude;

        // Check if player is in range
        if (distance > detectionRange) return false;

        // Check if line of sight is blocked
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            toPlayer.normalized,
            distance,
            obstacleLayer
        );

        if (showDebugLines)
        {
            Debug.DrawRay(transform.position, toPlayer, hit.collider != null ? Color.red : Color.green);
        }

        return hit.collider == null;
    }

    private void Fire()
    {
        lastFireTime = Time.time;

        // Play sound
        if (fireSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(fireSound, fireVolume);
        }

        // Trigger animation
        if (gunAnimator != null && !string.IsNullOrEmpty(fireAnimationTrigger))
        {
            gunAnimator.SetTrigger(fireAnimationTrigger);
        }

        // Spawn muzzle flash
        if (muzzleFlashPrefab != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, firePoint.position, Quaternion.identity);
            Destroy(flash, 0.1f);
        }

        // Apply recoil in Standoff mode
        if (isInStandoffMode && enableRecoil && rb != null)
        {
            ApplyRecoil();
        }

        // Notify GunAiming that shot was fired
        if (gunAiming != null)
        {
            gunAiming.OnShotFired();
        }

        // Fire projectiles based on type
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

    private void ApplyRecoil()
    {
        if (rb == null) return;

        // Apply force opposite to aim direction
        Vector2 recoilDirection = -currentAimDirection.normalized;
        rb.AddForce(recoilDirection * recoilForce, ForceMode2D.Impulse);
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
        // Raycast to find hit point
        RaycastHit2D hit = Physics2D.Raycast(
            firePoint.position,
            currentAimDirection,
            maxRange,
            targetLayer | obstacleLayer
        );

        float beamDistance = hit.collider != null ? hit.distance : maxRange;

        // Check if hit player
        if (hit.collider != null)
        {
            PlayerPawn playerPawn = hit.collider.GetComponent<PlayerPawn>();
            if (playerPawn != null)
            {
                playerPawn.TakeDamage(damage, gameObject.name);
            }
        }

        // Visual beam effect (you'll need to create a beam prefab or line renderer)
        if (projectilePrefab != null)
        {
            GameObject beam = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
            beam.transform.right = currentAimDirection;
            beam.transform.localScale = new Vector3(beamDistance, 1f, 1f);
            Destroy(beam, 0.2f);
        }
    }

    private void SpawnProjectile(Vector2 direction)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Projectile prefab not assigned!");
            return;
        }

        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        // Orient projectile
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        projectile.transform.rotation = Quaternion.Euler(0, 0, angle);

        // Setup projectile component
        Projectile proj = projectile.GetComponent<Projectile>();
        if (proj == null)
        {
            proj = projectile.AddComponent<Projectile>();
        }

        proj.Initialize(direction, projectileSpeed, maxRange, damage, gameObject.name);
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
