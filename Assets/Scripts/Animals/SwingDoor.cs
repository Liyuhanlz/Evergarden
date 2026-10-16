using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// A hinged door the player grabs (grip, hand on the door) and swings open or
// shut -- the door turns to follow the hand around the hinge. Also swings
// itself (SwingTo) so the Barn can shut up for the night.
//
// Unity setup:
//   1. An empty "hinge" GameObject at the door's hinge edge, UNIFORMLY
//      scaled (rotating a child of a non-uniformly scaled parent skews it),
//      with Y up -- the door mesh goes underneath as a child, closed
//   2. Add this script + an XRSimpleInteractable to the hinge, and a
//      collider to the door mesh so hands can grab it
//   3. Open Angle: degrees around the hinge's Y axis to fully open -- the
//      sign picks which way it swings (positive = clockwise seen from above)
[RequireComponent(typeof(XRSimpleInteractable))]
public class SwingDoor : MonoBehaviour
{
    [Tooltip("Angle (around the hinge's Y axis) when fully open -- sign sets the swing direction")]
    public float openAngle = 110f;

    [Tooltip("How far open (0-1) the door must be to count as open -- animals only pass an open door")]
    [Range(0f, 1f)]
    public float openThreshold = 0.6f;

    [Tooltip("Degrees per second when the door swings by itself")]
    public float autoSwingSpeed = 90f;

    [Tooltip("Optional -- played when the door starts swinging by itself")]
    public AudioSource swingAudioSource;

    // 0 = shut, 1 = fully open.
    public float Openness => Mathf.Clamp01(angle / openAngle);

    // How many degrees the door currently stands open, whichever way it swings.
    public float OpenDegrees => Mathf.Abs(angle);
    public bool IsOpen => Openness >= openThreshold;
    public bool IsHeld => holder != null;

    private XRSimpleInteractable interactable;
    private Quaternion closedRotation;
    private float angle;
    private float? autoTarget;

    private IXRSelectInteractor holder;
    private float grabHandAngle;
    private float grabDoorAngle;

    void Awake()
    {
        closedRotation = transform.localRotation;

        interactable = GetComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        holder = args.interactorObject;
        autoTarget = null;

        // Track the hand relative to where it grabbed, so the door doesn't
        // snap to point straight at the hand.
        grabHandAngle = HandAngle();
        grabDoorAngle = angle;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        if (args.interactorObject == holder) holder = null;
    }

    void Update()
    {
        if (holder != null)
        {
            SetAngle(grabDoorAngle + Mathf.DeltaAngle(grabHandAngle, HandAngle()));
        }
        else if (autoTarget.HasValue)
        {
            SetAngle(Mathf.MoveTowards(angle, autoTarget.Value, autoSwingSpeed * Time.deltaTime));
            if (Mathf.Approximately(angle, autoTarget.Value)) autoTarget = null;
        }
    }

    // Swing open or shut by itself (ignored while the player is holding it).
    public void SwingTo(bool open)
    {
        if (holder != null) return;

        float target = open ? openAngle : 0f;
        if (Mathf.Approximately(angle, target)) return;

        autoTarget = target;
        if (swingAudioSource != null) swingAudioSource.Play();
    }

    void SetAngle(float a)
    {
        angle = Mathf.Clamp(a, Mathf.Min(0f, openAngle), Mathf.Max(0f, openAngle));
        transform.localRotation = closedRotation * Quaternion.Euler(0f, angle, 0f);
    }

    // The hand's direction from the hinge, as an angle around world up.
    float HandAngle()
    {
        Vector3 toHand = holder.GetAttachTransform(interactable).position - transform.position;
        toHand.y = 0f;
        return Vector3.SignedAngle(Vector3.forward, toHand, Vector3.up);
    }
}
