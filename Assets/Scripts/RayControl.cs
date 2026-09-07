using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

// Attach to the same GameObject as your XR Ray Interactor.

[RequireComponent(typeof(XRRayInteractor))]
public class RayControl : MonoBehaviour
{
    [Tooltip("Assign the trigger action used for ray grabbing " +
             "(XRI RightHand Interaction/Select or whichever hand this ray is on)")]
    public InputActionProperty grabTriggerAction;

    private XRRayInteractor rayInteractor;

    void Awake()
    {
        rayInteractor = GetComponent<XRRayInteractor>();
    }

    void Update()
    {
        bool triggerHeld = grabTriggerAction.action != null &&
                           grabTriggerAction.action.IsPressed();

        bool holdingObject = rayInteractor.interactablesSelected.Count > 0;

        // Block NEW selections only when trigger is held AND something is
        // already grabbed, so a grabbed object doesn't fight the ray for the
        // same trigger press. Previously this disabled the whole component,
        // which also killed XRInteractorLineVisual (the ray disappeared) and
        // UI raycasting (backpack/shop clicks stopped working) for as long as
        // the trigger was held -- allowSelect blocks new selects without
        // touching either of those, so the ray and UI clicks keep working.
        rayInteractor.allowSelect = !(triggerHeld && holdingObject);
    }
}