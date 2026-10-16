using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

// The pull rope hanging from the FarmBell: grab the handle (grip) and yank it
// down -- or shake it side to side -- to ring the bell. The handle follows
// the hand within a little slack, and springs back to hang straight when
// let go.
//
// Unity setup:
//   1. Rope Top: an empty on the bell's SwingPivot where the rope ties on
//      (e.g. the clapper), so the rope sways with the bell
//   2. Add this script, an XRSimpleInteractable and a collider to the handle,
//      placed where the rope should hang at rest (NOT a child of the pivot)
//   3. Rope Visual: a cylinder (no collider) -- stretched between Rope Top
//      and the handle every frame
//   4. Wire On Pulled to FarmBell.Ring (FarmBell does this itself if its
//      Rope field is set)
[RequireComponent(typeof(XRSimpleInteractable))]
public class BellRope : MonoBehaviour
{
    public Transform ropeTop;

    [Tooltip("Cylinder stretched between Rope Top and this handle")]
    public Transform ropeVisual;

    [Tooltip("Rope thickness in meters")]
    public float ropeThickness = 0.025f;

    [Tooltip("How far (m) the handle can be pulled away from where it hangs")]
    public float maxStretch = 0.35f;

    [Tooltip("Pull the handle this far down (m) to ring")]
    public float pullDistance = 0.12f;

    [Tooltip("Or swing it this far sideways (m) to ring")]
    public float shakeDistance = 0.18f;

    [Tooltip("How quickly the handle drops back to hanging straight once let go")]
    public float returnSpeed = 8f;

    [Tooltip("Fires each time the rope is pulled or shaken hard enough to ring")]
    public UnityEvent onPulled;

    // How far the player is pulling the handle from where it hangs (zero
    // when nobody's holding it) -- FarmBell leans the bell toward this.
    public Vector3 Displacement => holder != null ? transform.position - PullRestPosition : Vector3.zero;

    // Where the handle hangs while free -- under the rope top, so the rope
    // sways along with the bell.
    Vector3 HangPosition => ropeTop.position + Vector3.down * ropeLength;

    // Where pulls are measured from: fixed to the bell post, NOT to the
    // swinging bell. Otherwise the bell's own swing after a ring moves the
    // rope's rest point and reads as another pull, ringing it again and again.
    Vector3 PullRestPosition => transform.parent != null ? transform.parent.TransformPoint(restLocal) : restLocal;

    private XRSimpleInteractable interactable;
    private IXRSelectInteractor holder;
    private Vector3 grabOffset;
    private float ropeLength;
    private Vector3 restLocal;

    // Must come back near rest between rings, so one long pull rings once.
    private bool armed = true;

    void Awake()
    {
        ropeLength = Vector3.Distance(ropeTop.position, transform.position);
        restLocal = transform.parent != null ? transform.parent.InverseTransformPoint(transform.position) : transform.position;

        interactable = GetComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        holder = args.interactorObject;
        armed = true;

        // Follow the hand from where it grabbed, so the handle doesn't jump
        // to the hand's (or a far-off ray controller's) exact position.
        grabOffset = transform.position - HandPosition();
    }

    void OnRelease(SelectExitEventArgs args)
    {
        if (args.interactorObject == holder) holder = null;
    }

    void Update()
    {
        if (holder == null)
        {
            transform.position = Vector3.Lerp(transform.position, HangPosition, 1f - Mathf.Exp(-returnSpeed * Time.deltaTime));
            return;
        }

        Vector3 rest = PullRestPosition;
        transform.position = rest + Vector3.ClampMagnitude(HandPosition() + grabOffset - rest, maxStretch);

        Vector3 d = transform.position - rest;
        float down = -d.y;
        float sideways = new Vector2(d.x, d.z).magnitude;

        if (armed && (down >= pullDistance || sideways >= shakeDistance))
        {
            armed = false;
            onPulled?.Invoke();
        }
        else if (!armed && d.magnitude < pullDistance * 0.4f)
        {
            armed = true;
        }
    }

    void LateUpdate()
    {
        if (ropeVisual == null) return;

        Vector3 top = ropeTop.position;
        Vector3 bottom = transform.position;
        Vector3 span = bottom - top;
        if (span.sqrMagnitude < 0.0001f) return;

        // Unity's cylinder is 2 units tall, centered on its pivot.
        ropeVisual.position = (top + bottom) * 0.5f;
        ropeVisual.rotation = Quaternion.FromToRotation(Vector3.up, span);
        ropeVisual.localScale = new Vector3(ropeThickness, span.magnitude * 0.5f, ropeThickness);
    }

    Vector3 HandPosition()
    {
        return holder.GetAttachTransform(interactable).position;
    }
}
