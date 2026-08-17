using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.Events;

// Generic "look at it to interact" prompt. Shows promptCanvas whenever the
// player is within range AND looking roughly at this object, and fires
// onInteractPressed when they press the right-hand A button while the prompt
// is showing. Drop this on any interactable (shop booth, hoe, watering can,
// ...) instead of writing bespoke proximity/gaze logic for each one.
//
// Multiple objects can share one Prompt Canvas (e.g. one shared "Hold Grip
// to Grab" canvas for several tools). Gaze state is computed in Update() and
// the shared canvas is only claimed in LateUpdate(), after every instance's
// Update() has already run -- this guarantees whichever object is actually
// gazed at this frame decides the canvas, instead of the result depending on
// arbitrary script execution order within the same Update pass.
//
// Unity setup:
//   1. Add this script to the interactable GameObject (or an empty child
//      positioned at a sensible "look at me" point, e.g. the object's center)
//   2. Tune Range and Max Gaze Angle to taste
//   3. Drag a small World Space prompt Canvas into Prompt Canvas -- the same
//      canvas can be shared across multiple GazeInteractables
//   4. Either wire On Interact Pressed in the Inspector to whatever this
//      object should do, or have another script on the same GameObject call
//      GetComponent<GazeInteractable>().onInteractPressed.AddListener(...)
//      in Awake (see MerchantBooth for an example of the latter)
public class GazeInteractable : MonoBehaviour
{
    [Header("Gaze Detection")]
    [Tooltip("Max distance from the player's head at which this becomes interactable")]
    public float range = 4f;

    [Tooltip("Max angle (degrees) between the player's look direction and this object for the prompt to appear")]
    public float maxGazeAngle = 35f;

    [Tooltip("Extra angle (degrees) beyond Max Gaze Angle before the prompt disappears again -- gives forgiveness so small head movements don't instantly dismiss it")]
    public float gazeAngleHysteresis = 15f;

    [Tooltip("Drag the player's camera here -- defaults to Camera.main if left empty")]
    public Transform playerCamera;

    [Header("Prompt")]
    public Canvas promptCanvas;

    [Tooltip("World-space offset from this object's current position where the prompt floats. Tracked every frame -- keeps the prompt correctly placed even if this object moves (e.g. grabbed, or auto-socketed into a holder at scene start)")]
    public Vector3 promptOffset = new Vector3(0f, 0.4f, 0f);

    [Tooltip("Keep the prompt facing the player. Recommended for anything that can be approached from more than one side (most grabbable props)")]
    public bool billboardPrompt = true;

    [Header("Events")]
    [Tooltip("Fires when the player presses the right-hand A button while this is being gazed at")]
    public UnityEvent onInteractPressed;

    public bool IsGazedAt { get; private set; }
    float currentGazeAngle;

    // Which GazeInteractable currently controls each shared canvas.
    static readonly Dictionary<Canvas, GazeInteractable> promptOwners = new Dictionary<Canvas, GazeInteractable>();

    // Every live instance, so LateUpdate can check "is some other object
    // sharing my canvas also gazed at, and more directly than me" -- this
    // makes the winner well-defined (smallest angle) and independent of
    // which instance's LateUpdate happens to run first, instead of two
    // objects with overlapping gaze cones (e.g. tools sitting close
    // together) fighting over the canvas based on arbitrary script order.
    static readonly List<GazeInteractable> allInstances = new List<GazeInteractable>();

    InputDevice rightHandDevice;
    bool prevAPressed = false;

    void Awake()
    {
        if (promptCanvas != null) promptCanvas.gameObject.SetActive(false);
        if (playerCamera == null && Camera.main != null) playerCamera = Camera.main.transform;
    }

    void OnEnable()
    {
        allInstances.Add(this);
    }

    void Start()
    {
        GetRightHandDevice();
    }

    void OnDisable()
    {
        allInstances.Remove(this);
        IsGazedAt = false;

        if (promptCanvas != null && promptOwners.TryGetValue(promptCanvas, out var owner) && owner == this)
        {
            promptOwners.Remove(promptCanvas);
            promptCanvas.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        UpdateGazeState();
        HandleInteractInput();
    }

    void LateUpdate()
    {
        ClaimOrReleaseSharedCanvas();
    }

    void UpdateGazeState()
    {
        if (playerCamera == null)
        {
            IsGazedAt = false;
            return;
        }

        Vector3 toTarget = transform.position - playerCamera.position;
        float distance = toTarget.magnitude;
        currentGazeAngle = Vector3.Angle(playerCamera.forward, toTarget);

        // Once showing, allow a wider angle before hiding again (hysteresis)
        // so it doesn't flicker or vanish the instant the player's gaze drifts.
        float activeThreshold = IsGazedAt ? maxGazeAngle + gazeAngleHysteresis : maxGazeAngle;

        bool inRange = distance <= range;
        IsGazedAt = inRange && currentGazeAngle <= activeThreshold;
    }

    // Called from LateUpdate, after every instance's Update() has already
    // run this frame -- so every sibling's IsGazedAt/currentGazeAngle below
    // is already final for this frame, regardless of which instance's
    // LateUpdate happens to execute first.
    void ClaimOrReleaseSharedCanvas()
    {
        if (promptCanvas == null) return;

        bool amBestCandidate = IsGazedAt && IsMostDirectlyGazedAt();

        if (amBestCandidate)
        {
            promptOwners[promptCanvas] = this;
            promptCanvas.gameObject.SetActive(true);
            PositionPrompt();
        }
        else if (promptOwners.TryGetValue(promptCanvas, out var owner) && owner == this)
        {
            promptOwners.Remove(promptCanvas);
            promptCanvas.gameObject.SetActive(false);
        }
    }

    bool IsMostDirectlyGazedAt()
    {
        foreach (var other in allInstances)
        {
            if (other == this || other.promptCanvas != promptCanvas) continue;
            if (other.IsGazedAt && other.currentGazeAngle < currentGazeAngle)
                return false;
        }
        return true;
    }

    // Tracks this object's current position every frame rather than assuming
    // it stays where it started -- grabbable props can move (picked up,
    // auto-socketed into a holder at scene start, etc), and a prompt left
    // behind at the object's original spot is invisible to a player standing
    // next to where it actually ended up.
    void PositionPrompt()
    {
        promptCanvas.transform.position = transform.position + promptOffset;

        if (billboardPrompt && playerCamera != null)
        {
            // This canvas's readable front faces its local -Z, so aiming
            // local +Z away from the player (dir already points away from
            // them) puts the front toward the player.
            Vector3 dir = promptCanvas.transform.position - playerCamera.position;
            dir.y = 0f;
            if (dir != Vector3.zero)
                promptCanvas.transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    // Always reads and tracks the button every frame so prevAPressed never
    // goes stale while not being gazed at -- see MerchantBooth/
    // ShopInteractionController for the bug this pattern avoids (a stale
    // edge misfiring the instant this becomes gazed-at again).
    void HandleInteractInput()
    {
        if (!rightHandDevice.isValid)
        {
            GetRightHandDevice();
            return;
        }

        if (rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool aPressed))
        {
            bool justPressed = aPressed && !prevAPressed;
            prevAPressed = aPressed;

            if (justPressed && IsGazedAt)
                onInteractPressed?.Invoke();
        }
    }

    void GetRightHandDevice()
    {
        rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }
}
