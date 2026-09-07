using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

// Scrolls the tool rack's Track left/right using the right-hand thumbstick
// while the tool inventory is open, settling on whichever socket is nearest
// center when the stick returns to neutral. Only the centered tool is shown
// -- every other tool's renderers/colliders are switched off, so the window
// only ever presents one tool at a time no matter how many sockets are on
// the rack.
//
// This replaced an earlier grab-and-drag version. A physical ray/hand grab
// requires XRI to keep re-validating that the interactor is still aimed at
// (or touching) the held object, and dragging sideways is exactly the
// motion that swings a ray off a thin handle -- it kept releasing before
// any drag distance registered, and couldn't be reliably tested without a
// headset. Reading a thumbstick axis has none of that failure mode.
public class ToolRackDrag : MonoBehaviour
{
    [Tooltip("The rack of sockets that slides as you scroll")]
    public Transform track;

    [Tooltip("The sockets on the track, left to right, in the same order they're placed. " +
             "Used to figure out which one is centered so every other tool can be hidden.")]
    public XRSocketInteractor[] slots;

    [Tooltip("Distance between adjacent tool sockets on the track -- also the snap step")]
    public float spacing = 0.6f;

    [Tooltip("How many sockets are on the track (rack spans (count-1) * spacing)")]
    public int socketCount = 3;

    [Tooltip("Units per second the rack scrolls while the stick is fully deflected")]
    public float scrollSpeed = 0.8f;

    [Tooltip("Units per second the rack glides to the nearest tool once the stick is released")]
    public float snapSpeed = 2.5f;

    [Tooltip("Stick deflection below this is ignored, so it settles instead of drifting")]
    public float stickDeadzone = 0.2f;

    private float minX;
    private float maxX;
    private bool snapping;
    private float snapTargetX;
    private int visibleSlot = -1;

    void Awake()
    {
        float half = Mathf.Max(0, socketCount - 1) * spacing * 0.5f;
        minX = -half;
        maxX = half;
    }

    void Update()
    {
        if (track == null) return;

        bool isOpen = MenuManager.Instance != null && MenuManager.Instance.ToolInventoryOpen;
        float stickX = 0f;

        if (isOpen)
        {
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand)
                .TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis);
            stickX = axis.x;
            if (Mathf.Abs(stickX) < stickDeadzone)
                stickX = 0f;
        }

        if (stickX != 0f)
        {
            snapping = false;

            Vector3 pos = track.localPosition;
            pos.x = Mathf.Clamp(pos.x + stickX * scrollSpeed * Time.deltaTime, minX, maxX);
            track.localPosition = pos;
        }
        else if (!snapping)
        {
            // Stick just returned to neutral (or the panel just closed) --
            // start gliding to whichever socket is nearest center.
            snapTargetX = Mathf.Clamp(Mathf.Round(track.localPosition.x / spacing) * spacing, minX, maxX);
            snapping = true;
        }

        if (snapping)
        {
            Vector3 pos = track.localPosition;
            pos.x = Mathf.MoveTowards(pos.x, snapTargetX, snapSpeed * Time.deltaTime);
            track.localPosition = pos;
        }

        UpdateVisibleSlot();
    }

    void UpdateVisibleSlot()
    {
        if (slots == null || slots.Length == 0 || track == null) return;

        // Whichever socket sits nearest panel-local x = 0 is the one
        // currently "in the window".
        int nearest = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            float panelX = track.localPosition.x + slots[i].transform.localPosition.x;
            float dist = Mathf.Abs(panelX);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = i;
            }
        }

        if (nearest == visibleSlot) return;
        visibleSlot = nearest;

        for (int i = 0; i < slots.Length; i++)
        {
            bool visible = i == nearest;
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
