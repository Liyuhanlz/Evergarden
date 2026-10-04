using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class SeedBag : MonoBehaviour
{
    [Header("References")]
    public ParticleSystem seedParticles;

    [Header("Crop Data")]
    [Tooltip("Which crop this bag currently pours -- reconfigurable from the backpack's Seeds tab " +
             "(see SeedPickerUI), no need to be holding the bag.")]
    public CropData seedData;

    [Header("Tilt Settings")]
    [Tooltip("Minimum lean from upright (degrees, in ANY direction) to start pouring")]
    public float tiltMin = 50f;

    [Tooltip("Maximum lean from upright (degrees) to keep pouring -- 180 is fully upside down")]
    public float tiltMax = 180f;

    // The one bag itself, regardless of whether it's currently held -- lets
    // SeedPickerUI (backpack Seeds tab) load a seed into it and highlight
    // which one is currently loaded.
    public static SeedBag Instance { get; private set; }

    private XRGrabInteractable grabInteractable;
    private bool isHeld = false;

    // The emitter is authored on one side of the bag's mouth (aimed out that
    // side). These describe the mouth so it can be swung round to whichever
    // edge is lowest -- otherwise the bag only poured when tipped toward the
    // authored side (e.g. right hand tilting left, but not left hand
    // tilting right).
    private Vector3 mouthCenterLocal;
    private float mouthRadius;
    private float emitDownward;
    private float emitOutward;

    void Awake()
    {
        Instance = this;
        grabInteractable = GetComponent<XRGrabInteractable>();
        CacheMouthShape();
    }

    void CacheMouthShape()
    {
        if (seedParticles == null) return;

        Vector3 pos = transform.InverseTransformPoint(seedParticles.transform.position);
        Vector3 dir = transform.InverseTransformDirection(seedParticles.transform.forward);

        Vector3 outward = new Vector3(dir.x, 0f, dir.z).normalized;
        mouthRadius = Vector3.Dot(new Vector3(pos.x, 0f, pos.z), outward);
        mouthCenterLocal = pos - outward * mouthRadius;
        emitOutward = new Vector2(dir.x, dir.z).magnitude;
        emitDownward = dir.y;
    }

    void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrab);
        grabInteractable.selectExited.RemoveListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        // The tool rack's socket "selects" the bag too (that's how it stays
        // seated there, including automatically at scene start) -- that's not
        // the player actually holding it, so it must not count here or the
        // bag would look "held" from the moment the scene loads.
        if (args.interactorObject is XRSocketInteractor) return;

        isHeld = true;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) return;

        isHeld = false;
    }

    void Update()
    {
        // Lean from upright in any direction, so either hand can pour by
        // tipping the bag whichever way is comfortable
        float tilt = Vector3.Angle(transform.up, Vector3.up);

        bool hasSeeds = seedData != null && SeedInventory.Instance != null && SeedInventory.Instance.GetCount(seedData) > 0;
        bool shouldPour = isHeld && hasSeeds && tilt > tiltMin && tilt <= tiltMax;

        if (shouldPour)
        {
            AimAtLowestEdge();

            if (!seedParticles.isPlaying)
            {
                seedParticles.Play();
            }
        }
        else
        {
            if (seedParticles.isPlaying)
            {
                seedParticles.Stop();
            }
        }
    }

    // Moves the emitter to the edge of the mouth that's currently lowest and
    // points it downhill, keeping the authored outward/downward pour angle
    void AimAtLowestEdge()
    {
        Vector3 downLocal = transform.InverseTransformDirection(Vector3.down);
        Vector3 downhill = new Vector3(downLocal.x, 0f, downLocal.z);
        if (downhill.sqrMagnitude < 0.0001f) return; // upside down -- any edge will do
        downhill.Normalize();

        Transform emitter = seedParticles.transform;
        emitter.position = transform.TransformPoint(mouthCenterLocal + downhill * mouthRadius);

        Vector3 dirLocal = downhill * emitOutward + Vector3.up * emitDownward;
        emitter.rotation = Quaternion.LookRotation(transform.TransformDirection(dirLocal), transform.up);
    }
}
