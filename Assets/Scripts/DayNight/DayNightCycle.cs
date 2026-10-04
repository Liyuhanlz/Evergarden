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

    // Exposed so other systems (e.g. the skybox blend) can follow the same
    // sun position without recomputing it themselves.
    public float CurrentSunAngle { get; private set; }
    public bool IsDaytime { get; private set; }

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
        CurrentSunAngle = angle;

        // 2. Handle Light Intensity (Night/Day)
        // Check the source angle directly, NOT transform.eulerAngles.x.
        // Reading the euler angle back off a Quaternion clamps pitch to
        // +/-90 degrees (Unity's decomposition uses asin, whose range is
        // limited), so a full 360-degree sweep gets folded into the wrong
        // range for about three quarters of the day and the sun reads as
        // "below the horizon" almost the whole time.
        IsDaytime = angle > 0f && angle < 180f;
        sunLight.intensity = IsDaytime ? maxIntensity : 0f;
    }
}