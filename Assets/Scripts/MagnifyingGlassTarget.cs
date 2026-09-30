using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

// Lives on the Magnifying Glass tool itself (not on the things it
// identifies) and shows info about whatever is close to its lens.
//
// Checks proximity with Physics.OverlapSphere each frame from a virtual
// point (lensPoint) rather than an actual trigger Collider on the tool --
// a real Collider here was picked up by the hand's own ray interactor while
// the glass was held (a physics raycast hits any collider in its path,
// including ones on the object currently in your hand), making the grab ray
// behave strangely. OverlapSphere is a pure spatial query with nothing for
// the ray to hit, so it can't interfere with anything else in the scene.
//
// Reads its data from whichever of two sources is on the detected
// collider's GameObject (or its parents, since a collider often sits on a
// child):
//   - Informational -- a fixed InfoData asset. Used for animals and tools,
//     whose identity never changes.
//   - Farmland -- reads farmland.cropData live instead. Which crop is even
//     planted on a tile changes over time (replanting), so a fixed InfoData
//     asset doesn't fit there -- this reads CropData's own summary/facts
//     fresh each time instead.
//
// Unity setup:
//   1. Add this to the Magnifying Glass tool
//   2. Drag an empty child Transform positioned at the lens end of the
//      glass into Lens Point (defaults to this object's own transform if
//      left empty)
//   3. Drag the shared info Canvas into Info Canvas, its title/body TMP
//      texts into Title Text / Body Text, and the player camera into
//      Player Camera (defaults to Camera.main if left empty)
//   4. Anything it should identify just needs Informational (with an
//      InfoData asset) or Farmland, plus any Collider (does not need to be
//      a trigger -- OverlapSphere doesn't care either way)
[RequireComponent(typeof(MagnifyingGlassTool))]
public class MagnifyingGlassTarget : MonoBehaviour
{
    [Tooltip("Where proximity is checked from -- defaults to this object's own transform if left empty")]
    public Transform lensPoint;

    [Tooltip("How close something needs to be to the lens to be identified")]
    public float detectionRadius = 0.08f;

    [Tooltip("Which layers to check -- leave as Everything unless it starts picking up things it shouldn't")]
    public LayerMask detectionMask = ~0;

    [Tooltip("Shared info window")]
    public Canvas infoCanvas;

    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bodyText;

    [Tooltip("Drag CenterEyeAnchor or Main Camera here -- only used to face the window toward the player. Defaults to Camera.main if left empty")]
    public Transform playerCamera;

    [Header("Placement (above the detected object)")]
    public Vector3 promptOffset = new Vector3(0f, 0.5f, 0f);

    [Tooltip("How long the info panel waits after losing proximity before actually hiding -- bridges brief separations (the lens grazing the edge of a walking animal) without flicker")]
    public float loseContactGrace = 0.2f;

    private static readonly Collider[] OverlapBuffer = new Collider[8];

    // Whatever the lens is currently (or was most recently, within the
    // grace period) near. Only one at a time -- if several things are in
    // range simultaneously, the closest wins.
    private Informational currentInfo;
    private Farmland currentFarmland;
    private int factIndex = -1;
    private Coroutine pendingHide;

    private InputDevice rightHandDevice;
    private bool prevAdvancePressed;

    void Awake()
    {
        if (lensPoint == null) lensPoint = transform;
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;
    }

    void Update()
    {
        CheckProximity();

        if (!HasSomethingToShow())
        {
            if (pendingHide == null) HideInfo();
            return;
        }

        // The tool can be set down while still near something -- hide
        // immediately rather than leaving stale info on screen.
        if (!MagnifyingGlassTool.IsEquipped)
        {
            HideInfo();
            return;
        }

        if (infoCanvas != null && !infoCanvas.gameObject.activeSelf)
            ShowInfo();

        RefreshText();
        PositionCanvas();
        HandleAdvanceInput();
    }

    void CheckProximity()
    {
        int count = Physics.OverlapSphereNonAlloc(lensPoint.position, detectionRadius, OverlapBuffer, detectionMask, QueryTriggerInteraction.Collide);

        Informational nearestInfo = null;
        Farmland nearestFarmland = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = OverlapBuffer[i];
            if (col == null) continue;

            // Skip the glass's own colliders (Handle, the lens mesh itself)
            // -- it has its own Informational for the rack nameplate, which
            // would otherwise always win as "nearest" since the lens point
            // sits right on the tool's own mesh.
            if (col.transform.IsChildOf(transform)) continue;

            Informational info = col.GetComponentInParent<Informational>();
            Farmland farmland = col.GetComponentInParent<Farmland>();
            if (info == null && farmland == null) continue;

            float dist = (col.transform.position - lensPoint.position).sqrMagnitude;
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestInfo = info;
                nearestFarmland = farmland;
            }
        }

        bool foundSomething = nearestInfo != null || nearestFarmland != null;
        bool sameAsBefore = foundSomething &&
            ((nearestInfo != null && nearestInfo == currentInfo) || (nearestFarmland != null && nearestFarmland == currentFarmland));

        if (foundSomething)
        {
            if (pendingHide != null)
            {
                StopCoroutine(pendingHide);
                pendingHide = null;
            }

            if (!sameAsBefore) factIndex = -1;

            currentInfo = nearestInfo;
            currentFarmland = nearestFarmland;
        }
        else if (HasSomethingToShow() && pendingHide == null)
        {
            // Nothing in range right now, but something was showing --
            // start the grace-period countdown rather than clearing state
            // immediately (a moving animal drifting just out of radius for
            // a single frame shouldn't reset anything).
            pendingHide = StartCoroutine(DelayedClear());
        }
    }

    IEnumerator DelayedClear()
    {
        yield return new WaitForSeconds(loseContactGrace);
        pendingHide = null;
        HideInfo();
        currentInfo = null;
        currentFarmland = null;
    }

    void OnDisable()
    {
        if (pendingHide != null)
        {
            StopCoroutine(pendingHide);
            pendingHide = null;
        }

        HideInfo();
        currentInfo = null;
        currentFarmland = null;
    }

    bool HasSomethingToShow()
    {
        if (currentInfo != null && currentInfo.infoData != null) return true;
        if (currentFarmland != null && currentFarmland.cropData != null) return true;
        return false;
    }

    string CurrentDisplayName()
    {
        if (currentInfo != null && currentInfo.infoData != null) return currentInfo.infoData.displayName;
        if (currentFarmland != null && currentFarmland.cropData != null) return currentFarmland.cropData.cropName;
        return "";
    }

    string CurrentSummary()
    {
        if (currentInfo != null && currentInfo.infoData != null) return currentInfo.infoData.summary;
        if (currentFarmland != null && currentFarmland.cropData != null) return currentFarmland.cropData.summary;
        return "";
    }

    List<string> CurrentFacts()
    {
        if (currentInfo != null && currentInfo.infoData != null) return currentInfo.infoData.facts;
        if (currentFarmland != null && currentFarmland.cropData != null) return currentFarmland.cropData.facts;
        return null;
    }

    Transform CurrentTargetTransform()
    {
        if (currentInfo != null) return currentInfo.transform;
        if (currentFarmland != null) return currentFarmland.transform;
        return null;
    }

    void ShowInfo()
    {
        if (infoCanvas != null) infoCanvas.gameObject.SetActive(true);
        RefreshText();
        PositionCanvas();
    }

    void HideInfo()
    {
        if (infoCanvas != null) infoCanvas.gameObject.SetActive(false);
    }

    void RefreshText()
    {
        if (!HasSomethingToShow()) return;

        if (titleText != null)
            titleText.text = CurrentDisplayName();

        if (bodyText != null)
            bodyText.text = CurrentBodyLine();
    }

    string CurrentBodyLine()
    {
        List<string> facts = CurrentFacts();

        if (factIndex < 0 || facts == null || facts.Count == 0)
            return CurrentSummary();

        int clamped = Mathf.Clamp(factIndex, 0, facts.Count - 1);
        return facts[clamped];
    }

    // Advances summary -> fact 1 -> fact 2 -> ... -> back to summary.
    void HandleAdvanceInput()
    {
        if (!rightHandDevice.isValid)
        {
            rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            return;
        }

        if (rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed))
        {
            bool justPressed = pressed && !prevAdvancePressed;
            prevAdvancePressed = pressed;

            if (justPressed) Advance();
        }
    }

    void Advance()
    {
        List<string> facts = CurrentFacts();
        if (facts == null || facts.Count == 0) return;

        factIndex++;
        if (factIndex >= facts.Count) factIndex = -1;

        RefreshText();
    }

    void PositionCanvas()
    {
        if (infoCanvas == null) return;

        Transform target = CurrentTargetTransform();
        if (target == null) return;

        infoCanvas.transform.position = target.position + promptOffset;

        if (playerCamera != null)
        {
            // This canvas's readable front faces its local -Z, so aiming
            // local +Z away from the player (dir already points away from
            // them) puts the front toward the player.
            Vector3 dir = infoCanvas.transform.position - playerCamera.position;
            dir.y = 0f;
            if (dir != Vector3.zero)
                infoCanvas.transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
