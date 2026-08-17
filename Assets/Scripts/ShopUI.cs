using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// World Space shop panel. Lets the player spend gold to "buy" seeds and sell
// crops from their inventory. Opened/closed by MerchantBooth.
//
// NOTE: BuySeed() currently only deducts gold and logs -- it does not yet spawn
// a physical seed bag into the player's hand. Hook that up once you decide how
// bought items should reach the player (e.g. spawn near a pickup point, or add
// to a seed-bag inventory system).
//
// Unity setup:
//   1. Create a World Space "ShopCanvas" (scale ~0.001) and attach this script
//   2. Build a Shop Row prefab with children named exactly:
//      NameText, PriceText, BuyButton, SellButton
//   3. Drag the ShopCanvas GameObject into Shop Canvas, the row prefab into
//      Shop Row Prefab, and a container Transform inside the canvas into Row Container
//   4. Drag your CropData assets into Seeds For Sale
public class ShopUI : MonoBehaviour
{
    [Header("Canvas")]
    [Tooltip("The Canvas GameObject to show/hide when the shop opens/closes")]
    public GameObject shopCanvas;

    [Header("Seeds For Sale")]
    [Tooltip("Which crops the player can buy seeds for")]
    public List<CropData> seedsForSale = new List<CropData>();

    [Header("Row Prefab")]
    [Tooltip("Prefab with child objects named: NameText, PriceText, BuyButton, SellButton")]
    public GameObject shopRowPrefab;

    [Tooltip("Parent Transform the rows get spawned into")]
    public Transform rowContainer;

    List<GameObject> spawnedRows = new List<GameObject>();

    void Awake()
    {
        if (shopCanvas != null)
            shopCanvas.SetActive(false);
    }

    void Start()
    {
        SpawnRows();
    }

    void SpawnRows()
    {
        if (shopRowPrefab == null || rowContainer == null)
        {
            Debug.LogWarning("[ShopUI] Missing shopRowPrefab or rowContainer.");
            return;
        }

        foreach (CropData crop in seedsForSale)
        {
            GameObject row = Instantiate(shopRowPrefab, rowContainer);
            spawnedRows.Add(row);

            TMP_Text nameText = row.transform.Find("NameText")?.GetComponent<TMP_Text>();
            TMP_Text priceText = row.transform.Find("PriceText")?.GetComponent<TMP_Text>();
            Button buyButton = row.transform.Find("BuyButton")?.GetComponent<Button>();
            Button sellButton = row.transform.Find("SellButton")?.GetComponent<Button>();

            if (nameText != null) nameText.text = crop.cropName;
            if (priceText != null) priceText.text = crop.sellPrice + "g";

            CropData capturedCrop = crop; // capture for the button listener
            if (buyButton != null) buyButton.onClick.AddListener(() => BuySeed(capturedCrop));
            if (sellButton != null) sellButton.onClick.AddListener(() => SellCrop(capturedCrop));
        }
    }

    public void BuySeed(CropData crop)
    {
        if (crop == null || PlayerWallet.Instance == null) return;

        if (PlayerWallet.Instance.SpendGold(crop.sellPrice))
            Debug.Log($"[ShopUI] Bought {crop.cropName} seed.");
        else
            Debug.Log("[ShopUI] Not enough gold.");
    }

    public void SellCrop(CropData crop)
    {
        if (crop == null || InventoryManager.Instance == null) return;

        if (InventoryManager.Instance.RemoveCrop(crop.cropName, 1))
        {
            PlayerWallet.Instance?.AddGold(crop.sellPrice);
            Debug.Log($"[ShopUI] Sold {crop.cropName}.");
        }
        else
        {
            Debug.Log($"[ShopUI] No {crop.cropName} to sell.");
        }
    }

    public void Open()
    {
        if (shopCanvas != null)
            shopCanvas.SetActive(true);
    }

    public void Close()
    {
        if (shopCanvas != null)
            shopCanvas.SetActive(false);
    }
}
