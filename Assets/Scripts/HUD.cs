using UnityEngine;
using TMPro;



public class HUD : MonoBehaviour
{
    // Singleton
    public static HUD Instance { get; private set; }

    [Header("Time Display")]
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI dayText;

    [Header("Harvest Alert")]
    public GameObject harvestAlertPanel;
    public TextMeshProUGUI harvestAlertText;

    [Tooltip("How many seconds the alert stays on screen")]
    public float alertDuration = 5f;

    [Header("VR Positioning")]
    [Tooltip("Turn on for floating world UI - makes the panel always face the player.\nTurn OFF if this is a wrist HUD parented to the hand.")]
    public bool billboardToCamera = false;

    [Tooltip("Assign your VR camera (Main Camera / CenterEyeAnchor) for billboard mode")]
    public Transform playerCamera;

    // Private
    private float alertTimer = 0f;
    private bool alertActive = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (harvestAlertPanel != null)
            harvestAlertPanel.SetActive(false);

        // Auto-find camera if not assigned
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;
    }

    void Update()
    {
        UpdateTimeDisplay();
        UpdateAlertTimer();
        UpdateBillboard();
    }

    // Update time and day text every frame
    void UpdateTimeDisplay()
    {
        if (GameClock.Instance == null) return;

        if (timeText != null)
            timeText.text = GameClock.Instance.GetTimeString();  // "08:30"

        if (dayText != null)
            dayText.text = GameClock.Instance.GetDayString();    // "Day 3"
    }

    // Auto-hide alert after duration
    void UpdateAlertTimer()
    {
        if (!alertActive) return;

        alertTimer -= Time.deltaTime;

        if (alertTimer <= 0f)
            HideHarvestAlert();
    }

    // Billboard: rotate to face the player camera (floating UI only)
    void UpdateBillboard()
    {
        if (!billboardToCamera || playerCamera == null) return;

        Vector3 directionToCamera = transform.position - playerCamera.position;
        directionToCamera.y = 0f;

        if (directionToCamera != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(directionToCamera);
    }

    // Called by FarmManager when a crop is ready
    public void ShowHarvestAlert(string cropName)
    {
        if (harvestAlertPanel != null)
            harvestAlertPanel.SetActive(true);

        if (harvestAlertText != null)
            harvestAlertText.text = cropName + " is ready to harvest! Press A to pick up.";

        alertTimer = alertDuration;
        alertActive = true;

        Debug.Log("[HUD] Alert: " + cropName + " is ready to harvest!");
    }

    // Called by FarmManager when all crops are harvested
    public void HideHarvestAlert()
    {
        if (harvestAlertPanel != null)
            harvestAlertPanel.SetActive(false);

        alertActive = false;
    }
}