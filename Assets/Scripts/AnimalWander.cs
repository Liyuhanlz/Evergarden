using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Attach to any animal GameObject that has:
//   - NavMeshAgent component
//   - Animator component with "Vert" and "State" float parameters
//   - AudioSource component
//
// Animator parameters match CreatureMover convention:
//   "Vert"  float: 0 = idle, >0 = walking (use axis magnitude)
//   "State" float: 0 = walk, 1 = run

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
    //  ANIMATION
    // ---------------------------------------------
    [Header("Animation Parameters")]
    [Tooltip("Float parameter that drives idle/walk blend (0 = idle, >0 = walking)")]
    public string vertParameterName = "Vert";

    [Tooltip("Float parameter that drives walk/run blend (0 = walk, 1 = run)")]
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
    //  PRIVATE
    // ---------------------------------------------
    private NavMeshAgent agent;
    private Animator animator;
    private AudioSource audioSource;
    private Vector3 startPosition;

    // =============================================
    //  UNITY LIFECYCLE
    // =============================================
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        startPosition = transform.position;

        agent.speed = moveSpeed;
        agent.angularSpeed = angularSpeed;

        audioSource.loop = false;
        audioSource.playOnAwake = false;
        audioSource.volume = soundVolume;

        StartCoroutine(WanderRoutine());
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

        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = paused;
    }

    void Update()
    {
        // Drive Vert from actual agent velocity magnitude so the blend
        // matches real movement speed, same as CreatureMover uses axis.magnitude
        float speed = agent.velocity.magnitude;
        float normalizedSpeed = Mathf.Clamp01(speed / moveSpeed);

        animator.SetFloat(vertParameterName, normalizedSpeed, animationDampTime, Time.deltaTime);
        animator.SetFloat(stateParameterName, 0f); // animals always walk, never run
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

        AudioClip clip = animalSounds[Random.Range(0, animalSounds.Length)];
        if (clip != null)
            audioSource.PlayOneShot(clip, soundVolume);
    }
}