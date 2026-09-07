using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

// Rides the ray interactor's hover events (already computed every frame for
// grabbing tools etc., so this adds nothing extra) instead of each tile
// polling its own distance/angle to the camera every frame like the earlier
// gaze-based version did. Every tile is always hoverable -- two things ride
// on that hover, independently:
//   1. The detailed status window + tile-edge highlight -- only while "crop
//      status mode" is active (toggled by the X button in MenuManager), so
//      it doesn't clutter the view all the time.
//   2. A thin highlight around the crop itself, shown whenever the hovered
//      tile's crop is ready to harvest -- ALWAYS active, no mode toggle
//      needed. Pressing A while it's showing harvests that exact crop (see
//      FarmManager.HarvestTile), replacing the old "press A near any ready
//      crop" proximity check with an aimed one.
//
// Unity setup (per tile, or batch this across all Farmland tiles):
//   1. Requires a Collider sized to the tile (most tiles already have one
//      for the Hoe/Seed/Water interactions) and an XRSimpleInteractable
//      pointed at that same collider.
//   2. Drag the shared status Canvas into Status Canvas, its TMP text into
//      Status Text, the shared tile-edge highlight into Tile Highlight, the
//      shared crop highlight into Harvest Highlight, and the player camera
//      into Player Camera.
[RequireComponent(typeof(Farmland))]
[RequireComponent(typeof(XRSimpleInteractable))]
public class FarmlandHoverStatus : MonoBehaviour
{
    [Tooltip("The shared dialogue-box-style status window -- same object every tile points at")]
    public Canvas statusCanvas;

    public TextMeshProUGUI statusText;

    [Tooltip("A shared flat quad that gets moved under whichever tile is hovered, as a selection highlight -- only shown in crop status mode")]
    public Transform tileHighlight;

    [Tooltip("A shared thin highlight that outlines whichever tile's crop is both hovered AND ready " +
             "to harvest -- shown regardless of crop status mode, since aiming-to-harvest is always available")]
    public Transform harvestHighlight;

    [Tooltip("Drag CenterEyeAnchor or Main Camera here -- used only to face the window toward " +
             "the player, not to place it. Defaults to Camera.main if left empty")]
    public Transform playerCamera;

    [Header("Placement (above the tile)")]
    public Vector3 promptOffset = new Vector3(0f, 1.1f, 0f);

    [Tooltip("Height the highlight quad sits above the tile's own pivot -- tiles are a solid " +
             "0.5m cube with the pivot at the base, not a thin ground plane, so this needs to " +
             "clear the whole cube or the highlight renders occluded inside it")]
    public float highlightHeight = 0.51f;

    [Tooltip("Height the harvest highlight sits at -- same reasoning as Highlight Height")]
    public float harvestHighlightHeight = 0.51f;

    private Farmland farmland;
    private XRSimpleInteractable interactable;

    // Whichever tile is currently showing the window -- so a stray
    // hoverExited from a tile that already lost ownership (e.g. the ray
    // swept straight from one tile to another) can't hide what the new
    // tile just showed.
    private static FarmlandHoverStatus currentOwner;

    // Every live instance, so toggling crop status mode can enable/disable
    // all of them in one batch instead of each tile polling a flag every
    // frame.
    private static readonly List<FarmlandHoverStatus> allInstances = new List<FarmlandHoverStatus>();
    private static bool modeActive;

    // Toggled by MenuManager's X button. Every tile stays hoverable either
    // way (harvest-aiming needs that always on) -- this only shows/hides the
    // detailed status window + tile-edge highlight for whichever tile is
    // currently hovered.
    public static bool ModeActive
    {
        get => modeActive;
        set
        {
            modeActive = value;

            // If a tile is already being hovered when the mode toggles, show
            // or hide its status right away -- otherwise it only updated on
            // the NEXT hoverEntered event, so toggling status mode while
            // already looking at a tile (the common case: walk up, aim, then
            // remember to press X) left the window never appearing until the
            // ray was moved off and back.
            if (currentOwner != null)
            {
                if (value) currentOwner.ShowStatus();
                else currentOwner.HideStatus();
            }
        }
    }

    // Shared A-button edge state for harvest input -- static rather than
    // per-instance, since "currently hovered" moves between instances and a
    // per-instance prevPressed would misread a still-held button as a fresh
    // press the moment a different tile becomes the owner.
    private static InputDevice rightHandDevice;
    private static bool prevHarvestAPressed;

    void Awake()
    {
        farmland = GetComponent<Farmland>();
        interactable = GetComponent<XRSimpleInteractable>();

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;
    }

    void OnEnable()
    {
        allInstances.Add(this);
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);

        // Always hoverable -- harvest-aiming works without toggling crop
        // status mode. See ModeActive.
        interactable.enabled = true;
    }

    void OnDisable()
    {
        allInstances.Remove(this);
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);

        if (currentOwner == this)
        {
            currentOwner = null;
            HideStatus();
            if (harvestHighlight != null) harvestHighlight.gameObject.SetActive(false);
        }
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        currentOwner = this;

        if (modeActive) ShowStatus();
        else RefreshAndPosition(); // no-op while not in status mode, but harmless
    }

    void ShowStatus()
    {
        if (statusCanvas != null) statusCanvas.gameObject.SetActive(true);
        if (tileHighlight != null) tileHighlight.gameObject.SetActive(true);
        RefreshAndPosition();
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (currentOwner != this) return;
        currentOwner = null;
        HideStatus();
        if (harvestHighlight != null) harvestHighlight.gameObject.SetActive(false);
    }

    void HideStatus()
    {
        if (statusCanvas != null) statusCanvas.gameObject.SetActive(false);
        if (tileHighlight != null) tileHighlight.gameObject.SetActive(false);
    }

    // Only the currently-hovered tile (at most one of the 168) does any
    // per-frame work here -- keeps the text live so harvesting, watering,
    // etc. while still hovering doesn't leave stale text on screen, and
    // keeps the window/highlight tracking the tile/player if either moves.
    void Update()
    {
        if (currentOwner != this) return;

        RefreshAndPosition();
        UpdateHarvestHighlight();
        HandleHarvestInput();
    }

    void RefreshAndPosition()
    {
        if (modeActive)
        {
            if (statusText != null)
                statusText.text = BuildStatusText();

            PositionCanvas();
            PositionHighlight();
        }
    }

    bool IsReadyToHarvestNow()
    {
        return farmland.cropData != null && farmland.cropData.IsReadyToHarvest(farmland.daysWatered);
    }

    void UpdateHarvestHighlight()
    {
        if (harvestHighlight == null) return;

        bool ready = IsReadyToHarvestNow();
        harvestHighlight.gameObject.SetActive(ready);

        if (ready)
            harvestHighlight.position = transform.position + Vector3.up * harvestHighlightHeight;
    }

    // Aim-only harvest: A only does anything while THIS tile is both hovered
    // and ready -- replaces the old "press A near any ready crop" proximity
    // check, see FarmManager.HarvestTile.
    void HandleHarvestInput()
    {
        if (!rightHandDevice.isValid)
            rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (!rightHandDevice.isValid) return;

        if (rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool aPressed))
        {
            bool justPressed = aPressed && !prevHarvestAPressed;
            prevHarvestAPressed = aPressed;

            if (justPressed && IsReadyToHarvestNow() && FarmManager.Instance != null)
                FarmManager.Instance.HarvestTile(farmland);
        }
    }

    void PositionCanvas()
    {
        if (statusCanvas == null) return;

        statusCanvas.transform.position = transform.position + promptOffset;

        if (playerCamera != null)
        {
            Vector3 dir = statusCanvas.transform.position - playerCamera.position;
            dir.y = 0f;
            if (dir != Vector3.zero)
                statusCanvas.transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    void PositionHighlight()
    {
        if (tileHighlight == null) return;
        tileHighlight.position = transform.position + Vector3.up * highlightHeight;
    }

    string BuildStatusText()
    {
        string soil = "Soil: " + Mathf.RoundToInt(farmland.soilHealth) + "%";
        CropData crop = farmland.cropData;

        if (crop == null)
            return farmland.state + "\n" + soil;

        string cropLine;
        if (crop.IsReadyToHarvest(farmland.daysWatered))
            cropLine = crop.cropName + "\nReady to harvest!\nSells for " + crop.sellPrice;
        else
        {
            int daysLeft = crop.daysToMature - farmland.daysWatered;
            string dayWord = daysLeft == 1 ? "day" : "days";
            cropLine = crop.cropName + "\n" + daysLeft + " " + dayWord + " until harvest\nSells for " + crop.sellPrice;
        }

        return cropLine + "\n" + soil;
    }
}
