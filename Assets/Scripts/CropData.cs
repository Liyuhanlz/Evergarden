using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "NewCrop", menuName = "Farming/Crop Data")]
public class CropData : ScriptableObject
{
    [Header("Identity")]
    public string cropName = "Unknown Crop";

    [Header("Growth")]
    [Tooltip("How many watered in-game days until this crop is harvest-ready")]
    public int daysToMature = 4;

    [Tooltip("One prefab per growth stage. Index 0 = seedling, last = fully grown.")]
    public List<GameObject> growthStagePrefabs = new List<GameObject>();

    [Header("Harvest")]
    [Tooltip("How many items the player receives on harvest")]
    public int harvestYield = 1;

    [Tooltip("Base sell price per item in the shop")]
    public int sellPrice = 10;

    // „Ÿ„Ÿ Helpers „Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ„Ÿ

    // Total number of visual stages (driven by how many prefabs you assign)
    public int StageCount => growthStagePrefabs.Count;

    // Returns the prefab for a given stage index, or null if out of range
    public GameObject GetPrefabForStage(int stage)
    {
        if (stage < 0 || stage >= growthStagePrefabs.Count) return null;
        return growthStagePrefabs[stage];
    }

    // Given how many days have been watered, returns which visual stage to show
    // Clamps so it never exceeds the last stage
    public int GetStageForDay(int daysWatered)
    {
        if (StageCount == 0) return 0;
        float progress = (float)daysWatered / daysToMature;
        int stage = Mathf.FloorToInt(progress * (StageCount - 1));
        return Mathf.Clamp(stage, 0, StageCount - 1);
    }

    // True when the crop has been watered enough days to harvest
    public bool IsReadyToHarvest(int daysWatered)
    {
        return daysWatered >= daysToMature;
    }


}
