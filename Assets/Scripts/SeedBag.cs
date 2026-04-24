using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class SeedBag : MonoBehaviour
{
    [Header("References")]
    public ParticleSystem seedParticles;

    [Header("Crop Data")]
    [Tooltip("Which crop this bag contains - drag a CropData asset here")]
    public CropData seedData;

    [Header("Tilt Settings")]
    [Tooltip("Minimum X-axis tilt angle to start pouring")]
    public float tiltMin = 20f;

    [Tooltip("Maximum X-axis tilt angle to pour (prevents upside-down triggering)")]
    public float tiltMax = 160f;

    // XR Grab
    private XRGrabInteractable grabInteractable;
    private bool isHeld = false;

    void Awake()
    {
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
        isHeld = true;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        isHeld = false;
    }

    void Update()
    {
        // localEulerAngles.x returns 0-360 even when tilting forward/back
        float tilt = transform.localEulerAngles.x;
        bool shouldPour = isHeld && tilt > tiltMin && tilt < tiltMax;

        if (shouldPour)
        {
            if (!seedParticles.isPlaying)
                seedParticles.Play();
        }
        else
        {
            if (seedParticles.isPlaying)
                seedParticles.Stop();
        }
    }
}