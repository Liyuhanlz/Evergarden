using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

// Steps the tool rack one tool at a time with the right-hand thumbstick while
// the tool inventory is open: each flick left/right moves to the previous/
// next socket in Slots (wrapping around past either end) and jumps the Track
// so that socket sits exactly at panel center. Only the selected tool is shown -- every other tool's
// renderers/colliders are switched off, so the window only ever
// presents one tool at a time no matter how many sockets are on the rack.
//
// This replaced a free-scrolling version that snapped the Track to multiples
// of a fixed spacing -- with an even number of sockets those snap points fell
// halfway between two tools, so nothing was ever actually centered. Centering
// on each socket's own position has no such dependency on count or spacing.
//
// That in turn replaced an earlier grab-and-drag version. A physical ray/hand grab
// requires XRI to keep re-validating that the interactor is still aimed at
// (or touching) the held object, and dragging sideways is exactly the
// motion that swings a ray off a thin handle -- it kept releasing before
// any drag distance registered, and couldn't be reliably tested without a
// headset. Reading a thumbstick axis has none of that failure mode.
public class ToolRackDrag : MonoBehaviour
{
    [Tooltip("The rack of sockets that moves to center the selected tool")]
    public Transform track;

    [Tooltip("The sockets on the track, left to right, in the same order they're placed. " +
             "The stick steps through them in this order.")]
    public XRSocketInteractor[] slots;

    [Tooltip("Stick deflection past this counts as a flick to the next/previous tool")]
    public float stickThreshold = 0.6f;

    [Tooltip("The stick must return below this before another flick registers, " +
             "so holding it over steps only once")]
    public float stickResetThreshold = 0.3f;

    private int selectedSlot = 0;
    private bool stickArmed = true;
    private int visibleSlot = -1;

    // A tool seated after the last visibility pass (e.g. snapped back by
    // ReturnToRackOnDrop into an off-center socket) would otherwise stay fully
    // visible, so force a fresh pass whenever any socket picks one up.
    void OnEnable()
    {
        if (slots == null) return;
        foreach (var slot in slots)
            if (slot != null) slot.selectEntered.AddListener(OnSlotSeated);
    }

    void OnDisable()
    {
        if (slots == null) return;
        foreach (var slot in slots)
            if (slot != null) slot.selectEntered.RemoveListener(OnSlotSeated);
    }

    void OnSlotSeated(SelectEnterEventArgs args) => visibleSlot = -1;

    void Update()
    {
        if (track == null) return;

        if (slots == null || slots.Length == 0) return;

        bool isOpen = MenuManager.Instance != null && MenuManager.Instance.ToolInventoryOpen;
        float stickX = 0f;

        if (isOpen)
        {
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand)
                .TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis);
            stickX = axis.x;
        }

        if (Mathf.Abs(stickX) < stickResetThreshold)
        {
            stickArmed = true;
        }
        else if (stickArmed && Mathf.Abs(stickX) >= stickThreshold)
        {
            stickArmed = false;
            // Wraps around at either end, so the rack loops in both directions.
            int step = stickX > 0f ? 1 : -1;
            selectedSlot = (selectedSlot + step + slots.Length) % slots.Length;
        }

        CenterSelectedSlot();
        UpdateVisibleSlot();
    }

    // Offsets the Track by the selected socket's own local x, so that socket
    // lands exactly at panel-local x = 0 regardless of how the sockets are spaced.
    void CenterSelectedSlot()
    {
        if (slots[selectedSlot] == null) return;

        Vector3 pos = track.localPosition;
        pos.x = -slots[selectedSlot].transform.localPosition.x;
        track.localPosition = pos;
    }

    void UpdateVisibleSlot()
    {
        if (selectedSlot == visibleSlot) return;
        visibleSlot = selectedSlot;

        for (int i = 0; i < slots.Length; i++)
        {
            bool visible = i == selectedSlot;
            SetSeatedToolVisible(slots[i], visible);
            SetCueVisible(slots[i], visible);
        }
    }

    // Shows/hides whatever tool is currently seated in this socket, without
    // touching the socket's own selection state -- disabling the whole
    // GameObject would unregister the interactable and drop it, so only its
    // renderers and colliders are toggled.
    static void SetSeatedToolVisible(XRSocketInteractor socket, bool visible)
    {
        if (socket == null || socket.interactablesSelected.Count == 0) return;

        Transform tool = (socket.interactablesSelected[0] as Component)?.transform;
        if (tool == null) return;

        foreach (var renderer in tool.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = visible;

        foreach (var collider in tool.GetComponentsInChildren<Collider>(true))
            collider.enabled = visible;
    }

    // Each socket's own name-cue Canvas -- shown only for the centered slot,
    // same as the tool model itself, so only one label is ever on screen at
    // once no matter how many sockets the rack has. Lives under a CueAnchor
    // wrapper (a plain Transform carrying the height offset -- the Canvas's
    // own RectTransform position doesn't reliably survive a scene reload, so
    // the offset was moved onto a plain Transform instead), so it's found by
    // a two-level path rather than a direct child lookup. Falls back to a
    // direct "Canvas" child for any socket that predates the wrapper.
    static void SetCueVisible(XRSocketInteractor socket, bool visible)
    {
        if (socket == null) return;

        Transform cue = socket.transform.Find("CueAnchor/Canvas");
        if (cue == null) cue = socket.transform.Find("Canvas");
        if (cue != null) cue.gameObject.SetActive(visible);
    }
}
