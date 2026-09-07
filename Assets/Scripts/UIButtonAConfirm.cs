using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

// Attach next to any Button to let it also be "clicked" by pressing A (either
// hand) while the ray is hovering it -- an alternative to trigger-clicking,
// matching how most VR titles offer face-button confirm as a backup to the
// trigger (which XRI's ray interactor already uses by default for the click
// itself). Doesn't replace or interfere with the normal click in any way.
[RequireComponent(typeof(Button))]
public class UIButtonAConfirm : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    static UIButtonAConfirm currentHovered;
    static InputDevice leftHandDevice;
    static InputDevice rightHandDevice;
    static bool prevAPressed;

    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && button.interactable)
            currentHovered = this;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentHovered == this)
            currentHovered = null;
    }

    void OnDisable()
    {
        if (currentHovered == this)
            currentHovered = null;
    }

    void Update()
    {
        // Only the currently-hovered instance needs to actually poll/dispatch.
        if (currentHovered != this) return;

        if (!leftHandDevice.isValid) leftHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!rightHandDevice.isValid) rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool aPressed = false;
        if (leftHandDevice.isValid && leftHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool leftA) && leftA)
            aPressed = true;
        if (rightHandDevice.isValid && rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool rightA) && rightA)
            aPressed = true;

        if (aPressed && !prevAPressed && button != null && button.interactable)
            button.onClick.Invoke();

        prevAPressed = aPressed;
    }
}
