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

        SetState(TileState.Tilled);
        Debug.Log("[Farmland] Harvested " + harvested.cropName + "!");
        return harvested;
    }

    // Swap the visible crop model to match current growth stage
    void RefreshCropVisual()
    {
        if (cropData == null) return;

        int stage = cropData.GetStageForDay(daysWatered);
        if (stage == lastDisplayedStage) return;
        lastDisplayedStage = stage;

        if (spawnedCropModel != null) Destroy(spawnedCropModel);

        GameObject prefab = cropData.GetPrefabForStage(stage);
        if (prefab != null)
            spawnedCropModel = Instantiate(prefab, cropSpawnPoint.position, Quaternion.identity, cropSpawnPoint);
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