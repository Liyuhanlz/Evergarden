using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit;

public class Watering : MonoBehaviour
{
    public ParticleSystem waterParticles;
    

    // private float maxWater = 10f;
    // private float currentWater = 10f;
    // private float waterUseReate = 1f;
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

    void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrab);
        grabInteractable.selectExited.RemoveListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        Debug.Log("Grabbed");
        isHeld = true;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        Debug.Log("Released");
        isHeld = false;
    }



    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        

        float tiltAmount = transform.localEulerAngles.x;

        if (tiltAmount > 2 && tiltAmount < 180 && isHeld)
            //&& currentWater > 0)
        {
            StartWater();
            // currentWater -= waterUseRate * Time.deltaTime;
        }
        else 
        {
            StopWater();
        }

    }
    
    void StartWater()
    {
        if (!waterParticles.isPlaying)
            waterParticles.Play();
    }

    void StopWater()
    {
        if (waterParticles.isPlaying)
            waterParticles.Stop();
    }
}
