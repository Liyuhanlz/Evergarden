using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Tracks whether the player currently has the Magnifying Glass actually in
// hand -- MagnifyingGlassTarget checks this before showing anything, so
// info only appears while the tool is out, not just because the player's
// ray happens to be aimed at a crop or animal.
//
// Same "ignore the tool rack's own socket selection" pattern as SeedBag --
// the rack socket holds/selects the tool too (that's how it stays seated
// there between uses), and that must not count as the player holding it.
//
// Unity setup:
//   1. Add this + XRGrabInteractable to the Magnifying Glass GameObject
//   2. Give it a home socket + ReturnToRackOnDrop like the other tools if it
//      should live in the tool rack
[RequireComponent(typeof(XRGrabInteractable))]
public class MagnifyingGlassTool : MonoBehaviour
{
    [Tooltip("True only while a real hand -- not the tool rack's socket -- is holding this")]
    public static bool IsEquipped { get; private set; }

    private XRGrabInteractable grabInteractable;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrab);
        grabInteractable.selectExited.RemoveListener(OnRelease);

        // Safety: if this object is disabled/destroyed while held, don't
        // leave IsEquipped stuck true with no way to clear it.
        IsEquipped = false;
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) return;
        IsEquipped = true;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) return;
        IsEquipped = false;
    }
}
