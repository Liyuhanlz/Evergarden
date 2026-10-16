using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The livestock's night shelter, and the way the player ends the day early.
//
//   Evening -- The player rings the FarmBell and every goat, sheep and horse
//              runs back to the barn. Each takes its own lane through the
//              doorway (and its own spot to wait at if the doors are shut),
//              so the herd doesn't pile up on one path.
//   Night   -- Once the last animal is in, the doors swing shut and the day
//              skips straight to the next morning (GameClock.SkipToNextMorning).
//   Morning -- The animals stay in until the player swings a barn door open
//              (see SwingDoor), then wander out one by one at random times.
//
// Animals only pass through an open door: ring the bell with the doors shut
// and they gather outside until the player opens one.
//
// Chickens aren't herded -- they already put themselves to bed in their
// ChickenCoop at dusk.
//
// Unity setup:
//   1. Add this script to any GameObject at the barn
//   2. Door Front: an empty at the middle of the doorway's threshold, on the
//      ground, with its blue Z axis pointing OUT of the barn. The yard in
//      front of it needs to be on the animals' NavMesh
//   3. Drag both SwingDoors into Doors -- each door's lanes are on its side
//      of Door Front
//   4. Leave Animals empty to use every AnimalWander in the scene
public class Barn : MonoBehaviour
{
    // A path through one side of the doorway: Approach is just outside (on
    // the NavMesh), Inside is where the animal disappears.
    public struct Lane
    {
        public Vector3 approach;
        public Vector3 inside;
    }

    [Header("Doorway")]
    [Tooltip("Middle of the doorway's threshold, Z axis pointing out of the barn")]
    public Transform doorFront;

    public List<SwingDoor> doors = new List<SwingDoor>();

    [Tooltip("How far outside the threshold each lane starts (keep it on the NavMesh)")]
    public float approachDistance = 1.2f;

    [Tooltip("How far inside the barn animals walk before disappearing")]
    public float insideDepth = 2.5f;

    [Tooltip("Gap kept clear at the middle of the doorway and next to each door panel -- " +
             "about half an animal's width plus the panel's thickness")]
    public float laneMargin = 0.8f;

    [Tooltip("How far out in front of the doors animals wait while they're shut")]
    public Vector2 gatherDistanceRange = new Vector2(3f, 7f);

    [Header("Animals")]
    [Tooltip("Animals that sleep in this barn -- every AnimalWander in the scene if left empty")]
    public List<AnimalWander> animals = new List<AnimalWander>();

    [Tooltip("Once a door is opened in the morning, each animal heads out at a random time in this range (real seconds)")]
    public Vector2 releaseDelayRange = new Vector2(0.5f, 15f);

    [Header("Audio")]
    [Tooltip("Optional -- played when the new day starts")]
    public AudioSource wakeAudioSource;

    public enum State { Out, BringingIn, In, LettingOut }
    public State CurrentState { get; private set; } = State.Out;

    public bool AnyDoorOpen
    {
        get
        {
            foreach (SwingDoor d in doors)
                if (d != null && d.IsOpen) return true;
            return false;
        }
    }

    private bool warnedDoorsShut;

    void Start()
    {
        if (animals.Count == 0)
            animals.AddRange(FindObjectsByType<AnimalWander>(FindObjectsSortMode.None));
        animals.RemoveAll(a => a == null);
    }

    void Update()
    {
        // Morning: the first open door lets the herd out.
        if (CurrentState == State.In && AnyDoorOpen)
            StartCoroutine(LetAnimalsOut());
    }

    // =============================================
    //  EVENING
    // =============================================

    // Called by the FarmBell.
    public void CallAnimals()
    {
        switch (CurrentState)
        {
            case State.Out:
                StartCoroutine(BringAnimalsIn());
                break;
            case State.BringingIn:
                HUD.Instance?.ShowAlert("The animals are on their way.");
                break;
            case State.In:
                HUD.Instance?.ShowAlert("The animals are already in -- open the barn doors to let them out.");
                break;
            case State.LettingOut:
                HUD.Instance?.ShowAlert("The animals are still heading out for the day.");
                break;
        }
    }

    IEnumerator BringAnimalsIn()
    {
        if (animals.Count == 0)
        {
            HUD.Instance?.ShowAlert("No animals to bring in.");
            yield break;
        }

        CurrentState = State.BringingIn;
        warnedDoorsShut = false;
        int dayWhenCalled = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 0;

        foreach (AnimalWander a in animals)
            if (a != null) a.GoToBarn(this);

        HUD.Instance?.ShowAlert(AnyDoorOpen
            ? "Calling the animals in for the night..."
            : "Calling the animals in -- open the barn doors for them!");

        while (CountInState(AnimalWander.State.InBarn) < LiveAnimalCount())
        {
            // Nudge the player once if the herd is stuck at a shut door.
            if (!warnedDoorsShut && !AnyDoorOpen && CountInState(AnimalWander.State.WaitingAtBarn) > 0)
            {
                warnedDoorsShut = true;
                HUD.Instance?.ShowAlert("The animals are waiting -- open the barn doors!");
            }
            yield return null;
        }

        foreach (SwingDoor d in doors)
            if (d != null) d.SwingTo(false);

        // Give the doors a moment to swing shut before the night passes.
        // Only switch to In afterwards -- Update lets the herd straight back
        // out of an open door once the barn is In.
        float t = 0f;
        while (t < 2f && AnyDoorOpen)
        {
            t += Time.deltaTime;
            yield return null;
        }

        CurrentState = State.In;

        if (GameClock.Instance == null) yield break;

        // The clock rolled over on its own while the herd was still coming
        // in -- a new day has already started, so don't skip a second one.
        if (GameClock.Instance.CurrentDay != dayWhenCalled)
        {
            HUD.Instance?.ShowAlert("The animals are all in.");
            yield break;
        }

        GameClock.Instance.SkipToNextMorning();

        if (wakeAudioSource != null) wakeAudioSource.Play();
        HUD.Instance?.ShowAlert("Good morning! " + GameClock.Instance.GetDayString() +
                                " -- open the barn doors to let the animals out.");
    }

    // =============================================
    //  MORNING
    // =============================================

    IEnumerator LetAnimalsOut()
    {
        CurrentState = State.LettingOut;

        // Each animal gets its own random moment to wander out, rather than
        // the whole herd pouring through the door at once.
        foreach (AnimalWander a in animals)
            if (a != null) StartCoroutine(ReleaseAfterDelay(a, Random.Range(releaseDelayRange.x, releaseDelayRange.y)));

        while (CountInState(AnimalWander.State.InBarn) + CountInState(AnimalWander.State.Leaving) > 0)
            yield return null;

        CurrentState = State.Out;
    }

    IEnumerator ReleaseAfterDelay(AnimalWander a, float delay)
    {
        // Only counts down while a door is open -- shut the doors again and
        // whoever hasn't left yet stays in.
        float elapsed = 0f;
        while (elapsed < delay || !AnyDoorOpen)
        {
            if (AnyDoorOpen) elapsed += Time.deltaTime;
            yield return null;
        }

        if (a != null) a.LeaveBarn(this);
    }

    // =============================================
    //  LANES
    // =============================================

    // A random lane through one of the currently open doors (or any door if
    // none is open, so a stuck animal still has somewhere to go).
    public Lane PickLane()
    {
        List<SwingDoor> usable = new List<SwingDoor>();
        foreach (SwingDoor d in doors)
            if (d != null && d.IsOpen) usable.Add(d);
        if (usable.Count == 0)
            foreach (SwingDoor d in doors)
                if (d != null) usable.Add(d);

        Vector3 right = doorFront.right;
        Vector3 outward = doorFront.forward;
        float lateral = 0f;

        if (usable.Count > 0)
        {
            // Each door covers the doorway from the middle out to its hinge.
            // A door that's only partly open still angles across the outer
            // part of its half (by cos of its opening angle), so lanes stay
            // between that and the middle -- the last steps through the
            // doorway are walked by hand and would otherwise clip the panel.
            SwingDoor door = usable[Random.Range(0, usable.Count)];
            float hingeOffset = Vector3.Dot(door.transform.position - doorFront.position, right);
            float halfWidth = Mathf.Abs(hingeOffset);
            float panelReach = halfWidth * Mathf.Max(0f, Mathf.Cos(door.OpenDegrees * Mathf.Deg2Rad));
            float width = Mathf.Max(0f, halfWidth - panelReach - laneMargin);
            lateral = Mathf.Sign(hingeOffset) * Random.Range(Mathf.Min(laneMargin, width), width);
        }

        Vector3 threshold = doorFront.position + right * lateral;
        Vector3 approach = threshold + outward * approachDistance;
        if (NavMesh.SamplePosition(approach, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            approach = hit.position;

        return new Lane { approach = approach, inside = threshold - outward * insideDepth };
    }

    // A spot out in front of the doors to wait at while they're shut --
    // random per animal so the herd spreads out instead of queueing.
    public Vector3 PickGatherSpot()
    {
        float halfWidth = 3f;
        foreach (SwingDoor d in doors)
            if (d != null)
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(d.transform.position - doorFront.position, doorFront.right)));

        for (int i = 0; i < 10; i++)
        {
            Vector3 p = doorFront.position
                        + doorFront.forward * Random.Range(gatherDistanceRange.x, gatherDistanceRange.y)
                        + doorFront.right * Random.Range(-halfWidth, halfWidth);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                return hit.position;
        }

        return doorFront.position + doorFront.forward * gatherDistanceRange.x;
    }

    // =============================================
    //  STATUS
    // =============================================

    public int LiveAnimalCount()
    {
        int n = 0;
        foreach (AnimalWander a in animals)
            if (a != null) n++;
        return n;
    }

    public int CountInState(AnimalWander.State s)
    {
        int n = 0;
        foreach (AnimalWander a in animals)
            if (a != null && a.CurrentState == s) n++;
        return n;
    }
}
