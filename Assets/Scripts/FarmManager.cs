using System.Collections.Generic;
using UnityEngine;

public class FarmManager : MonoBehaviour
{
    public static FarmManager Instance { get; private set; }

    [Header("All Farmland Tiles")]
    public List<Farmland> allTiles = new List<Farmland>();

    [Header("Player Reference")]
    public Transform playerTransform;

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

    [ContextMenu("Refresh Tile List")]
    public void RefreshTileList()
    {
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

    void HandleTileReady(Farmland tile)
    {
        if (!readyTiles.Contains(tile))
            readyTiles.Add(tile);

        // No more "X is ready to harvest!" HUD popup -- FarmlandHoverStatus's
        // ray-hover window already shows exactly this (see its "Ready to
        // harvest!" line), so the two were duplicating each other.
    }

    // Harvests one specific tile -- called by FarmlandHoverStatus when the
    // player is aiming the ray at a ready crop and presses A. Harvesting is
    // aim-only: there's no longer a "press A near any ready crop" fallback,
    // so this is the one path into actually harvesting anything.
    public CropData HarvestTile(Farmland tile)
    {
        if (tile == null) return null;

        // Crops with pickable fruit (tomato, corn) are harvested one fruit at
        // a time by grabbing -- see PickableFruit / FinishFruitHarvest
        if (tile.HasPickableFruit) return null;

        CropData result = tile.Harvest();

        if (result != null)
        {
            int amount = result.harvestYield;
            if (tile.LastHarvestWasRegrowth)
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * result.regrowthYieldMultiplier));

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddCrop(result, amount);
            }
            else
            {
                Debug.LogWarning("[FarmManager] InventoryManager instance missing.");
            }

            readyTiles.Remove(tile);
        }

        return result;
    }

    // Called by Farmland once the last pickable fruit is plucked. Each fruit
    // already went into the inventory as it was picked, so this only runs the
    // tile's harvest bookkeeping (soil, regrow or clear) -- no extra yield.
    public void FinishFruitHarvest(Farmland tile)
    {
        if (tile == null) return;

        if (tile.Harvest() != null)
            readyTiles.Remove(tile);
    }
}
