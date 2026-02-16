using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Farmland : MonoBehaviour
{
    public enum LandStatus
    {
         Grass, Tilled, Watered
    }

    public LandStatus landStatus;

    public GameObject grassPrefab;
    public GameObject tilledPrefab;

    private MeshRenderer currentRenderer;

    // Start is called before the first frame update
    void Start()
    {
        UpdateStatus(LandStatus.Grass);
    }



    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateStatus(LandStatus statusToSwitch)
    {
        landStatus = statusToSwitch;

        GameObject modelSwitch = grassPrefab;

        switch (statusToSwitch)
        {
            case LandStatus.Grass:
                modelSwitch = grassPrefab;
                break;

            case LandStatus.Tilled:
                modelSwitch = tilledPrefab;
                currentRenderer.material.color = new Color(0.55f, 0.27f, 0.07f);
                break;

            case LandStatus.Watered:
                currentRenderer.material.color = new Color(0.35f, 0.2f, 0.05f);
                break;
        }
    }
}
