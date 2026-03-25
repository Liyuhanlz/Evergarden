using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "PlantGrow", menuName = "Scriptable Objects/PlantGrow")]
public class PlantGrow : ScriptableObject
{
    public string PlantType;

    [SerializeField]
    private List<GameObject> plantPrefabs = new List<GameObject>();

    public int MaxStage => plantPrefabs.Count;

    public GameObject GetPlantByStage(int stage)
    {
        if (stage < 0 || stage >= plantPrefabs.Count)
            return null;

        return plantPrefabs[stage];
    }
}