using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

    [Tooltip("Enable to use XR direct polling instead of Input Action Asset")]
    public bool useDirectPolling = false;

 
    [Header("Panels")]
    [Tooltip("Canvas / panel shown when Inventory is open")]
    public GameObject inventoryPanel;

    [Tooltip("Canvas / panel shown when Pause is open")]
    public GameObject pausePanel;

    [Tooltip("Settings panel - opened from inside Pause")]
    public GameObject settingsPanel;


    [Header("Menu Placement")]
    public float menuDistance = 1.5f;
    public float menuHeightOffset = 0.1f;

    [Header("Camera")]
    [Tooltip("Assign CenterEyeAnchor or Main Camera")]
    public Transform vrCamera;


    private bool inventoryOpen = false;
    private bool pauseOpen = false;

    private bool bWasPressed = false;
    private bool menuWasPressed = false;


    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        HideAll();

        if (vrCamera == null && Camera.main != null)
            vrCamera = Camera.main.transform;
    }

    void OnEnable()
    {
        if (!useDirectPolling)
        {
            EnableAction(inventoryButtonAction, OnInventoryButtonPressed);
            EnableAction(pauseButtonAction, OnPauseButtonPressed);
        }
    }

    void OnDisable()
    {
        if (!useDirectPolling)
        {
            DisableAction(inventoryButtonAction, OnInventoryButtonPressed);
            DisableAction(pauseButtonAction, OnPauseButtonPressed);
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
    }

    void OnInventoryButtonPressed(InputAction.CallbackContext ctx) => ToggleInventory();

    void ToggleInventory()
    {
        if (pauseOpen) ClosePause();

        if (inventoryOpen) CloseInventory();
        else OpenInventory();
    }

    void OpenInventory()
    {
        inventoryOpen = true;
        PositionPanel(inventoryPanel);
        SetActive(inventoryPanel, true);
        Debug.Log("[MenuManager] Inventory opened.");
    }

    void CloseInventory()
    {
        inventoryOpen = false;
        SetActive(inventoryPanel, false);
        Debug.Log("[MenuManager] Inventory closed.");
    }

    void OnPauseButtonPressed(InputAction.CallbackContext ctx) => TogglePause();

    void TogglePause()
    {
        if (inventoryOpen) CloseInventory();

        if (pauseOpen) ClosePause();
        else OpenPause();
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
        if (vrCamera == null || panel == null) return;

        Vector3 forward = vrCamera.forward;
        forward.y = 0f;
        if (forward == Vector3.zero) forward = Vector3.forward;
        forward.Normalize();

        panel.transform.position = vrCamera.position
                                 + forward * menuDistance
                                 + Vector3.up * menuHeightOffset;
        panel.transform.rotation = Quaternion.LookRotation(forward);
    }

    void HideAll()
    {
        SetActive(inventoryPanel, false);
        SetActive(pausePanel, false);
        SetActive(settingsPanel, false);
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