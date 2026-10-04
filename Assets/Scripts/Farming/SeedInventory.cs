using System;
using System.Collections.Generic;
using UnityEngine;

// Tracks how many seeds of each crop the player owns. Separate from
// InventoryManager, which tracks harvested produce -- these two economies
// don't mix (you don't eat/sell seeds, and harvested crops don't plant
// themselves). Seeds are bought from the merchant (see ShopUI.BuySeed) and
// spent by Farmland.TryPlant whenever a seed bag successfully pours.
//
// Unity setup:
//   1. Put this script on an empty "SeedInventory" GameObject
//   2. Drag every CropData asset into Known Crops, in the order you want
//      them listed in the seed-picker UI
public class SeedInventory : MonoBehaviour
{
    public static SeedInventory Instance { get; private set; }

    [Tooltip("Every crop type the seed-picker UI can show, in display order")]
    public List<CropData> knownCrops = new List<CropData>();

    private readonly Dictionary<string, int> seedCounts = new Dictionary<string, int>();

    // SeedPickerUI subscribes to this so the on-screen counts stay live.
    public event Action OnSeedsChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void AddSeed(CropData data, int amount = 1)
    {
        if (data == null || amount <= 0) return;

        seedCounts.TryGetValue(data.cropName, out int current);
        seedCounts[data.cropName] = current + amount;

        OnSeedsChanged?.Invoke();
    }

    public bool RemoveSeed(string cropName, int amount = 1)
    {
        if (amount <= 0) return false;
        if (!seedCounts.TryGetValue(cropName, out int current) || current < amount)
            return false;

        seedCounts[cropName] = current - amount;
        OnSeedsChanged?.Invoke();
        return true;
    }

    public int GetCount(string cropName)
    {
        return seedCounts.TryGetValue(cropName, out int current) ? current : 0;
    }

    public int GetCount(CropData data)
    {
        return data != null ? GetCount(data.cropName) : 0;
    }
}
