using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// MenuManager.cs -- Controls the floating menu panel in VR.
// Press B on the right controller to toggle the menu open/closed.
// The menu floats in front of the player's camera each time it opens.
//
// Unity setup:
//   1. Create an empty GameObject called "MenuManager" and attach this script
//   2. Create a World Space Canvas called "MenuCanvas" with two child panels:
//        - "PausePanel"     (pause menu buttons)
//        - "InventoryPanel" (inventory grid)
//   3. Set Canvas scale to 0.001, width 800, height 600
//   4. Drag the Canvas, camera rig, and panels into the Inspector fields
//   5. Wire up button OnClick events to the public methods below
//
// Input setup:
//   In your XRI Default Input Actions (or your own Input Action Asset),
//   make sure the B Button action exists under XRI RightHand.
//   Drag the action reference into the menuButtonAction field.

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    [Header("Input")]
    [Tooltip("Assign the B Button action from your XR Input Action Asset")]
    public InputActionProperty menuButtonAction;

    [Header("Menu Canvas")]
    [Tooltip("The World Space Canvas that holds all menu panels")]
    public GameObject menuCanvas;

    [Tooltip("How far in front of the camera the menu floats (meters)")]
    public float menuDistance = 1.5f;

    [Tooltip("How high above center to offset the menu")]
    public float menuHeightOffset = 0f;

    [Header("Panels")]
    public GameObject pausePanel;
    public GameObject inventoryPanel;

    [Header("Camera")]
    [Tooltip("Assign your VR camera (CenterEyeAnchor or Main Camera)")]
    public Transform vrCamera;

    // Which panel is currently shown
    public enum MenuPanel { None, Pause, Inventory }
    private MenuPanel currentPanel = MenuPanel.None;
    private bool menuOpen = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Start with menu hidden
        if (menuCanvas != null) menuCanvas.SetActive(false);
    }

    void OnEnable()
    {
        menuButtonAction.action.Enable();
        menuButtonAction.action.performed += OnMenuButtonPressed;
    }

    void OnDisable()
    {
        menuButtonAction.action.performed -= OnMenuButtonPressed;
        menuButtonAction.action.Disable();
    }

    // Called when B button is pressed
    void OnMenuButtonPressed(InputAction.CallbackContext ctx)
    {
        if (menuOpen)
            CloseMenu();
        else
            OpenMenu(MenuPanel.Pause);  // default to pause panel on open
    }

    // Open menu and show a specific panel
    public void OpenMenu(MenuPanel panel)
    {
        menuOpen = true;
        PositionMenuInFrontOfPlayer();

        if (menuCanvas != null) menuCanvas.SetActive(true);

        ShowPanel(panel);

        // Optionally pause the game while menu is open
        // Time.timeScale = 0f;
    }

    public void CloseMenu()
    {
        menuOpen = false;
        if (menuCanvas != null) menuCanvas.SetActive(false);

        // Re-enable time if you paused it
        // Time.timeScale = 1f;
    }

    // Position the canvas in front of the player every time it opens
    void PositionMenuInFrontOfPlayer()
    {
        if (vrCamera == null || menuCanvas == null) return;

        // Place it in front of camera, ignoring vertical tilt
        Vector3 forward = vrCamera.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 position = vrCamera.position
                         + forward * menuDistance
                         + Vector3.up * menuHeightOffset;

        menuCanvas.transform.position = position;

        // Face toward the player
        menuCanvas.transform.rotation = Quaternion.LookRotation(forward);
    }

    // Show one panel and hide the other
    void ShowPanel(MenuPanel panel)
    {
        currentPanel = panel;
        if (pausePanel != null) pausePanel.SetActive(panel == MenuPanel.Pause);
        if (inventoryPanel != null) inventoryPanel.SetActive(panel == MenuPanel.Inventory);
    }

    // -----------------------------------------------------------------------
    // Button callbacks -- wire these to your UI buttons in the Inspector
    // -----------------------------------------------------------------------

    // Pause panel buttons
    public void OnResumePressed()
    {
        CloseMenu();
    }

    public void OnInventoryTabPressed()
    {
        ShowPanel(MenuPanel.Inventory);
    }

    public void OnPauseTabPressed()
    {
        ShowPanel(MenuPanel.Pause);
    }

    public void OnQuitPressed()
    {
        CloseMenu();
        SceneManager.LoadScene("StartMenu");  // change to your actual start scene name
    }
}