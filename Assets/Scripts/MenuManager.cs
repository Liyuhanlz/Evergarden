using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    [Header("Input")]
    [Tooltip("Assign XRI RightHand / secondaryButton action from your Input Action Asset")]
    public InputActionProperty menuButtonAction;

    [Tooltip("Turn on if you want to skip the Input Action Asset and poll B button directly")]
    public bool useDirectPolling = false;

    [Header("Menu Canvas")]
    public GameObject menuCanvas;
    public float menuDistance = 1.5f;
    public float menuHeightOffset = 0.1f;

    [Header("Panels")]
    public GameObject pausePanel;
    public GameObject inventoryPanel;

    [Header("Camera")]
    [Tooltip("Assign CenterEyeAnchor or Main Camera transform")]
    public Transform vrCamera;

    public enum MenuPanel { None, Pause, Inventory }
    private MenuPanel currentPanel = MenuPanel.None;
    private bool menuOpen = false;

    // For direct polling debounce
    private bool bWasPressed = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (menuCanvas != null) menuCanvas.SetActive(false);

        // Auto find camera if not assigned
        if (vrCamera == null && Camera.main != null)
            vrCamera = Camera.main.transform;
    }

    void OnEnable()
    {
        if (!useDirectPolling && menuButtonAction.action != null)
        {
            menuButtonAction.action.Enable();
            menuButtonAction.action.performed += OnMenuButtonPressed;
        }
    }

    void OnDisable()
    {
        if (!useDirectPolling && menuButtonAction.action != null)
        {
            menuButtonAction.action.performed -= OnMenuButtonPressed;
            menuButtonAction.action.Disable();
        }
    }

    void Update()
    {
        // OPTION B: direct polling fallback
        if (useDirectPolling)
        {
            // secondaryButton = B on right controller
            bool bPressed = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand)
                .TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool val) && val;

            // Only trigger on the frame the button goes down (not held)
            if (bPressed && !bWasPressed)
                ToggleMenu();

            bWasPressed = bPressed;
        }
    }

    // Called by InputAction event (Option A)
    void OnMenuButtonPressed(InputAction.CallbackContext ctx)
    {
        ToggleMenu();
    }

    void ToggleMenu()
    {
        if (menuOpen) CloseMenu();
        else OpenMenu(MenuPanel.Pause);
    }

    public void OpenMenu(MenuPanel panel)
    {
        menuOpen = true;
        PositionMenuInFrontOfPlayer();
        if (menuCanvas != null) menuCanvas.SetActive(true);
        ShowPanel(panel);
        Debug.Log("[MenuManager] Menu opened.");
    }

    public void CloseMenu()
    {
        menuOpen = false;
        if (menuCanvas != null) menuCanvas.SetActive(false);
        Debug.Log("[MenuManager] Menu closed.");
    }

    void PositionMenuInFrontOfPlayer()
    {
        if (vrCamera == null || menuCanvas == null) return;

        Vector3 forward = vrCamera.forward;
        forward.y = 0f;
        if (forward == Vector3.zero) forward = Vector3.forward;
        forward.Normalize();

        Vector3 position = vrCamera.position
                         + forward * menuDistance
                         + Vector3.up * menuHeightOffset;

        menuCanvas.transform.position = position;
        menuCanvas.transform.rotation = Quaternion.LookRotation(forward);
    }

    void ShowPanel(MenuPanel panel)
    {
        currentPanel = panel;
        if (pausePanel != null) pausePanel.SetActive(panel == MenuPanel.Pause);
        if (inventoryPanel != null) inventoryPanel.SetActive(panel == MenuPanel.Inventory);
    }

    // Wire these to your UI buttons in the Inspector
    public void OnResumePressed() => CloseMenu();
    public void OnInventoryTabPressed() => ShowPanel(MenuPanel.Inventory);
    public void OnPauseTabPressed() => ShowPanel(MenuPanel.Pause);
    public void OnQuitPressed()
    {
        CloseMenu();
        SceneManager.LoadScene("StartMenu");
    }
}