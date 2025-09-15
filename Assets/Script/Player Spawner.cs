using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Spawn a player pawn at the bottom-right tile of the generated grid.
/// Assumes hex tiles are children of HexGridGenerator.parentContainer and are named "Hex_q_r".
public class PlayerSpawner : MonoBehaviour
{
    // Reference to the HexGridGenerator in scene; if left null we try to find one.
    public HexGridGenerator gridGenerator;
    // Reference to the chequerboard for tile lookup and tileSize data.
    public Checkerboard checkerboard;
    // Pawn prefab with PlayerController component attached.
    public GameObject pawnPrefab;
    // Optional parent for the pawn; if null the pawn is instantiated at scene root (not parented to the grid).
    public Transform pawnParent;
    // Optional vertical offset in world units to nudge the pawn above the tile centre.
    public float verticalOffset = 0f;
    // Fallback search for colliders across the scene if none are found under the container.
    public bool allowSceneWideColliderSearch = true;

    // Small safety wait to let other Start() calls (e.g. grid generation) complete.
    private IEnumerator Start()
    {
        if (gridGenerator == null) gridGenerator = FindFirstObjectByType<HexGridGenerator>();
        if (gridGenerator == null)
        {
            Debug.LogError("[PlayerSpawner] No HexGridGenerator found in scene.");
            yield break;
        }
        if (checkerboard == null) checkerboard = FindFirstObjectByType<Checkerboard>();
        if (checkerboard == null)
        {
            Debug.LogError("[PlayerSpawner] No Chequerboard found in scene.");
            yield break;
        }

        // Ensure parent container reference exists
        Transform tileParent = gridGenerator.parentContainer == null ? gridGenerator.transform : gridGenerator.parentContainer;

        // Wait one frame so HexGridGenerator.Start() has a chance to create tiles if it does so in Start()
        yield return new WaitForEndOfFrame();

        // Collect candidate tiles by looking for PolygonCollider2D under tileParent first
        List<PolygonCollider2D> colliders = new List<PolygonCollider2D>();
        for (int i = 0; i < tileParent.childCount; i++)
        {
            Transform child = tileParent.GetChild(i);
            PolygonCollider2D pc = child.GetComponent<PolygonCollider2D>();
            if (pc != null) colliders.Add(pc);
        }

        // If none found and allowed, search the entire scene for PolygonCollider2D and pick those named like tiles
        if (colliders.Count == 0 && allowSceneWideColliderSearch)
        {
            PolygonCollider2D[] all = Object.FindObjectsByType<PolygonCollider2D>(FindObjectsSortMode.None);
            foreach (var pc in all)
            {
                // Accept colliders whose GameObject name starts with "Hex_"
                if (pc.gameObject.name.StartsWith("Hex_")) colliders.Add(pc);
            }
        }

        if (colliders.Count == 0)
        {
            Debug.LogWarning("[PlayerSpawner] No PolygonCollider2D tiles found. Make sure each tile has a PolygonCollider2D and is named 'Hex_q_r'.");
            // As a best-effort fallback, try the old transform-based approach (children of tileParent)
            SpawnUsingTransformScan(tileParent);
            yield break;
        }

        // Find the bottom-most (minimum bounds.min.y) then right-most (maximum bounds.center.x)
        float bestBottom = float.PositiveInfinity;
        foreach (var pc in colliders)
        {
            float bMinY = pc.bounds.min.y;
            if (bMinY < bestBottom) bestBottom = bMinY;
        }

        // tolerance to handle floating point differences
        const float eps = 0.0001f;
        PolygonCollider2D chosenCollider = null;
        float bestCenterX = float.NegativeInfinity;
        foreach (var pc in colliders)
        {
            if (pc.bounds.min.y <= bestBottom + eps)
            {
                float cx = pc.bounds.center.x;
                if (cx > bestCenterX)
                {
                    bestCenterX = cx;
                    chosenCollider = pc;
                }
            }
        }

        if (chosenCollider == null)
        {
            Debug.LogError("[PlayerSpawner] Failed to find a candidate tile from colliders.");
            yield break;
        }

        // Compute spawn position as the collider's bounds centre plus any provided vertical offset
        Vector3 spawnPos = new Vector3(chosenCollider.bounds.center.x, chosenCollider.bounds.center.y + verticalOffset, 0f);

        // Instantiate pawn at scene root or provided pawnParent (do not parent under grid by default)
        GameObject playerPawn = pawnParent == null ? Instantiate(pawnPrefab, spawnPos, Quaternion.identity) : Instantiate(pawnPrefab, spawnPos, Quaternion.identity, pawnParent);

        // Try to parse axial coords from the chosen tile name (expects "Hex_q_r")
        string name = chosenCollider.gameObject.name;
        string[] parts = name.Split('_');
        int q = 0, r = 0;
        if (parts.Length >= 3 && int.TryParse(parts[1], out q) && int.TryParse(parts[2], out r))
        {
            PlayerController pc = playerPawn.GetComponent<PlayerController>();
            if (pc == null) Debug.LogWarning("[PlayerSpawner] Pawn prefab has no PlayerController component.");
            else pc.Initialise(q, r, gridGenerator, checkerboard);
        } else {
            Debug.LogWarning("[PlayerSpawner] Could not parse axial coords from tile name. Pawn will be placed visually but movement may not map to tiles.");
        }
    }

    // Fallback method that scans child transforms (older behaviour) if no colliders are present.
    private void SpawnUsingTransformScan(Transform tileParent)
    {
        Transform chosen = null;
        float minY = float.PositiveInfinity;
        float bestX = float.NegativeInfinity;
        for (int i = 0; i < tileParent.childCount; i++)
        {
            Transform t = tileParent.GetChild(i);
            Vector3 pos = t.position;
            if (pos.y < minY - 0.0001f)
            {
                minY = pos.y;
                bestX = pos.x;
                chosen = t;
            } 
            else if (Mathf.Approximately(pos.y, minY)) 
            {
                if (pos.x > bestX)
                {
                    bestX = pos.x;
                    chosen = t;
                }
            }
        }

        if (chosen == null)
        {
            Debug.LogError("[PlayerSpawner] Fallback transform scan failed to locate tile.");
            return;
        }

        Vector3 spawnPos = chosen.position + new Vector3(0f, verticalOffset, 0f);
        GameObject playerPawn = pawnParent == null ? Instantiate(pawnPrefab, spawnPos, Quaternion.identity) : Instantiate(pawnPrefab, spawnPos, Quaternion.identity, pawnParent);

        string[] parts = chosen.name.Split('_');
        int q = 0, r = 0;
        if (parts.Length >= 3 && int.TryParse(parts[1], out q) && int.TryParse(parts[2], out r))
        {
            PlayerController pc = playerPawn.GetComponent<PlayerController>();
            if (pc == null) Debug.LogWarning("[PlayerSpawner] Pawn prefab has no PlayerController component.");
            else pc.Initialise(q, r, gridGenerator, checkerboard);
        } else {
            Debug.LogWarning("[PlayerSpawner] Could not parse axial coords from tile name in fallback scan.");
        }
    }
}