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
    [Tooltip("Minimum Z-axis tilt angle to start pouring")]
    public float tiltMin = 50f;

    [Tooltip("Maximum Z-axis tilt angle to pour")]
    public float tiltMax = 300f;

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
        float tilt = transform.localEulerAngles.z;

        bool shouldPour = isHeld && tilt > tiltMin && tilt < tiltMax;

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