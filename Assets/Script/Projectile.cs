using UnityEngine;

/// <summary>
/// Handles projectile movement, collision, and damage dealing
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    #region Private Fields

    private Vector2 direction;
    private float speed;
    private float maxRange;
    private int damage;
    private string sourceTag;
    private Vector2 startPosition;
    private Rigidbody2D rb;
    private bool isInitialized = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (!isInitialized) return;

        // Move projectile
        rb.velocity = direction * speed;

        // Check if exceeded max range
        float traveledDistance = Vector2.Distance(startPosition, transform.position);
        if (traveledDistance >= maxRange)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if hit player
        PlayerPawn playerPawn = collision.GetComponent<PlayerPawn>();
        if (playerPawn != null)
        {
            playerPawn.TakeDamage(damage, sourceTag);
            Destroy(gameObject);
            return;
        }

        // Check if hit obstacle (tiles, walls, etc.)
        if (collision.CompareTag("Tile") || collision.CompareTag("Wall") || collision.CompareTag("Obstacle"))
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initialize the projectile with movement parameters
    /// </summary>
    public void Initialize(Vector2 moveDirection, float moveSpeed, float range, int damageAmount, string source)
    {
        direction = moveDirection.normalized;
        speed = moveSpeed;
        maxRange = range;
        damage = damageAmount;
        sourceTag = source;
        startPosition = transform.position;
        isInitialized = true;
    }

    #endregion
}
