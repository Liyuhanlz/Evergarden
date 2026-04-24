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

    public Transform cropSpawnPoint;
    private GameObject spawnedCropModel;
    private int lastDisplayedStage = -1;

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
        if (state != TileState.Tilled || cropData != null) return false;

        cropData = data;
        daysWatered = 0;
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
        cropData = null;
        daysWatered = 0;
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