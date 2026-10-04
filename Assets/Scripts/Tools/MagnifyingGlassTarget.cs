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

    [Header("Tabs")]
    [Tooltip("Row of tab names under the title -- the current tab is highlighted. Not read aloud.")]
    public TextMeshProUGUI tabBarText;

    [Tooltip("\"B  Next\" hint button in the footer -- pressing B or clicking it shows the next tab")]
    public UnityEngine.UI.Button nextPageButton;

    public Color activeTabColor = new Color(0.30f, 0.50f, 0.20f);
    public Color inactiveTabColor = new Color(0.62f, 0.55f, 0.45f);

    [Tooltip("Drag CenterEyeAnchor or Main Camera here -- only used to face the window toward the player. Defaults to Camera.main if left empty")]
    public Transform playerCamera;

    [Header("Placement (beside the detected object)")]
    [Tooltip("Gap in meters between the object's edge (as seen from the player) and the near edge of the info window")]
    public float sideGap = 0.06f;

    [Tooltip("How far in meters the window is pulled from the object toward the player, so it reads as floating in front rather than level with it")]
    public float towardPlayer = 0.1f;

    [Tooltip("The window's bottom edge never goes lower than this many meters above the ground beneath it -- " +
             "low targets like crops (whose tiles sit partly below ground) would otherwise put half the window underground")]
    public float minHeightAboveGround = 0.5f;

    [Tooltip("How long the info panel stays up after losing proximity before actually hiding -- long enough to read it or press Listen after the animal wanders off, and also bridges brief separations without flicker. If a read-aloud is still playing when this runs out, it stays up until that finishes")]
    public float loseContactGrace = 5f;

    [Tooltip("Once something has been identified, its window stays up (no countdown) for as long as the " +
             "player's head is within this many meters of it -- the lens only has to touch it once. The " +
             "Lose Contact Grace countdown only starts when the player walks away, or a moving target " +
             "(an animal) wanders off")]
    public float keepVisibleDistance = 2f;

    private static readonly Collider[] OverlapBuffer = new Collider[8];
    private static readonly List<Collider> BoundsColliders = new List<Collider>();
    private static readonly List<Renderer> BoundsRenderers = new List<Renderer>();

    // Whatever the lens is currently (or was most recently, within the
    // grace period) near. Only one at a time -- if several things are in
    // range simultaneously, the closest wins.
    private Informational currentInfo;
    private Farmland currentFarmland;
    private int pageIndex = 0;
    private Coroutine pendingHide;

    private InputDevice rightHandDevice;
    private bool prevAdvancePressed;
    private bool prevReadPressed;

    // The window's Listen button -- A triggers the same thing, since a ray
    // click isn't practical while the glass is in hand.
    private ReadAloudButton readAloudButton;

    void Awake()
    {
        if (lensPoint == null) lensPoint = transform;
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (infoCanvas != null)
            readAloudButton = infoCanvas.GetComponentInChildren<ReadAloudButton>(true);

        TextToSpeech.Prewarm();
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

        // Once the lens has lost contact the window stays exactly where it
        // was for the grace period, rather than chasing a wandering animal
        // around -- it's only readable if it holds still.
        if (pendingHide == null) PositionCanvas();

        HandleButtonInput();
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

            if (!sameAsBefore) pageIndex = 0;

            currentInfo = nearestInfo;
            currentFarmland = nearestFarmland;
        }
        else if (HasSomethingToShow())
        {
            if (PlayerStillNearTarget())
            {
                // Lens moved off it, but the player is still right there
                // (e.g. reading the window beside a plant) -- keep showing,
                // and cancel any countdown if they've just come back
                if (pendingHide != null)
                {
                    StopCoroutine(pendingHide);
                    pendingHide = null;
                }
            }
            else if (pendingHide == null)
            {
                // Player walked away, or the animal wandered off -- start
                // the grace-period countdown rather than clearing state
                // immediately.
                pendingHide = StartCoroutine(DelayedClear());
            }
        }
    }

    bool PlayerStillNearTarget()
    {
        Transform target = CurrentTargetTransform();
        if (target == null || playerCamera == null) return false;

        Bounds bounds = TargetBounds(target);
        Vector3 head = playerCamera.position;
        return (bounds.ClosestPoint(head) - head).sqrMagnitude <= keepVisibleDistance * keepVisibleDistance;
    }

    IEnumerator DelayedClear()
    {
        yield return new WaitForSeconds(loseContactGrace);

        // Never cut a read-aloud off mid-sentence -- if it's still going
        // once the grace period is up, the window stays (still frozen in
        // place) until it finishes or the player stops it with A.
        while (readAloudButton != null && readAloudButton.IsReading)
            yield return null;

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

    // One page of the window: which tab it belongs to, and its text. Several
    // pages can share a tab (each fun fact is its own page under "Facts").
    struct Page
    {
        public string tab;
        public string text;
        public Page(string tab, string text) { this.tab = tab; this.text = text; }
    }

    // Reused every frame so building the pages doesn't allocate
    private static readonly List<Page> Pages = new List<Page>();
    private static readonly List<string> TabNames = new List<string>();

    const string AboutTab = "About";
    const string FactsTab = "Facts";

    // Everything B steps through, in order. Crops get a live Growth tab plus
    // Farm (real world) / Game comparison tabs; animals and tools just have About
    // and Facts. Rebuilt every frame, so the Growth tab stays live while the
    // window is open (watering, a new day, picking fruit).
    List<Page> CurrentPages()
    {
        Pages.Clear();

        if (currentInfo != null && currentInfo.infoData != null)
        {
            Pages.Add(new Page(AboutTab, currentInfo.infoData.summary));
            AddFacts(currentInfo.infoData.facts);
        }
        else if (currentFarmland != null && currentFarmland.cropData != null)
        {
            CropData crop = currentFarmland.cropData;
            Pages.Add(new Page(AboutTab, crop.summary));

            string status = currentFarmland.GrowthStatusText();
            if (status.Length > 0) Pages.Add(new Page("Growth", status));

            string realFarm = crop.RealFarmPage();
            if (realFarm.Length > 0) Pages.Add(new Page("Farm", realFarm));

            string game = crop.GamePage();
            if (game.Length > 0) Pages.Add(new Page("Game", game));

            AddFacts(crop.facts);
        }

        return Pages;
    }

    static void AddFacts(List<string> facts)
    {
        if (facts == null) return;
        foreach (string fact in facts) Pages.Add(new Page(FactsTab, fact));
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

        List<Page> pages = CurrentPages();
        if (pages.Count == 0) return;
        pageIndex = Mathf.Clamp(pageIndex, 0, pages.Count - 1);

        if (titleText != null)
            titleText.text = CurrentDisplayName();

        if (bodyText != null)
            bodyText.text = pages[pageIndex].text;

        RefreshTabBar(pages);

        // Nothing to page through -> no point hinting at B
        if (nextPageButton != null)
            nextPageButton.gameObject.SetActive(pages.Count > 1);
    }

    string CurrentBodyLine()
    {
        List<Page> pages = CurrentPages();
        if (pages.Count == 0) return "";
        return pages[Mathf.Clamp(pageIndex, 0, pages.Count - 1)].text;
    }

    // "About  Growth  Farm  Game  Facts 2/4" with the current tab
    // highlighted, so the player can see what B will cycle through
    void RefreshTabBar(List<Page> pages)
    {
        if (tabBarText == null) return;

        TabNames.Clear();
        foreach (Page page in pages)
            if (!TabNames.Contains(page.tab)) TabNames.Add(page.tab);

        string current = pages[pageIndex].tab;
        string active = ColorUtility.ToHtmlStringRGB(activeTabColor);
        string inactive = ColorUtility.ToHtmlStringRGB(inactiveTabColor);

        var bar = new System.Text.StringBuilder();
        foreach (string tab in TabNames)
        {
            if (bar.Length > 0) bar.Append("  ");

            string label = tab;
            if (tab == current && tab == FactsTab)
                label += " " + FactNumber(pages) + "/" + FactCount(pages);

            if (tab == current) bar.Append("<b><u><color=#").Append(active).Append(">").Append(label).Append("</color></u></b>");
            else bar.Append("<color=#").Append(inactive).Append(">").Append(label).Append("</color>");
        }

        tabBarText.text = bar.ToString();
    }

    int FactCount(List<Page> pages)
    {
        int n = 0;
        foreach (Page page in pages) if (page.tab == FactsTab) n++;
        return n;
    }

    int FactNumber(List<Page> pages)
    {
        int n = 0;
        for (int i = 0; i <= pageIndex; i++) if (pages[i].tab == FactsTab) n++;
        return n;
    }

    // Right-hand A reads the window aloud (again to stop); right-hand B goes
    // to the next page/tab, wrapping back to About after the last fact.
    void HandleButtonInput()
    {
        if (!rightHandDevice.isValid)
        {
            rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            return;
        }

        if (rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool readPressed))
        {
            bool justPressed = readPressed && !prevReadPressed;
            prevReadPressed = readPressed;

            if (justPressed) ReadAloud();
        }

        if (rightHandDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out bool advancePressed))
        {
            bool justPressed = advancePressed && !prevAdvancePressed;
            prevAdvancePressed = advancePressed;

            if (justPressed) NextPage();
        }
    }

    void ReadAloud()
    {
        if (readAloudButton != null) readAloudButton.Toggle();
        else TextToSpeech.Speak(CurrentDisplayName() + "\n" + CurrentBodyLine());
    }

    // Also wired to the "B  Next" footer button, so a ray click works too
    public void NextPage()
    {
        List<Page> pages = CurrentPages();
        if (pages.Count <= 1) return;

        pageIndex = (pageIndex + 1) % pages.Count;
        RefreshText();
    }

    void PositionCanvas()
    {
        if (infoCanvas == null) return;

        Transform target = CurrentTargetTransform();
        if (target == null || playerCamera == null) return;

        // Sits to the player's LEFT of the object, just past its edge -- the
        // glass is held in the right hand, so a window on the right ended up
        // behind the hand/glass and was hard to read.
        Bounds bounds = TargetBounds(target);

        Vector3 forward = bounds.center - playerCamera.position;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = playerCamera.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        // Half-width of the bounds along the player's right-hand direction.
        float halfWidth = Mathf.Abs(bounds.extents.x * right.x) + Mathf.Abs(bounds.extents.z * right.z);

        // Where the window's near (right-hand) edge should sit.
        Vector3 nearEdge = bounds.center - right * (halfWidth + sideGap) - forward * towardPlayer;

        // This canvas's readable front faces its local -Z, so aiming
        // local +Z away from the player (dir already points away from
        // them) puts the front toward the player.
        Vector3 dir = nearEdge - playerCamera.position;
        dir.y = 0f;
        if (dir != Vector3.zero)
            infoCanvas.transform.rotation = Quaternion.LookRotation(dir);

        // The Panel is pivoted on its left edge and extends to the canvas's
        // local +X (the player's right), so shift the canvas left by the
        // panel's width to make its RIGHT edge land on nearEdge -- the whole
        // window then sits clear of the object instead of overlapping it.
        infoCanvas.transform.position = nearEdge - infoCanvas.transform.right * PanelWorldWidth();

        KeepAboveGround();
    }

    static readonly Vector3[] PanelCorners = new Vector3[4];

    void KeepAboveGround()
    {
        RectTransform panel = titleText != null ? titleText.transform.parent as RectTransform : null;
        if (panel == null) return;

        panel.GetWorldCorners(PanelCorners);
        float bottom = Mathf.Min(PanelCorners[0].y, PanelCorners[3].y);

        if (!TryGetGroundHeight(infoCanvas.transform.position, out float groundY)) return;

        float lift = groundY + minHeightAboveGround - bottom;
        if (lift > 0f)
            infoCanvas.transform.position += Vector3.up * lift;
    }

    static readonly RaycastHit[] GroundHits = new RaycastHit[16];

    // Lowest surface straight below -- the lowest rather than the first hit,
    // so a tree canopy or fence rail overhead doesn't count as "ground".
    static bool TryGetGroundHeight(Vector3 around, out float groundY)
    {
        groundY = 0f;
        int count = Physics.RaycastNonAlloc(around + Vector3.up * 5f, Vector3.down, GroundHits, 20f, ~0, QueryTriggerInteraction.Ignore);
        if (count == 0) return false;

        groundY = float.MaxValue;
        for (int i = 0; i < count; i++)
            groundY = Mathf.Min(groundY, GroundHits[i].point.y);
        return true;
    }

    float PanelWorldWidth()
    {
        RectTransform panel = titleText != null ? titleText.transform.parent as RectTransform : null;
        if (panel == null) return 0f;
        return panel.rect.width * panel.lossyScale.x;
    }

    // Colliders rather than renderers where possible -- they're what the
    // lens actually detects, and some props carry oversized renderers (e.g.
    // particle effects) that would push the window far off to the side.
    static Bounds TargetBounds(Transform target)
    {
        target.GetComponentsInChildren(BoundsColliders);
        bool found = false;
        Bounds bounds = new Bounds(target.position, Vector3.zero);

        foreach (Collider col in BoundsColliders)
        {
            // Triggers are skipped (animals can carry large detection
            // zones) -- except a crop's InfoVolume, which is exactly the
            // plant's size and the only thing describing a tall crop's shape
            if (!col.enabled || (col.isTrigger && col.name != Farmland.InfoVolumeName)) continue;
            if (!found) { bounds = col.bounds; found = true; }
            else bounds.Encapsulate(col.bounds);
        }

        if (found) return bounds;

        target.GetComponentsInChildren(BoundsRenderers);
        foreach (Renderer r in BoundsRenderers)
        {
            if (!r.enabled) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }

        return bounds;
    }
}
