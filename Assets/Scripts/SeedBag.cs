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
    [Tooltip("Minimum Z-axis tilt angle to start pouring")]
    public float tiltMin = 50f;

    [Tooltip("Maximum Z-axis tilt angle to pour")]
    public float tiltMax = 300f;

    // The one bag itself, regardless of whether it's currently held -- lets
    // SeedPickerUI (backpack Seeds tab) load a seed into it and highlight
    // which one is currently loaded.
    public static SeedBag Instance { get; private set; }

    private XRGrabInteractable grabInteractable;
    private bool isHeld = false;

    void Awake()
    {
        Instance = this;
        grabInteractable = GetComponent<XRGrabInteractable>();
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
        float tilt = transform.localEulerAngles.z;

        bool hasSeeds = seedData != null && SeedInventory.Instance != null && SeedInventory.Instance.GetCount(seedData) > 0;
        bool shouldPour = isHeld && hasSeeds && tilt > tiltMin && tilt < tiltMax;

        if (shouldPour)
        {
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
}
