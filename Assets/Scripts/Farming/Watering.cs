using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class Watering : MonoBehaviour
{
    public ParticleSystem waterParticles;
    private XRGrabInteractable grabInteractable;
    private bool isHeld = false;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrab);
        grabInteractable.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args) => isHeld = true;
    private void OnRelease(SelectExitEventArgs args) => isHeld = false;

    void Update()
    {
        // Pour logic based on X-axis tilt
        float tiltAmount = transform.localEulerAngles.x;

        if (isHeld && tiltAmount > 20 && tiltAmount < 160)
        {
            if (!waterParticles.isPlaying) waterParticles.Play();
        }
        else
        {
            if (waterParticles.isPlaying) waterParticles.Stop();
        }
    }
}