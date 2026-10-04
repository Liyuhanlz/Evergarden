using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Put on each individual fruit (tomato, corn cob) attached to a crop's final
// "ready" stage prefab. Grabbing one -- hand or ray -- plucks just that fruit
// off the stem: a short haptic buzz on the grabbing controller and +1 of the
// crop in the inventory. The fruit stays in the player's hand while they hold
// it, and disappears the moment they let go (it's already in the inventory).
// The owning Farmland counts the picks and runs the normal Harvest() (regrow
// or clear the plot) once the last fruit is gone, so per-fruit picking
// replaces the old "press A to harvest the whole plant" for any crop whose
// ready stage has these on it.
//
// Unity setup (per fruit child of the ready-stage prefab):
//   Collider (sized to the fruit) + Rigidbody (Is Kinematic) + XRGrabInteractable
//   + this. Farmland finds these automatically when it spawns the stage.
[RequireComponent(typeof(XRGrabInteractable))]
public class PickableFruit : MonoBehaviour
{
    [Header("Haptics")]
    [Range(0f, 1f)] public float hapticAmplitude = 0.6f;
    public float hapticDuration = 0.12f;

    // Set by Farmland when it spawns the stage this fruit belongs to
    [HideInInspector] public Farmland owner;

    private XRGrabInteractable grab;
    private bool picked;

    // The player's body -- fruit on the plant must stay solid (the hand/ray
    // interactors ignore triggers, so a trigger collider can't be grabbed),
    // but walking through a crop row shouldn't bump into tomatoes
    private static CharacterController playerBody;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    void Start()
    {
        if (playerBody == null)
        {
            Unity.XR.CoreUtils.XROrigin origin = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null) playerBody = origin.GetComponent<CharacterController>();
        }

        if (playerBody != null)
            foreach (Collider c in GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(c, playerBody);
    }

    void OnDestroy()
    {
        if (grab == null) return;
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        // Sockets never pick fruit -- only the player's own hand/ray
        if (picked || args.interactorObject is XRSocketInteractor) return;
        picked = true;

        if (args.interactorObject is XRBaseControllerInteractor controllerInteractor)
            controllerInteractor.SendHapticImpulse(hapticAmplitude, hapticDuration);

        // Off the plant for good: the plant model gets swapped/destroyed when
        // the last fruit is picked, which must not take the held fruit with it
        transform.SetParent(null, true);

        // While held it shouldn't bump anything (the grab is already made,
        // so the interactors ignoring triggers no longer matters)
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.isTrigger = true;

        if (owner != null) owner.PickFruit(this);
    }

    void OnReleased(SelectExitEventArgs args)
    {
        if (!picked) return;
        Destroy(gameObject);
    }
}
