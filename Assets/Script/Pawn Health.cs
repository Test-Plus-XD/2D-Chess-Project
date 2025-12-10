using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Unified health system for both player and opponent pawns.
// Consolidates PlayerPawn and OpponentPawn functionality.
public class PawnHealth : MonoBehaviour
{
    #region Enums

    public enum PawnType
    {
        Player,
        Opponent
    }

    #endregion

    #region Inspector Fields

    [Header("Pawn Configuration")]
    [Tooltip("Whether this is a player or opponent pawn")]
    public PawnType pawnType = PawnType.Player;

    [Header("Health Settings")]
    [Tooltip("Maximum HP for the pawn")]
    public int MaxHP = 3;

    [Tooltip("Starting HP applied on spawn")]
    public int startingHP = 2;

    [Tooltip("Current HP")]
    public int HP;

    [Header("Visual")]
    [Tooltip("SpriteRenderer to display the pawn")]
    public SpriteRenderer spriteRenderer;

    [Tooltip("Array of sprites where index 0 => 0 HP")]
    public Sprite[] hpSprites;

    [Header("Events")]
    [Tooltip("Called when HP changes")]
    public UnityEvent<int> OnHPChanged;

    #endregion

    #region Inspector Fields - Opponent Death Physics

    [Header("Death Physics (Opponent Only)")]
    [Tooltip("Horizontal impulse magnitude")]
    public float expelForce = 8f;

    [Tooltip("Upward impulse component")]
    public float verticalImpulse = 4f;

    [Tooltip("Rotational impulse")]
    public float expelTorque = 6f;

    [Tooltip("Seconds before applying expulsion")]
    public float expelDelay = 1f;

    [Tooltip("Seconds before destroy after physics")]
    public float destroyDelay = 1.6f;

    [Header("Death Visual (Opponent Only)")]
    [Tooltip("Scale multiplier when bringing closer")]
    public float bringCloserScale = 1.2f;

    [Tooltip("Duration of scale animation")]
    public float bringCloserDuration = 0.2f;

    #endregion

    #region Private Fields

    protected PawnController pawnController;
    protected Rigidbody2D rigidBody;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        HP = pawnType == PawnType.Player ? startingHP : MaxHP;
        pawnController = GetComponent<PawnController>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        rigidBody = GetComponent<Rigidbody2D>();
        UpdateSpriteForHP();
    }

    private void OnEnable()
    {
        if (pawnType == PawnType.Player)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (MaxHP < 1) MaxHP = 1;
            startingHP = Mathf.Clamp(startingHP, 0, MaxHP);
            HP = startingHP;
            UpdateSpriteForHP();
            OnHPChanged?.Invoke(HP);
        }
    }

    #endregion

    #region Public Methods - Damage

    public bool TakeDamage(int amount, string source = "")
    {
        HP -= amount;
        if (HP <= 0)
        {
            HP = 0;
            UpdateSpriteForHP();
            Debug.Log($"[PawnHealth] {pawnType} killed by {source} at HP 0.");
            Death();
            return true;
        }
        Debug.Log($"[PawnHealth] {pawnType} took {amount} dmg from {source}. HP now {HP}/{MaxHP}.");
        UpdateSpriteForHP();
        OnHPChanged?.Invoke(HP);
        return false;
    }

    public void Damage(int amount = 1)
    {
        amount = Mathf.Max(0, amount);
        SetHP(HP - amount);
    }

    public void Heal(int amount = 1)
    {
        amount = Mathf.Max(0, amount);
        SetHP(HP + amount);
    }

    public void SetHP(int newHP)
    {
        int clamped = Mathf.Clamp(newHP, 0, MaxHP);
        if (clamped == HP) return;
        HP = clamped;
        UpdateSpriteForHP();
        OnHPChanged?.Invoke(HP);
        if (HP == 0) Death();
    }

    public void ResetToSpawnHP()
    {
        SetHP(startingHP);
    }

    public void SetMaxHP(int newMaxHP)
    {
        MaxHP = Mathf.Max(1, newMaxHP);
        HP = Mathf.Clamp(HP, 0, MaxHP);
        UpdateSpriteForHP();
        OnHPChanged?.Invoke(HP);
    }

    #endregion

    #region Public Methods - Getters

    public float GetHealthFraction()
    {
        if (MaxHP <= 0) return 0f;
        return (float)HP / (float)MaxHP;
    }

    public int GetCurrentHP() => HP;
    public int GetMaxHP() => MaxHP;

    #endregion

    #region Private Methods - Visual

    private void UpdateSpriteForHP()
    {
        if (spriteRenderer == null || hpSprites == null || hpSprites.Length == 0)
        {
            return;
        }
        int spriteIndex = Mathf.Clamp(HP, 0, hpSprites.Length - 1);
        spriteRenderer.sprite = hpSprites[spriteIndex];
    }

    #endregion

    #region Private Methods - Death

    private void Death()
    {
        if (pawnType == PawnType.Player)
        {
            PlayerDeath();
        }
        else
        {
            OpponentDeath();
        }
    }

    private void PlayerDeath()
    {
        // Player death triggers defeat
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerDefeat();
        }
    }

    private void OpponentDeath()
    {
        if (rigidBody == null)
        {
            Debug.LogWarning("[PawnHealth] No Rigidbody2D found, destroying immediately.");
            Destroy(gameObject);
            return;
        }

        StartCoroutine(ExpelAfterDelayCoroutine());

        if (FollowCamera.Instance != null)
        {
            FollowCamera.Instance.RegisterKillAndPulseAggregated();
        }
    }

    private IEnumerator ExpelAfterDelayCoroutine()
    {
        if (rigidBody == null) rigidBody = GetComponent<Rigidbody2D>();

        if (rigidBody != null)
        {
            RigidbodyType2D previousBodyType = rigidBody.bodyType;
            rigidBody.linearVelocity = Vector2.zero;
            rigidBody.angularVelocity = 0f;
            rigidBody.bodyType = RigidbodyType2D.Kinematic;

            StartCoroutine(BringCloserCoroutine());

            yield return new WaitForSeconds(expelDelay);

            rigidBody.bodyType = previousBodyType;
        }
        else
        {
            yield return new WaitForSeconds(expelDelay);
        }

        // Compute board bounds
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        bool foundTile = false;

        Transform parent = null;
        if (pawnController != null && pawnController.gridGenerator != null)
        {
            parent = pawnController.gridGenerator.parentContainer != null
                ? pawnController.gridGenerator.parentContainer
                : pawnController.gridGenerator.transform;
        }

        if (parent != null)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform t = parent.GetChild(i);
                if (t == null) continue;

                var pc2d = t.GetComponent<PolygonCollider2D>();
                if (pc2d != null)
                {
                    minX = Mathf.Min(minX, pc2d.bounds.min.x);
                    maxX = Mathf.Max(maxX, pc2d.bounds.max.x);
                    foundTile = true;
                    continue;
                }

                float x = t.position.x;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                foundTile = true;
            }
        }

        if (!foundTile)
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                float camZ = transform.position.z - camera.transform.position.z;
                Vector3 leftWorld = camera.ViewportToWorldPoint(new Vector3(0f, 0.5f, camZ));
                Vector3 rightWorld = camera.ViewportToWorldPoint(new Vector3(1f, 0.5f, camZ));
                minX = leftWorld.x;
                maxX = rightWorld.x;
            }
            else
            {
                minX = transform.position.x - 8f;
                maxX = transform.position.x + 8f;
            }
        }

        float pawnX = transform.position.x;
        float distToLeftEdge = Mathf.Abs(pawnX - minX);
        float distToRightEdge = Mathf.Abs(maxX - pawnX);
        float horizDir = (distToLeftEdge <= distToRightEdge) ? -1f : 1f;

        Vector2 impulse = new Vector2(horizDir * expelForce, verticalImpulse);

        if (rigidBody != null)
        {
            rigidBody.simulated = true;
            rigidBody.AddForce(impulse, ForceMode2D.Impulse);
            rigidBody.AddTorque(horizDir * expelTorque, ForceMode2D.Impulse);
        }
        else
        {
            Vector3 target = new Vector3(
                transform.position.x + horizDir * expelForce,
                transform.position.y + Random.Range(-0.5f, 0.5f),
                transform.position.z
            );
            StartCoroutine(SlideAndDestroy(target, destroyDelay));
            yield break;
        }

        Destroy(gameObject, destroyDelay);
    }

    private IEnumerator SlideAndDestroy(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, t / duration);
            yield return null;
        }
        Destroy(gameObject);
    }

    private IEnumerator BringCloserCoroutine()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) yield break;

        Vector3 origScale = transform.localScale;
        int origOrder = spriteRenderer.sortingOrder;

        spriteRenderer.sortingOrder = origOrder + 20;

        float t = 0f;
        while (t < bringCloserDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / bringCloserDuration);
            float ease = a * a * (3f - 2f * a);
            transform.localScale = Vector3.Lerp(origScale, origScale * bringCloserScale, ease);
            yield return null;
        }
        transform.localScale = origScale * bringCloserScale;
    }

    private string GetCoordsString()
    {
        if (pawnController != null) return $"{pawnController.q}_{pawnController.r}";
        return transform.position.ToString("F2");
    }

    #endregion
}
