using UnityEngine;

// Marks a crop growth-stage prefab or animal prefab as something the
// Spyglass can identify, and holds which InfoData asset to show for it.
// Pure data holder -- SpyglassTarget is what actually reacts to being
// pointed at (same split as CropData holding data vs Farmland driving the
// tile's behavior).
//
// Unity setup:
//   1. Drop this on the GameObject (each growth-stage prefab for a crop, or
//      the animal's prefab)
//   2. Drag the matching InfoData asset into Info Data
//   3. Also add SpyglassTarget alongside it -- that's what makes it actually
//      respond to the Spyglass's ray
public class Informational : MonoBehaviour
{
    [Tooltip("What the Spyglass should show when pointed at this")]
    public InfoData infoData;
}
