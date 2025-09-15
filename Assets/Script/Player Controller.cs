using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// Controls pawn movement driven by swipe anywhere on the board using EnhancedTouch.
/// Arrows are fixed on the pawn and selected based on swipe direction while the pointer is held.
public class PlayerController : MonoBehaviour
{
    // Current axial coordinates for this pawn.
    public int q, r;
    // Movement animation duration in seconds.
    public float moveDuration = 0.12f;
    // Dead zone in screen pixels (swipes shorter than this are ignored for movement).
    public float deadZonePixels = 24f;
    // Reference to the grid generator for tile lookup and tileSize data.
    public HexGridGenerator gridGenerator;
    // Reference to the chequerboard for tile lookup and tileSize data.
    public Checkerboard checkerboard;
    // Array of 6 arrow GameObjects in clockwise order starting from 12 o'clock.
    public GameObject[] directionArrows = new GameObject[6];
    // Cached camera reference for screen->world conversion.
    private Camera cam;
    // Neighbour axial directions for axial coords (axial deltas must match your axial system).
    private readonly int[] dirQ = { 1, 1, 0, -1, -1, 0 };
    private readonly int[] dirR = { 0, -1, -1, 0, 1, 1 };
    // Input tracking.
    private Vector2 swipeStartScreen;
    private Vector3 swipeStartWorld;
    private bool isPointerDown = false;

    // Enable EnhancedTouch when this component is enabled to get reliable touch phases.
    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    // Disable EnhancedTouch when this component is disabled.
    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    // Initialise the pawn with starting coords and a reference to the grid generator.
    // Call this from your spawner after parsing q,r.
    public void Initialise(int startQ, int startR, HexGridGenerator generator, Checkerboard checkerboard)
    {
        q = startQ;
        r = startR;
        gridGenerator = generator;
        cam = Camera.main;
        checkerboard.RegisterPlayer(this);
        // Try to position pawn at the tile's collider centre if present.
        Transform tileParent = gridGenerator != null && gridGenerator.parentContainer != null ? gridGenerator.parentContainer : gridGenerator.transform;
        Transform tile = tileParent.Find($"Hex_{q}_{r}");
        if (tile != null)
        {
            PolygonCollider2D pc = tile.GetComponent<PolygonCollider2D>();
            if (pc != null)
            {
                // Place pawn exactly at the collider's world-space centre so visual alignment matches physics/collider.
                Vector3 centre = pc.bounds.center;
                transform.position = new Vector3(centre.x, centre.y, transform.position.z);
                return;
            }
            // Fallback: use tile transform position if no collider exists.
            transform.position = tile.position;
            return;
        }
        Debug.LogWarning("[PlayerController] Initialise: Could not find tile transform for given coords, leaving pawn at current transform.");
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;
        HideAllArrows();
    }

    private void Update()
    {
        // Prefer EnhancedTouch active touches for mobile.
        var active = Touch.activeTouches;
        if (active.Count > 0)
        {
            // Use the first active touch for single-finger control.
            Touch t = active[0];
            switch (t.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    BeginPointer(t.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                    // Update arrow live while finger moves or holds on screen.
                    if (isPointerDown) UpdateArrowForScreenPos(t.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    EndPointer(t.screenPosition);
                    break;
            }
            return;
        }

        // Editor / desktop fallback using Mouse for convenience.
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) BeginPointer(mouse.position.ReadValue());
            if (mouse.leftButton.isPressed && isPointerDown) UpdateArrowForScreenPos(mouse.position.ReadValue());
            if (mouse.leftButton.wasReleasedThisFrame) EndPointer(mouse.position.ReadValue());
        }
    }

    // Called on pointer/touch start.
    private void BeginPointer(Vector2 screenPos)
    {
        // Prevent player input during opponent turn
        if (Checkerboard.Instance != null && !Checkerboard.Instance.IsPlayerTurn()) return;

        isPointerDown = true;
        swipeStartScreen = screenPos;
        // Record a world-space start point on the pawn's Z plane so direction is computed in world space.
        swipeStartWorld = ScreenToWorldPointOnZ(transform.position.z, screenPos);
        // Immediately show arrow based on any movement from the start (gives instant feedback).
        UpdateArrowForScreenPos(screenPos);
    }

    // Called on pointer/touch end.
    private void EndPointer(Vector2 screenEnd)
    {
        if (!isPointerDown) return;
        isPointerDown = false;

        Vector2 deltaScreen = screenEnd - swipeStartScreen;
        // If swipe is smaller than dead zone, hide arrow and ignore movement.
        if (deltaScreen.magnitude < deadZonePixels)
        {
            HideAllArrows();
            return;
        }

        // Convert end screen to world on pawn Z plane and compute swipe world vector.
        Vector3 endWorld = ScreenToWorldPointOnZ(transform.position.z, screenEnd);
        Vector3 swipeWorld = endWorld - swipeStartWorld;
        if (swipeWorld.sqrMagnitude < Mathf.Epsilon)
        {
            HideAllArrows();
            return;
        }

        // Use the swipeWorld direction to pick the best neighbouring tile.
        // We use dot-product alignment between swipe direction and the vector to each neighbour centre.
        Vector3 currentWorld = transform.position;
        int bestIndex = -1;
        float bestDot = float.NegativeInfinity;
        for (int i = 0; i < 6; i++)
        {
            int nq = q + dirQ[i];
            int nr = r + dirR[i];
            Vector3 neighbourWorld;
            if (TryGetTileWorldCentre(nq, nr, out neighbourWorld))
            {
                // Construct neighbour direction vector and normalise for dot product.
                Vector3 dirVec = neighbourWorld - currentWorld;
                float len = dirVec.magnitude;
                if (len <= Mathf.Epsilon) continue;
                dirVec /= len;
                Vector3 swipeDir = swipeWorld.normalized;
                float dot = Vector3.Dot(swipeDir, dirVec); // 1 => same direction
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestIndex = i;
                }
            }
        }

        // No valid neighbour found (swipe pointed off-board) — hide arrows and ignore.
        if (bestIndex == -1)
        {
            HideAllArrows();
            return;
        }

        int targetQ = q + dirQ[bestIndex];
        int targetR = r + dirR[bestIndex];

        Vector3 targetWorld;
        if (!TryGetTileWorldCentre(targetQ, targetR, out targetWorld))
        {
            HideAllArrows();
            return; // off-board
        }

        // Keep arrow visible during movement; MoveTo will hide it at the end.
        StartCoroutine(MoveTo(targetWorld, targetQ, targetR));
    }

    // Try to get the world-space centre of a tile's collider; returns false if tile not found.
    private bool TryGetTileWorldCentre(int qa, int ra, out Vector3 centre)
    {
        centre = Vector3.zero;
        if (gridGenerator == null)
        {
            Debug.LogWarning("[PlayerController] TryGetTileWorldCentre: gridGenerator is null.");
            return false;
        }
        Transform tileParent = gridGenerator.parentContainer == null ? gridGenerator.transform : gridGenerator.parentContainer;
        Transform tile = tileParent.Find($"Hex_{qa}_{ra}");
        if (tile == null) return false;
        PolygonCollider2D pc = tile.GetComponent<PolygonCollider2D>();
        if (pc != null)
        {
            Vector3 c = pc.bounds.center;
            centre = new Vector3(c.x, c.y, transform.position.z);
            return true;
        }
        // Fallback to transform position if there is no collider.
        centre = tile.position;
        return true;
    }

    // Coroutine to animate movement to target position smoothly, then hide arrows when done.
    private IEnumerator MoveTo(Vector3 targetPos, int targetQ, int targetR)
    {
        // Update axial coords at start so any code reading them during movement sees new coords.
        q = targetQ;
        r = targetR;

        Vector3 start = transform.position;
        float t = 0f;
        if ((targetPos - start).sqrMagnitude < 0.0001f)
        {
            transform.position = targetPos;
            HideAllArrows();
            // After movement, attempt capture using Checkerboard's cached opponents.
            TryPlayerCaptureAt(q, r);
            // Notify Checkerboard that player has moved.
            if (Checkerboard.Instance != null) Checkerboard.Instance.OnPlayerMoved();
            yield break;
        }
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / moveDuration);
            transform.position = Vector3.Lerp(start, targetPos, a);
            yield return null;
        }
        transform.position = targetPos;
        // Hide arrow after completing motion.
        HideAllArrows();

        // Ask Checkerboard for all opponent locations (and controllers) stored and log them.
        if (Checkerboard.Instance != null)
        {
            var oppCoords = Checkerboard.Instance.GetOpponentCoords();
            string s = "[PlayerController] Opponent coords:";
            foreach (var vc in oppCoords) s += $" {vc.x}_{vc.y};";
            Debug.Log(s);
        }

        // Attempt to capture any opponent present at the landing coords.
        TryPlayerCaptureAt(q, r);

        // Call Checkerboard after player's move so opponents take their turns.
        if (Checkerboard.Instance != null) Checkerboard.Instance.OnPlayerMoved();
    }

    // Used by MoveTo to resolve player capture by checking Checkerboard's cached opponents.
    private void TryPlayerCaptureAt(int coordQ, int coordR)
    {
        // Get opponent controllers from Checkerboard to avoid expensive Find calls.
        if (Checkerboard.Instance == null) return;
        var opps = Checkerboard.Instance.GetOpponentControllers();
        foreach (var opp in opps)
        {
            if (opp == null) continue;
            if (opp.q == coordQ && opp.r == coordR)
            {
                // Found an opponent at this tile; attempt capture.
                OpponentPawn opPawn = opp.GetComponent<OpponentPawn>();
                if (opPawn != null)
                {
                    // Capture deals damage equal to opponent HP (this kills 1HP opponents).
                    opPawn.TakeDamage(opPawn.HP, "Player");
                }
                else
                {
                    // If OpponentPawn not present, destroy PawnController GameObject as fallback.
                    Debug.LogWarning("[PlayerController] Opponent missing OpponentPawn component - destroying controller object.");
                    Destroy(opp.gameObject);
                }
                // Stop after first occupant (adjust if stacking allowed and you want different behaviour).
                break;
            }
        }
    }

    // Convert a screen point to a world point on a plane at specified world Z.
    private Vector3 ScreenToWorldPointOnZ(float worldZ, Vector2 screenPos)
    {
        if (cam == null) cam = Camera.main;
        float z = worldZ - cam.transform.position.z; // distance forward from camera to the plane at worldZ
        Vector3 sp = new Vector3(screenPos.x, screenPos.y, z);
        return cam.ScreenToWorldPoint(sp);
    }

    // Update the fixed-on-pawn arrow selection based on a screen position (pointer) while dragging.
    private void UpdateArrowForScreenPos(Vector2 screenPos)
    {
        // Convert pointer screen position to world on the pawn Z plane.
        Vector3 world = ScreenToWorldPointOnZ(transform.position.z, screenPos);
        // Compute swipe vector in world space from the initial touch point.
        Vector3 swipeWorld = world - swipeStartWorld;
        // Hide arrows if the swipe is negligible.
        if (swipeWorld.sqrMagnitude < 0.000001f)
        {
            HideAllArrows();
            return;
        }
        // Determine sector index (0..5) using swipeWorld direction.
        int sector = SectorIndexForDirection(swipeWorld.normalized);
        // Enable only the arrow corresponding to the computed sector.
        ShowArrowAtIndex(sector);
    }

    // Return the sector index (0..5) of the direction vector using the six compass centres
    // ordered clockwise starting at 12 o'clock (index 0 = 12 o'clock, 1 = 2 o'clock, ...).
    private int SectorIndexForDirection(Vector3 dirVec)
    {
        // Compute world angle in degrees where 0 deg = +X, 90 deg = +Y.
        float angleDeg = Mathf.Atan2(dirVec.y, dirVec.x) * Mathf.Rad2Deg;
        if (angleDeg < 0f) angleDeg += 360f;
        // Centres: 90 (12), 30 (2), 330 (4), 270 (6), 210 (8), 150 (10).
        float[] centres = { 90f, 30f, 330f, 270f, 210f, 150f };
        int best = 0;
        float bestDist = Mathf.Abs(Mathf.DeltaAngle(angleDeg, centres[0]));
        for (int i = 1; i < 6; i++)
        {
            float d = Mathf.Abs(Mathf.DeltaAngle(angleDeg, centres[i]));
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    // Enable only the arrow at the given index (arrows are expected to be children of the pawn).
    private void ShowArrowAtIndex(int index)
    {
        if (directionArrows == null || directionArrows.Length < 6) return;
        for (int i = 0; i < 6; i++)
        {
            var go = directionArrows[i];
            if (go == null) continue;
            go.SetActive(i == index);
        }
    }

    // Disable all arrow GameObjects if present.
    private void HideAllArrows()
    {
        if (directionArrows == null) return;
        for (int i = 0; i < directionArrows.Length && i < 6; i++)
        {
            if (directionArrows[i] == null) continue;
            directionArrows[i].SetActive(false);
        }
    }
}