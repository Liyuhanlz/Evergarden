using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

// Makes a tool rack socket display whatever is actually seated in it, read
// from that tool's own Informational component, instead of a name baked
// into the socket itself. Sockets aren't tied to one specific tool this way
// -- any tool can sit in any socket and the label always matches what's
// really there.
//
// Unity setup (per socket):
//   1. Add this alongside the socket's existing XRSocketInteractor
//   2. Drag its own CueAnchor/Canvas/Cue TMP text into Nameplate Text
[RequireComponent(typeof(XRSocketInteractor))]
public class SocketNameplate : MonoBehaviour
{
    public TextMeshProUGUI nameplateText;

    [Tooltip("Shown while nothing is seated in this socket")]
    public string emptyText = "";

    private XRSocketInteractor socket;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
    }

    void OnEnable()
    {
        socket.selectEntered.AddListener(OnToolSeated);
        socket.selectExited.AddListener(OnToolRemoved);

        // Something may already be seated when this is enabled (e.g. a tool
        // parked here from a previous session before Play started) -- read
        // it immediately rather than waiting for the next select event.
        RefreshFromCurrentlySeated();
    }

    void OnDisable()
    {
        socket.selectEntered.RemoveListener(OnToolSeated);
        socket.selectExited.RemoveListener(OnToolRemoved);
    }

    void OnToolSeated(SelectEnterEventArgs args)
    {
        SetText(NameFor(args.interactableObject.transform));
    }

    void OnToolRemoved(SelectExitEventArgs args)
    {
        SetText(emptyText);
    }

    void RefreshFromCurrentlySeated()
    {
        if (socket.interactablesSelected.Count > 0)
        {
            Transform seated = (socket.interactablesSelected[0] as Component)?.transform;
            SetText(seated != null ? NameFor(seated) : emptyText);
        }
        else
        {
            SetText(emptyText);
        }
    }

    string NameFor(Transform tool)
    {
        Informational info = tool.GetComponent<Informational>();
        if (info != null && info.infoData != null)
            return info.infoData.displayName;

        // Falls back to the GameObject's own name so a tool that hasn't
        // gotten an InfoData asset yet still shows something readable
        // instead of going blank.
        return tool.name;
    }

    void SetText(string text)
    {
        if (nameplateText != null) nameplateText.text = text;
    }
}
