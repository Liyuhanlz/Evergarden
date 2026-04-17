using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public Light sunLight;

    // In Unity, 90 degrees on the X-axis points the light straight down.
    // Since your day starts at 6:00 AM (TimeOfDay = 0), we start at 0 degrees (horizon).
    // At 12:00 PM (TimeOfDay = 0.25), the angle will be 90 degrees (overhead).
    public float startAngle = 0f;
    public float endAngle = 360f;

    [Header("Settings")]
    public float maxIntensity = 1f;

    private void Awake()
    {
        if (sunLight == null)
        {
            sunLight = GetComponent<Light>();
        }
    }

    void Update()
    {
        if (GameClock.Instance == null || sunLight == null) return;

        float time = GameClock.Instance.TimeOfDay;

        // 1. Handle Rotation
        // This ensures that at 12:00 PM (0.25 progress), the rotation is 90 degrees.
        float angle = Mathf.Lerp(startAngle, endAngle, time);
        sunLight.transform.rotation = Quaternion.Euler(angle, -30f, 0f);

        // 2. Handle Light Intensity (Night/Day)
        // Unity rotation 0-180 is generally "above ground" (Daylight)
        // We use the local rotation to check if the sun has "set"
        float currentX = sunLight.transform.eulerAngles.x;

        if (currentX > 0f && currentX < 180f)
        {
            sunLight.intensity = maxIntensity;
        }
        else
        {
            sunLight.intensity = 0f;
        }
    }
}