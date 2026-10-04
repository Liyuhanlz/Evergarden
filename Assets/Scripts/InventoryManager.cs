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

    [Header("Grid Layout")]
    [Tooltip("Boxes per row -- rows follow from Max Slots. Box size is worked out from the Slot " +
             "Container's own size, so the grid always fills the panel exactly instead of " +
             "spilling past its edges")]
    public int columns = 4;

    [Tooltip("Gap between boxes, in the canvas's UI units")]
    public float boxSpacing = 3f;

    [Tooltip("Margin between the outer boxes and the panel's edge, in the canvas's UI units")]
    public float gridPadding = 4f;

    private Dictionary<string, int> inventory = new Dictionary<string, int>();
    private Dictionary<string, CropData> cropDataMap = new Dictionary<string, CropData>();

    private List<GameObject> spawnedSlots = new List<GameObject>();

    // ShopUI's Sell tab subscribes to this so its counts stay live whether a
    // crop was added by harvesting or removed by selling.
    public event System.Action OnInventoryChanged;

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

        FitGridToContainer();

        for (int i = 0; i < maxSlots; i++)
        {
            GameObject slot = Instantiate(slotPrefab, slotContainer);
            slot.transform.localScale = Vector3.one;
            spawnedSlots.Add(slot);
        }

        RefreshUI();
    }

    // Square boxes as large as fit: columns x rows inside the container's
    // rect, minus padding and gaps.
    void FitGridToContainer()
    {
        GridLayoutGroup grid = slotContainer.GetComponent<GridLayoutGroup>();
        RectTransform area = slotContainer as RectTransform;
        if (grid == null || area == null) return;

        int cols = Mathf.Max(1, columns);
        int rows = Mathf.CeilToInt(maxSlots / (float)cols);

        float usableW = area.rect.width - 2f * gridPadding - (cols - 1) * boxSpacing;
        float usableH = area.rect.height - 2f * gridPadding - (rows - 1) * boxSpacing;
        float box = Mathf.Max(1f, Mathf.Min(usableW / cols, usableH / rows));

        grid.cellSize = new Vector2(box, box);
        grid.spacing = new Vector2(boxSpacing, boxSpacing);
        int pad = Mathf.RoundToInt(gridPadding);
        grid.padding = new RectOffset(pad, pad, pad, pad);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;
        grid.childAlignment = TextAnchor.MiddleCenter;
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
        OnInventoryChanged?.Invoke();
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
        OnInventoryChanged?.Invoke();
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
                    countText.text = "x" + qty;
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