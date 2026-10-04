using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// A hen's daily routine, modelled on how backyard chickens actually behave.
// Use this INSTEAD of AnimalWander on chickens (same Animator/NavMeshAgent/
// AudioSource setup, so the Animals_FREE Chicken prefab works as-is once
// AnimalWander is swapped for this).
//
//   Dawn    -- Hens leave the roost a little after first light, one at a
//              time rather than all at once.
//   Morning -- Most eggs are laid in the first ~6 hours after sunrise. A hen
//              that's due walks back into the coop, sits in the nest box for
//              a while, then announces it (the "egg song") and goes back out.
//   Day     -- Free-range foraging: short scratch-and-peck walks, long pauses,
//              staying fairly close to the coop and drifting toward flockmates.
//   Dusk    -- Chickens put themselves to bed: they head back to the coop on
//              their own as the light fades and stay in until morning.
//
// Egg production (rolled once per in-game day at GameClock.OnNewDay):
//   - At most ONE egg per day: a hen's laying cycle is ~24-26 hours, so she
//     physically can't lay two in a day.
//   - Daily chance = breed's average lay rate x age factor. Breed rates come
//     from typical yearly totals (e.g. Leghorn ~300/yr ~= 0.8/day).
//   - Age: pullets start laying at ~18-22 weeks ("point of lay"), peak for
//     their first ~2 years, then drop ~15% a year.
// Across a flock this naturally gives the player a varying, random number of
// eggs each morning.
//
// Unity setup:
//   1. On the chicken: remove AnimalWander, add this script
//   2. Drag the ChickenCoop into Coop (or leave empty to auto-find the nearest)
//   3. Pick a Breed and Age In Weeks -- mixing them across the flock makes
//      daily yields more interesting
//   4. Needs a baked NavMesh covering the yard and the coop's entrance
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AudioSource))]
public class Chicken : MonoBehaviour
{
    public enum Breed { Leghorn, RhodeIslandRed, PlymouthRock, Orpington, Silkie }

    // ---------------------------------------------
    //  HEN
    // ---------------------------------------------
    [Header("Hen")]
    public Breed breed = Breed.RhodeIslandRed;

    [Tooltip("Hens start laying at ~18-22 weeks, peak through ~2 years (104 weeks), then slowly decline. " +
             "Ages one week per 7 in-game days.")]
    public float ageInWeeks = 40f;

    [Tooltip("The coop this hen roosts and lays in -- auto-finds the nearest ChickenCoop if left empty")]
    public ChickenCoop coop;

    // ---------------------------------------------
    //  SCHEDULE
    // ---------------------------------------------
    [Header("Daily Schedule (in-game hours, 24h)")]
    [Tooltip("Earliest/latest hour this hen leaves the roost. Sunrise is 6:00 -- with the default 120s " +
             "GameClock day, one in-game hour is only 5 real seconds, so these windows are short in practice.")]
    public Vector2 wakeHourRange = new Vector2(6.25f, 7.25f);

    [Tooltip("Earliest/latest hour this hen starts heading back to roost. Sunset is 18:00 -- leaving a " +
             "bit before gives her time to walk back before dark.")]
    public Vector2 roostHourRange = new Vector2(16.5f, 17.5f);

    [Tooltip("Latest hour a laying hen will go to the nest -- most eggs are laid within ~6 hours of sunrise")]
    public float latestLayHour = 11.5f;

    [Tooltip("In-game hours a hen sits in the nest box before the egg is laid")]
    public Vector2 nestHoursRange = new Vector2(0.4f, 1f);

    // ---------------------------------------------
    //  FORAGING
    // ---------------------------------------------
    [Header("Foraging")]
    [Tooltip("How far from the coop the hen will wander while free-ranging")]
    public float forageRadius = 8f;

    [Tooltip("Chance each walk heads toward another hen instead of a random spot -- chickens are flock animals")]
    [Range(0f, 1f)]
    public float flockChance = 0.3f;

    [Tooltip("How far from the chosen flockmate she settles -- close, but not on top of her")]
    public Vector2 flockSpacingRange = new Vector2(1f, 2.5f);

    [Tooltip("Min and max real seconds a hen walks before stopping")]
    public Vector2 walkDurationRange = new Vector2(2f, 5f);

    [Tooltip("Min and max real seconds she stops to scratch and peck -- chickens spend most of the day doing this")]
    public Vector2 peckDurationRange = new Vector2(3f, 8f);

    public float moveSpeed = 1.0f;

    [Tooltip("Speed when hurrying back to the coop at dusk")]
    public float returnSpeed = 1.6f;

    public float angularSpeed = 240f;

    [Tooltip("How close to the entrance counts as 'at the door' -- generous, since hens crowding the " +
             "doorway at dusk push each other off the exact point")]
    public float doorRadius = 1f;

    [Tooltip("Real seconds before a hen stuck en route to the coop is simply popped inside")]
    public float coopWalkTimeout = 12f;

    // ---------------------------------------------
    //  ANIMATION
    // ---------------------------------------------
    [Header("Animation Parameters")]
    public string vertParameterName = "Vert";
    public string stateParameterName = "State";
    public float animationDampTime = 0.1f;

    // ---------------------------------------------
    //  AUDIO
    // ---------------------------------------------
    [Header("Audio")]
    [Tooltip("Everyday clucks, played at random while out of the coop")]
    public AudioClip[] cluckSounds;

    [Tooltip("The loud cackle hens make right after laying -- optional")]
    public AudioClip eggSong;

    public Vector2 soundIntervalRange = new Vector2(5f, 15f);

    [Range(0f, 1f)]
    public float soundVolume = 1f;

    // ---------------------------------------------
    //  STATE (read-only, handy for debugging / UI)
    // ---------------------------------------------
    public bool IsInsideCoop { get; private set; }
    public bool WillLayToday { get; private set; }
    public bool HasLaidToday { get; private set; }

    public bool LaysWhiteEggs => breed == Breed.Leghorn || breed == Breed.Silkie;

    private NavMeshAgent agent;
    private Animator animator;
    private AudioSource audioSource;
    private Renderer[] renderers;
    private Collider[] colliders;
    private AnimatorControllerParameterType? stateParameterType;

    private bool isPaused;
    private float todaysWakeHour;
    private float todaysRoostHour;
    private float todaysLayHour;

    // =============================================
    //  REAL-WORLD DATA
    // =============================================

    // Average eggs per day at peak, from typical yearly totals.
    public static float PeakLayRate(Breed b)
    {
        switch (b)
        {
            case Breed.Leghorn:        return 0.82f; // ~280-320 / yr, white eggs
            case Breed.RhodeIslandRed: return 0.75f; // ~250-300 / yr, brown eggs
            case Breed.PlymouthRock:   return 0.66f; // ~200-280 / yr, brown eggs
            case Breed.Orpington:      return 0.52f; // ~175-200 / yr, brown eggs
            case Breed.Silkie:         return 0.30f; // ~100-120 / yr, cream eggs
            default:                   return 0.7f;
        }
    }

    // 0 before point of lay, ramping up as a pullet, 1 at peak, then ~15%
    // less per year after two years (older hens still lay, just less often).
    public static float AgeFactor(float weeks)
    {
        const float pointOfLay = 20f;
        const float fullProduction = 26f;
        const float peakEnd = 104f;

        if (weeks < pointOfLay) return 0f;
        if (weeks < fullProduction) return Mathf.Lerp(0.5f, 1f, (weeks - pointOfLay) / (fullProduction - pointOfLay));
        if (weeks <= peakEnd) return 1f;

        float yearsPastPeak = (weeks - peakEnd) / 52f;
        return Mathf.Max(0.1f, 1f - 0.15f * yearsPastPeak);
    }

    public float DailyLayChance => PeakLayRate(breed) * AgeFactor(ageInWeeks);

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

        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == stateParameterName) stateParameterType = p.type;

        agent.speed = moveSpeed;
        agent.angularSpeed = angularSpeed;

        audioSource.loop = false;
        audioSource.playOnAwake = false;
        audioSource.volume = soundVolume;

        if (coop == null) coop = FindNearestCoop();
        if (coop == null)
            Debug.LogWarning("[Chicken] " + name + " has no ChickenCoop -- she'll forage but never roost or lay.");
        else
            coop.Register(this);

        PlanDay();

        if (GameClock.Instance != null)
        {
            GameClock.Instance.OnNewDay += HandleNewDay;
            GameClock.Instance.OnPauseChanged += HandlePauseChanged;
        }

        // Scene starts at dawn: the flock begins the game still on the roost
        // and wanders out over the next in-game hour, like a real morning.
        if (coop != null && ShouldBeRoosting())
            GoInside();

        StartCoroutine(LifeRoutine());
        StartCoroutine(SoundRoutine());
    }

    void OnDestroy()
    {
        if (GameClock.Instance != null)
        {
            GameClock.Instance.OnNewDay -= HandleNewDay;
            GameClock.Instance.OnPauseChanged -= HandlePauseChanged;
        }
        if (coop != null) coop.Unregister(this);
    }

    void Update()
    {
        float speed = agent.enabled ? agent.velocity.magnitude : 0f;
        float normalizedSpeed = Mathf.Clamp01(speed / moveSpeed);
        animator.SetFloat(vertParameterName, normalizedSpeed, animationDampTime, Time.deltaTime);

        // Hurrying home at dusk uses the run animation; everything else walks.
        bool running = agent.enabled && agent.speed > moveSpeed + 0.01f && speed > 0.1f;
        if (stateParameterType == AnimatorControllerParameterType.Float)
            animator.SetFloat(stateParameterName, running ? 1f : 0f);
        else if (stateParameterType == AnimatorControllerParameterType.Int)
            animator.SetInteger(stateParameterName, running ? 1 : 0);
    }

    void HandlePauseChanged(bool paused)
    {
        isPaused = paused;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = paused;
    }

    void HandleNewDay()
    {
        ageInWeeks += 1f / 7f;
        PlanDay();
    }

    // Each morning: does she lay today, and roughly when do things happen.
    // Randomised per hen so the flock doesn't move in lockstep.
    void PlanDay()
    {
        todaysWakeHour = Random.Range(wakeHourRange.x, wakeHourRange.y);
        todaysRoostHour = Random.Range(roostHourRange.x, roostHourRange.y);
        todaysLayHour = Random.Range(todaysWakeHour - 0.25f, latestLayHour);

        WillLayToday = Random.value < DailyLayChance;
        HasLaidToday = false;
    }

    // =============================================
    //  ROUTINE
    // =============================================
    IEnumerator LifeRoutine()
    {
        while (true)
        {
            while (isPaused) yield return null;

            if (coop != null && ShouldBeRoosting())
            {
                if (!IsInsideCoop) yield return WalkIntoCoop(hurry: true);
                else yield return null;
                continue;
            }

            if (coop != null && EggIsDue())
            {
                if (!IsInsideCoop) yield return WalkIntoCoop(hurry: false);
                yield return SitInNest();
                continue;
            }

            if (IsInsideCoop) ComeOutside();

            yield return ForageStep();
        }
    }

    bool ShouldBeRoosting()
    {
        float hour = CurrentHour();
        return hour >= todaysRoostHour || hour < todaysWakeHour;
    }

    bool EggIsDue()
    {
        return WillLayToday && !HasLaidToday && CurrentHour() >= todaysLayHour;
    }

    // Anything that should cut a foraging walk short.
    bool RoutineInterrupted()
    {
        return coop != null && (ShouldBeRoosting() || EggIsDue());
    }

    IEnumerator ForageStep()
    {
        agent.speed = moveSpeed;
        agent.SetDestination(PickForageSpot());

        yield return WaitUnlessPaused(Random.Range(walkDurationRange.x, walkDurationRange.y), RoutineInterrupted);
        if (agent.isOnNavMesh) agent.ResetPath();
        if (RoutineInterrupted()) yield break;

        // Scratch and peck in place.
        yield return WaitUnlessPaused(Random.Range(peckDurationRange.x, peckDurationRange.y), RoutineInterrupted);
    }

    IEnumerator WalkIntoCoop(bool hurry)
    {
        agent.speed = hurry ? returnSpeed : moveSpeed;
        agent.SetDestination(coop.EntrancePosition);

        float elapsed = 0f;
        while (elapsed < coopWalkTimeout)
        {
            if (!isPaused)
            {
                elapsed += Time.deltaTime;
                Vector3 toDoor = coop.EntrancePosition - transform.position;
                toDoor.y = 0f;
                if (toDoor.magnitude <= doorRadius)
                    break;
            }
            yield return null;
        }

        agent.speed = moveSpeed;
        GoInside();
    }

    IEnumerator SitInNest()
    {
        yield return WaitGameHours(Random.Range(nestHoursRange.x, nestHoursRange.y));

        HasLaidToday = true;
        coop.AddEgg(LaysWhiteEggs);

        if (eggSong != null)
            audioSource.PlayOneShot(eggSong, soundVolume);
    }

    void GoInside()
    {
        if (agent.isOnNavMesh) agent.ResetPath();
        agent.enabled = false;
        transform.position = coop.EntrancePosition;
        SetVisible(false);
        IsInsideCoop = true;
    }

    void ComeOutside()
    {
        transform.position = coop.EntrancePosition;
        agent.enabled = true;
        agent.Warp(coop.EntrancePosition);
        agent.isStopped = isPaused;
        SetVisible(true);
        IsInsideCoop = false;
    }

    void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers) r.enabled = visible;
        foreach (Collider c in colliders) c.enabled = visible;
    }

    Vector3 PickForageSpot()
    {
        Vector3 center = coop != null ? coop.EntrancePosition : transform.position;

        // Drift toward a flockmate some of the time so the hens loosely
        // cluster, rather than scattering evenly across the yard.
        if (coop != null && Random.value < flockChance)
        {
            Chicken mate = coop.RandomOutsideHen(this);
            if (mate != null)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(flockSpacingRange.x, flockSpacingRange.y);
                Vector3 nearMate = mate.transform.position + new Vector3(offset.x, 0f, offset.y);
                if (NavMesh.SamplePosition(nearMate, out NavMeshHit mateHit, 2f, NavMesh.AllAreas))
                    return mateHit.position;
            }
        }

        for (int i = 0; i < 10; i++)
        {
            Vector3 randomPoint = center + Random.insideUnitSphere * forageRadius;
            randomPoint.y = center.y;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, forageRadius, NavMesh.AllAreas))
                return hit.position;
        }

        return transform.position;
    }

    // =============================================
    //  TIME HELPERS
    // =============================================
    static float CurrentHour()
    {
        if (GameClock.Instance == null) return 12f;
        return GameClock.Instance.CurrentHour + GameClock.Instance.CurrentMinute / 60f;
    }

    // Like WaitForSeconds, but frozen while paused and cut short if
    // stopEarly says so.
    IEnumerator WaitUnlessPaused(float seconds, System.Func<bool> stopEarly)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (!isPaused)
            {
                elapsed += Time.deltaTime;
                if (stopEarly()) yield break;
            }
            yield return null;
        }
    }

    // Waits a span of in-game time, tracking GameClock's day length.
    IEnumerator WaitGameHours(float hours)
    {
        float secondsPerHour = GameClock.Instance != null ? GameClock.Instance.realSecondsPerDay / 24f : 5f;
        yield return WaitUnlessPaused(hours * secondsPerHour, () => false);
    }

    // =============================================
    //  SOUND
    // =============================================
    IEnumerator SoundRoutine()
    {
        yield return new WaitForSeconds(Random.Range(0f, soundIntervalRange.y));

        while (true)
        {
            // Hens are quiet on the roost at night.
            if (!IsInsideCoop && !isPaused && cluckSounds != null && cluckSounds.Length > 0 && !audioSource.isPlaying)
            {
                AudioClip clip = cluckSounds[Random.Range(0, cluckSounds.Length)];
                if (clip != null) audioSource.PlayOneShot(clip, soundVolume);
            }

            yield return new WaitForSeconds(Random.Range(soundIntervalRange.x, soundIntervalRange.y));
        }
    }

    ChickenCoop FindNearestCoop()
    {
        ChickenCoop nearest = null;
        float best = float.MaxValue;
        foreach (ChickenCoop c in FindObjectsByType<ChickenCoop>(FindObjectsSortMode.None))
        {
            float d = (c.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; nearest = c; }
        }
        return nearest;
    }
}
