using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Camera helper that recentres and zooms an orthographic Camera so all HexGrids fit on screen.
/// Attach to the Main Camera (orthographic) and either assign grid containers or allow auto-discovery.
public class FollowCamera : MonoBehaviour
{
    // When true the script will find all HexGridGenerator instances and include their parentContainer.
    public bool autoFindGrids = true;
    // Optional explicit list of grid containers to include; if empty and autoFindGrids is true the script will discover grids automatically.
    public List<Transform> gridContainers = new List<Transform>();
    // Extra padding (world units) added around the combined bounds of all grids to avoid edge clipping.
    public float padding = 1f;
    // Minimum orthographic half-height (orthographicSize) to avoid over-zooming.
    public float minOrthographicSize = 2f;
    // Maximum orthographic half-height (orthographicSize) to avoid under-zooming.
    public float maxOrthographicSize = 100f;
    // Smooth time for position and size damping; set to 0 for instant snapping.
    public float smoothTime = 0.25f;
    // When true the camera will smooth its movement and size changes.
    public bool smoothFollow = true;
    // When true the camera recalculates every frame; set false to recalc only when ForceRecalculate() is called.
    public bool continuousUpdate = true;

    // Zoom pulse configuration.
    public float pulseDefaultMultiplier = 2f; // Default multiplier when triggered with no args
    public float pulseDefaultDuration = 0.9f; // Default total pulse time in seconds
    public float pulseHoldFraction = 0.2f; // Fraction of total duration spent holding at max size
    // Pulse scaling configuration.
    public float pulseMultiplierPerKill = 0.5f; // Extra multiplier added per kill (linear)
    public float pulseMaxMultiplier = 4f; // Maximum multiplier clamp for safety
    public float killWindow = 0.2f; // Seconds to wait for additional kills before firing the pulse

    // Singleton instance for easy calls from other scripts.
    public static FollowCamera Instance { get; private set; }

    // Cached camera reference.
    private new Camera camera;
    // Velocity vector used by SmoothDamp for position smoothing.
    private Vector3 positionVelocity = Vector3.zero;
    // Velocity float used by SmoothDamp for size smoothing.
    private float sizeVelocity = 0f;
    // Pulse state.
    private bool isPulseActive = false;
    // Aggregation window to combine several kills into one larger pulse.
    private int pendingKills = 0; // Accumulator inside the aggregation window
    private Coroutine killWindowCoroutine = null; // Coroutine handle for kill aggregation

    // Initialise and optionally perform the first recalc.
    private void Awake()
    {
        // Singleton assignment
        if (Instance != null && Instance != this) { Destroy(this.gameObject); return; }
        Instance = this;
    }

    // Initialise and optionally perform the first recalc.
    private void Start()
    {
        camera = GetComponent<Camera>() ?? Camera.main;
        if (camera == null)
        {
            Debug.LogError("[FollowCamera] No Camera found on this GameObject and Camera.main is null.");
            enabled = false;
            return;
        }
        if (!camera.orthographic)
        {
            Debug.LogWarning("[FollowCamera] Camera is not orthographic. Behaviour is designed for orthographic cameras.");
        }
        // Subscribe to game state changes to refresh grid containers when entering gameplay modes
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged.AddListener(OnGameStateChanged);
        }
        // If no explicit containers provided and auto-find is enabled, discover grids now.
        if (autoFindGrids && (gridContainers == null || gridContainers.Count == 0)) AutoDiscoverGridContainers();
        // Perform initial calculation immediately so camera starts correctly.
        ForceRecalculate();
    }

    // Handle game state changes to refresh grid discovery when tiles become active.
    private void OnGameStateChanged(GameManager.GameState newState)
    {
        // Refresh grid containers when entering Chess or Standoff modes
        if (newState == GameManager.GameState.ChessMode || newState == GameManager.GameState.Standoff)
        {
            // Delay refresh to allow tiles to fully activate
            StartCoroutine(DelayedGridRefresh());
        }
    }

    // Coroutine to wait one frame before refreshing grids (ensures tiles are fully enabled).
    private IEnumerator DelayedGridRefresh()
    {
        // Wait for end of frame to ensure all tiles are enabled
        yield return new WaitForEndOfFrame();
        // Re-discover grid containers now that tiles should be active
        if (autoFindGrids) AutoDiscoverGridContainers();
        // Force recalculate bounds and camera position
        ForceRecalculate();
    }

    // Update per-frame when continuousUpdate is enabled.
    private void LateUpdate()
    {
        if (!continuousUpdate) return;
        RecalculateAndApply();
    }

    // Public method to force a recalculation and apply new camera transform immediately (or smoothly depending on settings).
    public void ForceRecalculate()
    {
        // Refresh discovery if auto-find enabled
        if (autoFindGrids) AutoDiscoverGridContainers();
        RecalculateAndApply();
    }

    // Discover HexGridGenerator instances and use their parentContainer (or the generator transform) as grid containers.
    // Also discovers Platform generator for Standoff mode arena bounds.
    private void AutoDiscoverGridContainers()
    {
        gridContainers = new List<Transform>();

        // Find HexGridGenerator containers (Chess mode)
        HexGridGenerator[] gens = Object.FindObjectsByType<HexGridGenerator>(FindObjectsSortMode.InstanceID);
        foreach (var g in gens)
        {
            if (g == null || !g.gameObject.activeInHierarchy) continue;
            if (g.parentContainer != null && g.parentContainer.gameObject.activeInHierarchy)
                gridContainers.Add(g.parentContainer);
            else if (g.gameObject.activeInHierarchy)
                gridContainers.Add(g.transform);
        }

        // Find Platform containers (Standoff mode)
        Platform[] platforms = Object.FindObjectsByType<Platform>(FindObjectsSortMode.InstanceID);
        foreach (var p in platforms)
        {
            if (p == null || !p.gameObject.activeInHierarchy) continue;
            // Use platform transform as container for bounds calculation
            gridContainers.Add(p.transform);
        }
    }

    // Compute combined bounds of all supplied grid containers in world space and apply camera position & size to fit.
    private void RecalculateAndApply()
    {
        // Compute combined bounds from containers. If nothing found, use a tiny bounds at origin.
        Bounds combined = ComputeCombinedBounds();
        // Provide a minimal bounds to avoid division by zero later.
        if (combined.size == Vector3.zero) combined = new Bounds(Vector3.zero, Vector3.one * 0.1f);
        // Desired camera world position is the centre of the combined bounds.
        Vector3 targetPos = combined.center;
        // Preserve current camera Z so the orthographic camera remains at its original depth.
        targetPos.z = transform.position.z;
        // Size handling (skip automatic size updates while pulse is active so pulse coroutine can control size).
        if (isPulseActive) return;

        // Determine required orthographic half-height (orthographicSize) so bounds fits vertically.
        // requiredHalfHeight is half of the combined vertical span plus padding.
        float requiredHalfHeight = combined.extents.y + padding;
        // Determine required half-height implied by how wide the bounds are, taking aspect ratio into account.
        // The horizontal half-size of the camera view in world units equals orthoSize * aspect.
        // Therefore to fit a horizontal half-width W, we need orthoSize >= W / aspect.
        float requiredHalfWidth = combined.extents.x + padding;
        float requiredHalfHeightFromWidth = requiredHalfWidth / camera.aspect;
        // Final required orthographic half-height is the maximum of both constraints and the configured min size.
        float targetSize = Mathf.Max(requiredHalfHeight, requiredHalfHeightFromWidth, minOrthographicSize);
        targetSize = Mathf.Min(targetSize, maxOrthographicSize);

        // Apply position and size to camera either smoothly or instantly.
        if (smoothFollow && smoothTime > 0f)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref positionVelocity, smoothTime);
            camera.orthographicSize = Mathf.SmoothDamp(camera.orthographicSize, targetSize, ref sizeVelocity, smoothTime);
        }
        else
        {
            transform.position = targetPos;
            camera.orthographicSize = targetSize;
        }
    }

    // Compute a combined world-space Bounds that encapsulates Renderers and PolygonCollider2D of each container's children.
    // Only includes active GameObjects to properly handle tiles that are disabled.
    private Bounds ComputeCombinedBounds()
    {
        bool hasAny = false;
        Bounds combined = new Bounds();
        foreach (var container in gridContainers)
        {
            if (container == null || !container.gameObject.activeInHierarchy) continue;
            // Collect SpriteRenderers under the container for visual bounds (active only).
            SpriteRenderer[] srs = container.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
            foreach (var spriteRenderer in srs)
            {
                if (spriteRenderer == null || !spriteRenderer.gameObject.activeInHierarchy) continue;
                if (!hasAny)
                {
                    combined = spriteRenderer.bounds; // Initialise with first found renderer bounds
                    hasAny = true;
                }
                else combined.Encapsulate(spriteRenderer.bounds);
            }
            // Collect PolygonCollider2D under the container for physics bounds which often match visual trimmed shapes (active only).
            PolygonCollider2D[] pcs = container.GetComponentsInChildren<PolygonCollider2D>(includeInactive: false);
            foreach (var polygonCollider in pcs)
            {
                if (polygonCollider == null || !polygonCollider.gameObject.activeInHierarchy) continue;
                if (!hasAny)
                {
                    combined = polygonCollider.bounds; // Initialise if no renderer was found yet
                    hasAny = true;
                }
                else combined.Encapsulate(polygonCollider.bounds);
            }
            // Fallback: include child transforms positions so single-point tiles without renderers still contribute (active only).
            Transform[] children = container.GetComponentsInChildren<Transform>(includeInactive: false);
            foreach (var t in children)
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                if (!hasAny)
                {
                    combined = new Bounds(t.position, Vector3.zero);
                    hasAny = true;
                }
                else combined.Encapsulate(new Bounds(t.position, Vector3.zero));
            }
        }
        return combined;
    }

    // Register a kill event; pulses will be aggregated within killWindow seconds into one larger pulse.
    public void RegisterKillAndPulseAggregated()
    {
        pendingKills++;
        // Start aggregation timer if not running
        if (killWindowCoroutine == null) killWindowCoroutine = StartCoroutine(KillWindowCoroutine());
    }

    // Coroutine that waits for killWindow seconds then fires a single pulse sized for the accumulated kills.
    private IEnumerator KillWindowCoroutine()
    {
        yield return new WaitForSeconds(killWindow);
        int kills = pendingKills;
        pendingKills = 0;
        killWindowCoroutine = null;
        // Fire the aggregated pulse (duration uses default)
        ZoomOutPulseForKillCount(kills);
    }

    // Public API: trigger a temporary zoom-out pulse.
    // Trigger a pulse sized by number of kills. clamps multiplier to pulseMaxMultiplier.
    public void ZoomOutPulseForKillCount(int killCount, float duration = -1f)
    {
        // Minimum 1 kill yields at least the default multiplier.
        int k = Mathf.Max(0, killCount);
        // Compute multiplier: default + per-kill increment (minus one because default already represents 1x event)
        float mult = pulseDefaultMultiplier + (k - 1) * pulseMultiplierPerKill;
        // Clamp to maximum allowed multiplier
        mult = Mathf.Clamp(mult, 1f, pulseMaxMultiplier);
        // Start a pulse with the chosen multiplier
        ZoomOutPulse(mult, duration);
    }

    // multiplier: how much to multiply the current size (eg 2f => double). duration: total seconds of the pulse.
    public void ZoomOutPulse(float multiplier = -1f, float duration = -1f)
    {
        // Use defaults when negative args passed.
        float mult = (multiplier <= 0f) ? pulseDefaultMultiplier : multiplier;
        float dur = (duration <= 0f) ? pulseDefaultDuration : duration;
        // Start coroutine (will no-op if another pulse is active).
        StartCoroutine(ZoomPulseCoroutine(mult, dur));
    }

    // Coroutine that eases orthographicSize to multiplier * current, holds, then eases back.
    private IEnumerator ZoomPulseCoroutine(float multiplier, float totalDuration)
    {
        if (isPulseActive) yield break;
        if (camera == null) yield break;

        isPulseActive = true;

        // Compute timings: easeOut, hold, easeIn fractions.
        float hold = Mathf.Clamp01(pulseHoldFraction) * totalDuration;
        float half = (totalDuration - hold) * 0.5f;
        float expandDuration = Mathf.Max(0.01f, half);
        float contractDuration = expandDuration;

        // Record original size and clamp target against min/max.
        float originalSize = camera.orthographicSize;
        float targetSize = Mathf.Clamp(originalSize * multiplier, minOrthographicSize, maxOrthographicSize);

        // Expand (ease)
        float t = 0f;
        while (t < expandDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / expandDuration);
            float ease = a * a * (3f - 2f * a); // smoothstep
            camera.orthographicSize = Mathf.Lerp(originalSize, targetSize, ease);
            yield return null;
        }
        camera.orthographicSize = targetSize;

        // Hold at max
        if (hold > 0f) yield return new WaitForSeconds(hold);

        // Contract back (ease)
        t = 0f;
        while (t < contractDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / contractDuration);
            float ease = a * a * (3f - 2f * a);
            camera.orthographicSize = Mathf.Lerp(targetSize, originalSize, ease);
            yield return null;
        }
        camera.orthographicSize = originalSize;
        isPulseActive = false;
    }
}