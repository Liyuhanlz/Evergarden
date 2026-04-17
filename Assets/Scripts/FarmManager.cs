using System.Collections.Generic;
using UnityEngine;

// FarmManager.cs - Scene registry and harvest notification system.
// Attach to an empty GameObject called "FarmManager" in your scene.
//
// What it does:
//   - Finds all Farmland tiles in the scene at startup
//   - Subscribes to each tile's OnReadyToHarvest event
//   - Tells HUD to show a harvest alert with the crop name when ready
//   - Handles harvest input (call TryHarvestNearest from your player script)

public class FarmManager : MonoBehaviour
{
    // Singleton
    public static FarmManager Instance { get; private set; }

    [Header("All Farmland Tiles (auto-found at Start)")]
    public List<Farmland> allTiles = new List<Farmland>();

    [Header("Harvest Settings")]
    [Tooltip("How close the player must be to a tile to harvest it")]
    public float harvestRadius = 2f;

    [Tooltip("Drag your player's Transform here so we can check distance")]
    public Transform playerTransform;

    // Private
    private List<Farmland> readyTiles = new List<Farmland>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        RefreshTileList();
    }

    // Find and subscribe to all tiles
    [ContextMenu("Refresh Tile List")]
    public void RefreshTileList()
    {
        // Unsubscribe from old tiles first to avoid duplicate listeners
        foreach (Farmland tile in allTiles)
        {
            if (tile != null)
                tile.OnReadyToHarvest -= HandleTileReady;
        }

        allTiles.Clear();
        allTiles.AddRange(FindObjectsByType<Farmland>(FindObjectsSortMode.None));

        foreach (Farmland tile in allTiles)
        {
            tile.OnReadyToHarvest += HandleTileReady;
        }

        Debug.Log("[FarmManager] Tracking " + allTiles.Count + " tiles.");
    }

    // Called when a tile's crop finishes growing
    void HandleTileReady(Farmland tile)
    {
        if (!readyTiles.Contains(tile))
            readyTiles.Add(tile);

        // Tell the HUD to show a harvest alert
        if (HUD.Instance != null)
            HUD.Instance.ShowHarvestAlert(tile.cropData.cropName);
    }

    // Call this from your player interaction script
    // Returns the CropData of the harvested crop, or null if nothing nearby
    public CropData TryHarvestNearest()
    {
        if (playerTransform == null)
        {
            Debug.LogWarning("[FarmManager] Player transform not assigned.");
            return null;
        }

        Farmland closest = null;
        float closestDist = harvestRadius;

        foreach (Farmland tile in readyTiles)
        {
            if (tile == null) continue;

            float dist = Vector3.Distance(playerTransform.position, tile.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = tile;
            }
        }

        if (closest == null)
        {
            Debug.Log("[FarmManager] No harvest-ready crop nearby.");
            return null;
        }

        CropData result = closest.Harvest();
        if (result != null)
        {
            readyTiles.Remove(closest);

            // Hide the alert if no more ready tiles remain
            if (readyTiles.Count == 0 && HUD.Instance != null)
                HUD.Instance.HideHarvestAlert();
        }

        return result;
    }
}