using UnityEngine;
using TMPro;

// The farm's dinner bell: grab the rope and pull it down or shake it (see
// BellRope) to ring the bell, calling every animal back to the Barn for the
// night (which then ends the day once they're all in).
//
// The bell hangs free: while the rope is held off to one side its mouth
// leans that way, and after a ring it rocks back and forth along the
// direction it was pulled.
//
// Unity setup:
//   1. Add this script and a GazeInteractable to the bell post -- the gaze
//      only shows the "pull the rope" hint, it doesn't ring anything
//   2. Swing Pivot: the empty the bell hangs from
//   3. Drag the Barn and the BellRope in, plus an AudioSource with the ring
//      sound
//   4. Give the GazeInteractable a World Space prompt canvas, and drag that
//      canvas's text into Prompt Text
[RequireComponent(typeof(GazeInteractable))]
public class FarmBell : MonoBehaviour
{
    public Barn barn;

    [Tooltip("The pull rope -- pulling or shaking it rings the bell")]
    public BellRope rope;

    [Tooltip("The bell's hanging point -- it tilts around this")]
    public Transform swingPivot;

    [Tooltip("Max swing angle in degrees after a ring")]
    public float swingAngle = 30f;

    [Tooltip("Swings per second")]
    public float swingFrequency = 1.2f;

    [Tooltip("Real seconds for the swing to die down")]
    public float swingDuration = 3f;

    [Tooltip("How far the bell leans toward the rope while it's held off to one side (degrees per meter)")]
    public float ropeTiltPerMeter = 70f;

    [Tooltip("Max lean toward the rope in degrees")]
    public float maxRopeTilt = 30f;

    [Tooltip("Plays the ring -- overlapping rings layer rather than cut each other off")]
    public AudioSource ringAudioSource;

    [Tooltip("Real seconds between passing the call on to the barn -- ringing over and over just rings, " +
             "instead of repeating the same message")]
    public float callCooldown = 4f;

    [Tooltip("Text on the GazeInteractable's prompt canvas")]
    public TMP_Text promptText;

    private Quaternion pivotRestRotation;
    private float swingTime = -1f;
    private Vector3 swingDirection;
    private float lastCallTime = -999f;

    void Awake()
    {
        GetComponent<GazeInteractable>().onCanvasClaimed.AddListener(RefreshPrompt);

        if (rope != null) rope.onPulled.AddListener(Ring);

        if (swingPivot != null) pivotRestRotation = swingPivot.localRotation;
        if (barn == null) barn = FindFirstObjectByType<Barn>();
    }

    public void Ring()
    {
        if (ringAudioSource != null && ringAudioSource.clip != null)
            ringAudioSource.PlayOneShot(ringAudioSource.clip);

        // Rock along the direction the rope was pulled; a straight-down yank
        // keeps the previous direction (or the post's facing, the first time).
        Vector3 pull = rope != null ? Flat(rope.Displacement) : Vector3.zero;
        if (pull.sqrMagnitude > 0.0001f) swingDirection = pull.normalized;
        else if (swingDirection == Vector3.zero) swingDirection = Flat(transform.forward).normalized;
        swingTime = 0f;

        if (barn != null && Time.time - lastCallTime >= callCooldown)
        {
            lastCallTime = Time.time;
            barn.CallAnimals();
        }
    }

    void Update()
    {
        if (swingPivot == null) return;

        // Work out where the bell's mouth should point as a sideways offset
        // from straight down: a lean toward the held rope, plus the damped
        // rocking left over from the last ring.
        Vector3 offset = Vector3.zero;

        if (rope != null)
        {
            Vector3 pull = Flat(rope.Displacement);
            float lean = Mathf.Min(pull.magnitude * ropeTiltPerMeter, maxRopeTilt);
            if (lean > 0.01f)
                offset += pull.normalized * Mathf.Tan(lean * Mathf.Deg2Rad);
        }

        if (swingTime >= 0f)
        {
            swingTime += Time.deltaTime;
            if (swingTime >= swingDuration)
            {
                swingTime = -1f;
            }
            else
            {
                float damping = 1f - swingTime / swingDuration;
                float a = swingAngle * damping * damping * Mathf.Sin(swingTime * swingFrequency * 2f * Mathf.PI);
                offset += swingDirection * Mathf.Tan(a * Mathf.Deg2Rad);
            }
        }

        Quaternion tilt = Quaternion.FromToRotation(Vector3.down, (Vector3.down + offset).normalized);
        Quaternion restWorld = swingPivot.parent != null ? swingPivot.parent.rotation * pivotRestRotation : pivotRestRotation;
        swingPivot.rotation = tilt * restWorld;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    void RefreshPrompt()
    {
        if (promptText == null) return;

        const string ring = "Grab the rope and pull to ring the bell";

        if (barn == null)
        {
            promptText.text = ring;
            return;
        }

        int total = barn.LiveAnimalCount();
        int inside = barn.CountInState(AnimalWander.State.InBarn);

        switch (barn.CurrentState)
        {
            case Barn.State.Out:
                promptText.text = ring + "\n(call the animals in and end the day)";
                break;
            case Barn.State.BringingIn:
                promptText.text = barn.AnyDoorOpen
                    ? "Animals heading in... (" + inside + "/" + total + ")"
                    : "Open the barn doors! (" + inside + "/" + total + " in)";
                break;
            case Barn.State.In:
                promptText.text = "Animals are in -- open the barn doors to let them out";
                break;
            case Barn.State.LettingOut:
                promptText.text = "Animals heading out for the day";
                break;
        }
    }
}
