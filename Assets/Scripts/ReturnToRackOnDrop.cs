using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Attach to any tool that lives in the tool rack (Hoe, Watering Can, Seed
// Bag). Whenever the player opens the backpack's Tool tab, every such tool
// that isn't currently being held snaps straight back to its own socket --
// see MenuManager.SetInventoryTab, which calls ReturnAllNow(). Simpler and
// far more reliable than trying to catch the exact moment of a drop (a
// stray nearby socket claiming a released tool before this could react was
// a real, observed failure mode) -- it doesn't matter where a tool ended up
// or how long ago it was set down, opening the Tool tab always tidies up.
[RequireComponent(typeof(XRGrabInteractable))]
public class ReturnToRackOnDrop : MonoBehaviour
{
    [Tooltip("This tool's own socket -- where it snaps back to")]
    public XRSocketInteractor homeSocket;

    private static readonly List<ReturnToRackOnDrop> AllInstances = new List<ReturnToRackOnDrop>();

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable() => AllInstances.Add(this);
    void OnDisable() => AllInstances.Remove(this);

    // Called by MenuManager.SetInventoryTab whenever the Tool tab is opened.
    public static void ReturnAllNow()
    {
        foreach (ReturnToRackOnDrop t in AllInstances)
            t.ReturnIfNotHeld();
    }

    void ReturnIfNotHeld()
    {
        if (homeSocket == null) return;

        // Already exactly where it belongs -- leave it completely alone.
        // Forcibly cycling an already-correctly-seated tool's selection every
        // time the Tool tab opens (the previous version of this method did
        // exactly that) was itself a bug: it briefly deselects a tool that
        // was already fine, and ToolRackDrag's "only show the centered tool"
        // visibility toggle keys off socket selection -- caught in that gap,
        // a perfectly good tool could be left invisible.
        foreach (var interactor in grabInteractable.interactorsSelecting)
            if ((Object)interactor == homeSocket) return;

        // A real hand (or any non-socket interactor) actively holding it
        // should also block the return.
        foreach (var interactor in grabInteractable.interactorsSelecting)
            if (!(interactor is XRSocketInteractor)) return;

        // Reaching here means: either not selected by anything right now, or
        // claimed by some OTHER (wrong) socket -- both genuinely need fixing.
        if (grabInteractable.isSelected)
        {
            var manager = grabInteractable.interactionManager;
            if (manager != null)
            {
                try { manager.CancelInteractableSelection(grabInteractable); }
                catch { /* best-effort -- still proceed to force the position below */ }
            }
        }

        Transform target = homeSocket.attachTransform != null ? homeSocket.attachTransform : homeSocket.transform;
        transform.SetPositionAndRotation(target.position, target.rotation);

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
