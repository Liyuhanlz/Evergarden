using UnityEngine;

// Drives the Skybox/Cubemap Blend material's day/night crossfade
// (the "Skybox Cubemap Extended Blend" material's _CubemapTransition
// property) to follow the same sun angle as DayNightCycle, so the sky
// and the sun move in lockstep instead of on separate clocks.
//
// Also interpolates exposure/tint between the tuned values from the
// packaged Day and Night demo materials -- the Blend material on its own
// only swaps the cubemap texture and keeps one flat exposure/tint for
// both, so night ends up looking like a dim day instead of the punchier
// look the dedicated Night material actually has.
public class SkyboxDayNightBlend : MonoBehaviour
{
    [Tooltip("The DayNightCycle driving the sun. Auto-found in the scene if left empty.")]
    public DayNightCycle dayNightCycle;

    [Tooltip("Material using the Skybox/Cubemap Blend shader. Defaults to RenderSettings.skybox " +
             "-- assign the skybox material in Window > Rendering > Lighting > Environment first.")]
    public Material skyboxMaterial;

    [Header("Day/Night Look (matches the packaged Day/Night demo materials)")]
    public float dayExposure = 1.98f;
    public float nightExposure = 2.26f;
    public Color dayTint = new Color(0.29223037f, 0.30331424f, 0.31132078f);
    public Color nightTint = new Color(0.14150941f, 0.14150941f, 0.14150941f);

    private const string BlendProperty = "_CubemapTransition";
    private const string ExposureProperty = "_Exposure";
    private const string TintProperty = "_TintColor";

    void Awake()
    {
        if (dayNightCycle == null)
            dayNightCycle = FindObjectOfType<DayNightCycle>();

        if (skyboxMaterial == null)
            skyboxMaterial = RenderSettings.skybox;
    }

    void Update()
    {
        if (dayNightCycle == null || skyboxMaterial == null) return;

        // 0 = full day cubemap, 1 = full night cubemap. Sine of the sun
        // angle peaks at noon (angle 90) and clamps to 0 for the entire
        // angle 180-360 range (dusk through dawn), so night holds at full
        // strength for that whole stretch rather than a hard cut.
        float dayAmount = Mathf.Clamp01(Mathf.Sin(dayNightCycle.CurrentSunAngle * Mathf.Deg2Rad));

        skyboxMaterial.SetFloat(BlendProperty, 1f - dayAmount);
        skyboxMaterial.SetFloat(ExposureProperty, Mathf.Lerp(nightExposure, dayExposure, dayAmount));
        skyboxMaterial.SetColor(TintProperty, Color.Lerp(nightTint, dayTint, dayAmount));
    }
}
