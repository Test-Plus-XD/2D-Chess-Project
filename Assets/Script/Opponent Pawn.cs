using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Simple opponent pawn health, damage and sprite logic.
/// Opponents have MaxHP = 1 by default and sprite array shows visual HP state.
public class OpponentPawn : MonoBehaviour
{
    // Max HP for this opponent (default 1).
    public int MaxHP = 1;
    // Current HP value initialised in Awake.
    public int HP;
    // Optional array of sprites where index 0 represents 0HP; if HP > sprites length we use highest available.
    public Sprite[] hpSprites;
    // Cached PawnController reference on the same GameObject.
    protected PawnController pawnController;
    // Cached SpriteRenderer used to display HP sprite.
    protected SpriteRenderer spriteRenderer;
    // Cached Rigidbody2D reference used to yeet.
    protected Rigidbody2D rigidBody2D;
    // Physics expel parameters.
    public float expelForce = 8f; // Horizontal impulse magnitude
    public float verticalImpulse = 4f; // Upward impulse component
    public float expelTorque = 6f; // Rotational impulse
    public float expelDelay = 1f; // Seconds before applying the expulsion impulse.
    public float destroyDelay = 1.6f; // Seconds before final destroy after applying physics
    // Visual "bring closer" settings used during death pause.
    public float bringCloserScale = 1.2f; // Scale multiplier when "closer"
    public float bringCloserDuration = 0.2f; // Time to animate scale up (during expelDelay)

    // Awake initialises HP and caches components.
    private void Awake()
    {
        HP = MaxHP;
        pawnController = GetComponent<PawnController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rigidBody2D = GetComponent<Rigidbody2D>();
        UpdateSpriteForHP();
    }

    // Public method to apply damage; returns true if pawn died as a result.
    public bool TakeDamage(int amount, string source = "")
    {
        HP -= amount;
        if (HP <= 0)
        {
            HP = 0;
            UpdateSpriteForHP();
            Debug.Log($"[OpponentPawn] Killed by {source}. Destroying {gameObject.name} at {GetCoordsString()}");
            Death();
            return true;
        }
        UpdateSpriteForHP();
        Debug.Log($"[OpponentPawn] Took {amount} dmg from {source}. HP now {HP}/{MaxHP} on {GetCoordsString()}");
        return false;
    }

    // Update sprite based on current HP using hpSprites mapping rules:
    // index = clamp(HP, 0, hpSprites.Length - 1); index 0 corresponds to 0 HP sprite.
    private void UpdateSpriteForHP()
    {
        if (hpSprites == null || hpSprites.Length == 0 || spriteRenderer == null) return;
        int idx = Mathf.Clamp(HP, 0, hpSprites.Length - 1);
        spriteRenderer.sprite = hpSprites[idx];
    }

    // Handle death: invoke event and optionally disable the GameObject (customise as needed).
    private void Death()
    {
        if (rigidBody2D == null)
        {
            Debug.LogWarning("[OpponentPawn] ExpelFromBoard: no Rigidbody2D found, destroying immediately.");
            Destroy(gameObject);
            return;
        }

        // Start the delayed expulsion to show a brief pause before launch.
        StartCoroutine(ExpelAfterDelayCoroutine());
        // Trigger camera zoom pulse when this pawn dies (optional).
        if (FollowCamera.Instance != null) FollowCamera.Instance.ZoomOutPulse(2f, 0.9f);
        // Determine board minX/maxX by scanning children of the grid's parent container.
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        bool foundTile = false;
        Transform parent = null;
        if (pawnController != null && pawnController.gridGenerator != null)
        {
            parent = pawnController.gridGenerator.parentContainer != null ? pawnController.gridGenerator.parentContainer : pawnController.gridGenerator.transform;
        }
        if (parent != null)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform t = parent.GetChild(i);
                if (t == null) continue;
                // Use collider bounds when present for accurate sprite extents; otherwise use transform position.
                var pc2d = t.GetComponent<PolygonCollider2D>();
                if (pc2d != null)
                {
                    // Consider collider's world-space bounds so large sprites are accounted.
                    float left = pc2d.bounds.min.x;
                    float right = pc2d.bounds.max.x;
                    minX = Mathf.Min(minX, left);
                    maxX = Mathf.Max(maxX, right);
                    foundTile = true;
                    continue;
                }
                // Fallback: use transform position
                float x = t.position.x;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                foundTile = true;
            }
        }

        // If no tiles found, fallback to camera viewport edges at pawn Z depth.
        if (!foundTile)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                float camZ = transform.position.z - cam.transform.position.z;
                Vector3 leftWorld = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, camZ));
                Vector3 rightWorld = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, camZ));
                minX = leftWorld.x;
                maxX = rightWorld.x;
                Debug.Log($"[OpponentPawn] ExpelFromBoard: fallback to camera bounds minX={minX:F2}, maxX={maxX:F2}");
            }
            else
            {
                // Last-resort fallback to origin-based bounds.
                minX = transform.position.x - 8f;
                maxX = transform.position.x + 8f;
            }
        }

        // Pawn world X position
        float pawnX = transform.position.x;
        // Distances to each horizontal side of the board (use distance to edges)
        float distToLeftEdge = Mathf.Abs(pawnX - minX);
        float distToRightEdge = Mathf.Abs(maxX - pawnX);
        // Choose direction: -1 => left, +1 => right based on which edge is nearer.
        float horizDir = (distToLeftEdge <= distToRightEdge) ? -1f : 1f;
        // Build randomised impulse vector: horizontal toward chosen side + slight upward component.
        Vector2 impulse = new Vector2(horizDir * Random.Range(expelForce, expelForce * 1.5f), Random.Range(verticalImpulse, verticalImpulse * 2.5f));

        // Ensure Rigidbody2D is non-kinematic so physics responds.
        rigidBody2D.bodyType = RigidbodyType2D.Dynamic;
        rigidBody2D.simulated = true;
        // Apply impulse and torque for visual flair.
        rigidBody2D.AddForce(impulse, ForceMode2D.Impulse);
        rigidBody2D.AddTorque(horizDir * expelTorque, ForceMode2D.Impulse);

        Debug.Log($"[OpponentPawn] Expelled toward {(horizDir < 0f ? "left" : "right")} (pawnX={pawnX:F2} minX={minX:F2} maxX={maxX:F2}) impulse={impulse}.");

        // Destroy after delay to allow physics to play out.
        Destroy(gameObject, destroyDelay);
    }

    // Coroutine that waits for expelDelay then applies physics impulse and destroys the pawn.
    private IEnumerator ExpelAfterDelayCoroutine()
    {
        // Ensure Rigidbody2D is cached.
        if (rigidBody2D == null) rigidBody2D = GetComponent<Rigidbody2D>();
        // Optional: hold pawn in place during delay to make the pause obvious.
        if (rigidBody2D != null)
        {
            // Remember previous bodyType to restore later if needed.
            RigidbodyType2D previousBodyType = rigidBody2D.bodyType;
            // Freeze physics during the wait so the pawn doesn't drift.
            rigidBody2D.linearVelocity = Vector2.zero;
            rigidBody2D.angularVelocity = 0f;
            rigidBody2D.bodyType = RigidbodyType2D.Kinematic;
            // Start visual pop and hold it during the delay.
            StartCoroutine(BringCloserCoroutine());
            // Wait the adjustable delay
            yield return new WaitForSeconds(expelDelay);
            // Restore physics and apply impulse/torque below.
            rigidBody2D.bodyType = previousBodyType;
        }
        else
        {
            // If no Rigidbody2D, still wait to preserve timing.
            yield return new WaitForSeconds(expelDelay);
        }

        // After the pause, compute bounds and apply impulse as before.
        // Determine board minX/maxX by scanning children of the grid's parent container.
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        bool foundTile = false;
        Transform parent = null;
        if (pawnController != null && pawnController.gridGenerator != null)
        {
            parent = pawnController.gridGenerator.parentContainer != null ? pawnController.gridGenerator.parentContainer : pawnController.gridGenerator.transform;
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
            Camera cam = Camera.main;
            if (cam != null)
            {
                float camZ = transform.position.z - cam.transform.position.z;
                Vector3 leftWorld = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, camZ));
                Vector3 rightWorld = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, camZ));
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

        if (rigidBody2D != null)
        {
            // Ensure physics simulated and apply force/torque.
            rigidBody2D.simulated = true;
            rigidBody2D.AddForce(impulse, ForceMode2D.Impulse);
            rigidBody2D.AddTorque(horizDir * expelTorque, ForceMode2D.Impulse);
        }
        else
        {
            // No rigidbody: fallback to instant translate over destroyDelay for visual continuity.
            Vector3 target = new Vector3(transform.position.x + horizDir * expelForce, transform.position.y + Random.Range(-0.5f, 0.5f), transform.position.z);
            StartCoroutine(SlideAndDestroy(target, destroyDelay));
            yield break;
        }

        // Destroy after configured delay so physics can play out.
        Destroy(gameObject, destroyDelay);
    }

    // Simple slide coroutine used when Rigidbody2D is absent.
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

    // Coroutine that temporarily brings the pawn visually closer (scale + sorting order + optional z offset).
    private IEnumerator BringCloserCoroutine()
    {
        // Ensure sprite renderer is cached.
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) yield break;

        // Save original values to restore if desired.
        Vector3 origScale = transform.localScale;
        int origOrder = spriteRenderer.sortingOrder;
        float origZ = transform.position.z;

        // Increase sorting order so sprite draws on top during the effect.
        spriteRenderer.sortingOrder = origOrder + 20;

        // Animate scale up over bringCloserDuration (non-blocking).
        float t = 0f;
        while (t < bringCloserDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / bringCloserDuration);
            // Smoothstep easing for nicer pop.
            float ease = a * a * (3f - 2f * a);
            transform.localScale = Vector3.Lerp(origScale, origScale * bringCloserScale, ease);
            yield return null;
        }
        transform.localScale = origScale * bringCloserScale;
        yield break;
    }

    // Helper string for logs showing axial coords if PawnController available.
    private string GetCoordsString()
    {
        if (pawnController != null) return $"{pawnController.q}_{pawnController.r}";
        return transform.position.ToString("F2");
    }
}