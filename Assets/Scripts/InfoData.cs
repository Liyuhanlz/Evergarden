using System.Collections.Generic;
using UnityEngine;

// InfoData.cs -- ScriptableObject (DATA ONLY, no scene logic), same shape as
// CropData but for the Spyglass's educational content. Kept as its own asset
// type rather than added onto CropData/AnimalWander so the Spyglass system
// stays fully decoupled from growth/shop/inventory logic -- nothing here can
// regress those.
//
// Create one asset per crop or animal:
//   Right-click in Project window -> Create -> Education -> Info Data
// Drag it into the Informational component on that crop's growth-stage
// prefab(s) or on the animal's prefab.
[CreateAssetMenu(fileName = "NewInfo", menuName = "Education/Info Data")]
public class InfoData : ScriptableObject
{
    public enum Category { Crop, Animal, Tool }

    [Header("Identity")]
    public string displayName = "Unknown";
    public Category category = Category.Crop;

    [Header("Content")]
    [TextArea(2, 4)]
    [Tooltip("Shown first, as soon as the Spyglass is pointed at this -- one or two sentences")]
    public string summary;

    [Tooltip("Extra fun facts, paged through one at a time with the A button, in order. Keep each one short -- it reads like a dialogue line, not a paragraph.")]
    public List<string> facts = new List<string>();
}
