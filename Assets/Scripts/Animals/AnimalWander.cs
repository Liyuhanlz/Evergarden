using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Attach to any animal GameObject that has:
//   - NavMeshAgent component
//   - Animator component with a "Vert" float and a "State" parameter (float
//     or int -- the Goat & Sheep pack's controller uses an int, the
//     Animals_FREE ones use a float; whichever it is gets detected in Start)
//   - AudioSource component
//
// Animator parameters match CreatureMover convention:
//   "Vert"  float: 0 = idle, >0 = walking (use axis magnitude)
//   "State" float/int: 0 = walk, 1 = run
//
// When the player rings the FarmBell, the Barn calls GoToBarn: the animal
// runs back, waits outside if the doors are shut, then walks in through its
// own lane of the doorway. In the morning the Barn calls LeaveBarn.

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AudioSource))]
public class AnimalWander : MonoBehaviour
{
    // ---------------------------------------------
    //  MOVEMENT
    // ---------------------------------------------
    [Header("Wander Settings")]
    [Tooltip("How far from the animal's start position it can wander")]
    public float wanderRadius = 10f;

    [Tooltip("Min and max time the animal walks before stopping")]
    public Vector2 walkDurationRange = new Vector2(3f, 8f);

    [Tooltip("Min and max time the animal idles before walking again")]
    public Vector2 idleDurationRange = new Vector2(2f, 6f);

    [Tooltip("Movement speed while wandering")]
    public float moveSpeed = 1.2f;

    [Tooltip("How fast the animal turns toward its destination")]
    public float angularSpeed = 120f;

    // ---------------------------------------------
    //  BARN
    // ---------------------------------------------
    [Header("Barn")]
    [Tooltip("Min and max running speed when called to the barn -- each animal picks its own, so the herd strings out instead of moving as one block")]
    public Vector2 herdSpeedRange = new Vector2(2.2f, 3f);

    [Tooltip("Max real seconds before an animal reacts to the bell")]
    public float reactionDelayMax = 1.5f;

    [Tooltip("How close to a target counts as arrived")]
    public float arriveRadius = 0.8f;

    [Tooltip("Real seconds before an animal stuck en route is simply moved on to the next step")]
    public float barnWalkTimeout = 25f;

    // ---------------------------------------------
    //  ANIMATION
    // ---------------------------------------------
    [Header("Animation Parameters")]
    [Tooltip("Float parameter that drives idle/walk blend (0 = idle, >0 = walking)")]
    public string vertParameterName = "Vert";

    [Tooltip("Float or int parameter that drives walk/run (0 = walk, 1 = run)")]
    public string stateParameterName = "State";

    [Tooltip("How smoothly Vert transitions between 0 and 1")]
    public float animationDampTime = 0.1f;

    // ---------------------------------------------
    //  AUDIO
    // ---------------------------------------------
    [Header("Audio")]
    [Tooltip("Array of animal sound clips to play randomly")]
    public AudioClip[] animalSounds;

    [Tooltip("Min and max seconds between random sound playback")]
    public Vector2 soundIntervalRange = new Vector2(5f, 15f);

    [Range(0f, 1f)]
    public float soundVolume = 1f;

    // ---------------------------------------------
    //  STATE
    // ---------------------------------------------
    public enum State { Wandering, HeadingToBarn, WaitingAtBarn, Entering, InBarn, Leaving }
    public State CurrentState { get; private set; } = State.Wandering;

    // ---------------------------------------------
    //  PRIVATE
    // ---------------------------------------------
    private NavMeshAgent agent;
    private Animator animator;
    private AudioSource audioSource;
    private Vector3 startPosition;

    // The State parameter's actual type on this animal's controller (or null
    // if it has none). Calling SetFloat on an int parameter logs an error with
    // a full stack trace -- doing that every frame for every goat/sheep
    // flooded the log fast enough to freeze play mode.
    private AnimatorControllerParameterType? stateParameterType;

    private Renderer[] renderers;
    private Collider[] colliders;
    private Coroutine routine;

    // Speed while walked by hand through the barn doorway (off the NavMesh),
    // so the walk animation still plays with the agent disabled.
    private float manualSpeed;

    // =============================================
    //  UNITY LIFECYCLE
    // =============================================
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
        startPosition = transform.position;

        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == stateParameterName) stateParameterType = p.type;

        agent.speed = moveSpeed;
        agent.angularSpeed = angularSpeed;

        audioSource.loop = false;
        audioSource.playOnAwake = false;
        audioSource.volume = soundVolume;

        routine = StartCoroutine(WanderRoutine());
        StartCoroutine(SoundRoutine());

        // Freezes in place whenever a menu pauses game time (Inventory, the
        // Seed picker, etc.) -- same GameClock.IsPaused flag Farmland already
        // watches for crop growth, so animals stop wandering right alongside
        // day progression rather than continuing to roam in the background.
        if (GameClock.Instance != null)
            GameClock.Instance.OnPauseChanged += HandlePauseChanged;
    }

    void OnDestroy()
    {
        if (GameClock.Instance != null)
            GameClock.Instance.OnPauseChanged -= HandlePauseChanged;
    }

    private bool isPaused;

    void HandlePauseChanged(bool paused)
    {
        isPaused = paused;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = paused;
    }

    void Update()
    {
        // Drive Vert from actual agent velocity magnitude so the blend
        // matches real movement speed, same as CreatureMover uses axis.magnitude
        float speed = agent.enabled ? agent.velocity.magnitude : manualSpeed;
        float normalizedSpeed = Mathf.Clamp01(speed / moveSpeed);

        animator.SetFloat(vertParameterName, normalizedSpeed, animationDampTime, Time.deltaTime);

        // Animals walk while wandering, and run when called to the barn.
        bool running = agent.enabled && agent.speed > moveSpeed + 0.01f && speed > 0.1f;
        if (stateParameterType == AnimatorControllerParameterType.Float)
            animator.SetFloat(stateParameterName, running ? 1f : 0f);
        else if (stateParameterType == AnimatorControllerParameterType.Int)
            animator.SetInteger(stateParameterName, running ? 1 : 0);
    }

    // =============================================
    //  BARN
    // =============================================

    // Stops wandering and heads into the barn for the night.
    public void GoToBarn(Barn barn)
    {
        if (CurrentState != State.Wandering) return;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(WalkIntoBarn(barn));
    }

    // Comes back out of the barn and resumes wandering around its usual spot.
    public void LeaveBarn(Barn barn)
    {
        if (CurrentState != State.InBarn) return;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(WalkOutOfBarn(barn));
    }

    IEnumerator WalkIntoBarn(Barn barn)
    {
        CurrentState = State.HeadingToBarn;

        // Not everyone hears the bell at the same instant.
        if (agent.isOnNavMesh) agent.ResetPath();
        yield return WaitRealSecondsUnlessPaused(Random.Range(0f, reactionDelayMax));

        agent.speed = Random.Range(herdSpeedRange.x, herdSpeedRange.y);

        // Doors shut: wait at our own spot out front until one opens.
        if (!barn.AnyDoorOpen)
        {
            yield return RunTo(barn.PickGatherSpot());

            CurrentState = State.WaitingAtBarn;
            if (agent.isOnNavMesh) agent.ResetPath();
            while (!barn.AnyDoorOpen) yield return null;

            // Don't all bolt for the door the same instant it opens.
            yield return WaitRealSecondsUnlessPaused(Random.Range(0f, reactionDelayMax));
        }

        // Head for our own lane through the doorway, so the herd files in
        // side by side instead of single file down one path.
        CurrentState = State.HeadingToBarn;
        Barn.Lane lane = barn.PickLane();
        yield return RunTo(lane.approach);
        bool reachedDoor = lastRunArrived;

        CurrentState = State.Entering;
        if (agent.isOnNavMesh) agent.ResetPath();
        agent.speed = moveSpeed;
        agent.enabled = false;

        // The last few steps through the doorway are off the NavMesh. Only
        // walked if the animal actually made it to the door -- one that got
        // stuck somewhere is just popped inside.
        if (reachedDoor)
            yield return StrollTo(lane.inside);

        SetVisible(false);
        CurrentState = State.InBarn;
    }

    // Set by RunTo: whether the last run actually arrived (vs. timed out).
    private bool lastRunArrived;

    IEnumerator RunTo(Vector3 target)
    {
        lastRunArrived = false;
        if (!agent.isOnNavMesh) yield break;

        agent.SetDestination(target);

        float elapsed = 0f;
        while (elapsed < barnWalkTimeout)
        {
            if (!isPaused)
            {
                elapsed += Time.deltaTime;
                if (FlatDistance(transform.position, target) <= arriveRadius)
                {
                    lastRunArrived = true;
                    yield break;
                }
            }
            yield return null;
        }
    }

    IEnumerator WalkOutOfBarn(Barn barn)
    {
        CurrentState = State.Leaving;

        Barn.Lane lane = barn.PickLane();
        transform.position = lane.inside;
        transform.rotation = Quaternion.LookRotation(Flat(lane.approach - lane.inside));
        SetVisible(true);

        yield return StrollTo(lane.approach);

        agent.enabled = true;
        agent.Warp(lane.approach);
        agent.speed = moveSpeed;
        agent.isStopped = isPaused;

        CurrentState = State.Wandering;
        routine = StartCoroutine(WanderRoutine());
    }

    // Walks straight to target with the agent disabled.
    IEnumerator StrollTo(Vector3 target)
    {
        while ((target - transform.position).sqrMagnitude > 0.0025f)
        {
            if (isPaused)
            {
                manualSpeed = 0f;
                yield return null;
                continue;
            }

            Vector3 dir = Flat(target - transform.position);
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), angularSpeed * Time.deltaTime);

            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            manualSpeed = moveSpeed;
            yield return null;
        }

        manualSpeed = 0f;
    }

    void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers) r.enabled = visible;
        foreach (Collider c in colliders) c.enabled = visible;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        return Flat(a - b).magnitude;
    }

    // =============================================
    //  WANDER
    // =============================================
    IEnumerator WanderRoutine()
    {
        while (true)
        {
            // Don't start a new walk/idle cycle while paused -- and don't let
            // WaitForSeconds keep counting down in the background either,
            // since its own SetDestination/ResetPath calls at the end of each
            // wait would otherwise clear NavMeshAgent.isStopped right back to
            // false out from under HandlePauseChanged's one-time set above.
            while (isPaused) yield return null;

            Vector3 destination = GetRandomNavMeshPosition();
            agent.SetDestination(destination);

            yield return WaitRealSecondsUnlessPaused(Random.Range(walkDurationRange.x, walkDurationRange.y));

            agent.ResetPath();

            yield return WaitRealSecondsUnlessPaused(Random.Range(idleDurationRange.x, idleDurationRange.y));
        }
    }

    // Like WaitForSeconds, but frozen (not just slowed) while paused -- the
    // countdown simply doesn't advance until HandlePauseChanged clears isPaused.
    IEnumerator WaitRealSecondsUnlessPaused(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (!isPaused) elapsed += Time.deltaTime;
            yield return null;
        }
    }

    Vector3 GetRandomNavMeshPosition()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomPoint = startPosition + Random.insideUnitSphere * wanderRadius;
            randomPoint.y = startPosition.y;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                return hit.position;
        }

        return startPosition;
    }

    // =============================================
    //  SOUND
    // =============================================
    IEnumerator SoundRoutine()
    {
        yield return new WaitForSeconds(Random.Range(0f, soundIntervalRange.y));

        while (true)
        {
            PlayRandomSound();
            float interval = Random.Range(soundIntervalRange.x, soundIntervalRange.y);
            yield return new WaitForSeconds(interval);
        }
    }

    void PlayRandomSound()
    {
        if (animalSounds == null || animalSounds.Length == 0) return;
        if (audioSource.isPlaying) return;

        // Quiet in the barn overnight.
        if (CurrentState == State.InBarn) return;

        AudioClip clip = animalSounds[Random.Range(0, animalSounds.Length)];
        if (clip != null)
            audioSource.PlayOneShot(clip, soundVolume);
    }
}
