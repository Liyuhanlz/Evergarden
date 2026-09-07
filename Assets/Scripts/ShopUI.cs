using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// World Space shop panel, opened/closed by ShopInteractionController. Two
// tabs, like the backpack's:
//   Buy  -- spend gold on seeds (added to SeedInventory -- the reconfigurable
//           Seed Bag draws from there, see SeedPickerUI)
//   Sell -- turn harvested produce (InventoryManager) back into gold
//
// Selecting a crop row doesn't transact immediately -- it opens that tab's
// detail panel with a quantity stepper (+/-), the current wallet balance,
// a running total (red "-$X" to buy, green "+$X" to sell), and -- right
// below that -- what your balance will be after confirming, so the cost
// line and its consequence read together instead of just showing "-$X" and
// leaving the player to do the subtraction themselves. A Purchase/Sell
// button confirms the trade.
//
// Unity setup:
//   1. Create a World Space "ShopCanvas" and attach this script
//   2. Build a tab bar with two Buttons -> Buy Tab Button, Sell Tab Button
//   3. Build a row prefab (children: NameText, PriceText, [CountText for
//      sell] -- Button on the row root) -> Buy/Sell Row Prefab, and a
//      container Transform for each -> Buy/Sell Row Container
//   4. Build each tab's detail panel (see field tooltips below) -> Buy/Sell
//      Detail Panel and its child references
//   5. Drag your CropData assets into Tradeable Crops
public class ShopUI : MonoBehaviour
{
    public enum ShopTab { Buy, Sell }

    [Header("Canvas")]
    [Tooltip("The Canvas GameObject to show/hide when the shop opens/closes")]
    public GameObject shopCanvas;

    [Header("Tradeable Crops")]
    [Tooltip("Every crop the shop trades -- same list drives both the Buy and Sell tab")]
    public List<CropData> tradeableCrops = new List<CropData>();

    [Header("Tabs")]
    public Button buyTabButton;
    public Button sellTabButton;

    [Header("Buy Tab - Row List")]
    [Tooltip("Prefab with child objects named: NameText, PriceText -- Button lives on the row root")]
    public GameObject buyRowPrefab;
    public Transform buyRowContainer;

    [Header("Buy Tab - Detail Panel")]
    [Tooltip("Shown once a crop is selected from the row list")]
    public GameObject buyDetailPanel;
    public TMP_Text buyCropNameText;
    public TMP_Text buyQtyText;
    public Button buyQtyMinusButton;
    public Button buyQtyPlusButton;
    [Tooltip("Current gold, shown above the cost -- so it reads balance, then cost")]
    public TMP_Text buyWalletText;
    [Tooltip("Total cost, red with a leading minus, e.g. \"-$30\"")]
    public TMP_Text buyCostText;
    [Tooltip("Balance after this purchase, shown right under the cost, e.g. \"$70 left\"")]
    public TMP_Text buyRemainingText;
    public Button buyConfirmButton;

    [Header("Sell Tab - Row List")]
    [Tooltip("Prefab with child objects named: NameText, CountText, PriceText -- Button on the row root")]
    public GameObject sellRowPrefab;
    public Transform sellRowContainer;

    [Header("Sell Tab - Detail Panel")]
    public GameObject sellDetailPanel;
    public TMP_Text sellCropNameText;
    public TMP_Text sellQtyText;
    public Button sellQtyMinusButton;
    public Button sellQtyPlusButton;
    public TMP_Text sellWalletText;
    [Tooltip("Total earnings, green with a leading plus, e.g. \"+$30\"")]
    public TMP_Text sellEarningsText;
    [Tooltip("Balance after this sale, shown right under the earnings, e.g. \"$130 total\"")]
    public TMP_Text sellRemainingText;
    public Button sellConfirmButton;

    [Header("Wallet Display (header)")]
    [Tooltip("Optional -- shows the running gold balance at the top of the shop, regardless of tab")]
    public TMP_Text goldText;

    [Header("Audio")]
    [Tooltip("Plays on both Purchase and Sell confirms -- assign a clip to this AudioSource in the " +
             "Inspector whenever you have one; left with no clip, Play() is a harmless no-op")]
    public AudioSource tradeAudioSource;

    static readonly Color CostColor = new Color(0.75f, 0.1f, 0.1f);
    static readonly Color EarningsColor = new Color(0.1f, 0.5f, 0.1f);

    private ShopTab currentTab = ShopTab.Buy;

    public ShopTab CurrentTab => currentTab;

    private readonly List<GameObject> buyRows = new List<GameObject>();
    private readonly List<GameObject> sellRows = new List<GameObject>();

    private CropData selectedBuyCrop;
    private int buyQty = 1;
    private CropData selectedSellCrop;
    private int sellQty = 1;

    // shopCanvas is never SetActive(false) -- same reasoning as the backpack/
    // tool rack (see MenuManager): a World Space canvas that gets fully
    // disabled has to run its GraphicRaycaster/TrackedDeviceGraphicRaycaster
    // back through OnEnable the next time it's shown, and that re-registration
    // isn't guaranteed to be ready the instant the player can first raycast
    // against it -- the shop felt unresponsive for a moment after every open
    // because of exactly this. Parking it far away instead sidesteps the
    // whole class of problem, the same way it already does for those panels.
    private Vector3 openPosition;
    private Quaternion openRotation;
    private static readonly Vector3 HiddenOffset = new Vector3(0f, -500f, 0f);

    void Awake()
    {
        if (shopCanvas != null)
        {
            openPosition = shopCanvas.transform.position;
            openRotation = shopCanvas.transform.rotation;
            shopCanvas.transform.position = openPosition + HiddenOffset;
        }

        if (buyTabButton != null) buyTabButton.onClick.AddListener(() => SelectTab(ShopTab.Buy));
        if (sellTabButton != null) sellTabButton.onClick.AddListener(() => SelectTab(ShopTab.Sell));

        if (buyQtyMinusButton != null) buyQtyMinusButton.onClick.AddListener(() => ChangeBuyQty(-1));
        if (buyQtyPlusButton != null) buyQtyPlusButton.onClick.AddListener(() => ChangeBuyQty(1));
        if (buyConfirmButton != null) buyConfirmButton.onClick.AddListener(ConfirmPurchase);

        if (sellQtyMinusButton != null) sellQtyMinusButton.onClick.AddListener(() => ChangeSellQty(-1));
        if (sellQtyPlusButton != null) sellQtyPlusButton.onClick.AddListener(() => ChangeSellQty(1));
        if (sellConfirmButton != null) sellConfirmButton.onClick.AddListener(ConfirmSell);

        if (buyDetailPanel != null) buyDetailPanel.SetActive(false);
        if (sellDetailPanel != null) sellDetailPanel.SetActive(false);

        if (buyCostText != null) buyCostText.color = CostColor;
        if (sellEarningsText != null) sellEarningsText.color = EarningsColor;
    }

    void OnEnable()
    {
        if (InventoryManager.Instance != null) InventoryManager.Instance.OnInventoryChanged += HandleInventoryChanged;
        if (PlayerWallet.Instance != null) PlayerWallet.Instance.OnGoldChanged += HandleGoldChanged;
    }

    void OnDisable()
    {
        if (InventoryManager.Instance != null) InventoryManager.Instance.OnInventoryChanged -= HandleInventoryChanged;
        if (PlayerWallet.Instance != null) PlayerWallet.Instance.OnGoldChanged -= HandleGoldChanged;
    }

    void HandleInventoryChanged()
    {
        RefreshSellTab();
        if (selectedSellCrop != null) RefreshSellDetail();
    }

    void HandleGoldChanged(int newGold)
    {
        RefreshGoldText();
        if (selectedBuyCrop != null) RefreshBuyDetail();
        if (selectedSellCrop != null) RefreshSellDetail();
    }

    public Color selectedTabColor = new Color(1f, 0.85f, 0.3f, 1f);
    private Color buyTabDefaultColor, sellTabDefaultColor;
    private bool tabDefaultColorsCaptured;

    void UpdateTabHighlight(ShopTab tab)
    {
        if (!tabDefaultColorsCaptured)
        {
            if (buyTabButton != null) buyTabDefaultColor = buyTabButton.image.color;
            if (sellTabButton != null) sellTabDefaultColor = sellTabButton.image.color;
            tabDefaultColorsCaptured = true;
        }

        if (buyTabButton != null) buyTabButton.image.color = tab == ShopTab.Buy ? selectedTabColor : buyTabDefaultColor;
        if (sellTabButton != null) sellTabButton.image.color = tab == ShopTab.Sell ? selectedTabColor : sellTabDefaultColor;
    }

    public void SelectTab(ShopTab tab)
    {
        currentTab = tab;
        UpdateTabHighlight(tab);

        if (buyRowContainer != null) SetContainerActive(buyRowContainer, tab == ShopTab.Buy);
        if (sellRowContainer != null) SetContainerActive(sellRowContainer, tab == ShopTab.Sell);
        if (buyDetailPanel != null) buyDetailPanel.SetActive(tab == ShopTab.Buy && selectedBuyCrop != null);
        if (sellDetailPanel != null) sellDetailPanel.SetActive(tab == ShopTab.Sell && selectedSellCrop != null);

        if (tab == ShopTab.Buy) RefreshBuyTab();
        else RefreshSellTab();

        // Freshly-instantiated row buttons (RefreshBuyTab/RefreshSellTab above)
        // can otherwise sit a frame behind the UI raycaster's own canvas scan,
        // making them briefly un-clickable the moment the shop first opens.
        Canvas.ForceUpdateCanvases();
    }

    static void SetContainerActive(Transform container, bool active)
    {
        // The container itself (not each row) is what tabs toggle -- keeps
        // this in one call regardless of how many rows are spawned.
        container.gameObject.SetActive(active);
    }

    // ---------------------------------------------
    //  BUY TAB
    // ---------------------------------------------
    void RefreshBuyTab()
    {
        if (buyRowPrefab == null || buyRowContainer == null) return;

        while (buyRows.Count < tradeableCrops.Count)
            buyRows.Add(Instantiate(buyRowPrefab, buyRowContainer));

        for (int i = 0; i < buyRows.Count; i++)
            buyRows[i].SetActive(i < tradeableCrops.Count);

        for (int i = 0; i < tradeableCrops.Count; i++)
        {
            CropData crop = tradeableCrops[i];
            GameObject row = buyRows[i];
            if (crop == null) { row.SetActive(false); continue; }

            TMP_Text nameText = row.transform.Find("NameText")?.GetComponent<TMP_Text>();
            TMP_Text priceText = row.transform.Find("PriceText")?.GetComponent<TMP_Text>();
            Button rowButton = row.GetComponent<Button>();

            if (nameText != null) nameText.text = crop.cropName;
            if (priceText != null) priceText.text = "$" + crop.sellPrice;

            if (rowButton != null)
            {
                rowButton.onClick.RemoveAllListeners();
                CropData captured = crop;
                rowButton.onClick.AddListener(() => SelectBuyCrop(captured));
            }
        }
    }

    void SelectBuyCrop(CropData crop)
    {
        selectedBuyCrop = crop;
        buyQty = 1;

        if (buyDetailPanel != null) buyDetailPanel.SetActive(true);

        RefreshBuyDetail();
    }

    void ChangeBuyQty(int delta)
    {
        if (selectedBuyCrop == null) return;
        buyQty = Mathf.Max(1, buyQty + delta);
        RefreshBuyDetail();
    }

    void RefreshBuyDetail()
    {
        if (selectedBuyCrop == null) return;

        int cost = selectedBuyCrop.sellPrice * buyQty;
        int gold = PlayerWallet.Instance != null ? PlayerWallet.Instance.Gold : 0;

        if (buyCropNameText != null) buyCropNameText.text = selectedBuyCrop.cropName;
        if (buyQtyText != null) buyQtyText.text = buyQty.ToString();
        if (buyWalletText != null) buyWalletText.text = "You have $" + gold;
        if (buyCostText != null) buyCostText.text = "-$" + cost;
        if (buyRemainingText != null) buyRemainingText.text = "$" + (gold - cost) + " left";
        if (buyConfirmButton != null) buyConfirmButton.interactable = gold >= cost;
    }

    public void ConfirmPurchase()
    {
        if (selectedBuyCrop == null || PlayerWallet.Instance == null) return;

        int cost = selectedBuyCrop.sellPrice * buyQty;

        if (PlayerWallet.Instance.SpendGold(cost))
        {
            SeedInventory.Instance?.AddSeed(selectedBuyCrop, buyQty);
            tradeAudioSource?.Play();
            Debug.Log($"[ShopUI] Bought {buyQty}x {selectedBuyCrop.cropName} for ${cost}.");

            buyQty = 1;
            RefreshBuyDetail();
        }
        else
        {
            Debug.Log("[ShopUI] Not enough gold.");
        }
    }

    // ---------------------------------------------
    //  SELL TAB
    // ---------------------------------------------
    void RefreshSellTab()
    {
        if (sellRowPrefab == null || sellRowContainer == null) return;
        // Only the currently-open tab needs to redraw; the other tab's rows
        // just get skipped until the player switches to it.
        if (currentTab != ShopTab.Sell) return;

        while (sellRows.Count < tradeableCrops.Count)
            sellRows.Add(Instantiate(sellRowPrefab, sellRowContainer));

        for (int i = 0; i < sellRows.Count; i++)
            sellRows[i].SetActive(i < tradeableCrops.Count);

        for (int i = 0; i < tradeableCrops.Count; i++)
        {
            CropData crop = tradeableCrops[i];
            GameObject row = sellRows[i];
            if (crop == null) { row.SetActive(false); continue; }

            int owned = InventoryManager.Instance != null ? InventoryManager.Instance.GetCount(crop.cropName) : 0;

            TMP_Text nameText = row.transform.Find("NameText")?.GetComponent<TMP_Text>();
            TMP_Text countText = row.transform.Find("CountText")?.GetComponent<TMP_Text>();
            TMP_Text priceText = row.transform.Find("PriceText")?.GetComponent<TMP_Text>();
            Button rowButton = row.GetComponent<Button>();

            if (nameText != null) nameText.text = crop.cropName;
            if (countText != null) countText.text = "x" + owned;
            if (priceText != null) priceText.text = "$" + crop.sellPrice;

            if (rowButton != null)
            {
                rowButton.onClick.RemoveAllListeners();
                rowButton.interactable = owned > 0;
                CropData captured = crop;
                rowButton.onClick.AddListener(() => SelectSellCrop(captured));
            }
        }
    }

    void SelectSellCrop(CropData crop)
    {
        selectedSellCrop = crop;
        sellQty = 1;

        if (sellDetailPanel != null) sellDetailPanel.SetActive(true);

        RefreshSellDetail();
    }

    void ChangeSellQty(int delta)
    {
        if (selectedSellCrop == null) return;

        int owned = InventoryManager.Instance != null ? InventoryManager.Instance.GetCount(selectedSellCrop.cropName) : 0;
        sellQty = Mathf.Clamp(sellQty + delta, 1, Mathf.Max(1, owned));
        RefreshSellDetail();
    }

    void RefreshSellDetail()
    {
        if (selectedSellCrop == null) return;

        int owned = InventoryManager.Instance != null ? InventoryManager.Instance.GetCount(selectedSellCrop.cropName) : 0;
        sellQty = Mathf.Clamp(sellQty, 1, Mathf.Max(1, owned));

        int earnings = selectedSellCrop.sellPrice * sellQty;
        int gold = PlayerWallet.Instance != null ? PlayerWallet.Instance.Gold : 0;

        if (sellCropNameText != null) sellCropNameText.text = selectedSellCrop.cropName + " (have " + owned + ")";
        if (sellQtyText != null) sellQtyText.text = sellQty.ToString();
        if (sellWalletText != null) sellWalletText.text = "You have $" + gold;
        if (sellEarningsText != null) sellEarningsText.text = "+$" + earnings;
        if (sellRemainingText != null) sellRemainingText.text = "$" + (gold + earnings) + " total";
        if (sellConfirmButton != null) sellConfirmButton.interactable = owned > 0;
    }

    public void ConfirmSell()
    {
        if (selectedSellCrop == null || InventoryManager.Instance == null) return;

        int owned = InventoryManager.Instance.GetCount(selectedSellCrop.cropName);
        int qty = Mathf.Clamp(sellQty, 1, Mathf.Max(1, owned));
        if (qty <= 0 || owned <= 0) return;

        if (InventoryManager.Instance.RemoveCrop(selectedSellCrop.cropName, qty))
        {
            int earnings = selectedSellCrop.sellPrice * qty;
            PlayerWallet.Instance?.AddGold(earnings);
            tradeAudioSource?.Play();
            Debug.Log($"[ShopUI] Sold {qty}x {selectedSellCrop.cropName} for ${earnings}.");

            sellQty = 1;
            RefreshSellDetail();
        }
    }

    void RefreshGoldText()
    {
        if (goldText != null && PlayerWallet.Instance != null)
            goldText.text = "$" + PlayerWallet.Instance.Gold;
    }

    public void Open()
    {
        if (shopCanvas != null)
            shopCanvas.transform.SetPositionAndRotation(openPosition, openRotation);

        // Any other menu (backpack, pause) open at the same time would fight
        // over GameClock.IsPaused below -- Close() unconditionally clears it,
        // which would wrongly resume time if, say, the backpack were still
        // open underneath. The shop should have the screen to itself anyway.
        MenuManager.Instance?.CloseAllMenus();

        if (GameClock.Instance != null)
            GameClock.Instance.IsPaused = true;

        RefreshGoldText();

        selectedBuyCrop = null;
        selectedSellCrop = null;

        SelectTab(ShopTab.Buy);
    }

    public void Close()
    {
        if (shopCanvas != null)
            shopCanvas.transform.position = openPosition + HiddenOffset;

        if (GameClock.Instance != null)
            GameClock.Instance.IsPaused = false;
    }
}
