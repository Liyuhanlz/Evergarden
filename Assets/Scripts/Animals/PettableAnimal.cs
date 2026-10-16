using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Lets the player pet a farm animal with their hands: reach out and stroke
// its back. No button needed -- slow strokes while the hand is on the
// animal build up, and once there's been enough the animal stops what it's
// doing, turns to face the player and enjoys it:
//   - hearts float up from its back with every stroke
//   - a little controller buzz on the stroking hand
//   - a happy sound now and then
//   - goats/sheep sit down (stand_to_sit), everyone else does a small
//     happy bounce
// Stop stroking for a moment and it gets back up and carries on.
//
// Works alongside AnimalWander or Chicken (whichever the animal has) --
// they're held still while petted and never pettable on the way to or
// inside the barn/coop.
//
// Unity setup:
//   1. Add this script to the animal (next to AnimalWander / Chicken)
//   2. Hearts Prefab: Assets/Prefabs/Effects/PetHearts
//   3. Happy Sounds: optional -- falls back to the animal's own sounds
//   4. Goats/sheep: leave Sit State / Stand State as they are. For animals
//      without those states nothing needs changing -- they bounce instead.
// Hands are found automatically: every XRDirectInteractor in the scene.
[RequireComponent(typeof(AudioSource))]
public class PettableAnimal : MonoBehaviour
{
    // ---------------------------------------------
    //  TOUCH
    // ---------------------------------------------
    [Header("Touch")]
    [Tooltip("How far (m) outside the animal's body a hand still counts as touching it -- " +
             "a bit of slack so the player doesn't have to put their hand right through it")]
    public float touchPadding = 0.1f;

    [Tooltip("Hand speeds (m/s) that count as a stroke. Slower is just resting a hand on it; faster is a swipe, not a pet.")]
    public Vector2 strokeSpeedRange = new Vector2(0.05f, 1.5f);

    [Tooltip("How much stroking (m of hand travel) it takes before the animal settles in to be petted")]
    public float strokeToStart = 0.25f;

    [Tooltip("How fast built-up stroking fades away (m/s) if the player stops before the animal settles")]
    public float strokeDecay = 0.1f;

    [Tooltip("Seconds without a stroke before the animal decides petting is over")]
    public float releaseDelay = 1.5f;

    // ---------------------------------------------
    //  REACTION
    // ---------------------------------------------
    [Header("Reaction")]
    [Tooltip("Turn to face the player while being petted")]
    public bool facePlayer = true;

    [Tooltip("Degrees per second when turning to face the player")]
    public float turnSpeed = 120f;

    [Tooltip("Animator state played when petting starts (skipped if the controller doesn't have it)")]
    public string sitState = "stand_to_sit";

    [Tooltip("Animator state played when petting ends, before returning to Idle State")]
    public string standState = "sit_to_stand";

    [Tooltip("The normal idle/walk state to go back to afterwards")]
    public string idleState = "Blend Tree";

    [Tooltip("Seconds the Stand State takes before going back to Idle State")]
    public float standUpDuration = 2.2f;

    [Tooltip("For animals without a sit animation: how much they squash and stretch with joy")]
    public float bounceAmount = 0.06f;

    [Tooltip("Bounces per second")]
    public float bounceSpeed = 2.5f;

    // ---------------------------------------------
    //  HEARTS
    // ---------------------------------------------
    [Header("Hearts")]
    public ParticleSystem heartsPrefab;

    [Tooltip("A heart pops out for every this many meters of stroking")]
    public float strokePerHeart = 0.12f;

    [Tooltip("Hearts in the burst when the animal first settles in")]
    public int startBurst = 4;

    // ---------------------------------------------
    //  AUDIO
    // ---------------------------------------------
    [Header("Audio")]
    [Tooltip("Played when petting starts and now and then during it. Empty = use the animal's own sounds.")]
    public AudioClip[] happySounds;

    [Tooltip("Min and max seconds between happy sounds while being petted")]
    public Vector2 happySoundInterval = new Vector2(2.5f, 5f);

    [Range(0f, 1f)]
    public float happyVolume = 1f;

    // ---------------------------------------------
    //  HAPTICS
    // ---------------------------------------------
    [Header("Haptics")]
    [Range(0f, 1f)]
    public float hapticAmplitude = 0.15f;

    public float hapticDuration = 0.05f;

    // ---------------------------------------------
    //  STATE
    // ---------------------------------------------
    public bool IsBeingPetted { get; private set; }

    // ---------------------------------------------
    //  PRIVATE
    // ---------------------------------------------
    private AnimalWander wander;
    private Chicken chicken;
    private Animator animator;
    private AudioSource audioSource;
    private Renderer[] renderers;

    // The animal's body in its own local space, found from its renderers the
    // first time it's visible (a hen may start the game hidden in the coop).
    private Bounds localBody;
    private bool hasBody;

    private ParticleSystem hearts;
    private float strokeAmount;
    private float strokeSinceHeart;
    private float lastStrokeTime;
    private float nextHappySound;
    private bool sat;
    private Vector3 baseScale;
    private float bounceTime;
    private Coroutine standRoutine;

    private static readonly List<XRDirectInteractor> hands = new List<XRDirectInteractor>();
    private readonly Dictionary<XRDirectInteractor, Vector3> lastHandPos = new Dictionary<XRDirectInteractor, Vector3>();

    // =============================================
    //  UNITY LIFECYCLE
    // =============================================
    void Start()
    {
        wander = GetComponent<AnimalWander>();
        chicken = GetComponent<Chicken>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        renderers = GetComponentsInChildren<Renderer>();
        baseScale = transform.localScale;
    }

    void OnDisable()
    {
        if (IsBeingPetted) StopPetting();
    }

    void Update()
    {
        if (!CanBePetted())
        {
            if (IsBeingPetted) StopPetting();
            strokeAmount = 0f;
            lastHandPos.Clear();
            return;
        }

        if (!hasBody && !TryMeasureBody()) return;

        XRDirectInteractor strokingHand = TrackHands();

        if (IsBeingPetted)
        {
            if (Time.time - lastStrokeTime > releaseDelay)
                StopPetting();
            else
                WhilePetted(strokingHand);
        }
        else
        {
            if (strokeAmount >= strokeToStart)
                StartPetting(strokingHand);
            else
                strokeAmount = Mathf.Max(0f, strokeAmount - strokeDecay * Time.deltaTime);
        }
    }

    bool CanBePetted()
    {
        if (wander != null && !wander.CanBePetted) return false;
        if (chicken != null && !chicken.CanBePetted) return false;
        return true;
    }

    // =============================================
    //  HANDS
    // =============================================

    // Adds up stroking from every hand on the animal this frame. Returns the
    // hand that stroked (for the controller buzz), or null.
    XRDirectInteractor TrackHands()
    {
        if (hands.Count == 0) FindHands();

        XRDirectInteractor stroking = null;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        foreach (XRDirectInteractor hand in hands)
        {
            if (hand == null || !hand.isActiveAndEnabled) continue;

            Vector3 pos = hand.transform.position;
            if (!IsTouching(pos))
            {
                lastHandPos.Remove(hand);
                continue;
            }

            // A hand holding a tool (watering can, seed bag...) isn't petting.
            if (hand.hasSelection)
            {
                lastHandPos.Remove(hand);
                continue;
            }

            if (lastHandPos.TryGetValue(hand, out Vector3 last))
            {
                float moved = (pos - last).magnitude;
                float speed = moved / dt;
                if (speed >= strokeSpeedRange.x && speed <= strokeSpeedRange.y)
                {
                    strokeAmount += moved;
                    strokeSinceHeart += moved;
                    lastStrokeTime = Time.time;
                    stroking = hand;
                }
            }
            lastHandPos[hand] = pos;
        }

        return stroking;
    }

    static void FindHands()
    {
        hands.Clear();
        hands.AddRange(FindObjectsByType<XRDirectInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None));
    }

    bool IsTouching(Vector3 worldPos)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        Bounds padded = localBody;
        padded.Expand(touchPadding * 2f / Mathf.Max(transform.lossyScale.x, 0.0001f));
        return padded.Contains(local);
    }

    bool TryMeasureBody()
    {
        bool any = false;
        Bounds b = new Bounds();

        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;

            Bounds wb = r.bounds;
            Vector3 c = wb.center, e = wb.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 local = transform.InverseTransformPoint(corner);
                if (!any) { b = new Bounds(local, Vector3.zero); any = true; }
                else b.Encapsulate(local);
            }
        }

        if (!any) return false;

        localBody = b;
        hasBody = true;

        if (heartsPrefab != null)
        {
            // Moved to wherever the hand is stroking each time hearts pop out.
            hearts = Instantiate(heartsPrefab, transform);
            hearts.transform.localPosition = new Vector3(b.center.x, b.max.y, b.center.z);
            hearts.transform.localRotation = Quaternion.identity;
        }
        return true;
    }

    // =============================================
    //  PETTING
    // =============================================
    void StartPetting(XRDirectInteractor hand)
    {
        IsBeingPetted = true;
        lastStrokeTime = Time.time;
        strokeSinceHeart = 0f;
        bounceTime = 0f;

        if (wander != null) wander.SetPetted(true);
        if (chicken != null) chicken.SetPetted(true);

        if (standRoutine != null)
        {
            StopCoroutine(standRoutine);
            standRoutine = null;
        }

        sat = HasState(sitState);
        if (sat) animator.CrossFadeInFixedTime(sitState, 0.25f);

        EmitHearts(startBurst, hand);
        Buzz(hand, hapticAmplitude * 2f, hapticDuration * 3f);
        PlayHappySound();
    }

    void WhilePetted(XRDirectInteractor hand)
    {
        if (facePlayer && Camera.main != null)
        {
            Vector3 toPlayer = Camera.main.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), turnSpeed * Time.deltaTime);
        }

        if (strokeSinceHeart >= strokePerHeart)
        {
            strokeSinceHeart = 0f;
            EmitHearts(1, hand);
            Buzz(hand, hapticAmplitude, hapticDuration);
        }

        if (Time.time >= nextHappySound)
            PlayHappySound();

        // No sit animation: a gentle squash-and-stretch instead.
        if (!sat && bounceAmount > 0f)
        {
            bounceTime += Time.deltaTime;
            float s = Mathf.Abs(Mathf.Sin(bounceTime * bounceSpeed * Mathf.PI)) * bounceAmount;
            transform.localScale = Vector3.Scale(baseScale, new Vector3(1f - s * 0.5f, 1f + s, 1f - s * 0.5f));
        }
    }

    void StopPetting()
    {
        IsBeingPetted = false;
        strokeAmount = 0f;
        transform.localScale = baseScale;

        if (sat && isActiveAndEnabled)
        {
            // Get up before wandering off again.
            standRoutine = StartCoroutine(StandUp());
        }
        else
        {
            Release();
        }
    }

    IEnumerator StandUp()
    {
        animator.CrossFadeInFixedTime(standState, 0.25f);
        yield return new WaitForSeconds(standUpDuration);
        if (HasState(idleState)) animator.CrossFadeInFixedTime(idleState, 0.3f);
        sat = false;
        standRoutine = null;
        Release();
    }

    void Release()
    {
        if (wander != null) wander.SetPetted(false);
        if (chicken != null) chicken.SetPetted(false);
    }

    bool HasState(string stateName)
    {
        return animator != null && !string.IsNullOrEmpty(stateName)
            && animator.HasState(0, Animator.StringToHash(stateName));
    }

    // =============================================
    //  FEEDBACK
    // =============================================
    // Hearts pop out right under the stroking hand (it's the hearts' world
    // simulation space that lets them float up and away from there).
    void EmitHearts(int count, XRDirectInteractor hand)
    {
        if (hearts == null || count <= 0) return;
        if (hand != null) hearts.transform.position = hand.transform.position;
        hearts.Emit(count);
    }

    static void Buzz(XRDirectInteractor hand, float amplitude, float duration)
    {
        if (hand != null && amplitude > 0f) hand.SendHapticImpulse(Mathf.Clamp01(amplitude), duration);
    }

    void PlayHappySound()
    {
        nextHappySound = Time.time + Random.Range(happySoundInterval.x, happySoundInterval.y);

        AudioClip[] pool = happySounds != null && happySounds.Length > 0 ? happySounds
            : wander != null ? wander.animalSounds
            : chicken != null ? chicken.cluckSounds
            : null;
        if (pool == null || pool.Length == 0) return;

        AudioClip clip = pool[Random.Range(0, pool.Length)];
        if (clip == null) return;

        audioSource.PlayOneShot(clip, happyVolume);
    }
}
