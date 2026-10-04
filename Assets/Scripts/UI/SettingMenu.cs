using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

// Attach to your Settings panel GameObject.

public class SettingsMenu : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("The Audio Mixer that controls master volume")]
    public AudioMixer audioMixer;

    [Tooltip("The exposed parameter name on the Audio Mixer")]
    public string mixerParameterName = "MasterVolume";

    [Header("UI")]
    public Slider volumeSlider;

    [Header("Panels")]
    [Tooltip("The pause panel to return to when Save is pressed")]
    public GameObject pausePanel;

    private const string VOLUME_KEY = "MasterVolume";

    // Holds the slider value at the moment settings was opened,
    // so we can revert if the player closes without saving.
    private float volumeOnOpen;

    // =============================================
    //  UNITY LIFECYCLE
    // =============================================
    void Start()
    {
        float savedVolume = PlayerPrefs.GetFloat(VOLUME_KEY, 1f);
        volumeSlider.value = savedVolume;
        ApplyVolume(savedVolume);
    }

    void OnEnable()
    {
        // Snapshot current value when panel opens
        volumeOnOpen = PlayerPrefs.GetFloat(VOLUME_KEY, 1f);
        volumeSlider.value = volumeOnOpen;
        ApplyVolume(volumeOnOpen);
    }

    // =============================================
    //  CALLED BY SLIDER onValueChanged IN INSPECTOR
    // =============================================
    public void OnVolumeChanged(float value)
    {
        // Preview the volume change live without saving yet
        ApplyVolume(value);
    }

    // =============================================
    //  CALLED BY SAVE BUTTON onClick IN INSPECTOR
    // =============================================
    public void OnSavePressed()
    {
        // Commit the current slider value to PlayerPrefs
        PlayerPrefs.SetFloat(VOLUME_KEY, volumeSlider.value);
        PlayerPrefs.Save();

        ReturnToPause();
    }

    // =============================================
    //  CALLED BY BACK/CANCEL BUTTON onClick (optional)
    // =============================================
    public void OnCancelPressed()
    {
        // Revert to the value from before settings was opened
        ApplyVolume(volumeOnOpen);
        volumeSlider.value = volumeOnOpen;

        ReturnToPause();
    }

    // =============================================
    //  HELPERS
    // =============================================
    void ReturnToPause()
    {
        gameObject.SetActive(false);

        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    void ApplyVolume(float value)
    {
        if (audioMixer == null) return;

        float db = Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f;
        audioMixer.SetFloat(mixerParameterName, db);
    }
}