using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR;

// Locks the player into a fixed vantage point while the shop is open: smoothly
// moves the XR rig to ShopViewPoint (so Merchant/booth read on one side and the
// ShopUI reads on the other), disables locomotion so they can't wander off
// mid-shop, and shows a "Press B to exit" cue. Pressing the right-hand B
// button smoothly returns them to wherever they were standing before and
// re-enables movement.
//
// Unity setup:
//   1. Put this script on an empty "ShopInteractionController" GameObject
//   2. Drag the XR Origin (XR Rig) into Xr Origin and Player Character
//      Controller, and its Main Camera into Player Camera
//   3. Drag ActionBasedContinuousMoveProvider, ActionBasedContinuousTurnProvider,
//      and ActionBasedSnapTurnProvider (all on the XR Origin) into Locomotion
//      Providers To Disable
//   4. Drag the ShopViewPoint Transform into Shop View Point -- its position is
//      where the player stands, its forward direction is which way they face
//   5. Drag ShopUI into Shop UI, and a small "Press B to exit" canvas into Exit Cue Canvas
public class ShopInteractionController : MonoBehaviour
{
    public static ShopInteractionController Instance { get; private set; }

    [Header("Player Rig")]
    public Transform xrOrigin;
    public Transform playerCamera;
    public CharacterController playerCharacterController;

    [Header("Locomotion (disabled while shopping)")]
    public Behaviour[] locomotionProvidersToDisable;

    [Header("Shop View")]
    [Tooltip("Position = where the player stands, forward = which way they face")]
    public Transform shopViewPoint;
    public float transitionDuration = 1f;

    [Header("References")]
    public ShopUI shopUI;
    public Canvas exitCueCanvas;

    bool inShopMode = false;
    Vector3 savedOriginPos;
    Quaternion savedOriginRot;
    bool[] savedLocomotionEnabled;
    Coroutine transitionRoutine;

    InputDevice rightHandDevice;
    bool prevBPressed = false;

    public bool IsInShopMode => inShopMode;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (exitCueCanvas != null)
            exitCueCanvas.gameObject.SetActive(false);
    }

    void Start()
    {
        GetRightHandDevice();
    }

    // Always reads and tracks the button every frame, regardless of whether
    // we're currently in shop mode -- otherwise prevBPressed goes stale while
    // out shopping (e.g. never true during the tutorial or general play), and
    // the first B press after entering the shop can misread as an edge
    // against ancient state and instantly exit again.
    void Update()
    {
        if (!rightHandDevice.isValid)
        {
            GetRightHandDevice();
            return;
        }

        if (rightHandDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bPressed))
        {
            bool justPressed = bPressed && !prevBPressed;
            prevBPressed = bPressed;

            if (justPressed && inShopMode)
                ExitShop();
        }
    }

    public void EnterShop()
    {
        if (inShopMode || shopViewPoint == null || xrOrigin == null || playerCamera == null) return;

        inShopMode = true;
        savedOriginPos = xrOrigin.position;
        savedOriginRot = xrOrigin.rotation;

        DisableLocomotion();

        Vector3 targetPos;
        Quaternion targetRot;
        ComputeOriginTargetForCameraPose(shopViewPoint.position, shopViewPoint.forward, out targetPos, out targetRot);

        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(TransitionOrigin(targetPos, targetRot, OnEnterShopComplete));
    }

    public void ExitShop()
    {
        if (!inShopMode) return;

        if (shopUI != null) shopUI.Close();
        if (exitCueCanvas != null) exitCueCanvas.gameObject.SetActive(false);

        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(TransitionOrigin(savedOriginPos, savedOriginRot, OnExitShopComplete));
    }

    void OnEnterShopComplete()
    {
        if (shopUI != null) shopUI.Open();
        if (exitCueCanvas != null) exitCueCanvas.gameObject.SetActive(true);
    }

    void OnExitShopComplete()
    {
        RestoreLocomotion();
        inShopMode = false;
    }

    // Remembers each provider's own enabled state before turning it off, so
    // ExitShop can put things back exactly as they were -- rather than
    // assuming every provider should end up enabled, which was quietly
    // turning ones that are normally off (e.g. snap turn, if this project
    // only uses continuous turn) back on after every shop visit.
    void DisableLocomotion()
    {
        if (locomotionProvidersToDisable == null) return;

        savedLocomotionEnabled = new bool[locomotionProvidersToDisable.Length];

        for (int i = 0; i < locomotionProvidersToDisable.Length; i++)
        {
            var provider = locomotionProvidersToDisable[i];
            if (provider == null) continue;

            savedLocomotionEnabled[i] = provider.enabled;
            provider.enabled = false;
        }
    }

    void RestoreLocomotion()
    {
        if (locomotionProvidersToDisable == null || savedLocomotionEnabled == null) return;

        for (int i = 0; i < locomotionProvidersToDisable.Length; i++)
        {
            var provider = locomotionProvidersToDisable[i];
            if (provider == null) continue;

            provider.enabled = savedLocomotionEnabled[i];
        }
    }

    // Figures out where/how the XR Origin needs to sit so the player's camera
    // ends up at desiredCameraPos facing desiredCameraForward -- rotating
    // around the camera's own position so the transition doesn't swing the
    // player through a wide arc, just reorients them in place then slides them.
    void ComputeOriginTargetForCameraPose(Vector3 desiredCameraPos, Vector3 desiredCameraForward, out Vector3 targetOriginPos, out Quaternion targetOriginRot)
    {
        float currentYaw = Mathf.Atan2(playerCamera.forward.x, playerCamera.forward.z) * Mathf.Rad2Deg;
        float desiredYaw = Mathf.Atan2(desiredCameraForward.x, desiredCameraForward.z) * Mathf.Rad2Deg;
        float deltaYaw = Mathf.DeltaAngle(currentYaw, desiredYaw);

        Vector3 pivot = playerCamera.position;
        Quaternion rot = Quaternion.Euler(0f, deltaYaw, 0f);

        Vector3 originPosAfterRotate = pivot + rot * (xrOrigin.position - pivot);
        Quaternion originRotAfterRotate = rot * xrOrigin.rotation;

        // Rotating around the camera's own position leaves its XZ unchanged,
        // so the remaining gap to the desired position is a simple translation.
        Vector3 offset = desiredCameraPos - pivot;
        offset.y = 0f;

        targetOriginPos = originPosAfterRotate + offset;
        targetOriginRot = originRotAfterRotate;
    }

    IEnumerator TransitionOrigin(Vector3 targetPos, Quaternion targetRot, Action onComplete)
    {
        // A CharacterController drives its own GameObject's position each frame
        // and will otherwise fight a direct Transform write, the same issue we
        // hit with NavMeshAgent -- disable it for the duration of the slide.
        if (playerCharacterController != null) playerCharacterController.enabled = false;

        Vector3 startPos = xrOrigin.position;
        Quaternion startRot = xrOrigin.rotation;
        float t = 0f;

        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / transitionDuration);
            float eased = p * p * (3f - 2f * p); // smoothstep

            xrOrigin.position = Vector3.Lerp(startPos, targetPos, eased);
            xrOrigin.rotation = Quaternion.Slerp(startRot, targetRot, eased);
            yield return null;
        }

        xrOrigin.position = targetPos;
        xrOrigin.rotation = targetRot;

        if (playerCharacterController != null) playerCharacterController.enabled = true;

        onComplete?.Invoke();
    }

    void GetRightHandDevice()
    {
        rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }
}
