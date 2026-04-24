using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// InventoryManager.cs -- Manages the player's inventory of harvested crops.
// Displays items as a grid of slots inside the InventoryPanel.
//
// Unity setup:
//   1. Inside your InventoryPanel Canvas, create a child GameObject "Grid"
//      and add a GridLayoutGroup component to it
//   2. Set GridLayoutGroup cell size to roughly 100x100, spacing 10x10
//   3. Create an InventorySlot prefab (see bottom of this file for structure)
//      and drag it into the slotPrefab field
//   4. Drag the Grid transform into the slotContainer field
//   5. Set maxSlots to however many slots your grid shows (e.g. 16)
//
// InventorySlot prefab structure:
//   InventorySlot (Image -- slot background)
//   |-- Icon     (Image -- crop icon)
//   |-- Count    (TextMeshPro -- "x3" quantity label)

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Parent transform with GridLayoutGroup -- slots spawn here")]
    public Transform slotContainer;

    [Tooltip("Prefab for each inventory slot")]
    public GameObject slotPrefab;

    [Tooltip("Maximum number of slots shown in the grid")]
    public int maxSlots = 16;

    // Internal inventory -- maps crop name to quantity
    private Dictionary<string, int> inventory = new Dictionary<string, int>();

    // Maps crop name to its CropData for icon/display info
    private Dictionary<string, CropData> cropDataMap = new Dictionary<string, CropData>();

    // Spawned slot UI objects (reused, not recreated each time)
    private List<GameObject> spawnedSlots = new List<GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        SpawnSlots();
    }

    // Create all slot UI objects once at start
    void SpawnSlots()
    {
        if (slotPrefab == null || slotContainer == null)
        {
            Debug.LogWarning("[InventoryManager] slotPrefab or slotContainer not assigned.");
            return;
        }

        for (int i = 0; i < maxSlots; i++)
        {
            GameObject slot = Instantiate(slotPrefab, slotContainer);
            slot.SetActive(true);
            spawnedSlots.Add(slot);
        }

        RefreshUI();
    }

    // Add harvested crop to inventory -- called by FarmManager after harvest
    public void AddCrop(CropData data, int amount = 1)
    {
        if (data == null) return;

        if (inventory.ContainsKey(data.cropName))
            inventory[data.cropName] += amount;
        else
        {
            inventory[data.cropName] = amount;
            cropDataMap[data.cropName] = data;
        }

        Debug.Log($"[Inventory] Added {amount}x {data.cropName}. Total: {inventory[data.cropName]}");
        RefreshUI();
    }

    // Remove crops (for selling/cooking later)
    public bool RemoveCrop(string cropName, int amount = 1)
    {
        if (!inventory.ContainsKey(cropName) || inventory[cropName] < amount)
        {
            Debug.LogWarning($"[Inventory] Not enough {cropName} to remove.");
            return false;
        }

        inventory[cropName] -= amount;
        if (inventory[cropName] <= 0)
        {
            inventory.Remove(cropName);
            cropDataMap.Remove(cropName);
        }

        RefreshUI();
        return true;
    }

    // Check how many of a crop the player has
    public int GetCount(string cropName)
    {
        return inventory.ContainsKey(cropName) ? inventory[cropName] : 0;
    }

    // Update all slot visuals to match current inventory data
    void RefreshUI()
    {
        // Flatten inventory into a list for slot assignment
        List<string> keys = new List<string>(inventory.Keys);

        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            GameObject slot = spawnedSlots[i];
            Image iconImage = slot.transform.Find("Icon")?.GetComponent<Image>();
            TextMeshProUGUI count = slot.transform.Find("Count")?.GetComponent<TextMeshProUGUI>();

            if (i < keys.Count)
            {
                string cropName = keys[i];
                int qty = inventory[cropName];

                // Show crop icon if CropData has a sprite assigned
                if (iconImage != null && cropDataMap.ContainsKey(cropName))
                {
                    iconImage.gameObject.SetActive(cropDataMap[cropName].icon != null);
                    if (cropDataMap[cropName].icon != null)
                        iconImage.sprite = cropDataMap[cropName].icon;
                }

                // Show quantity
                if (count != null)
                    count.text = qty > 1 ? $"x{qty}" : "";
            }
            else
            {
                // Empty slot -- hide icon and count
                if (iconImage != null) iconImage.gameObject.SetActive(false);
                if (count != null) count.text = "";
            }
        }
    }
}