using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    // ---------------------------------------------
    //  INPUT
    // ---------------------------------------------
    [Header("Input - Left Controller")]
    [Tooltip("Left Y button -> opens/closes Inventory. " +
             "Bind to: XRI LeftHand Interaction / secondaryButton")]
    public InputActionProperty inventoryButtonAction;

    [Tooltip("Left Menu/Start button -> opens/closes Pause. " +
             "Bind to: XRI LeftHand Interaction / menu")]
    public InputActionProperty pauseButtonAction;

    [Tooltip("Left X button -> toggles crop status mode (ray-hover a farmland tile to see its " +
             "status). " +
             "Bind to: XRI LeftHand Interaction / primaryButton")]
    public InputActionProperty toolInventoryButtonAction;

    [Tooltip("Enable to use XR direct polling instead of Input Action Asset")]
    public bool useDirectPolling = false;


    [Header("Panels")]
    [Tooltip("Canvas / panel shown when Inventory is open")]
    public GameObject inventoryPanel;

    [Tooltip("Harvest/Tool/Seeds tab buttons -- a sibling of InventoryPanel under MenuCanvas, not a " +
             "child of it, so it needs its own explicit show/hide alongside InventoryPanel rather " +
             "than following it automatically")]
    public GameObject tabBar;

    [Tooltip("Canvas / panel shown when Pause is open")]
    public GameObject pausePanel;

    [Tooltip("Settings panel - opened from inside Pause")]
    public GameObject settingsPanel;

    [Tooltip("Scrollable rack of tool interactables - shown when the Tool tab is selected inside " +
             "the Y-button backpack")]
    public GameObject toolInventoryPanel;

    public enum InventoryTab { Harvest, Tool, Seeds }

    [Header("Inventory Tabs (Y-button backpack)")]
    [Tooltip("The harvest grid (crops, eggs, ...) -- shown only while the Harvest tab is selected")]
    [FormerlySerializedAs("cropsTabContent")]
    public GameObject harvestTabContent;

    [Tooltip("InventoryPanel's own solid background -- covers a large area (the whole backpack " +
             "canvas), so it stays visible only on the Harvest tab. Otherwise it sits between the " +
             "player and whatever's on the Tool/Seeds sub-panel below and visually blocks it, " +
             "even though that sub-panel isn't actually behind it.")]
    public Image inventoryBackgroundImage;

    [FormerlySerializedAs("cropsTabButton")]
    public Button harvestTabButton;
    public Button toolTabButton;
    public Button seedsTabButton;

    [Tooltip("Tint applied to whichever tab button is currently selected, so it's visually obvious " +
             "which one you're on -- the other two stay at their normal Image color")]
    public Color selectedTabColor = new Color(1f, 0.85f, 0.3f, 1f);
    private Color harvestTabDefaultColor, toolTabDefaultColor, seedsTabDefaultColor;
    private bool tabDefaultColorsCaptured;

    // Also doubles as "which tab to reopen to" -- OpenInventory() reuses
    // whatever this already is instead of always resetting to Harvest, so the
    // backpack remembers the last tab you were on.
    private InventoryTab currentTab = InventoryTab.Harvest;

    [Tooltip("Locomotion providers (ActionBasedContinuousMoveProvider, turn providers, etc.) " +
             "disabled while the harvest inventory or tool inventory is open -- same reasoning as " +
             "ShopInteractionController: don't let the player wander (or, for the tool rack, " +
             "turn) while browsing a menu.")]
    public Behaviour[] menuLocomotionProvidersToDisable;


    [Header("Menu Placement")]
    public float menuDistance = 1.5f;
    public float menuHeightOffset = 0.1f;

    [Tooltip("How far below the backpack's own panel the Tool rack / Seed picker sub-panel sits " +
             "when its tab is selected. Tool and Seeds share this one slot (never shown at the " +
             "same time), positioned relative to the backpack panel itself rather than " +
             "independently off the camera -- otherwise both land at the exact same spot in front " +
             "of the player and visually overlap it.")]
    public float subPanelVerticalOffset = 0.58f;

    [Tooltip("Extra upward nudge applied only to the Seeds tab's position, on top of " +
             "subPanelVerticalOffset -- compensates for the seed picker canvas being centered on " +
             "the shared anchor point rather than offset by a fixed label height the way the Tool " +
             "rack's cue is, so both tabs' content ends up starting the same distance under the tab bar.")]
    public float seedsTabExtraHeight = 0.1f;

    [Tooltip("Where the tool inventory rack sits while closed. It is never " +
             "SetActive(false) -- disabling its sockets would drop whatever " +
             "tool is currently seated in them -- so it's parked out of reach instead.")]
    public Vector3 toolInventoryHiddenPosition = new Vector3(0f, -500f, 0f);

    [Header("Camera")]
    [Tooltip("Assign CenterEyeAnchor or Main Camera")]
    public Transform vrCamera;


    private bool inventoryOpen = false;
    private bool pauseOpen = false;

    private bool bWasPressed = false;
    private bool menuWasPressed = false;
    private bool xWasPressed = false;

    // Read by ToolRackDrag to know whether it should be listening to the
    // scroll stick right now -- true only while the backpack is open AND
    // the Tool tab is the one currently selected.
    public bool ToolInventoryOpen => inventoryOpen && currentTab == InventoryTab.Tool;

    // Read by ReturnToRackOnDrop -- unlike ToolInventoryOpen above, this is
    // true on ANY tab, since locomotion freezes for the whole backpack (see
    // OpenInventory/CloseInventory) regardless of which tab happens to be
    // selected. A tool dropped while checking the Seeds tab, say, still needs
    // to come back -- the player can't walk over for it either way.
    public bool InventoryOpen => inventoryOpen;


    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // The rack must stay active so its sockets never unregister and drop
        // a held tool -- it's hidden by position, not by SetActive.
        if (toolInventoryPanel != null)
            toolInventoryPanel.SetActive(true);

        if (vrCamera == null && Camera.main != null)
            vrCamera = Camera.main.transform;

        // HideAll() (which SetActive(false)s inventoryPanel/pausePanel/settingsPanel)
        // deliberately waits for Start(), not here -- InventoryManager lives on
        // inventoryPanel itself, and disabling it from another script's Awake(),
        // before InventoryManager's own Awake() has necessarily run yet, can
        // permanently skip that Awake() (Unity checks active state right before
        // invoking each queued Awake) and leave InventoryManager.Instance null
        // for the rest of the session -- silently breaking anything that reads
        // it (e.g. selling at the shop) until the player happens to open the
        // backpack at least once. Start() runs only after every object's Awake
        // has already completed, so it's safe there.
    }

    void Start()
    {
        HideAll();

        if (harvestTabButton != null) harvestTabButton.onClick.AddListener(() => SetInventoryTab(InventoryTab.Harvest));
        if (toolTabButton != null) toolTabButton.onClick.AddListener(() => SetInventoryTab(InventoryTab.Tool));
        if (seedsTabButton != null) seedsTabButton.onClick.AddListener(() => SetInventoryTab(InventoryTab.Seeds));
    }

    void OnEnable()
    {
        if (!useDirectPolling)
        {
            EnableAction(inventoryButtonAction, OnInventoryButtonPressed);
            EnableAction(pauseButtonAction, OnPauseButtonPressed);
            EnableAction(toolInventoryButtonAction, OnToolInventoryButtonPressed);
        }
    }

    void OnDisable()
    {
        if (!useDirectPolling)
        {
            DisableAction(inventoryButtonAction, OnInventoryButtonPressed);
            DisableAction(pauseButtonAction, OnPauseButtonPressed);
            DisableAction(toolInventoryButtonAction, OnToolInventoryButtonPressed);
        }
    }

    void Update()
    {
        if (!useDirectPolling) return;

        // Left hand Y button -> Inventory (moved off right-hand B, which
        // ShopInteractionController uses to exit the shop -- the two were
        // firing on the same press and fighting each other)
        bool bNow = GetButton(UnityEngine.XR.XRNode.LeftHand,
                              UnityEngine.XR.CommonUsages.secondaryButton);
        if (bNow && !bWasPressed) ToggleInventory();
        bWasPressed = bNow;

        // Left hand Menu button -> Pause
        bool menuNow = GetButton(UnityEngine.XR.XRNode.LeftHand,
                                 UnityEngine.XR.CommonUsages.menuButton);
        if (menuNow && !menuWasPressed) TogglePause();
        menuWasPressed = menuNow;

        // Left hand X button -> Crop Status mode
        bool xNow = GetButton(UnityEngine.XR.XRNode.LeftHand,
                              UnityEngine.XR.CommonUsages.primaryButton);
        if (xNow && !xWasPressed) ToggleCropStatusMode();
        xWasPressed = xNow;
    }

    void OnInventoryButtonPressed(InputAction.CallbackContext ctx) => ToggleInventory();

    void ToggleInventory()
    {
        bool wasOpen = inventoryOpen;
        CloseAllMenus();

        if (!wasOpen) OpenInventory();
    }

    void OpenInventory()
    {
        inventoryOpen = true;

        // Positions the whole MenuCanvas (InventoryPanel's parent), not just
        // InventoryPanel itself -- TabBar and the HUD's date/time/money text
        // are siblings of InventoryPanel under the same canvas, not children
        // of it, so moving InventoryPanel alone left them stranded at their
        // last edit-time position instead of following the panel to face the
        // player, which is what made the tab bar appear to float somewhere
        // else entirely whenever the backpack opened from a different spot.
        PositionPanel(inventoryPanel.transform.parent.gameObject);
        SetActive(inventoryPanel, true);
        SetActive(tabBar, true);
        SetInventoryTab(currentTab); // reopen to whichever tab was last selected
        DisableMenuLocomotion();
        PauseGameClock();
        Debug.Log("[MenuManager] Inventory opened.");
    }

    void CloseInventory()
    {
        inventoryOpen = false;
        SetActive(inventoryPanel, false);
        SetActive(tabBar, false);

        // Tab content that lives outside the InventoryPanel itself (the tool
        // rack's 3D sockets, the seed picker's own canvas) needs to be told
        // to hide separately -- SetActive(inventoryPanel, false) above only
        // covers the Harvest tab's own grid.
        if (toolInventoryPanel != null)
            toolInventoryPanel.transform.position = toolInventoryHiddenPosition;
        SeedPickerUI.Instance?.Close();

        RestoreMenuLocomotion();
        ResumeGameClock();
        Debug.Log("[MenuManager] Inventory closed.");
    }

    // Switches which tab's content is visible inside the already-open backpack.
    // Tool and Seeds live in their own world-positioned objects (the rack's
    // physical sockets, the seed-picker canvas) rather than inside
    // InventoryPanel's own RectTransform, so "showing" them means moving/
    // activating those objects rather than a simple SetActive on a child --
    // and both share one sub-panel slot below the backpack panel itself
    // (never both selected at once) so they never land on top of each other.
    public void SetInventoryTab(InventoryTab tab)
    {
        currentTab = tab;
        UpdateTabHighlight(tab);

        SetActive(harvestTabContent, tab == InventoryTab.Harvest);
        if (inventoryBackgroundImage != null)
            inventoryBackgroundImage.enabled = (tab == InventoryTab.Harvest);

        if (tab == InventoryTab.Tool)
        {
            PositionSubPanel(toolInventoryPanel.transform);

            // Tidy the rack every time it's actually opened -- whatever tools
            // aren't currently held snap back to their own sockets, regardless
            // of where/when they were set down. See ReturnToRackOnDrop.
            ReturnToRackOnDrop.ReturnAllNow();
        }
        else if (toolInventoryPanel != null)
        {
            toolInventoryPanel.transform.position = toolInventoryHiddenPosition;
        }

        if (tab == InventoryTab.Seeds && SeedPickerUI.Instance != null)
        {
            // SubPanelPosition() is the same shared anchor point the Tool
            // rack uses, but the seed picker's own canvas is centered on it
            // (rather than offset by a fixed cue height the way the rack's
            // label is), so its top edge lands lower than the rack's --
            // this nudges it up to match the same gap under the tab bar.
            Vector3 pos = SubPanelPosition() + Vector3.up * seedsTabExtraHeight;
            SeedPickerUI.Instance.OpenViewOnly(pos, inventoryPanel.transform.rotation);
        }
        else
        {
            SeedPickerUI.Instance?.Close();
        }
    }

    Vector3 SubPanelPosition()
    {
        return inventoryPanel.transform.position - Vector3.up * subPanelVerticalOffset;
    }

    // Captured once (first call) so re-tinting a button never loses track of
    // its real default color, regardless of how many times tabs get switched.
    void UpdateTabHighlight(InventoryTab tab)
    {
        if (!tabDefaultColorsCaptured)
        {
            if (harvestTabButton != null) harvestTabDefaultColor = harvestTabButton.image.color;
            if (toolTabButton != null) toolTabDefaultColor = toolTabButton.image.color;
            if (seedsTabButton != null) seedsTabDefaultColor = seedsTabButton.image.color;
            tabDefaultColorsCaptured = true;
        }

        if (harvestTabButton != null) harvestTabButton.image.color = tab == InventoryTab.Harvest ? selectedTabColor : harvestTabDefaultColor;
        if (toolTabButton != null) toolTabButton.image.color = tab == InventoryTab.Tool ? selectedTabColor : toolTabDefaultColor;
        if (seedsTabButton != null) seedsTabButton.image.color = tab == InventoryTab.Seeds ? selectedTabColor : seedsTabDefaultColor;
    }

    void PositionSubPanel(Transform panel)
    {
        if (panel == null || inventoryPanel == null) return;
        panel.position = SubPanelPosition();
        panel.rotation = inventoryPanel.transform.rotation;
    }

    void OnPauseButtonPressed(InputAction.CallbackContext ctx) => TogglePause();

    void TogglePause()
    {
        bool wasOpen = pauseOpen;
        CloseAllMenus();

        if (!wasOpen) OpenPause();
    }

    void OnToolInventoryButtonPressed(InputAction.CallbackContext ctx) => ToggleCropStatusMode();

    // X no longer opens the tool rack directly -- it toggles whether ray-hovering
    // a farmland tile shows its status window (FarmlandHoverStatus.ModeActive).
    // The tool rack itself now lives inside the Y-button backpack's Tool tab.
    //
    // Temporarily disabled -- crop status mode is too buggy/unstable right now.
    // X press is a no-op until this is revisited; FarmlandHoverStatus.ModeActive
    // stays false so the status window never shows.
    void ToggleCropStatusMode()
    {
        return;
#pragma warning disable CS0162
        FarmlandHoverStatus.ModeActive = !FarmlandHoverStatus.ModeActive;
        Debug.Log("[MenuManager] Crop status mode " + (FarmlandHoverStatus.ModeActive ? "ON" : "OFF") + ".");
#pragma warning restore CS0162
    }

    // Called by anything that needs the screen to itself for a moment --
    // SeedPickerUI when the player holds the seed bag and presses A, or the
    // Inventory/Pause toggles themselves before opening one so the other two
    // (including the bag's own popup, which isn't tracked by inventoryOpen/
    // pauseOpen) never end up visible/overlapping at the same time.
    public void CloseAllMenus()
    {
        if (pauseOpen) ClosePause();
        if (inventoryOpen) CloseInventory();
        SeedPickerUI.Instance?.Close();
    }

    // Exposed so SeedPickerUI can freeze/restore locomotion and game time for
    // its own bag-held popup the same way every other menu does. It takes
    // its own lock (separate from the inventory panel's), so closing one
    // never unfreezes the player while the other is still open.
    static readonly object popupLockOwner = new object();

    public void FreezeForMenu()
    {
        LocomotionLock.Lock(popupLockOwner, menuLocomotionProvidersToDisable);
        PauseGameClock();
    }

    public void UnfreezeForMenu()
    {
        LocomotionLock.Unlock(popupLockOwner);
        ResumeGameClock();
    }

    // Shared by Inventory and Tool Inventory -- both freeze the same rig.
    // Goes through LocomotionLock (shared with the shop) rather than each
    // system snapshotting/restoring provider states on its own, which used to
    // restore the other system's "off" and leave the player unable to move.
    void DisableMenuLocomotion()
    {
        LocomotionLock.Lock(this, menuLocomotionProvidersToDisable);
    }

    void RestoreMenuLocomotion()
    {
        LocomotionLock.Unlock(this);
    }

    // GameClock.IsPaused freezes day progression (and, transitively, the
    // sun/skybox) without touching Time.timeScale -- Pause already uses
    // timeScale = 0, but Inventory/Tool Inventory can't: the tool rack's
    // scroll-and-snap animation needs Time.deltaTime to keep working while
    // those panels are open.
    static void PauseGameClock()
    {
        if (GameClock.Instance != null)
            GameClock.Instance.IsPaused = true;
    }

    static void ResumeGameClock()
    {
        if (GameClock.Instance != null)
            GameClock.Instance.IsPaused = false;
    }

    void OpenPause()
    {
        pauseOpen = true;
        Time.timeScale = 0f;
        PositionPanel(pausePanel);
        SetActive(pausePanel, true);
        SetActive(settingsPanel, false);
        Debug.Log("[MenuManager] Game paused.");
    }

    void ClosePause()
    {
        pauseOpen = false;
        Time.timeScale = 1f;
        SetActive(pausePanel, false);
        SetActive(settingsPanel, false);
        Debug.Log("[MenuManager] Game resumed.");
    }

    // Pause panel -> Resume button
    public void OnResumePressed() => ClosePause();

    // Pause panel -> Settings button
    public void OnSettingsPressed()
    {
        SetActive(pausePanel, false);
        PositionPanel(settingsPanel);
        SetActive(settingsPanel, true);
    }

    // Settings panel -> Back button
    public void OnSettingsBackPressed()
    {
        SetActive(settingsPanel, false);
        PositionPanel(pausePanel);
        SetActive(pausePanel, true);
    }

    // Pause panel -> Quit button
    public void OnQuitPressed()
    {
        /*Time.timeScale = 1f;
        HideAll();
        SceneManager.LoadScene("StartMenu");*/

        Time.timeScale = 1f;
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif

    }


    void PositionPanel(GameObject panel)
    {
        if (panel == null) return;

        if (GetMenuPose(out Vector3 position, out Quaternion rotation))
        {
            panel.transform.position = position;
            panel.transform.rotation = rotation;
        }
    }

    // Exposed so other menus that pop up in front of the player -- right now,
    // SeedPickerUI's bag-held popup -- can stand exactly where every other
    // menu stands instead of picking their own (different, closer) distance
    // and ending up right in the player's face.
    public bool GetMenuPose(out Vector3 position, out Quaternion rotation)
    {
        if (vrCamera == null)
        {
            position = default;
            rotation = default;
            return false;
        }

        Vector3 forward = vrCamera.forward;
        forward.y = 0f;
        if (forward == Vector3.zero) forward = Vector3.forward;
        forward.Normalize();

        position = vrCamera.position + forward * menuDistance + Vector3.up * menuHeightOffset;
        rotation = Quaternion.LookRotation(forward);
        return true;
    }

    void HideAll()
    {
        SetActive(inventoryPanel, false);
        SetActive(tabBar, false);
        SetActive(pausePanel, false);
        SetActive(settingsPanel, false);

        if (toolInventoryPanel != null)
            toolInventoryPanel.transform.position = toolInventoryHiddenPosition;
    }

    static void SetActive(GameObject go, bool state)
    {
        if (go != null) go.SetActive(state);
    }

    static void EnableAction(InputActionProperty prop, System.Action<InputAction.CallbackContext> cb)
    {
        if (prop.action == null) return;
        prop.action.Enable();
        prop.action.performed += cb;
    }

    static void DisableAction(InputActionProperty prop, System.Action<InputAction.CallbackContext> cb)
    {
        if (prop.action == null) return;
        prop.action.performed -= cb;
        prop.action.Disable();
    }

    static bool GetButton(UnityEngine.XR.XRNode node, UnityEngine.XR.InputFeatureUsage<bool> usage)
    {
        return UnityEngine.XR.InputDevices
            .GetDeviceAtXRNode(node)
            .TryGetFeatureValue(usage, out bool v) && v;
    }
}