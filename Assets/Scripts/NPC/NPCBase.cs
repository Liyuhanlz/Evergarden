using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

// Base class for every NPC in the game. Handles movement, facing, animation,
// player-range detection, and its own speech bubble. Never put this script
// directly on a GameObject -- use a subclass instead (MerchantNPC, VillagerNPC,
// QuestNPC). Subclasses only need to implement the OnXxx() hooks below;
// everything else is shared.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public abstract class NPCBase : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the player's XR Origin (or root) Transform here")]
    public Transform playerTransform;

    [Header("Movement")]
    [Tooltip("How close the NPC gets to a destination before it's considered arrived")]
    public float arrivalDistance = 1.5f;

    [Tooltip("How fast the NPC turns to face a target")]
    public float turnSpeed = 5f;

    [Header("Player Detection")]
    [Tooltip("Distance at which the NPC considers the player 'in range'")]
    public float playerDetectionRadius = 3f;

    [Header("Speech Bubble (child of this NPC)")]
    [Tooltip("Drag this NPC's own speech bubble Canvas here -- it should be a child of this GameObject, positioned near its head")]
    public Canvas speechBubbleCanvas;
    public TMP_Text speechBubbleText;

    [Tooltip("How long the pop-in scale animation takes, in seconds")]
    public float bubblePopDuration = 0.2f;

    protected NavMeshAgent agent;
    protected Animator animator;
    protected bool isTalking = false;

    bool playerInRange = false;
    Vector3 speechBubbleBaseScale;
    Coroutine bubblePopRoutine;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (speechBubbleCanvas != null)
        {
            speechBubbleBaseScale = speechBubbleCanvas.transform.localScale;
            speechBubbleCanvas.gameObject.SetActive(false);
        }
    }

    protected virtual void Update()
    {
        UpdateAnimator();
        CheckPlayerRange();
        BillboardSpeechBubble();
    }

    // ---------------------------------------------
    //  MOVEMENT HELPERS -- call these from subclasses
    // ---------------------------------------------
    protected void MoveTo(Vector3 destination)
    {
        agent.SetDestination(destination);
    }

    protected bool HasArrived(Vector3 destination)
    {
        return Vector3.Distance(transform.position, destination) <= arrivalDistance;
    }

    protected void StopMoving()
    {
        agent.ResetPath();
    }

    protected void FaceTarget(Transform target)
    {
        if (target == null) return;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir == Vector3.zero) return;

        Quaternion lookRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * turnSpeed);
    }

    // ---------------------------------------------
    //  ANIMATION
    // ---------------------------------------------
    void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetBool("IsMoving", agent.velocity.magnitude > 0.05f);
        animator.SetBool("IsTalking", isTalking);
    }

    // ---------------------------------------------
    //  DIALOGUE -- routes through the DialogueManager singleton
    // ---------------------------------------------
    protected void StartDialogue(string[] lines)
    {
        isTalking = true;

        if (DialogueManager.Instance != null)
            DialogueManager.Instance.StartDialogueLines(this, lines, HandleDialogueComplete);
        else
        {
            Debug.LogWarning($"[{name}] No DialogueManager in scene -- skipping dialogue.");
            HandleDialogueComplete();
        }
    }

    void HandleDialogueComplete()
    {
        isTalking = false;
        OnDialogueComplete();
    }

    // ---------------------------------------------
    //  SPEECH BUBBLE -- this NPC's own child object, called by DialogueManager
    // ---------------------------------------------
    public void ShowSpeechBubbleLine(string line)
    {
        if (speechBubbleCanvas == null) return;

        if (speechBubbleText != null) speechBubbleText.text = line;
        speechBubbleCanvas.gameObject.SetActive(true);

        if (bubblePopRoutine != null) StopCoroutine(bubblePopRoutine);
        bubblePopRoutine = StartCoroutine(BubblePopRoutine());
    }

    public void HideSpeechBubble()
    {
        if (speechBubbleCanvas != null)
            speechBubbleCanvas.gameObject.SetActive(false);
    }

    void BillboardSpeechBubble()
    {
        if (speechBubbleCanvas == null || !speechBubbleCanvas.gameObject.activeSelf) return;
        if (playerTransform == null) return;

        Vector3 dir = speechBubbleCanvas.transform.position - playerTransform.position;
        dir.y = 0f;
        // This canvas's readable front faces its local -Z, so aiming local +Z
        // away from the player (dir already points away from them) puts the
        // front toward the player.
        if (dir != Vector3.zero)
            speechBubbleCanvas.transform.rotation = Quaternion.LookRotation(dir);
    }

    // Quick "pop" -- scales past 100% then settles back, like a speech bubble
    // popping into existence. Standard easeOutBack curve.
    IEnumerator BubblePopRoutine()
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        Transform bubble = speechBubbleCanvas.transform;
        float t = 0f;

        while (t < bubblePopDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / bubblePopDuration);
            float eased = 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);

            bubble.localScale = speechBubbleBaseScale * eased;
            yield return null;
        }

        bubble.localScale = speechBubbleBaseScale;
    }

    // ---------------------------------------------
    //  PLAYER RANGE DETECTION
    // ---------------------------------------------
    void CheckPlayerRange()
    {
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        bool nowInRange = dist <= playerDetectionRadius;

        if (nowInRange && !playerInRange)
        {
            playerInRange = true;
            OnPlayerEnterRange();
        }
        else if (!nowInRange && playerInRange)
        {
            playerInRange = false;
            OnPlayerExitRange();
        }
    }

    // ---------------------------------------------
    //  HOOKS -- override in subclasses, base does nothing
    // ---------------------------------------------
    protected virtual void OnDialogueComplete() { }
    protected virtual void OnPlayerEnterRange() { }
    protected virtual void OnPlayerExitRange() { }

    // Called by the calendar system (GameClock/FarmManager) when a new day starts.
    public virtual void OnNewDay() { }
}
