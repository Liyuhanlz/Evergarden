using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The backpack's Seeds tab (see MenuManager) -- a clickable grid of every
// crop the player has bought at least one seed of. Clicking one loads it
// into the reconfigurable Seed Bag (SeedBag.Instance) -- no need to be
// physically holding the bag first -- and the clicked cell's background
// switches to LoadedRowColor so it's obvious which seed is currently loaded.
// If nothing's ever been picked (or the player owns none of the loaded
// seed anymore), the bag simply has nothing to pour -- see SeedBag.Update.
//
// This used to also pop up next to the player whenever they held the bag
// (in or out of the shop), so it could be reconfigured on the spot. That
// popup kept reappearing and blocking the view, so picking now happens only
// here, in the backpack, same as every other tab.
//
// Unity setup:
//   1. Put this script on the "SeedPickerCanvas" World Space Canvas
//   2. Drag that same Canvas GameObject into Panel Root
//   3. Drag the row container Transform into Row Container, and the SeedRow
//      prefab (Icon / NameFallback / CountText children, Button on the root)
//      into Row Prefab
public class SeedPickerUI : MonoBehaviour
{
    public static SeedPickerUI Instance { get; private set; }

    [Header("Canvas")]
    public GameObject panelRoot;

    [Header("Rows")]
    public Transform rowContainer;
    public GameObject rowPrefab;

    [Header("Title")]
    [Tooltip("Left blank -- picking here is a plain click, same as every other backpack tab, so no " +
             "extra instruction is needed. Kept as a field in case a future tab wants a caption.")]
    public TMP_Text titleText;

    private SeedBag targetBag;
    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void OnEnable()
    {
        if (SeedInventory.Instance != null) SeedInventory.Instance.OnSeedsChanged += Refresh;
    }

    void OnDisable()
    {
        if (SeedInventory.Instance != null) SeedInventory.Instance.OnSeedsChanged -= Refresh;
    }

    // Backpack Seed tab -- positioned wherever MenuManager wants it (its
    // shared Tool/Seeds sub-panel slot below the backpack panel).
    public void OpenViewOnly(Vector3 position, Quaternion rotation)
    {
        // Picks straight into the one bag in the game (SeedBag.Instance),
        // regardless of whether it's currently held -- no need to physically
        // grab the bag first just to reconfigure it.
        targetBag = SeedBag.Instance;

        if (panelRoot != null)
        {
            panelRoot.transform.position = position;
            panelRoot.transform.rotation = rotation;
        }

        Show();
    }

    void Show()
    {
        Refresh();
        if (panelRoot != null) panelRoot.SetActive(true);

        // Freshly-instantiated rows (Refresh, above) can otherwise sit a
        // frame behind the UI raycaster's own canvas scan, making them
        // briefly un-clickable -- same fix as ShopUI's row lists.
        Canvas.ForceUpdateCanvases();
    }

    public void Close()
    {
        targetBag = null;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    static readonly Color DefaultRowColor = new Color(1f, 1f, 1f, 0.15f);
    static readonly Color LoadedRowColor = new Color(1f, 0.82f, 0.15f, 0.75f);

    private readonly List<CropData> ownedCrops = new List<CropData>();

    void Refresh()
    {
        if (titleText != null) titleText.text = "";

        if (rowPrefab == null || rowContainer == null || SeedInventory.Instance == null) return;

        // Only crops the player has actually bought at least one of -- an
        // empty picker (nothing purchased yet) is the correct state, not a
        // list of every known crop sitting at "x0". The bag simply won't
        // pour anything until a seed is picked (see SeedBag.Update).
        ownedCrops.Clear();
        foreach (CropData c in SeedInventory.Instance.knownCrops)
            if (c != null && SeedInventory.Instance.GetCount(c) > 0)
                ownedCrops.Add(c);

        // Which crop the bag is actually loaded with right now.
        CropData loadedCrop = SeedBag.Instance != null ? SeedBag.Instance.seedData : null;

        while (spawnedRows.Count < ownedCrops.Count)
            spawnedRows.Add(Instantiate(rowPrefab, rowContainer));

        for (int i = 0; i < spawnedRows.Count; i++)
            spawnedRows[i].SetActive(i < ownedCrops.Count);

        for (int i = 0; i < ownedCrops.Count; i++)
        {
            CropData crop = ownedCrops[i];
            GameObject row = spawnedRows[i];

            bool isLoaded = loadedCrop != null && loadedCrop == crop;

            TMP_Text countText = row.transform.Find("CountText")?.GetComponent<TMP_Text>();
            Button select = row.GetComponent<Button>();
            Image rowBackground = row.GetComponent<Image>();

            SetCellIcon(row, crop);
            if (countText != null) countText.text = "x" + SeedInventory.Instance.GetCount(crop);
            if (rowBackground != null) rowBackground.color = isLoaded ? LoadedRowColor : DefaultRowColor;

            if (select != null)
            {
                select.onClick.RemoveAllListeners();
                select.interactable = targetBag != null;

                CropData captured = crop;
                select.onClick.AddListener(() => PickSeed(captured));
            }
        }
    }

    // Icon if the crop has one (only Carrot does right now); otherwise falls
    // back to a small name label in the same spot so every crop stays
    // identifiable even without dedicated artwork yet.
    static void SetCellIcon(GameObject cell, CropData crop)
    {
        Image icon = cell.transform.Find("Icon")?.GetComponent<Image>();
        TMP_Text nameFallback = cell.transform.Find("NameFallback")?.GetComponent<TMP_Text>();

        bool hasIcon = crop.icon != null;

        if (icon != null)
        {
            icon.sprite = crop.icon;
            icon.enabled = hasIcon;
        }

        if (nameFallback != null)
        {
            nameFallback.gameObject.SetActive(!hasIcon);
            nameFallback.text = crop.cropName;
        }
    }

    void PickSeed(CropData crop)
    {
        if (targetBag == null) return;

        targetBag.seedData = crop;
        Debug.Log("[SeedPickerUI] Loaded " + crop.cropName + " into the seed bag.");

        // Stays open and just re-highlights the newly-loaded cell, same as
        // clicking any other backpack tab entry doesn't close the backpack.
        Refresh();
    }
}
