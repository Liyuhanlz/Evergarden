using UnityEngine;

// The tutorial merchant: waits idle at the booth until the player walks into
// range, then walks up to the player, runs the tutorial dialogue, and walks
// back to the booth to stay as the shopkeeper. Triggering on player-range
// (rather than a fixed timer) keeps the walk inside the small NavMesh area
// baked around the market -- it never needs to path across the whole map.
// The booth's MerchantBooth script handles the actual shop-opening trigger.
//
// Unity setup:
//   1. Add this script to your NPC GameObject (needs NavMeshAgent + Animator --
//      NPCBase already requires those, Unity will add them automatically)
//   2. Make sure the NPC's Player Detection Radius (in NPCBase) fits inside
//      the baked NavMesh area, so the player triggers the walk while still
//      standing on walkable ground the NPC can path to
//   3. In the Animator, create two Bool parameters: "IsMoving" and "IsTalking"
//   4. Drag the player transform and booth transform into the Inspector fields
//   5. Fill in Tutorial Lines -- one dialogue bubble per array entry
public class MerchantNPC : NPCBase
{
    enum State { Idle, WalkToPlayer, Tutorial, WalkToBooth, Merchant }

    [Header("Merchant Setup")]
    [Tooltip("Drag the booth/counter Transform here -- where the NPC settles")]
    public Transform boothTransform;

    [Tooltip("If the booth sits somewhere the NavMesh can't quite reach (e.g. under a low canopy), the NPC snaps into place once it's stopped moving and gotten at least this close -- rather than never arriving")]
    public float boothSettleDistance = 3f;

    [TextArea(2, 4)]
    [Tooltip("Lines shown during the tutorial dialogue, one bubble per entry")]
    public string[] tutorialLines;

    State state = State.Idle;
    bool tutorialDone = false;

    protected override void Awake()
    {
        base.Awake();
        agent.speed = 1.4f;
    }

    protected override void Update()
    {
        base.Update();

        switch (state)
        {
            case State.WalkToPlayer: UpdateWalkToPlayer(); break;
            case State.WalkToBooth: UpdateWalkToBooth(); break;
        }
    }

    void BeginTutorialSequence()
    {
        state = State.WalkToPlayer;
        MoveTo(playerTransform.position);
    }

    void UpdateWalkToPlayer()
    {
        MoveTo(playerTransform.position); // keep following in case player moves

        if (HasArrived(playerTransform.position))
        {
            StopMoving();
            FaceTarget(playerTransform);
            state = State.Tutorial;
            StartDialogue(tutorialLines);
        }
    }

    protected override void OnDialogueComplete()
    {
        tutorialDone = true;

        if (boothTransform == null)
        {
            Debug.LogWarning($"[{name}] No booth transform assigned -- staying put.");
            state = State.Merchant;
            return;
        }

        state = State.WalkToBooth;
        MoveTo(boothTransform.position);
    }

    void UpdateWalkToBooth()
    {
        bool reachedExactly = HasArrived(boothTransform.position);

        // The NavMesh may not physically reach the booth (e.g. too little
        // headroom under a canopy) -- if the agent has genuinely stopped
        // moving and got reasonably close, settle in rather than wait forever.
        bool stoppedNearby = !agent.pathPending && !agent.hasPath
            && Vector3.Distance(transform.position, boothTransform.position) <= boothSettleDistance;

        if (reachedExactly || stoppedNearby)
        {
            StopMoving();

            // He's done moving for good once he's back at the booth, so disable
            // the agent entirely before the snap. agent.Warp() would just re-snap
            // to the nearest NavMesh point (which may not be the exact booth spot,
            // e.g. under a low canopy), and leaving the agent enabled means it
            // actively drives position/rotation each frame and silently overrides
            // direct Transform writes -- which was quietly fighting this snap before.
            agent.enabled = false;
            transform.position = boothTransform.position;
            transform.rotation = boothTransform.rotation;

            state = State.Merchant;
            Debug.Log($"[{name}] Arrived at booth -- now merchant.");
        }
    }

    protected override void OnPlayerEnterRange()
    {
        if (state == State.Idle)
        {
            BeginTutorialSequence();
            return;
        }

        if (state == State.Merchant)
            FaceTarget(playerTransform);
    }

    public bool TutorialDone => tutorialDone;

    // True only once he's actually settled at the booth (not mid-tutorial or
    // still walking back) -- gate shop access on this, not just TutorialDone,
    // so the player can't pull him into the shop-view teleport while he's
    // still mid-walk toward them.
    public bool IsAtBooth => state == State.Merchant;
}
