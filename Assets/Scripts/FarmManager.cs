using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class FarmManager : MonoBehaviour
{
    public static FarmManager Instance { get; private set; }

    [Header("All Farmland Tiles")]
    public List<Farmland> allTiles = new List<Farmland>();

    [Header("Harvest Settings")]
    public float harvestRadius = 2f;

    [Header("Player Reference")]
    public Transform playerTransform;

    private List<Farmland> readyTiles = new List<Farmland>();

    private InputDevice rightHandDevice;
    private bool prevAPressed = false;

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
        GetRightHandDevice();
    }

    void Update()
    {
        HandleHarvestInput();
    }

    void HandleHarvestInput()
    {
        if (!rightHandDevice.isValid)
        {
            GetRightHandDevice();
            return;
        }

        if (rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool aPressed))
        {
            if (aPressed && !prevAPressed)
            {
                TryHarvestNearest();
            }

            prevAPressed = aPressed;
        }
    }

    void GetRightHandDevice()
    {
        rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
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

        if (HUD.Instance != null)
            HUD.Instance.ShowHarvestAlert(tile.cropData.cropName);
    }

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
            int amount = result.harvestYield;

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddCrop(result, amount);
            }
            else
            {
                Debug.LogWarning("[FarmManager] InventoryManager instance missing.");
            }

            readyTiles.Remove(closest);

            if (readyTiles.Count == 0 && HUD.Instance != null)
                HUD.Instance.HideHarvestAlert();
        }

        return result;
    }
}