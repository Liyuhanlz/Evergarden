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

        // Disable ray only when trigger is held AND something is grabbed
        // This keeps the ray active for UI clicks and empty-air trigger pulls
        rayInteractor.enabled = !(triggerHeld && holdingObject);
    }
}