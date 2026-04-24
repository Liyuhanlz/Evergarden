using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("UI References")]
    public Transform slotContainer;
    public GameObject slotPrefab;
    public int maxSlots = 16;

    private Dictionary<string, int> inventory = new Dictionary<string, int>();
    private Dictionary<string, CropData> cropDataMap = new Dictionary<string, CropData>();

    private List<GameObject> spawnedSlots = new List<GameObject>();

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
        SpawnSlots();
    }

    void SpawnSlots()
    {
        if (slotPrefab == null || slotContainer == null)
        {
            Debug.LogWarning("[InventoryManager] Missing slotPrefab or slotContainer.");
            return;
        }

        for (int i = 0; i < maxSlots; i++)
        {
            GameObject slot = Instantiate(slotPrefab, slotContainer);
            spawnedSlots.Add(slot);
        }

        RefreshUI();
    }

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

        RefreshUI();
    }

    public bool RemoveCrop(string cropName, int amount = 1)
    {
        if (!inventory.ContainsKey(cropName) || inventory[cropName] < amount)
            return false;

        inventory[cropName] -= amount;

        if (inventory[cropName] <= 0)
        {
            inventory.Remove(cropName);
            cropDataMap.Remove(cropName);
        }

        RefreshUI();
        return true;
    }

    public int GetCount(string cropName)
    {
        return inventory.ContainsKey(cropName) ? inventory[cropName] : 0;
    }

    void RefreshUI()
    {
        List<string> keys = new List<string>(inventory.Keys);

        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            GameObject slot = spawnedSlots[i];

            Image iconImage = slot.transform.Find("Icon")?.GetComponent<Image>();
            TextMeshProUGUI countText = slot.transform.Find("Count")?.GetComponent<TextMeshProUGUI>();

            if (i < keys.Count)
            {
                string cropName = keys[i];
                int qty = inventory[cropName];

                CropData data = cropDataMap[cropName];

                if (iconImage != null)
                {
                    if (data.icon != null)
                    {
                        iconImage.sprite = data.icon;
                        iconImage.enabled = true;
                    }
                    else
                    {
                        iconImage.enabled = false;
                    }
                }

                if (countText != null)
                    countText.text = qty > 1 ? "x" + qty : "";
            }
            else
            {
                if (iconImage != null)
                    iconImage.enabled = false;

                if (countText != null)
                    countText.text = "";
            }
        }
    }
}