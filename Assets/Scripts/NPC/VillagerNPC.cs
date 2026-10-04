using System.Collections;
using UnityEngine;

// A background NPC that wanders between waypoints and greets the player.
// Friendship increases through gifts/events later (AddFriendship) and can
// unlock better dialogue or rewards. Not wired to any GameObject yet --
// use this once you add more villagers to the scene.
//
// Unity setup: same as MerchantNPC (NavMeshAgent + Animator are automatic),
// plus drag in a list of empty Transforms as Waypoints for it to patrol between.
public class VillagerNPC : NPCBase
{
    [Header("Wander Settings")]
    [Tooltip("Points this villager walks between. Leave empty to stay put.")]
    public Transform[] waypoints;

    [Tooltip("Seconds to wait at each waypoint before moving to the next")]
    public Vector2 waitDurationRange = new Vector2(2f, 5f);

    [Header("Friendship")]
    [Range(0, 100)]
    public int friendshipLevel = 0;

    [Header("Greetings")]
    [TextArea(2, 3)]
    public string[] lowFriendshipGreetings = { "Oh, hello." };

    [TextArea(2, 3)]
    public string[] highFriendshipGreetings = { "So good to see you again!" };

    [Tooltip("Friendship level at which the NPC switches to the high-friendship greetings")]
    public int highFriendshipThreshold = 50;

    int currentWaypoint = -1;

    protected override void Awake()
    {
        base.Awake();
        agent.speed = 1f;
    }

    void Start()
    {
        if (waypoints != null && waypoints.Length > 0)
            StartCoroutine(WanderRoutine());
    }

    IEnumerator WanderRoutine()
    {
        while (true)
        {
            if (isTalking)
            {
                yield return null;
                continue;
            }

            currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
            Vector3 destination = waypoints[currentWaypoint].position;
            MoveTo(destination);

            yield return new WaitUntil(() => HasArrived(destination) || isTalking);

            StopMoving();

            float waitTime = Random.Range(waitDurationRange.x, waitDurationRange.y);
            yield return new WaitForSeconds(waitTime);
        }
    }

    protected override void OnPlayerEnterRange()
    {
        FaceTarget(playerTransform);

        string[] greeting = friendshipLevel >= highFriendshipThreshold
            ? highFriendshipGreetings
            : lowFriendshipGreetings;

        if (greeting.Length > 0)
            StartDialogue(new[] { greeting[Random.Range(0, greeting.Length)] });
    }

    public void AddFriendship(int amount)
    {
        friendshipLevel = Mathf.Clamp(friendshipLevel + amount, 0, 100);
    }

    public override void OnNewDay()
    {
        // Hook for the calendar system: e.g. change which waypoint schedule
        // this villager follows based on the day/season.
    }
}
