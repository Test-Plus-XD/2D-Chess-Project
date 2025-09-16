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

    // Cached camera reference.
    private Camera cam;
    // Velocity vector used by SmoothDamp for position smoothing.
    private Vector3 positionVelocity = Vector3.zero;
    // Velocity float used by SmoothDamp for size smoothing.
    private float sizeVelocity = 0f;

    // Initialise and optionally perform the first recalc.
    private void Start()
    {
        cam = GetComponent<Camera>() ?? Camera.main;
        if (cam == null)
        {
            Debug.LogError("[FollowCamera] No Camera found on this GameObject and Camera.main is null.");
            enabled = false;
            return;
        }
        if (!cam.orthographic)
        {
            Debug.LogWarning("[FollowCamera] Camera is not orthographic. Behaviour is designed for orthographic cameras.");
        }
        // If no explicit containers provided and auto-find is enabled, discover grids now.
        if (autoFindGrids && (gridContainers == null || gridContainers.Count == 0)) AutoDiscoverGridContainers();
        // Perform initial calculation immediately so camera starts correctly.
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
        // refresh discovery if auto-find enabled
        if (autoFindGrids) AutoDiscoverGridContainers();
        RecalculateAndApply();
    }

    // Discover HexGridGenerator instances and use their parentContainer (or the generator transform) as grid containers.
    private void AutoDiscoverGridContainers()
    {
        gridContainers = new List<Transform>();
        HexGridGenerator[] gens = Object.FindObjectsByType<HexGridGenerator>(FindObjectsSortMode.InstanceID);
        foreach (var g in gens)
        {
            if (g == null) continue;
            if (g.parentContainer != null) gridContainers.Add(g.parentContainer);
            else gridContainers.Add(g.transform);
        }
    }

    // Compute combined bounds of all supplied grid containers in world space and apply camera position & size to fit.
    private void RecalculateAndApply()
    {
        // Compute combined bounds from containers. If nothing found, use a tiny bounds at origin.
        Bounds combined = ComputeCombinedBounds();
        if (combined.size == Vector3.zero)
        {
            // Provide a minimal bounds to avoid division by zero later.
            combined = new Bounds(Vector3.zero, Vector3.one * 0.1f);
        }
        // Desired camera world position is the centre of the combined bounds.
        Vector3 targetPos = combined.center;
        // Preserve current camera Z so the orthographic camera remains at its original depth.
        targetPos.z = transform.position.z;

        // Determine required orthographic half-height (orthographicSize) so bounds fits vertically.
        // requiredHalfHeight is half of the combined vertical span plus padding.
        float requiredHalfHeight = combined.extents.y + padding;
        // Determine required half-height implied by how wide the bounds are, taking aspect ratio into account.
        // The horizontal half-size of the camera view in world units equals orthoSize * aspect.
        // Therefore to fit a horizontal half-width W, we need orthoSize >= W / aspect.
        float requiredHalfWidth = combined.extents.x + padding;
        float requiredHalfHeightFromWidth = requiredHalfWidth / cam.aspect;
        // Final required orthographic half-height is the maximum of both constraints and the configured min size.
        float targetSize = Mathf.Max(requiredHalfHeight, requiredHalfHeightFromWidth, minOrthographicSize);
        targetSize = Mathf.Min(targetSize, maxOrthographicSize);

        // Apply position and size to camera either smoothly or instantly.
        if (smoothFollow && smoothTime > 0f)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref positionVelocity, smoothTime);
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetSize, ref sizeVelocity, smoothTime);
        }
        else
        {
            transform.position = targetPos;
            cam.orthographicSize = targetSize;
        }
    }

    // Compute a combined world-space Bounds that encapsulates Renderers and PolygonCollider2D of each container's children.
    private Bounds ComputeCombinedBounds()
    {
        bool hasAny = false;
        Bounds combined = new Bounds();
        foreach (var container in gridContainers)
        {
            if (container == null) continue;
            // Collect SpriteRenderers under the container for visual bounds.
            SpriteRenderer[] srs = container.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            foreach (var sr in srs)
            {
                if (!hasAny)
                {
                    combined = sr.bounds; // Initialise with first found renderer bounds
                    hasAny = true;
                }
                else combined.Encapsulate(sr.bounds);
            }
            // Collect PolygonCollider2D under the container for physics bounds which often match visual trimmed shapes.
            PolygonCollider2D[] pcs = container.GetComponentsInChildren<PolygonCollider2D>(includeInactive: true);
            foreach (var pc in pcs)
            {
                if (!hasAny)
                {
                    combined = pc.bounds; // Initialise if no renderer was found yet
                    hasAny = true;
                }
                else combined.Encapsulate(pc.bounds);
            }
            // Fallback: include child transforms positions so single-point tiles without renderers still contribute.
            Transform[] children = container.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (var t in children)
            {
                if (t == null) continue;
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
}
