using UnityEngine;


public class Farmland : MonoBehaviour
{
    // Tile state
    public enum TileState { Grass, Tilled, Planted, Watered }

    [Header("Current State (read-only at runtime)")]
    public TileState state = TileState.Grass;

    // Visuals
    [Header("Visuals")]
    public GameObject grassModel;
    public GameObject tilledModel;
    public Renderer blockRenderer;
    public Material tilledMat;
    public Material wateredMat;

    // Crop tracking
    [Header("Crop (set at runtime)")]
    public CropData cropData;
    public int daysWatered = 0;
    public int timesHarvested = 0;

    [Header("Soil Health (0-100, read-only at runtime)")]
    [Tooltip("Nutrient level of this plot. Depletes when you harvest, recovers while left fallow " +
             "(tilled but unplanted). Real farmland works the same way -- repeated planting drains " +
             "the specific nutrients that crop needs, rotating to a different crop is gentler, and " +
             "resting the field lets it recover.")]
    public float soilHealth = 100f;

    [Tooltip("Soil below this refuses new plantings -- it needs to rest fallow, or be rotated to a " +
             "different crop, before it recovers enough to plant again")]
    public float minHealthToPlant = 15f;

    [Tooltip("Fertility lost on harvest when the same crop is grown here twice in a row")]
    public float sameCropDepletion = 20f;

    [Tooltip("Fertility lost on harvest when a different crop is grown here than last time -- " +
             "crop rotation is easier on the soil than monoculture")]
    public float rotatedCropDepletion = 6f;

    [Tooltip("Fertility regained per day this tile sits tilled and empty (fallow)")]
    public float fallowRecoveryPerDay = 3f;

    private string lastHarvestedCropName;

    public Transform cropSpawnPoint;
    private GameObject spawnedCropModel;
    private int lastDisplayedStage = -1;
    private int fruitRemaining = 0;

    // Set by Harvest() so FarmManager can scale the yield down for a
    // regrowth (side-shoot) harvest instead of a full first harvest.
    public bool LastHarvestWasRegrowth { get; private set; }

    // FarmManager subscribes to this to show harvest alerts
    public event System.Action<Farmland> OnReadyToHarvest;

    void Start()
    {
        SetState(TileState.Grass);

        if (GameClock.Instance != null)
            GameClock.Instance.OnNewDay += HandleNewDay;
        else
            Debug.LogWarning("[Farmland] No GameClock found. Crop growth won't work.");
    }

    void OnDestroy()
    {
        if (GameClock.Instance != null)
            GameClock.Instance.OnNewDay -= HandleNewDay;
    }

    // Called by GameClock every in-game day
    void HandleNewDay()
    {
        if (state == TileState.Watered && cropData != null)
        {
            daysWatered++;
            RefreshCropVisual();

            if (cropData.IsReadyToHarvest(daysWatered))
            {
                Debug.Log("[Farmland] " + cropData.cropName + " is ready to harvest!");
                OnReadyToHarvest?.Invoke(this);
            }
        }

        // Reset watered tiles back to planted each morning
        if (state == TileState.Watered)
            SetState(TileState.Planted);

        // Resting: tilled but nothing planted lets the soil recover
        if (state == TileState.Tilled && cropData == null)
            soilHealth = Mathf.Clamp(soilHealth + fallowRecoveryPerDay, 0f, 100f);
    }

    // State machine
    public void SetState(TileState newState)
    {
        state = newState;

        if (grassModel) grassModel.SetActive(newState == TileState.Grass);
        if (tilledModel) tilledModel.SetActive(newState != TileState.Grass);

        if (blockRenderer != null)
        {
            if (newState == TileState.Tilled || newState == TileState.Planted)
                blockRenderer.material = tilledMat;
            else if (newState == TileState.Watered)
                blockRenderer.material = wateredMat;
        }
    }

    // Called by SeedBag particle collision
    public bool TryPlant(CropData data)
    {
        if (data == null) return false;
        if (state != TileState.Tilled || cropData != null) return false;

        if (soilHealth < minHealthToPlant)
        {
            Debug.Log("[Farmland] Soil too depleted to plant here (" + soilHealth.ToString("F0") +
                      "%). Let it rest fallow, or rotate to a different crop next time.");
            return false;
        }

        // Seeds are a real, merchant-bought resource (see SeedInventory/ShopUI) --
        // checked last so a doomed planting attempt (bad tile state, depleted
        // soil) never costs the player a seed they never had a chance to plant.
        if (SeedInventory.Instance != null && !SeedInventory.Instance.RemoveSeed(data.cropName, 1))
        {
            Debug.Log("[Farmland] No " + data.cropName + " seeds left -- buy more from the merchant.");
            return false;
        }

        cropData = data;
        daysWatered = 0;
        timesHarvested = 0;
        lastDisplayedStage = -1;
        SetState(TileState.Planted);
        RefreshCropVisual();
        Debug.Log("[Farmland] Planted " + data.cropName + ".");
        return true;
    }

    // Called by FarmManager when player harvests
    public CropData Harvest()
    {
        if (cropData == null || !cropData.IsReadyToHarvest(daysWatered)) return null;

        CropData harvested = cropData;
        timesHarvested++;

        // Same crop twice in a row drains the soil harder than rotating --
        // regrowth (side-shoot) harvests are gentler since it's still the
        // same planting, not a fresh crop drawing on the same nutrients again.
        bool willRegrow = harvested.isMultiHarvest && timesHarvested < harvested.maxHarvests;
        float depletion = harvested.cropName == lastHarvestedCropName ? sameCropDepletion : rotatedCropDepletion;
        if (willRegrow) depletion *= 0.5f;
        soilHealth = Mathf.Clamp(soilHealth - depletion, 0f, 100f);

        LastHarvestWasRegrowth = timesHarvested > 1;

        if (willRegrow)
        {
            // Leave the plant in place -- rewind daysWatered so it needs
            // regrowDays more waterings before the next (smaller) harvest,
            // reusing the same maturity/stage math instead of a parallel system.
            daysWatered = Mathf.Max(0, harvested.daysToMature - harvested.regrowDays);
            lastDisplayedStage = -1;
            RefreshCropVisual();
            SetState(TileState.Planted);
            Debug.Log("[Farmland] Harvested " + harvested.cropName + " -- it will regrow for another round.");
            return harvested;
        }

        lastHarvestedCropName = harvested.cropName;
        cropData = null;
        daysWatered = 0;
        timesHarvested = 0;
        lastDisplayedStage = -1;

        if (spawnedCropModel != null)
        {
            Destroy(spawnedCropModel);
            spawnedCropModel = null;
        }
        fruitRemaining = 0;

        SetState(TileState.Tilled);
        Debug.Log("[Farmland] Harvested " + harvested.cropName + "!");
        return harvested;
    }

    // True when the currently shown stage has individually pickable fruit
    // on it -- those crops are harvested by grabbing fruit, not by pressing A
    public bool HasPickableFruit => fruitRemaining > 0;

    // Live growth status for the Magnifying Glass -- how long until harvest,
    // whether it still needs water today, and which harvest this is for
    // crops that regrow. Empty if nothing is planted.
    public string GrowthStatusText()
    {
        if (cropData == null) return "";

        string text;
        if (cropData.IsReadyToHarvest(daysWatered))
        {
            text = "Ready to harvest now!";
            if (fruitRemaining > 0)
                text += " " + fruitRemaining + " left to pick.";
        }
        else
        {
            int left = cropData.daysToMature - daysWatered;
            text = (timesHarvested > 0 ? "Regrowing: " : "") + left +
                   (left == 1 ? " more watered day" : " more watered days") + " until harvest.";
            text += state == TileState.Watered ? " Already watered today." : " Needs water today!";
        }

        if (cropData.isMultiHarvest && cropData.maxHarvests > 1)
            text += "\nHarvest " + (timesHarvested + 1) + " of " + cropData.maxHarvests + ".";

        return text;
    }

    // Called by PickableFruit when the player plucks one fruit off the stem
    public void PickFruit(PickableFruit fruit)
    {
        if (cropData == null || fruitRemaining <= 0) return;

        fruitRemaining--;
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.AddCrop(cropData, 1);

        // Last fruit gone -- run the normal harvest (regrow or clear the plot)
        if (fruitRemaining == 0 && FarmManager.Instance != null)
            FarmManager.Instance.FinishFruitHarvest(this);
    }

    // Swap the visible crop model to match current growth stage
    void RefreshCropVisual()
    {
        if (cropData == null) return;

        int stage = cropData.GetStageForDay(daysWatered);
        bool ready = cropData.IsReadyToHarvest(daysWatered);

        // A regrowing fruiting plant (tomato) keeps its full-size body instead
        // of shrinking back to a sapling -- only the fruit grows back, hidden
        // until ripe. Crops without pickable fruit (broccoli) regrow through
        // their normal stages, since their last stage IS the harvestable part.
        if (timesHarvested > 0 && LastStageHasFruit())
            stage = cropData.StageCount - 1;

        int displayKey = stage * 2 + (ready ? 1 : 0);
        if (displayKey == lastDisplayedStage) return;
        lastDisplayedStage = displayKey;

        if (spawnedCropModel != null) Destroy(spawnedCropModel);
        fruitRemaining = 0;

        GameObject prefab = cropData.GetPrefabForStage(stage);
        if (prefab != null)
        {
            spawnedCropModel = Instantiate(prefab, cropSpawnPoint.position, Quaternion.identity, cropSpawnPoint);
            SetUpFruit(ready);
            AddInfoVolume();
        }
    }

    // Name of the detection-only trigger wrapped around the visible crop --
    // MagnifyingGlassTarget also uses it to place its window beside the plant
    public const string InfoVolumeName = "InfoVolume";

    // Crop models have no colliders of their own, so the Magnifying Glass
    // (which finds things by colliders near its lens) could only see the
    // tile cube at ground level -- fine for knee-high crops, useless for a
    // 1.5 m tomato plant. This wraps the whole plant in a trigger box. It's
    // on Ignore Raycast and is a trigger, so hands, rays, seed/water
    // particles and the player's body all ignore it.
    void AddInfoVolume()
    {
        Renderer[] renderers = spawnedCropModel.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

        GameObject volume = new GameObject(InfoVolumeName);
        volume.layer = 2; // Ignore Raycast
        volume.transform.SetParent(spawnedCropModel.transform, false);
        volume.transform.position = bounds.center;

        BoxCollider box = volume.AddComponent<BoxCollider>();
        box.isTrigger = true;
        Vector3 scale = volume.transform.lossyScale;
        Vector3 size = Vector3.Max(bounds.size, Vector3.one * 0.15f);
        box.size = new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z);
    }

    bool LastStageHasFruit()
    {
        GameObject last = cropData.GetPrefabForStage(cropData.StageCount - 1);
        return last != null && last.GetComponentInChildren<PickableFruit>(true) != null;
    }

    // Hooks up the pickable fruit on a freshly spawned stage. Regrowth
    // rounds show fewer fruit (Regrowth Yield Multiplier), like a plant's
    // later, smaller flushes.
    void SetUpFruit(bool ready)
    {
        PickableFruit[] fruit = spawnedCropModel.GetComponentsInChildren<PickableFruit>();
        if (fruit.Length == 0) return;

        int count = fruit.Length;
        Vector2Int range = cropData.fruitCountRange;
        if (range.y > 0)
            count = Mathf.Clamp(Random.Range(range.x, range.y + 1), 1, fruit.Length);
        if (timesHarvested > 0)
            count = Mathf.Clamp(Mathf.RoundToInt(count * cropData.regrowthYieldMultiplier), 1, count);
        if (!ready) count = 0;

        // Shuffle so a smaller flush isn't always the same fruit positions
        for (int i = fruit.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (fruit[i], fruit[j]) = (fruit[j], fruit[i]);
        }

        for (int i = 0; i < fruit.Length; i++)
        {
            if (i < count) fruit[i].owner = this;
            else Destroy(fruit[i].gameObject);
        }

        fruitRemaining = count;
    }

    // Hoe -> Tilled
    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Hoe") && state == TileState.Grass)
        {
            SetState(TileState.Tilled);
            Debug.Log("[Farmland] Tilled!");
        }
    }

    // Particle collisions
 
    private void OnParticleCollision(GameObject other)
    {
        if (other.CompareTag("Seed") && state == TileState.Tilled)
        {
            SeedBag bag = other.GetComponentInParent<SeedBag>();
            if (bag != null && bag.seedData != null)
                TryPlant(bag.seedData);

            //Debug.Log("[Farmland] Planted!");
        }

        if (other.CompareTag("Water") && state == TileState.Planted)
        {
            SetState(TileState.Watered);
            Debug.Log("[Farmland] Watered!");
        }
    }
}