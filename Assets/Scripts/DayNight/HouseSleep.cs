using UnityEngine;

// Lets the player sleep in the house: look at it while nearby and press A to
// skip straight to the next morning (see GameClock.SkipToNextMorning). Crops
// grow and hens plan their day exactly as on a normal new day, so this is
// also the quick way to fast-forward while testing.
//
// Unity setup:
//   1. Add this script and a GazeInteractable to an empty child at the
//      house's front door (gaze is measured to this object's position)
//   2. Give the GazeInteractable a "Press A to sleep" World Space prompt
//      canvas, and drag that canvas's text into Prompt Text
[RequireComponent(typeof(GazeInteractable))]
public class HouseSleep : MonoBehaviour
{
    [Tooltip("Text on the GazeInteractable's prompt canvas")]
    public TMPro.TMP_Text promptText;

    [Tooltip("Optional -- played when the player wakes up")]
    public AudioSource wakeAudioSource;

    void Awake()
    {
        GazeInteractable gaze = GetComponent<GazeInteractable>();
        gaze.onInteractPressed.AddListener(Sleep);
        gaze.onCanvasClaimed.AddListener(RefreshPrompt);
    }

    void Sleep()
    {
        if (GameClock.Instance == null) return;

        GameClock.Instance.SkipToNextMorning();

        if (wakeAudioSource != null) wakeAudioSource.Play();
        HUD.Instance?.ShowAlert("Good morning! " + GameClock.Instance.GetDayString());
    }

    void RefreshPrompt()
    {
        if (promptText != null) promptText.text = "Press A to sleep until morning";
    }
}
