using System.Collections.Generic;
using UnityEngine;
using TMPro;

// The flock's home: hens (see Chicken) walk in through Entrance to roost at
// night and to lay in the nest boxes each morning. Eggs pile up in the nest
// until the player looks at the coop and presses A to collect them all into
// the inventory -- however many the hens happened to lay since last time.
//
// Unity setup:
//   1. Add this script and a GazeInteractable to the coop GameObject
//   2. Create an empty child at the coop's door, on the NavMesh -> Entrance
//   3. Create one or more empty children where eggs should show up (inside
//      the nest boxes, or anywhere visible through the door) -> Nest Spots
//   4. Drag the Egg CropData into Egg Item, and egg_brown / egg_white
//      prefabs into Brown/White Egg Prefab
//   5. Give the GazeInteractable a World Space prompt canvas, and drag that
//      canvas's text into Prompt Text so it can show the current egg count
[RequireComponent(typeof(GazeInteractable))]
public class ChickenCoop : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Where hens enter and leave -- must sit on the NavMesh, just outside the door")]
    public Transform entrance;

    [Tooltip("Where laid eggs appear. Eggs are spread across these in turn")]
    public List<Transform> nestSpots = new List<Transform>();

    [Header("Eggs")]
    [Tooltip("The inventory item collected eggs become (sold at the shop like any crop)")]
    public CropData eggItem;

    public GameObject brownEggPrefab;
    public GameObject whiteEggPrefab;

    [Tooltip("Uniform scale applied to each spawned egg visual")]
    public float eggVisualScale = 1f;

    [Tooltip("Visual eggs stop being added past this many (the count still goes up) -- keeps a neglected nest from overflowing")]
    public int maxVisibleEggs = 12;

    [Header("Prompt")]
    [Tooltip("Text on the GazeInteractable's prompt canvas -- retexted with the current egg count")]
    public TMP_Text promptText;

    [Header("Audio")]
    [Tooltip("Optional -- played when eggs are collected")]
    public AudioSource collectAudioSource;

    public int StoredEggs { get; private set; }

    public Vector3 EntrancePosition => entrance != null ? entrance.position : transform.position;

    private readonly List<Chicken> hens = new List<Chicken>();
    private readonly List<GameObject> eggVisuals = new List<GameObject>();

    void Awake()
    {
        GazeInteractable gaze = GetComponent<GazeInteractable>();
        gaze.onInteractPressed.AddListener(CollectEggs);
        gaze.onCanvasClaimed.AddListener(RefreshPrompt);
    }

    public void Register(Chicken hen)
    {
        if (!hens.Contains(hen)) hens.Add(hen);
    }

    public void Unregister(Chicken hen)
    {
        hens.Remove(hen);
    }

    // Another hen that's currently out in the yard, for flocking.
    public Chicken RandomOutsideHen(Chicken except)
    {
        int start = Random.Range(0, Mathf.Max(1, hens.Count));
        for (int i = 0; i < hens.Count; i++)
        {
            Chicken h = hens[(start + i) % hens.Count];
            if (h != except && h != null && !h.IsInsideCoop) return h;
        }
        return null;
    }

    public void AddEgg(bool white)
    {
        StoredEggs++;

        GameObject prefab = white && whiteEggPrefab != null ? whiteEggPrefab : brownEggPrefab;
        if (prefab == null || nestSpots.Count == 0 || eggVisuals.Count >= maxVisibleEggs) return;

        Transform spot = nestSpots[eggVisuals.Count % nestSpots.Count];
        Vector2 jitter = Random.insideUnitCircle * 0.06f;
        Vector3 pos = spot.position + new Vector3(jitter.x, 0f, jitter.y);
        Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * prefab.transform.rotation;

        GameObject egg = Instantiate(prefab, pos, rot, spot);
        egg.transform.localScale *= eggVisualScale;

        // Purely decorative -- nothing should knock it around or grab it.
        foreach (Collider c in egg.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (Rigidbody rb in egg.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;

        eggVisuals.Add(egg);
    }

    public void CollectEggs()
    {
        if (StoredEggs <= 0)
        {
            HUD.Instance?.ShowAlert("No eggs yet -- hens lay in the morning.");
            return;
        }

        int collected = StoredEggs;
        if (eggItem != null && InventoryManager.Instance != null)
            InventoryManager.Instance.AddCrop(eggItem, collected);

        StoredEggs = 0;
        foreach (GameObject egg in eggVisuals)
            if (egg != null) Destroy(egg);
        eggVisuals.Clear();

        // Explicit check, not ?. -- an unassigned serialized field is Unity's
        // "fake null", which ?. doesn't catch.
        if (collectAudioSource != null) collectAudioSource.Play();
        HUD.Instance?.ShowAlert("Collected " + collected + (collected == 1 ? " egg!" : " eggs!"));
        Debug.Log("[ChickenCoop] Collected " + collected + " eggs.");

        RefreshPrompt();
    }

    void RefreshPrompt()
    {
        if (promptText == null) return;

        promptText.text = StoredEggs > 0
            ? "Press A to collect " + StoredEggs + (StoredEggs == 1 ? " egg" : " eggs")
            : "No eggs yet";
    }
}
