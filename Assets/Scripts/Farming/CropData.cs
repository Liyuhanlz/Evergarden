using System.Collections.Generic;
using UnityEngine;

// CropData.cs -- ScriptableObject (DATA ONLY, no scene logic)
// Create one asset per crop type:
//   Right-click in Project window -> Create -> Farming -> Crop Data
// Drag each asset into a SeedBag in the scene.

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

    [Header("Multi-Harvest (e.g. broccoli side shoots)")]
    [Tooltip("Real crops like broccoli, tomatoes, and lettuce keep producing after the first cut, " +
             "instead of being pulled up whole like a carrot. If true, harvesting this crop leaves " +
             "the plant in the ground to regrow for another (smaller) harvest, up to Max Harvests " +
             "times, before the plot is finally cleared.")]
    public bool isMultiHarvest = false;

    [Tooltip("Extra watered days needed to regrow a follow-up harvest -- usually much shorter than " +
             "the initial Days To Mature, since the plant is already established")]
    public int regrowDays = 2;

    [Tooltip("Total harvests obtainable from one planting before the plot is cleared. 1 = behaves " +
             "like a normal single-harvest crop even if Is Multi Harvest is on.")]
    public int maxHarvests = 1;

    [Tooltip("Yield multiplier for regrowth harvests (the 2nd and later) -- side shoots are smaller " +
             "than the main harvest")]
    [Range(0f, 1f)]
    public float regrowthYieldMultiplier = 0.5f;

    [Header("Pickable Fruit (crops with PickableFruit on their ready stage)")]
    [Tooltip("How many fruit grow on a plant each round, picked at random in this range (inclusive) " +
             "from the fruit spots on the ready-stage prefab -- e.g. a corn stalk grows 1-3 ears. " +
             "0,0 = use every fruit spot.")]
    public Vector2Int fruitCountRange = Vector2Int.zero;

    [Header("UI")]
    [Tooltip("Icon shown in the inventory slot -- drag a Sprite here")]
    public Sprite icon;

    [Header("Education (Magnifying Glass)")]
    [TextArea(2, 4)]
    [Tooltip("Shown first when the Magnifying Glass is pointed at this crop's tile -- one or two sentences")]
    public string summary;

    [Tooltip("Extra fun facts, paged through one at a time with the B button, in order")]
    public List<string> facts = new List<string>();

    [Tooltip("Citations for the summary, facts and real-farm text above, so the content can be checked " +
             "and cited -- e.g. \"University of Minnesota Extension. Growing tomatoes in home gardens. " +
             "https://extension.umn.edu/...\". Not shown in game; see Evergarden_Info_Text_and_Sources.txt.")]
    public List<string> sources = new List<string>();

    [Header("Real Farm vs. Game (Magnifying Glass)")]
    [Tooltip("How long the real crop takes, finishing the sentence \"Real farm: ready in ...\" " +
             "-- e.g. \"60 to 85 days after planting out\". Leave empty to skip the comparison page.")]
    public string realGrowTime;

    [Tooltip("Optional second sentence for the Farm page -- e.g. \"Farmers stake or cage the plants " +
             "to keep the fruit off the ground!\" (a period is added if it has no ending punctuation)")]
    public string realHarvestPattern;

    // Helpers

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

    // The Magnifying Glass's "real farm vs. this game" pages -- two short
    // pages rather than one long one, so the text stays big enough to read
    // in VR. The game page is built from this asset's own growth settings,
    // so it never goes stale when those are tuned. Empty if no real-world
    // info has been filled in.
    public string RealFarmPage()
    {
        if (string.IsNullOrWhiteSpace(realGrowTime)) return "";

        string page = "On a real farm: ready in " + realGrowTime + ".";
        if (!string.IsNullOrWhiteSpace(realHarvestPattern))
        {
            string pattern = realHarvestPattern.Trim();
            page += " " + pattern + (EndsWithPunctuation(pattern) ? "" : ".");
        }
        return page;
    }

    static bool EndsWithPunctuation(string s)
    {
        char last = s[s.Length - 1];
        return last == '.' || last == '!' || last == '?';
    }

    public string GamePage()
    {
        if (string.IsNullOrWhiteSpace(realGrowTime)) return "";

        string page = "In this game: ready after " + daysToMature + (daysToMature == 1 ? " watered day" : " watered days");
        if (isMultiHarvest && maxHarvests > 1)
            page += ", then picked " + maxHarvests + " times, regrowing in " + regrowDays + (regrowDays == 1 ? " day" : " days");
        else
            page += ", harvested once";
        if (fruitCountRange.y > 0)
            page += " (" + fruitCountRange.x + " to " + fruitCountRange.y + " per plant)";

        return page + ". Game days are sped up so you can watch it grow!";
    }

    // True when the crop has been watered enough days to harvest
    public bool IsReadyToHarvest(int daysWatered)
    {
        return daysWatered >= daysToMature;
    }
}