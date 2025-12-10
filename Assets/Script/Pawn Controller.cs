using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Per-pawn AI behaviour in both Chess and Standoff modes.
/// Chess mode: Weighted movement on hex grid based on AI type.
/// Standoff mode: 2D platformer AI with jumping and shooting.
[RequireComponent(typeof(Rigidbody2D))]
public class PawnController : MonoBehaviour
{
    #region Enums and Types
    // AI types available for this pawn.
    public enum AIType { Basic, Handcannon, Shotgun, Sniper }

    /// Modifier types that enhance opponent pawn capabilities
    public enum Modifier
    {
        None,        // No modifier
        Tenacious,   // Requires two captures to remove (2 HP)
        Confrontational, // Shoots on LOS entry (chess), -25% fire interval (standoff)
        Fleet,       // Extra move in chess, +25% move speed in standoff
        Observant,   // Bullets only damage player (chess), -50% firing delay (standoff)
        Reflexive    // Recalculate aim after player move (chess), fixed on player (standoff), -25% firing delay
    }
    #endregion

    #region Chess Mode Fields
    // Public fields.
    public AIType aiType = AIType.Basic;
    public Modifier modifier = Modifier.None;
    public HexGridGenerator gridGenerator;
    public float aiMoveDuration = 0.12f; // Duration of AI movement animation.
    public bool Moved = false; // Set true after pawn completes its AI move.
    public int q, r; // Axial coordinates tracked by this pawn.

    [Header("Modifier Visual")]
    [Tooltip("UI image displaying the modifier icon at top-right of pawn")]
    public UnityEngine.UI.Image modifierIconImage;
    [Tooltip("Sprite for Tenacious modifier")]
    public Sprite tenaciousIcon;
    [Tooltip("Sprite for Confrontational modifier")]
    public Sprite confrontationalIcon;
    [Tooltip("Sprite for Fleet modifier")]
    public Sprite fleetIcon;
    [Tooltip("Sprite for Observant modifier")]
    public Sprite observantIcon;
    [Tooltip("Sprite for Reflexive modifier")]
    public Sprite reflexiveIcon;

    // Neighbour axial deltas (must match your project's convention).
    private readonly int[] DIR_Q = { 1, 1, 0, -1, -1, 0 };
    private readonly int[] DIR_R = { 0, -1, -1, 0, 1, 1 };

    // Keep the original prefab/type label for nicer names (optional).
    private string typeLabel = "";
    #endregion

    #region Standoff Mode Fields
    [Header("Standoff Mode Settings")]
    [Tooltip("Movement speed in Standoff mode")]
    public float standoffMoveSpeed = 3f;
    [Tooltip("Jump force for AI")]
    public float jumpForce = 8f;
    [Tooltip("Ground check distance")]
    public float groundCheckDistance = 0.1f;
    [Tooltip("Layer mask for ground detection")]
    public LayerMask groundLayer;
    [Tooltip("Decision update interval (seconds)")]
    public float aiThinkInterval = 0.5f;

    // Standoff mode state
    private bool isStandoffMode = false;
    private Rigidbody2D rigidBody;
    private bool isGrounded = false;
    private float lastThinkTime = 0f;
    private float currentMoveDirection = 0f; // -1 = left, 0 = none, 1 = right
    private Transform playerTransform;
    private WeaponSystem weaponSystem;
    private SpriteRenderer spriteRenderer;
    #endregion

    // Data-holder for neighbour candidates.
    private class Candidate { public int q, r, index; public float weight = 1f; public int distToPlayer; }

    #region Unity Lifecycle

    // Notify Checkerboard when this pawn is created/destroyed.
    private void Start()
    {
        // Attempt to pick a grid generator if not assigned.
        if (gridGenerator == null) gridGenerator = FindFirstObjectByType<HexGridGenerator>();

        // Get components
        rigidBody = GetComponent<Rigidbody2D>();
        weaponSystem = GetComponent<WeaponSystem>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Configure Rigidbody2D for Chess mode by default
        if (rigidBody != null)
        {
            rigidBody.bodyType = RigidbodyType2D.Kinematic;
            rigidBody.gravityScale = 0f;
        }

        // Lightweight label for naming.
        typeLabel = this.gameObject.name.Split('(')[0].Trim();
        if (Checkerboard.Instance != null) Checkerboard.Instance.RegisterOpponent(this);
        Debug.Log($"[PawnController] Spawned {typeLabel} at {q}_{r}");

        // Find player
        FindPlayer();
    }

    private void Update()
    {
        if (isStandoffMode)
        {
            UpdateStandoffMode();
        }
    }

    private void FixedUpdate()
    {
        if (isStandoffMode)
        {
            FixedUpdateStandoffMode();
        }
    }

    private void OnDestroy()
    {
        if (Checkerboard.Instance != null) Checkerboard.Instance.DeregisterOpponent(this);
    }

    #endregion

    #region Mode Switching

    /// Switch between Chess and Standoff modes
    public void SetStandoffMode(bool standoffMode)
    {
        isStandoffMode = standoffMode;

        if (rigidBody == null) rigidBody = GetComponent<Rigidbody2D>();

        if (isStandoffMode)
        {
            // Configure for Standoff mode (platformer physics)
            if (rigidBody != null)
            {
                rigidBody.bodyType = RigidbodyType2D.Dynamic;
                rigidBody.gravityScale = 2f;
                rigidBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            // Configure weapon system
            if (weaponSystem != null)
            {
                weaponSystem.SetStandoffMode(true);
            }
        }
        else
        {
            // Configure for Chess mode (kinematic movement)
            if (rigidBody != null)
            {
                rigidBody.bodyType = RigidbodyType2D.Kinematic;
                rigidBody.gravityScale = 0f;
                rigidBody.linearVelocity = Vector2.zero;
            }

            // Configure weapon system
            if (weaponSystem != null)
            {
                weaponSystem.SetStandoffMode(false);
            }
        }
    }

    #endregion

    #region Standoff Mode AI

    private void FindPlayer()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void UpdateStandoffMode()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        // Check ground status
        CheckGroundStatus();

        // AI thinking at intervals
        if (Time.time >= lastThinkTime + aiThinkInterval)
        {
            lastThinkTime = Time.time;
            MakeStandoffDecision();
        }

        // Sprite flipping
        if (spriteRenderer != null && Mathf.Abs(currentMoveDirection) > 0.1f)
        {
            spriteRenderer.flipX = currentMoveDirection < 0;
        }
    }

    private void FixedUpdateStandoffMode()
    {
        if (rigidBody == null) return;

        // Apply horizontal movement
        rigidBody.linearVelocity = new Vector2(currentMoveDirection * standoffMoveSpeed, rigidBody.linearVelocity.y);
    }

    private void MakeStandoffDecision()
    {
        if (playerTransform == null) return;

        Vector2 toPlayer = playerTransform.position - transform.position;
        float distance = toPlayer.magnitude;

        // Decide movement and jumping based on AI type (matches Chess mode personalities)
        switch (aiType)
        {
            case AIType.Basic:
            case AIType.Shotgun:
                // Aggressive: Always move toward player (matches Chess mode behavior)
                if (distance > 1.5f)
                {
                    currentMoveDirection = Mathf.Sign(toPlayer.x);
                    TryJumpIfObstacle();
                }
                else
                {
                    currentMoveDirection = 0f;
                }
                break;

            case AIType.Handcannon:
                // Mid-range: Maintain 2-4 unit distance (matches Chess mode preference)
                if (distance > 4f)
                {
                    // Too far, move closer
                    currentMoveDirection = Mathf.Sign(toPlayer.x);
                    TryJumpIfObstacle();
                }
                else if (distance < 2f)
                {
                    // Too close, back up
                    currentMoveDirection = -Mathf.Sign(toPlayer.x);
                    TryJumpIfObstacle();
                }
                else
                {
                    // In optimal range, stop moving
                    currentMoveDirection = 0f;
                }
                break;

            case AIType.Sniper:
                // Long-range: Keep distance, move away if too close (matches Chess mode behavior)
                if (distance < 6f)
                {
                    // Too close, retreat
                    currentMoveDirection = -Mathf.Sign(toPlayer.x);
                    TryJumpIfObstacle();
                }
                else
                {
                    // Far enough, stop moving
                    currentMoveDirection = 0f;
                }
                break;
        }
    }

    private void CheckGroundStatus()
    {
        if (rigidBody == null) return;

        // Raycast downward to check for ground
        Vector2 position = transform.position;
        RaycastHit2D hit = Physics2D.Raycast(position, Vector2.down, groundCheckDistance, groundLayer);

        isGrounded = hit.collider != null;
    }

    private void TryJumpIfObstacle()
    {
        if (!isGrounded || rigidBody == null) return;

        // Check for obstacle ahead
        Vector2 position = transform.position;
        Vector2 checkDirection = new Vector2(currentMoveDirection, 0f);
        RaycastHit2D hit = Physics2D.Raycast(position, checkDirection, 1f, groundLayer);

        // If obstacle detected, try to jump
        if (hit.collider != null)
        {
            // Check if obstacle is jumpable (not too high)
            if (hit.point.y - position.y < 2f)
            {
                rigidBody.linearVelocity = new Vector2(rigidBody.linearVelocity.x, jumpForce);
            }
        }

        // Also jump if at edge (to avoid falling)
        Vector2 edgeCheck = position + new Vector2(currentMoveDirection * 0.5f, -0.5f);
        RaycastHit2D edgeHit = Physics2D.Raycast(edgeCheck, Vector2.down, 1f, groundLayer);

        if (edgeHit.collider == null)
        {
            // No ground ahead, stop moving or jump gap
            float gapDistance = 2f; // Max jumpable gap
            Vector2 farCheck = position + new Vector2(currentMoveDirection * gapDistance, -0.5f);
            RaycastHit2D farHit = Physics2D.Raycast(farCheck, Vector2.down, 2f, groundLayer);

            if (farHit.collider != null)
            {
                // Ground exists after gap, jump it
                rigidBody.linearVelocity = new Vector2(rigidBody.linearVelocity.x, jumpForce);
            }
            else
            {
                // No ground ahead, don't move forward
                currentMoveDirection = 0f;
            }
        }
    }

    #endregion

    #region Chess Mode Methods (Existing)

    // Choose a neighbor target using this pawn's q,r and the given player's q,r.
    // Returns true and out coords if a target was chosen.
    // Respects occupied/reserved tiles, occupied may be null; player tile (playerQ,playerR) is always allowed (so capture works).
    public bool ChooseMoveTarget(int playerQ, int playerR, HashSet<Vector2Int> occupied, out int outQ, out int outR)
    {
        outQ = 0; outR = 0;
        if (gridGenerator == null)
        {
            Debug.LogWarning("[PawnController] ChooseMoveTarget: gridGenerator is null.");
            return false;
        }

        // Build neighbour candidates that actually exist as tiles and exclude the player's tile.
        List<Candidate> candidates = new List<Candidate>();
        for (int i = 0; i < 6; i++)
        {
            int nq = q + DIR_Q[i];
            int nr = r + DIR_R[i];
            //if (nq == playerQ && nr == playerR) continue; // Exclude the player's tile explicitly so opponents never try to occupy the player.
            if (!TileExists(nq, nr)) continue;
            Candidate c = new Candidate() { q = nq, r = nr, index = i };
            candidates.Add(c);
        }
        if (candidates.Count == 0) return false;
        // Compute distances to player for all candidates (axial distance via cube coords).
        foreach (var c in candidates) c.distToPlayer = AxialDistance(c.q, c.r, playerQ, playerR);
        // Apply the single combined weight logic for this pawn's AI type.
        ApplyCombinedWeights(candidates, aiType, playerQ, playerR);

        // Debug: log candidate list and weights for transparency.
        string debugLine = $"[PawnController] {typeLabel} @{q}_{r} Candidates:";
        foreach (var c in candidates) debugLine += $" ({c.q}_{c.r}:w={c.weight},d={c.distToPlayer},idx={c.index})";
        Debug.Log(debugLine);

        // Remove candidates that are occupied by other opponents, unless it's the player's tile (allow capture).
        List<Candidate> allowed = new List<Candidate>();
        foreach (var c in candidates)
        {
            var coord = new Vector2Int(c.q, c.r);
            bool isPlayerTile = (c.q == playerQ && c.r == playerR);
            if (occupied != null && occupied.Contains(coord) && !isPlayerTile) continue;
            if (c.weight <= 0f) continue;
            allowed.Add(c);
        }
        if (allowed.Count == 0) return false;
        // Weighted random selection among allowed.
        float sum = 0f;
        foreach (var c in allowed) sum += c.weight;
        float pick = Random.Range(0f, sum);
        float acc = 0f;
        foreach (var c in allowed)
        {
            acc += c.weight;
            if (pick <= acc)
            {
                outQ = c.q; outR = c.r;
                Debug.Log($"[PawnController] {typeLabel} @{q}_{r} chose target {outQ}_{outR} (pick={pick:F3} sum={sum:F3}).");
                return true;
            }
        }
        // Fallback
        outQ = allowed[allowed.Count - 1].q; outR = allowed[allowed.Count - 1].r;
        Debug.Log($"[PawnController] {typeLabel} @{q}_{r} fallback chose {outQ}_{outR}.");
        return true;
    }

    // Combined weight application for all AI types in one place (Chess mode movement strategy)
    private void ApplyCombinedWeights(List<Candidate> candidates, AIType type, int playerQ, int playerR)
    {
        if (candidates == null || candidates.Count == 0) return;
        // Find min/max distances among candidates to compare relative distances to player
        int minDist = int.MaxValue; int maxDist = int.MinValue;
        foreach (var c in candidates) { minDist = Mathf.Min(minDist, c.distToPlayer); maxDist = Mathf.Max(maxDist, c.distToPlayer); }

        switch (type)
        {
            case AIType.Basic:
                // Like chess pawns: only move forward (down in world space), never backward (up)
                // Restricted to bottom 3 directions: (0,-1), (-1,0), (1,-1) - can't move upward
                foreach (var c in candidates) c.weight = 0f;

                // Get current tile world position for Y-axis comparison (world space)
                Vector3 currentWorldPos;
                if (!TryGetTileWorldCentre(q, r, out currentWorldPos))
                {
                    // Fallback to allowing all 3 directions if can't get world pos (directional check only)
                    foreach (var c in candidates)
                    {
                        int dq = c.q - q; int dr = c.r - r;
                        // Allow only: (0,-1) bottom, (-1,0) bottom-left, (1,-1) bottom-right
                        if ((dq == 0 && dr == -1) || (dq == -1 && dr == 0) || (dq == 1 && dr == -1))
                        {
                            c.weight = 1f;
                        }
                    }
                    break;
                }

                foreach (var c in candidates)
                {
                    // Get candidate tile world position to compare Y values
                    Vector3 candidateWorldPos;
                    if (!TryGetTileWorldCentre(c.q, c.r, out candidateWorldPos))
                    {
                        c.weight = 0f;
                        continue;
                    }

                    // Block any move that increases Y (moving backward/upward in world space means can't move up)
                    if (candidateWorldPos.y > currentWorldPos.y)
                    {
                        c.weight = 0f;
                        continue;
                    }

                    int dq = c.q - q; int dr = c.r - r;
                    // Check if direction is one of the allowed 3 bottom directions
                    // bottom = (0,-1); bottom-left = (-1,0); bottom-right = (1,-1)
                    if ((dq == 0 && dr == -1) || (dq == -1 && dr == 0) || (dq == 1 && dr == -1))
                    {
                        c.weight = 1f;
                    }
                }
                // Give extra weight (5x) to allowed tile(s) that are closest to player (prioritize approaching)
                float bestDist = float.PositiveInfinity;
                foreach (var c in candidates) if (c.weight > 0f) bestDist = Mathf.Min(bestDist, c.distToPlayer);
                foreach (var c in candidates) if (c.weight > 0f && c.distToPlayer == bestDist) c.weight = 5f;
                break;

            case AIType.Handcannon:
                // All 6 allowed; closest to player has weight 3, others 1.
                foreach (var c in candidates) c.weight = (c.distToPlayer == minDist) ? 3f : 1f;
                break;

            case AIType.Shotgun:
                // Aggressive toward player with directional preferences (tries to get close)
                // 4 weight: closest to player (highest priority - aggressive pursuit)
                // 3 weight: top-right and top-left diagonal moves (flanking positioning)
                // 2 weight: bottom-right and bottom-left (side positioning)
                // 1 weight: side/horizontal moves and farthest away (avoids retreating)
                foreach (var c in candidates)
                {
                    // Determine hex direction index by calculating offset from current position
                    int dq = c.q - q;
                    int dr = c.r - r;
                    int dirIndex = -1;
                    for (int i = 0; i < 6; i++)
                    {
                        if (DIR_Q[i] == dq && DIR_R[i] == dr)
                        {
                            dirIndex = i;
                            break;
                        }
                    }

                    // Assign weights with distance and directional priority
                    if (c.distToPlayer == minDist)
                    {
                        // Closest to player = aggressive pursuit (4x weight, highest priority)
                        c.weight = 4f;
                    }
                    else if (c.distToPlayer == maxDist)
                    {
                        // Farthest from player = avoid retreat (1x weight, lowest priority)
                        c.weight = 1f;
                    }
                    else if (dirIndex == 1 || dirIndex == 2) // Top-right (1,-1) or top-left (0,-1)
                    {
                        // Diagonal upper moves = flanking maneuver (3x weight)
                        c.weight = 3f;
                    }
                    else if (dirIndex == 4 || dirIndex == 5) // Bottom-left (-1,1) or bottom-right (0,1)
                    {
                        // Side moves = medium priority (2x weight)
                        c.weight = 2f;
                    }
                    else // Right (1,0) or left (-1,0)
                    {
                        // Pure horizontal moves = low priority (1x weight)
                        c.weight = 1f;
                    }
                }
                break;

            case AIType.Sniper:
                // Defensive positioning: Prefer farthest distance from player (keeps distance for long-range shots)
                // 4 weight: farthest from player (highest priority - maximize distance)
                // 2 weight: medium distance tiles (balanced)
                // 1 weight: closest to player (lowest priority - avoid close quarters where shotguns excel)
                foreach (var c in candidates)
                {
                    if (c.distToPlayer == maxDist) c.weight = 4f; // Far = safe sniping position
                    else if (c.distToPlayer == minDist) c.weight = 1f; // Close = dangerous to sniper
                    else c.weight = 2f; // Medium = balanced
                }
                break;
        }
    }

    // Execute this pawn's AI move using the pawn's own q,r and the player's q,r.
    // Returns true if a move was started.
    public bool ExecuteAIMoveTo(int targetQ, int targetR)
    {
        if (gridGenerator == null)
        {
            Debug.LogWarning("[PawnController] ExecuteAIMoveTo: gridGenerator is null.");
            return false;
        }
        Vector3 world;
        if (!TryGetTileWorldCentre(targetQ, targetR, out world)) return false;
        // Start coroutine to move and update q,r after movement.
        StartCoroutine(AIMoveCoroutine(world, targetQ, targetR));
        return true;
    }

    // Coroutine that moves the pawn and updates its tracked q,r when complete.
    private IEnumerator AIMoveCoroutine(Vector3 targetWorld, int targetQ, int targetR)
    {
        Moved = false;
        int fromQ = q; int fromR = r;
        Vector3 start = transform.position;
        float t = 0f;
        if ((targetWorld - start).sqrMagnitude < 0.0001f)
        {
            transform.position = targetWorld;
            q = targetQ; r = targetR; Moved = true; UpdateNameWithCoords();
            Debug.Log($"[PawnController] {typeLabel} moved (instant) {fromQ}_{fromR} -> {q}_{r}");
            yield break;
        }
        while (t < aiMoveDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / aiMoveDuration);
            transform.position = Vector3.Lerp(start, targetWorld, a);
            yield return null;
        }

        transform.position = targetWorld;
        q = targetQ; r = targetR; Moved = true; UpdateNameWithCoords();

        // After this pawn finishes moving, tries to capture player.
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            PawnHealth playerPawn = player.GetComponent<PawnHealth>();
            PlayerController playerController = player.GetComponent<PlayerController>();
            if (playerPawn != null && playerController.q == q && playerController.r == r)
            {
                // Deal damage equal to this Player's MaxHP (PawnHealth component).
                playerPawn.TakeDamage(playerPawn.MaxHP, $"Opponent_{aiType}");
            }
        }

        Debug.Log($"[PawnController] {typeLabel} moved {fromQ}_{fromR} -> {q}_{r}");
    }

    // Set initial coordinates and optionally reposition pawn to tile centre.
    public void SetCoordsAndSnap(int startQ, int startR)
    {
        q = startQ; r = startR;
        Vector3 world;
        if (TryGetTileWorldCentre(q, r, out world)) transform.position = world;
        UpdateNameWithCoords();
        Debug.Log($"[PawnController] {typeLabel} initialised at {q}_{r}");
    }

    // Utility: update GameObject name to include axial coords for debugging.
    private void UpdateNameWithCoords()
    {
        // Use aiType for readable label
        string modifierLabel = modifier != Modifier.None ? $" [{modifier}]" : "";
        this.gameObject.name = $"Opponent {aiType}{modifierLabel}: {q}_{r}";
    }

    #endregion

    #region Modifier System

    /// Set the modifier for this pawn and update visual display
    public void SetModifier(Modifier newModifier)
    {
        modifier = newModifier;
        UpdateModifierIcon();
        UpdateNameWithCoords();

        // Apply modifier effects to other components
        ApplyModifierEffects();
    }

    /// Update the modifier icon UI display
    private void UpdateModifierIcon()
    {
        if (modifierIconImage == null) return;

        Sprite iconSprite = null;
        switch (modifier)
        {
            case Modifier.Tenacious:
                iconSprite = tenaciousIcon;
                break;
            case Modifier.Confrontational:
                iconSprite = confrontationalIcon;
                break;
            case Modifier.Fleet:
                iconSprite = fleetIcon;
                break;
            case Modifier.Observant:
                iconSprite = observantIcon;
                break;
            case Modifier.Reflexive:
                iconSprite = reflexiveIcon;
                break;
            case Modifier.None:
            default:
                iconSprite = null;
                break;
        }

        if (iconSprite != null)
        {
            modifierIconImage.sprite = iconSprite;
            modifierIconImage.enabled = true;
        }
        else
        {
            modifierIconImage.enabled = false;
        }
    }

    /// Apply modifier effects to components (health, weapon, movement)
    private void ApplyModifierEffects()
    {
        // Apply Tenacious: Set health to 2
        if (modifier == Modifier.Tenacious)
        {
            PawnHealth pawnHealth = GetComponent<PawnHealth>();
            if (pawnHealth != null)
            {
                pawnHealth.MaxHP = 2;
                pawnHealth.SetHP(2);
            }
        }

        // Apply Fleet: Increase standoff movement speed by 25%
        if (modifier == Modifier.Fleet && isStandoffMode)
        {
            standoffMoveSpeed *= 1.25f;
        }

        // Apply modifier effects to weapon system
        if (weaponSystem != null)
        {
            weaponSystem.ApplyModifier(modifier);
        }
    }

    /// Get the fire interval multiplier based on modifier (Standoff mode only)
    public float GetFireIntervalMultiplier()
    {
        switch (modifier)
        {
            case Modifier.Confrontational:
                // Confrontational: Fires more frequently (-25% interval = 0.75x multiplier)
                // Example: 3s interval → 2.25s interval (fires 33% more often)
                return 0.75f;
            default:
                return 1.0f;
        }
    }

    /// Get the firing delay multiplier based on modifier (Standoff mode only)
    public float GetFiringDelayMultiplier()
    {
        switch (modifier)
        {
            case Modifier.Observant:
                // Observant: Much faster firing delay (-50% delay = 0.5x multiplier)
                // Example: 0.5s delay → 0.25s delay (fires sooner after interval expires)
                return 0.5f;
            case Modifier.Reflexive:
                // Reflexive: Faster firing delay (-25% delay = 0.75x multiplier)
                // Example: 0.5s delay → 0.375s delay (fires sooner than normal)
                return 0.75f;
            default:
                return 1.0f;
        }
    }

    /// Check if this pawn should get an extra move in Chess mode (Fleet modifier only)
    public bool HasExtraMove()
    {
        // Fleet modifier: Gets 2 moves per turn instead of 1 (but only shoots once at turn start)
        return modifier == Modifier.Fleet;
    }

    /// Check if bullets should only damage the player (Observant modifier, Chess mode only)
    public bool BulletsOnlyDamagePlayer()
    {
        // Observant modifier (Chess): Bullets pass through opponents, only damage player
        // Useful for tactical positioning without friendly fire risk
        return modifier == Modifier.Observant && !isStandoffMode;
    }

    /// Check if this pawn should recalculate aim after player moves (Reflexive modifier, Chess mode only)
    public bool ShouldRecalculateAimAfterPlayerMove()
    {
        // Reflexive modifier (Chess): After player moves, this pawn recalculates best aim direction
        // Gives tactical advantage by always aiming at player's new position
        return modifier == Modifier.Reflexive && !isStandoffMode;
    }

    /// Check if this pawn should fire when entering LOS (Confrontational modifier, Chess mode only)
    public bool ShouldFireOnLineOfSight()
    {
        // Confrontational modifier (Chess): Shoots not just at turn start, but also when
        // another piece enters their line of sight (in addition to regular turn firing)
        return modifier == Modifier.Confrontational && !isStandoffMode;
    }

    /// Check if gun should be fixed on player (Reflexive modifier, Standoff mode only)
    public bool ShouldFixGunOnPlayer()
    {
        // Reflexive modifier (Standoff): Gun tracks player instantly without lerp/angular velocity
        // Combined with reduced firing delay for rapid targeting
        return modifier == Modifier.Reflexive && isStandoffMode;
    }

    /// Convert Basic type to Handcannon (used when last opponent enters standoff)
    public void ConvertBasicToHandcannon()
    {
        if (aiType == AIType.Basic)
        {
            aiType = AIType.Handcannon;
            UpdateNameWithCoords();
            Debug.Log($"[PawnController] Converted Basic to Handcannon with modifier: {modifier}");
        }
    }

    // Utility: check tile existence under the generator parent container.
    private bool TileExists(int qa, int ra)
    {
        Transform parent = gridGenerator.parentContainer == null ? gridGenerator.transform : gridGenerator.parentContainer;
        return parent.Find($"Hex_{qa}_{ra}") != null;
    }

    // Utility: get tile world centre using PolygonCollider2D if available.
    private bool TryGetTileWorldCentre(int qa, int ra, out Vector3 centre)
    {
        centre = Vector3.zero;
        Transform parent = gridGenerator.parentContainer == null ? gridGenerator.transform : gridGenerator.parentContainer;
        Transform tile = parent.Find($"Hex_{qa}_{ra}");
        if (tile == null) return false;
        PolygonCollider2D polygonCollider = tile.GetComponent<PolygonCollider2D>();
        if (polygonCollider != null)
        {
            Vector3 c = polygonCollider.bounds.center;
            centre = new Vector3(c.x, c.y, transform.position.z);
            return true;
        }
        centre = tile.position; return true;
    }

    // Axial distance using cube coords: distance = max(|dx|, |dy|, |dz|).
    private int AxialDistance(int q1, int r1, int q2, int r2)
    {
        int x1 = q1; int z1 = r1; int y1 = -x1 - z1;
        int x2 = q2; int z2 = r2; int y2 = -x2 - z2;
        return Mathf.Max(Mathf.Abs(x1 - x2), Mathf.Abs(y1 - y2), Mathf.Abs(z1 - z2));
    }

    // Helper to reset Moved (used by Checkerboard between turns).
    public void ResetMovedFlag() { Moved = false; }

    #endregion
}