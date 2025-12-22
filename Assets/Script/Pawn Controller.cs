using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Per-pawn AI behaviour in both Chess and Standoff modes.
/// Chess mode: Weighted movement on hex grid based on AI type.
/// Standoff mode: 2D platformer AI with jumping and shooting.
[RequireComponent(typeof(Rigidbody2D))]
public class PawnController : MonoBehaviour
{
    // AI types available for this pawn.
    public enum AIType { Basic, Handcannon, Shotgun, Sniper }

    /// Modifier types that enhance opponent pawn capabilities.
    public enum Modifier
    {
        None,        // No modifier
        Tenacious,   // Requires two captures to remove (2 HP)
        Confrontational, // Shoots on LOS entry (chess), -25% fire interval (standoff)
        Fleet,       // Extra move in chess, +25% move speed in standoff
        Observant,   // Bullets only damage player (chess), -50% firing delay (standoff)
        Reflexive    // Recalculate aim after player move (chess), fixed on player (standoff), -25% firing delay
    }

    [Header("AI Configuration")]
    [Tooltip("ScriptableObject containing AI behaviour and modifier configurations")]
    public PawnCustomiser pawnCustomiser;
    [Tooltip("AI type for this pawn")]
    public AIType aiType = AIType.Basic;
    [Tooltip("Modifier enhancing this pawn's capabilities")]
    public Modifier modifier = Modifier.None;

    [Header("Chess Mode Settings")]
    [Tooltip("Reference to the hex grid generator")]
    public HexGridGenerator gridGenerator;
    [Tooltip("Set true after pawn completes its AI move")]
    public bool Moved = false;
    [Tooltip("Axial coordinates (q, r) tracked by this pawn")]
    public int q, r;

    [Header("Modifier Visual")]
    [Tooltip("UI image displaying the modifier icon at top-right of pawn")]
    public UnityEngine.UI.Image modifierIconImage;

    // Keep the original prefab/type label for nicer names (optional).
    private string typeLabel = "";

    [Header("Standoff Mode Settings")]
    [Tooltip("Layer mask for ground detection")]
    public LayerMask groundLayer;
    [Tooltip("Gravity scale for standoff mode")]
    public float standoffGravityScale = 2f;

    // Standoff mode state.
    // Whether pawn is currently in standoff platformer mode.
    private bool isStandoffMode = false;
    // Rigidbody2D for physics-based movement.
    private Rigidbody2D rigidBody;
    // Whether pawn is currently touching ground.
    private bool isGrounded = false;
    // Timestamp of last AI decision.
    private float lastThinkTime = 0f;
    // Current horizontal movement direction (-1 = left, 0 = none, 1 = right).
    private float currentMoveDirection = 0f;
    // Transform reference to player for AI decision-making.
    private Transform playerTransform;
    // WeaponSystem component for standoff mode shooting.
    private WeaponSystem weaponSystem;
    // SpriteRenderer for sprite flipping based on direction.
    private SpriteRenderer spriteRenderer;

    // Data-holder for neighbour candidates.
    private class Candidate { public int q, r, index; public float weight = 1f; public int distToPlayer; }

    // Notify Checkerboard when this pawn is created/destroyed.
    private void Start()
    {
        // Attempt to pick a grid generator if not assigned.
        if(gridGenerator == null) gridGenerator = FindFirstObjectByType<HexGridGenerator>();

        // Check if pawnCustomiser is assigned.
        if(pawnCustomiser == null)
        {
            Debug.LogWarning($"[PawnController] {gameObject.name}: No Pawn Customiser assigned! Using Basic AI with no modifiers as fallback. " +
                           $"Please create a Pawn Customiser asset (Right-click → Create → Game → Pawn Customiser) and assign it to this pawn.");
        }

        // Get components.
        rigidBody = GetComponent<Rigidbody2D>();
        weaponSystem = GetComponent<WeaponSystem>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Configure Rigidbody2D for Chess mode by default.
        if(rigidBody != null)
        {
            rigidBody.bodyType = RigidbodyType2D.Kinematic;
            rigidBody.gravityScale = 0f;
        }

        // Disable Canvas child if no modifier.
        if(modifier == Modifier.None)
        {
            Transform canvasTransform = transform.Find("Canvas");
            if(canvasTransform != null)
            {
                canvasTransform.gameObject.SetActive(false);
            }
        }

        // Lightweight label for naming.
        typeLabel = this.gameObject.name.Split('(')[0].Trim();
        if(Checkerboard.Instance != null) Checkerboard.Instance.RegisterOpponent(this);
        Debug.Log($"[PawnController] Spawned {typeLabel} at {q}_{r}");

        // Find player.
        FindPlayer();
    }

    private void Update()
    {
        // Don't update pawns when game is paused
        if (Time.timeScale == 0f) return;
        
        if(isStandoffMode) UpdateStandoffMode();
    }

    private void FixedUpdate()
    {
        // Don't update pawns when game is paused
        if (Time.timeScale == 0f) return;
        
        if(isStandoffMode) FixedUpdateStandoffMode();
    }

    private void OnDestroy()
    {
        if(Checkerboard.Instance != null) Checkerboard.Instance.DeregisterOpponent(this);
    }

    /// Switch between Chess and Standoff modes.
    public void SetStandoffMode(bool standoffMode)
    {
        isStandoffMode = standoffMode;

        if(rigidBody == null) rigidBody = GetComponent<Rigidbody2D>();

        if(isStandoffMode)
        {
            // Configure for Standoff mode (platformer physics).
            if(rigidBody != null)
            {
                rigidBody.bodyType = RigidbodyType2D.Dynamic;
                rigidBody.gravityScale = standoffGravityScale;
                rigidBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            // Configure weapon system.
            if(weaponSystem != null) weaponSystem.SetStandoffMode(true);
        } else
        {
            // Configure for Chess mode (kinematic movement).
            if(rigidBody != null)
            {
                rigidBody.bodyType = RigidbodyType2D.Kinematic;
                rigidBody.gravityScale = 0f;
                rigidBody.linearVelocity = Vector2.zero;
            }

            // Configure weapon system.
            if(weaponSystem != null) weaponSystem.SetStandoffMode(false);
        }
    }

    #region Standoff Mode AI

    private void FindPlayer()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if(player != null) playerTransform = player.transform;
    }

    private void UpdateStandoffMode()
    {
        if(playerTransform == null)
        {
            FindPlayer();
            return;
        }

        // Check ground status.
        CheckGroundStatus();

        // AI thinking at intervals.
        float thinkInterval = 0.5f; // Basic AI default.
        if(pawnCustomiser != null) thinkInterval = pawnCustomiser.aiThinking.standoffThinkInterval;
        if(Time.time >= lastThinkTime + thinkInterval)
        {
            lastThinkTime = Time.time;
            MakeStandoffDecision();
        }

        // Sprite flipping.
        if(spriteRenderer != null && Mathf.Abs(currentMoveDirection) > 0.1f) spriteRenderer.flipX = currentMoveDirection < 0;
    }

    private void FixedUpdateStandoffMode()
    {
        if(rigidBody == null) return;

        // Apply horizontal movement with customiser values.
        float moveSpeed = 3f; // Basic AI default.
        float speedMultiplier = 1f; // No modifier for Basic AI.
        if(pawnCustomiser != null)
        {
            moveSpeed = pawnCustomiser.platformerMovement.baseMoveSpeed;
            speedMultiplier = pawnCustomiser.GetMoveSpeedMultiplier(modifier);
        }
        rigidBody.linearVelocity = new Vector2(currentMoveDirection * moveSpeed * speedMultiplier, rigidBody.linearVelocity.y);
    }

    private void MakeStandoffDecision()
    {
        if(playerTransform == null) return;

        Vector2 toPlayer = playerTransform.position - transform.position;
        float distance = toPlayer.magnitude;

        // Use Basic AI behaviour if no customiser assigned.
        if(pawnCustomiser == null)
        {
            // Basic AI: Simple aggressive approach (always move toward player).
            if(distance > 1.5f)
            {
                float desiredDirection = Mathf.Sign(toPlayer.x);
                if(IsLedgeAhead(desiredDirection))
                {
                    // Try alternative: jump if possible, otherwise try opposite direction briefly
                    if(CanSafelyJumpGap(desiredDirection))
                    {
                        TryJumpGap(desiredDirection);
                        currentMoveDirection = desiredDirection * 0.5f; // Reduced speed for jumping
                    } else
                    {
                        currentMoveDirection = 0f; // Stop if no safe options
                    }
                } else
                {
                    currentMoveDirection = desiredDirection;
                    TryJumpIfObstacle();
                }
            } else currentMoveDirection = 0f;
            return;
        }

        // Get distance thresholds from customiser for this AI type.
        PawnCustomiser.AITypeStandoffDistances distances = pawnCustomiser.GetStandoffDistances(aiType);
        float minDistance = distances.minDistance;
        float maxDistance = distances.maxDistance;

        // Decide movement and jumping based on AI type (matches Chess mode personalities).
        switch(aiType)
        {
            case AIType.Basic:
            case AIType.Shotgun:
                // Aggressive: Always move toward player (matches Chess mode behaviour).
                if(distance > maxDistance)
                {
                    float desiredDirection = Mathf.Sign(toPlayer.x);
                    if(IsLedgeAhead(desiredDirection))
                    {
                        HandleLedgeEncounter(desiredDirection);
                    } else
                    {
                        currentMoveDirection = desiredDirection;
                        TryJumpIfObstacle();
                    }
                } else currentMoveDirection = 0f;
                break;

            case AIType.Handcannon:
                // Mid-range: Maintain optimal distance (matches Chess mode preference).
                if(distance > maxDistance)
                {
                    // Too far, move closer.
                    float desiredDirection = Mathf.Sign(toPlayer.x);
                    if(IsLedgeAhead(desiredDirection))
                    {
                        HandleLedgeEncounter(desiredDirection);
                    } else
                    {
                        currentMoveDirection = desiredDirection;
                        TryJumpIfObstacle();
                    }
                } else if(distance < minDistance)
                {
                    // Too close, back up.
                    float desiredDirection = -Mathf.Sign(toPlayer.x);
                    if(IsLedgeAhead(desiredDirection))
                    {
                        HandleLedgeEncounter(desiredDirection);
                    } else
                    {
                        currentMoveDirection = desiredDirection;
                        TryJumpIfObstacle();
                    }
                } else currentMoveDirection = 0f;
                break;

            case AIType.Sniper:
                // Long-range: Keep distance, move away if too close (matches Chess mode behaviour).
                if(distance < minDistance)
                {
                    // Too close, retreat.
                    float desiredDirection = -Mathf.Sign(toPlayer.x);
                    if(IsLedgeAhead(desiredDirection))
                    {
                        HandleLedgeEncounter(desiredDirection);
                    } else
                    {
                        currentMoveDirection = desiredDirection;
                        TryJumpIfObstacle();
                    }
                } else currentMoveDirection = 0f;
                break;
        }
    }

    /// Handle encountering a ledge with smart behavior options.
    private void HandleLedgeEncounter(float desiredDirection)
    {
        // Option 1: Try to jump the gap if it's safe
        if(CanSafelyJumpGap(desiredDirection))
        {
            TryJumpGap(desiredDirection);
            currentMoveDirection = desiredDirection * 0.6f; // Reduced speed for precision
            return;
        }

        // Option 2: For aggressive AI types, try moving in opposite direction briefly
        if(aiType == AIType.Basic || aiType == AIType.Shotgun)
        {
            float alternateDirection = -desiredDirection;
            if(!IsLedgeAhead(alternateDirection))
            {
                currentMoveDirection = alternateDirection * 0.3f; // Very slow alternate movement
                return;
            }
        }

        // Option 3: Stop moving (safest fallback)
        currentMoveDirection = 0f;
    }

    /// Check if the pawn can safely jump a gap in the given direction.
    private bool CanSafelyJumpGap(float direction)
    {
        if(!isGrounded || rigidBody == null) return false;

        float maxGapDist = 2f;
        float farCheckDist = 2f;
        float edgeOffset = 0.5f;
        float edgeVertical = 0.5f;

        if(pawnCustomiser != null)
        {
            maxGapDist = pawnCustomiser.platformerMovement.maxJumpableGap;
            farCheckDist = pawnCustomiser.platformerMovement.farGroundCheckDistance;
            edgeOffset = pawnCustomiser.platformerMovement.edgeCheckOffset;
            edgeVertical = pawnCustomiser.platformerMovement.edgeCheckVerticalOffset;
        }

        Vector2 position = transform.position;
        
        // Check if there's ground after the gap at a reasonable distance
        for(float checkDist = maxGapDist * 0.5f; checkDist <= maxGapDist; checkDist += 0.5f)
        {
            Vector2 farCheck = position + new Vector2(direction * checkDist, -edgeVertical);
            RaycastHit2D farHit = Physics2D.Raycast(farCheck, Vector2.down, farCheckDist, groundLayer);

            if(farHit.collider != null)
            {
                // Check if the landing spot is at a reasonable height
                float heightDifference = farHit.point.y - position.y;
                if(heightDifference > -3f && heightDifference < 1f) // Not too far down or up
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// Attempt to jump a gap with appropriate force.
    private void TryJumpGap(float direction)
    {
        if(!isGrounded || rigidBody == null) return;

        float jumpForceValue = 8f;
        if(pawnCustomiser != null)
        {
            jumpForceValue = pawnCustomiser.platformerMovement.jumpForce;
        }

        // Apply slightly more horizontal force for gap jumping
        Vector2 jumpVector = new Vector2(direction * jumpForceValue * 0.3f, jumpForceValue);
        rigidBody.linearVelocity = new Vector2(rigidBody.linearVelocity.x + jumpVector.x, jumpVector.y);
    }

    private void CheckGroundStatus()
    {
        if(rigidBody == null) return;

        // Raycast downward to check for ground.
        float checkDistance = 0.1f; // Basic AI default.
        if(pawnCustomiser != null) checkDistance = pawnCustomiser.platformerMovement.groundCheckDistance;
        Vector2 position = transform.position;
        RaycastHit2D hit = Physics2D.Raycast(position, Vector2.down, checkDistance, groundLayer);

        isGrounded = hit.collider != null;
    }

    private void TryJumpIfObstacle()
    {
        if(!isGrounded || rigidBody == null) return;

        // Get jump parameters from customiser or use Basic AI defaults.
        float jumpForceValue = 8f; // Basic AI default.
        float maxJumpHeight = 2f;
        float edgeOffset = 0.5f;
        float edgeVertical = 0.5f;
        float edgeRayDist = 1f;
        float maxGapDist = 2f;
        float farCheckDist = 2f;

        if(pawnCustomiser != null)
        {
            jumpForceValue = pawnCustomiser.platformerMovement.jumpForce;
            maxJumpHeight = pawnCustomiser.platformerMovement.maxJumpableHeight;
            edgeOffset = pawnCustomiser.platformerMovement.edgeCheckOffset;
            edgeVertical = pawnCustomiser.platformerMovement.edgeCheckVerticalOffset;
            edgeRayDist = pawnCustomiser.platformerMovement.edgeRaycastDistance;
            maxGapDist = pawnCustomiser.platformerMovement.maxJumpableGap;
            farCheckDist = pawnCustomiser.platformerMovement.farGroundCheckDistance;
        }

        // Check for obstacle ahead.
        Vector2 position = transform.position;
        Vector2 checkDirection = new Vector2(currentMoveDirection, 0f);
        RaycastHit2D hit = Physics2D.Raycast(position, checkDirection, 1f, groundLayer);

        // If obstacle detected, try to jump.
        if(hit.collider != null)
        {
            // Check if obstacle is jumpable (not too high).
            if(hit.point.y - position.y < maxJumpHeight) rigidBody.linearVelocity = new Vector2(rigidBody.linearVelocity.x, jumpForceValue);
        }

        // Also jump if at edge (to avoid falling).
        Vector2 edgeCheck = position + new Vector2(currentMoveDirection * edgeOffset, -edgeVertical);
        RaycastHit2D edgeHit = Physics2D.Raycast(edgeCheck, Vector2.down, edgeRayDist, groundLayer);

        if(edgeHit.collider == null)
        {
            // No ground ahead, stop moving or jump gap.
            Vector2 farCheck = position + new Vector2(currentMoveDirection * maxGapDist, -edgeVertical);
            RaycastHit2D farHit = Physics2D.Raycast(farCheck, Vector2.down, farCheckDist, groundLayer);

            if(farHit.collider != null)
            {
                // Ground exists after gap, jump it.
                rigidBody.linearVelocity = new Vector2(rigidBody.linearVelocity.x, jumpForceValue);
            } else currentMoveDirection = 0f;
        }
    }

    /// Enhanced ledge detection with multiple rays and smart behavior.
    /// Uses fixed detection width for robust results regardless of pawn size.
    private bool IsLedgeAhead(float direction)
    {
        if(!isGrounded || Mathf.Abs(direction) < 0.1f) return false;

        // Get ledge detection parameters from customiser or use improved defaults.
        float ledgeCheckDistance = 1.0f;
        float ledgeCheckDepth = 2.0f;
        float ledgeCheckOffset = 0.2f;
        int rayCount = 3;
        float maxSafeDropDistance = 1.0f;
        float safeGroundThreshold = 0.5f;

        if(pawnCustomiser != null)
        {
            ledgeCheckDistance = pawnCustomiser.platformerMovement.edgeCheckOffset;
            ledgeCheckDepth = pawnCustomiser.platformerMovement.edgeRaycastDistance;
            ledgeCheckOffset = pawnCustomiser.platformerMovement.edgeCheckVerticalOffset;
            rayCount = pawnCustomiser.platformerMovement.ledgeDetectionRayCount;
            maxSafeDropDistance = pawnCustomiser.platformerMovement.maxSafeDropDistance;
            safeGroundThreshold = pawnCustomiser.platformerMovement.safeGroundThreshold;
        }

        Vector2 pawnPosition = transform.position;
        
        // Use a robust fixed detection width instead of relying on pawn's collider size
        // This ensures consistent detection regardless of pawn size
        float detectionWidth = 0.8f; // Fixed robust detection width
        
        // Cast multiple rays across the detection width for comprehensive coverage
        int safeGroundHits = 0;
        float stepSize = rayCount > 1 ? detectionWidth / (rayCount - 1) : 0f;
        
        for(int i = 0; i < rayCount; i++)
        {
            float rayOffset = rayCount > 1 ? -detectionWidth * 0.5f + (i * stepSize) : 0f;
            Vector2 rayStart = pawnPosition + new Vector2(direction * ledgeCheckDistance + rayOffset, -ledgeCheckOffset);
            
            RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, ledgeCheckDepth, groundLayer);
            
            if(hit.collider != null)
            {
                // Check if the ground is at a safe distance
                float dropDistance = Mathf.Abs(hit.point.y - (pawnPosition.y - ledgeCheckOffset));
                if(dropDistance <= maxSafeDropDistance)
                {
                    safeGroundHits++;
                    Debug.DrawRay(rayStart, Vector2.down * hit.distance, Color.green, 0.1f);
                } else
                {
                    // Ground exists but it's too far down (dangerous drop)
                    Debug.DrawRay(rayStart, Vector2.down * hit.distance, Color.yellow, 0.1f);
                }
            } else
            {
                // No ground detected
                Debug.DrawRay(rayStart, Vector2.down * ledgeCheckDepth, Color.red, 0.1f);
            }
        }
        
        // Additional comprehensive safety checks with multiple forward positions
        bool hasAdequateForwardSupport = false;
        int forwardSupportChecks = 3;
        int forwardSupportHits = 0;
        
        for(int i = 0; i < forwardSupportChecks; i++)
        {
            float forwardDistance = ledgeCheckDistance * (0.5f + (i * 0.2f)); // Check at 0.5x, 0.7x, 0.9x distance
            Vector2 forwardCheck = pawnPosition + new Vector2(direction * forwardDistance, 0f);
            RaycastHit2D forwardHit = Physics2D.Raycast(forwardCheck, Vector2.down, ledgeCheckOffset + 0.5f, groundLayer);
            
            if(forwardHit.collider != null)
            {
                forwardSupportHits++;
                Debug.DrawRay(forwardCheck, Vector2.down * forwardHit.distance, Color.blue, 0.1f);
            } else
            {
                Debug.DrawRay(forwardCheck, Vector2.down * (ledgeCheckOffset + 0.5f), Color.magenta, 0.1f);
            }
        }
        
        hasAdequateForwardSupport = forwardSupportHits >= (forwardSupportChecks / 2);
        
        // Enhanced ledge detection with multiple criteria:
        // 1. Safe ground percentage check
        float safeGroundPercentage = (float)safeGroundHits / rayCount;
        bool insufficientSafeGround = safeGroundPercentage < safeGroundThreshold;
        
        // 2. Forward support check
        bool inadequateForwardSupport = !hasAdequateForwardSupport;
        
        // 3. Additional edge case: Check for narrow platform detection
        // Cast a wider ray pattern to detect very narrow platforms
        bool narrowPlatformDetected = false;
        if(insufficientSafeGround)
        {
            // Check for narrow platforms with wider spacing
            for(float wideOffset = -0.6f; wideOffset <= 0.6f; wideOffset += 0.3f)
            {
                Vector2 wideRayStart = pawnPosition + new Vector2(direction * ledgeCheckDistance + wideOffset, -ledgeCheckOffset);
                RaycastHit2D wideHit = Physics2D.Raycast(wideRayStart, Vector2.down, ledgeCheckDepth, groundLayer);
                
                if(wideHit.collider != null)
                {
                    float dropDistance = Mathf.Abs(wideHit.point.y - (pawnPosition.y - ledgeCheckOffset));
                    if(dropDistance <= maxSafeDropDistance)
                    {
                        narrowPlatformDetected = true;
                        Debug.DrawRay(wideRayStart, Vector2.down * wideHit.distance, Color.cyan, 0.1f);
                        break;
                    }
                }
            }
        }
        
        // Final ledge determination with robust logic
        bool isLedge = (insufficientSafeGround && !narrowPlatformDetected) || inadequateForwardSupport;
        
        return isLedge;
    }

    #endregion

    #region Chess Mode Methods

    /// Choose a neighbour target using this pawn's q,r and the given player's q,r.
    /// Returns true and out coords if a target was chosen.
    /// Respects occupied/reserved tiles, occupied may be null; player tile (playerQ,playerR) is always allowed (so capture works).
    public bool ChooseMoveTarget(int playerQ, int playerR, HashSet<Vector2Int> occupied, out int outQ, out int outR)
    {
        outQ = 0; outR = 0;
        if(gridGenerator == null)
        {
            Debug.LogWarning("[PawnController] ChooseMoveTarget: gridGenerator is null.");
            return false;
        }

        // Build neighbour candidates that actually exist as tiles and exclude the player's tile.
        List<Candidate> candidates = new List<Candidate>();
        for(int i = 0; i < 6; i++)
        {
            int nq = q + PlayerController.HEX_DIR_Q[i];
            int nr = r + PlayerController.HEX_DIR_R[i];
            if(!TileExists(nq, nr)) continue;
            Candidate c = new Candidate() { q = nq, r = nr, index = i };
            candidates.Add(c);
        }
        if(candidates.Count == 0) return false;
        // Compute distances to player for all candidates (axial distance via cube coords).
        foreach(var c in candidates) c.distToPlayer = AxialDistance(c.q, c.r, playerQ, playerR);
        // Apply the single combined weight logic for this pawn's AI type.
        ApplyCombinedWeights(candidates, aiType, playerQ, playerR);

        // Debug: log candidate list and weights for transparency.
        string debugLine = $"[PawnController] {typeLabel} @{q}_{r} Candidates:";
        foreach(var c in candidates) debugLine += $" ({c.q}_{c.r}:w={c.weight},d={c.distToPlayer},idx={c.index})";
        Debug.Log(debugLine);

        // Remove candidates that are occupied by other opponents, unless it's the player's tile (allow capture).
        List<Candidate> allowed = new List<Candidate>();
        foreach(var c in candidates)
        {
            var coord = new Vector2Int(c.q, c.r);
            bool isPlayerTile = (c.q == playerQ && c.r == playerR);
            if(occupied != null && occupied.Contains(coord) && !isPlayerTile) continue;
            if(c.weight <= 0f) continue;
            allowed.Add(c);
        }
        if(allowed.Count == 0) return false;
        // Weighted random selection amongst allowed.
        float sum = 0f;
        foreach(var c in allowed) sum += c.weight;
        float pick = Random.Range(0f, sum);
        float acc = 0f;
        foreach(var c in allowed)
        {
            acc += c.weight;
            if(pick <= acc)
            {
                outQ = c.q; outR = c.r;
                Debug.Log($"[PawnController] {typeLabel} @{q}_{r} chose target {outQ}_{outR} (pick={pick:F3} sum={sum:F3}).");
                return true;
            }
        }
        // Fallback.
        outQ = allowed[allowed.Count - 1].q; outR = allowed[allowed.Count - 1].r;
        Debug.Log($"[PawnController] {typeLabel} @{q}_{r} fallback chose {outQ}_{outR}.");
        return true;
    }

    /// Combined weight application for all AI types in one place (Chess mode movement strategy).
    private void ApplyCombinedWeights(List<Candidate> candidates, AIType type, int playerQ, int playerR)
    {
        if(candidates == null || candidates.Count == 0) return;
        // Find min/max distances amongst candidates to compare relative distances to player.
        int minDist = int.MaxValue; int maxDist = int.MinValue;
        foreach(var c in candidates) { minDist = Mathf.Min(minDist, c.distToPlayer); maxDist = Mathf.Max(maxDist, c.distToPlayer); }

        // Use Basic AI behaviour if no customiser assigned.
        if(pawnCustomiser == null)
        {
            // Without customiser, use simple distance-based weighting for all AI types.
            foreach(var c in candidates) c.weight = (c.distToPlayer == minDist) ? 5f : 1f;
            return;
        }

        // Get weights from customiser for this AI type.
        PawnCustomiser.AITypeWeights weights = pawnCustomiser.GetChessWeights(type);

        switch(type)
        {
            case AIType.Basic:
                // Like chess pawns: only move forward (down in world space), never backward (up).
                // Restricted to bottom 3 directions: (0,-1), (-1,0), (1,-1) - can't move upward.
                foreach(var c in candidates) c.weight = 0f;

                // Get current tile world position for Y-axis comparison (world space).
                Vector3 currentWorldPos;
                if(!TryGetTileWorldCentre(q, r, out currentWorldPos))
                {
                    // Fallback to allowing all 3 directions if can't get world pos (directional check only).
                    foreach(var c in candidates)
                    {
                        int dq = c.q - q; int dr = c.r - r;
                        // Allow only: (0,-1) bottom, (-1,0) bottom-left, (1,-1) bottom-right.
                        if((dq == 0 && dr == -1) || (dq == -1 && dr == 0) || (dq == 1 && dr == -1)) c.weight = 1f;
                    }
                    break;
                }

                foreach(var c in candidates)
                {
                    // Get candidate tile world position to compare Y values.
                    Vector3 candidateWorldPos;
                    if(!TryGetTileWorldCentre(c.q, c.r, out candidateWorldPos))
                    {
                        c.weight = 0f;
                        continue;
                    }

                    // Block any move that increases Y (moving backward/upward in world space).
                    if(candidateWorldPos.y > currentWorldPos.y)
                    {
                        c.weight = 0f;
                        continue;
                    }

                    int dq = c.q - q; int dr = c.r - r;
                    // Check if direction is one of the allowed 3 bottom directions.
                    // bottom = (0,-1); bottom-left = (-1,0); bottom-right = (1,-1).
                    if((dq == 0 && dr == -1) || (dq == -1 && dr == 0) || (dq == 1 && dr == -1)) c.weight = 1f;
                }
                // Give extra weight to allowed tile(s) that are closest to player (prioritise approaching).
                float bestDist = float.PositiveInfinity;
                foreach(var c in candidates) if(c.weight > 0f) bestDist = Mathf.Min(bestDist, c.distToPlayer);
                foreach(var c in candidates) if(c.weight > 0f && c.distToPlayer == bestDist) c.weight = weights.closestWeight;
                break;

            case AIType.Handcannon:
                // All 6 allowed; closest to player has higher weight, others lower.
                foreach(var c in candidates) c.weight = (c.distToPlayer == minDist) ? weights.closestWeight : weights.farthestWeight;
                break;

            case AIType.Shotgun:
                // Aggressive towards player with directional preferences (tries to get close).
                foreach(var c in candidates)
                {
                    // Determine hex direction index by calculating offset from current position.
                    int dq = c.q - q;
                    int dr = c.r - r;
                    int dirIndex = -1;
                    for(int i = 0; i < 6; i++)
                    {
                        if(PlayerController.HEX_DIR_Q[i] == dq && PlayerController.HEX_DIR_R[i] == dr)
                        {
                            dirIndex = i;
                            break;
                        }
                    }

                    // Assign weights with distance and directional priority.
                    if(c.distToPlayer == minDist) c.weight = weights.closestWeight;
                    else if(c.distToPlayer == maxDist) c.weight = weights.farthestWeight;
                    else if(dirIndex == 1 || dirIndex == 2) c.weight = weights.diagonalWeight; // Diagonal upper.
                    else if(dirIndex == 4 || dirIndex == 5) c.weight = weights.sideWeight; // Side moves.
                    else c.weight = weights.farthestWeight; // Pure horizontal moves.
                }
                break;

            case AIType.Sniper:
                // Defensive positioning: Prefer farthest distance from player (keeps distance for long-range shots).
                foreach(var c in candidates)
                {
                    if(c.distToPlayer == maxDist) c.weight = weights.farthestWeight; // Far = safe sniping position.
                    else if(c.distToPlayer == minDist) c.weight = weights.closestWeight; // Close = dangerous to sniper.
                    else c.weight = weights.diagonalWeight; // Medium = balanced (using diagonal weight as medium).
                }
                break;
        }
    }

    /// Execute this pawn's AI move using the pawn's own q,r and the player's q,r.
    /// Returns true if a move was started.
    public bool ExecuteAIMoveTo(int targetQ, int targetR)
    {
        if(gridGenerator == null)
        {
            Debug.LogWarning("[PawnController] ExecuteAIMoveTo: gridGenerator is null.");
            return false;
        }
        Vector3 world;
        if(!TryGetTileWorldCentre(targetQ, targetR, out world)) return false;
        // Start coroutine to move and update q,r after movement.
        StartCoroutine(AIMoveCoroutine(world, targetQ, targetR));
        return true;
    }

    /// Coroutine that moves the pawn and updates its tracked q,r when complete.
    private IEnumerator AIMoveCoroutine(Vector3 targetWorld, int targetQ, int targetR)
    {
        Moved = false;
        int fromQ = q; int fromR = r;
        Vector3 start = transform.position;
        float t = 0f;
        float moveDuration = 0.12f; // Basic AI default.
        if(pawnCustomiser != null) moveDuration = pawnCustomiser.aiThinking.chessMoveAnimationDuration;

        if((targetWorld - start).sqrMagnitude < 0.0001f)
        {
            transform.position = targetWorld;
            q = targetQ; r = targetR; Moved = true; UpdateNameWithCoords();
            Debug.Log($"[PawnController] {typeLabel} moved (instant) {fromQ}_{fromR} -> {q}_{r}");
            yield break;
        }
        while(t < moveDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / moveDuration);
            transform.position = Vector3.Lerp(start, targetWorld, a);
            yield return null;
        }

        transform.position = targetWorld;
        q = targetQ; r = targetR; Moved = true; UpdateNameWithCoords();

        // After this pawn finishes moving, tries to capture player.
        GameObject player = GameObject.FindWithTag("Player");
        if(player != null)
        {
            PawnHealth playerPawn = player.GetComponent<PawnHealth>();
            PlayerController playerController = player.GetComponent<PlayerController>();
            if(playerPawn != null && playerController.q == q && playerController.r == r)
            {
                // Deal damage equal to this Player's MaxHP (PawnHealth component).
                playerPawn.TakeDamage(playerPawn.MaxHP, $"Opponent_{aiType}");
            }
        }

        Debug.Log($"[PawnController] {typeLabel} moved {fromQ}_{fromR} -> {q}_{r}");
    }

    /// Set initial coordinates and optionally reposition pawn to tile centre (Chess mode only).
    public void SetCoordsAndSnap(int startQ, int startR)
    {
        q = startQ; r = startR;

        // Only snap to tile position in Chess mode, not in Standoff mode
        if (!isStandoffMode)
        {
            Vector3 world;
            if(TryGetTileWorldCentre(q, r, out world)) transform.position = world;
        }

        UpdateNameWithCoords();
        Debug.Log($"[PawnController] {typeLabel} initialised at {q}_{r}");
    }

    /// Utility: update GameObject name to include axial coords for debugging.
    private void UpdateNameWithCoords()
    {
        // Use aiType for readable label.
        string modifierLabel = modifier != Modifier.None ? $" [{modifier}]" : "";
        this.gameObject.name = $"Opponent {aiType}{modifierLabel}: {q}_{r}";
    }

    #endregion

    #region Modifier System

    /// Set the modifier for this pawn and update visual display.
    public void SetModifier(Modifier newModifier)
    {
        modifier = newModifier;
        UpdateModifierIcon();
        UpdateNameWithCoords();

        // Apply modifier effects to other components.
        ApplyModifierEffects();
    }

    /// Update the modifier icon UI display.
    private void UpdateModifierIcon()
    {
        // If no modifier assigned, destroy the Canvas child GameObject entirely
        if (modifier == Modifier.None)
        {
            // Find and destroy any Canvas child GameObject
            Canvas canvasChild = GetComponentInChildren<Canvas>();
            if (canvasChild != null)
            {
                Destroy(canvasChild.gameObject);
                Debug.Log($"[PawnController] Removed Canvas child from {gameObject.name} (no modifier)");
            }
            return;
        }

        // If modifier is assigned, ensure Canvas is enabled
        Canvas canvas = GetComponentInChildren<Canvas>(true); // Include inactive children
        if (canvas != null)
        {
            canvas.gameObject.SetActive(true);
        }

        if(modifierIconImage == null) return;

        // Get modifier icon from Pawn Customiser (centralised icon storage).
        Sprite iconSprite = null;
        if(pawnCustomiser != null) iconSprite = pawnCustomiser.GetModifierIcon(modifier);

        // Display icon if found, otherwise hide the image.
        if(iconSprite != null)
        {
            modifierIconImage.sprite = iconSprite;
            modifierIconImage.enabled = true;
            modifierIconImage.gameObject.SetActive(true);
        }
        else
        {
            modifierIconImage.sprite = null;
            modifierIconImage.enabled = false;
            modifierIconImage.gameObject.SetActive(false);
        }
    }

    /// Apply modifier effects to components (health, weapon, movement).
    private void ApplyModifierEffects()
    {
        // Basic AI has no modifiers.
        if(pawnCustomiser == null) return;

        // Apply Tenacious: Multiply opponent HP (floor to int).
        if(modifier == Modifier.Tenacious)
        {
            PawnHealth pawnHealth = GetComponent<PawnHealth>();
            if(pawnHealth != null && pawnHealth.pawnType == PawnHealth.PawnType.Opponent)
            {
                float multiplier = pawnCustomiser.modifierEffects.tenaciousHPMultiplier;
                int multipliedHP = Mathf.FloorToInt(pawnHealth.GetCurrentHP() * multiplier);
                pawnHealth.SetOpponentHP(multipliedHP);
            }
        }

        // Apply modifier effects to weapon system.
        if(weaponSystem != null) weaponSystem.ApplyModifier(modifier);
    }

    /// Get the fire interval multiplier based on modifier (Standoff mode only).
    public float GetFireIntervalMultiplier()
    {
        // Basic AI has no modifiers.
        if(pawnCustomiser == null) return 1.0f;
        return pawnCustomiser.GetFireIntervalMultiplier(modifier);
    }

    /// Get the firing delay multiplier based on modifier (Standoff mode only).
    public float GetFiringDelayMultiplier()
    {
        // Basic AI has no modifiers.
        if(pawnCustomiser == null) return 1.0f;
        return pawnCustomiser.GetFiringDelayMultiplier(modifier);
    }

    /// Check if this pawn should get an extra move in Chess mode (Fleet modifier only).
    public bool HasExtraMove()
    {
        // Fleet modifier: Gets 2 moves per turn instead of 1 (but only shoots once at turn start).
        return modifier == Modifier.Fleet;
    }

    /// Check if bullets should only damage the player (Observant modifier, Chess mode only).
    public bool BulletsOnlyDamagePlayer()
    {
        // Observant modifier (Chess): Bullets pass through opponents, only damage player.
        // Useful for tactical positioning without friendly fire risk.
        return modifier == Modifier.Observant && !isStandoffMode;
    }

    /// Check if this pawn should recalculate aim after player moves (Reflexive modifier, Chess mode only).
    public bool ShouldRecalculateAimAfterPlayerMove()
    {
        // Reflexive modifier (Chess): After player moves, this pawn recalculates best aim direction.
        // Gives tactical advantage by always aiming at player's new position.
        return modifier == Modifier.Reflexive && !isStandoffMode;
    }

    /// Check if this pawn should fire when entering LOS (Confrontational modifier, Chess mode only).
    public bool ShouldFireOnLineOfSight()
    {
        // Confrontational modifier (Chess): Shoots not just at turn start, but also when
        // another piece enters their line of sight (in addition to regular turn firing).
        return modifier == Modifier.Confrontational && !isStandoffMode;
    }

    /// Check if gun should be fixed on player (Reflexive modifier, Standoff mode only).
    public bool ShouldFixGunOnPlayer()
    {
        // Reflexive modifier (Standoff): Gun tracks player instantly without lerp/angular velocity.
        // Combined with reduced firing delay for rapid targeting.
        return modifier == Modifier.Reflexive && isStandoffMode;
    }

    /// Convert Basic to Handcannon (used when last opponent enters standoff).
    public void ConvertBasicToHandcannon()
    {
        if(aiType == AIType.Basic)
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
        if(tile == null) return false;
        PolygonCollider2D polygonCollider = tile.GetComponent<PolygonCollider2D>();
        if(polygonCollider != null)
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