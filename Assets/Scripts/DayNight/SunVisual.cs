using UnityEngine;

// The Skybox/Cubemap Blend shader has no sun-disc rendering of its own
// (unlike Unity's old Procedural skybox) -- it only samples two cubemap
// textures. So there's nothing in the sky that tracks the light's
// direction unless something else draws it. This keeps a simple bright
// sphere positioned far along the sun's direction from the viewer, and
// hides it at night since the light itself is off then.
//
// Visibility is toggled via the Renderer, NOT gameObject.SetActive --
// disabling this object from its own LateUpdate would stop LateUpdate
// from ever running again, permanently freezing it invisible the first
// time night arrived.
public class SunVisual : MonoBehaviour
{
    [Tooltip("The light whose direction the visual should track.")]
    public Transform sunLight;

    [Tooltip("Who the sun should appear far away from. Defaults to the main camera.")]
    public Transform viewer;

    [Tooltip("The DayNightCycle used to hide the visual at night.")]
    public DayNightCycle dayNightCycle;

    public float distance = 400f;

    private Renderer[] renderers;

    void Awake()
    {
        if (viewer == null && Camera.main != null)
            viewer = Camera.main.transform;

        renderers = GetComponentsInChildren<Renderer>(true);
    }

    void LateUpdate()
    {
        if (sunLight == null || viewer == null) return;

        // A directional light's forward is the direction light travels
        // (sun -> ground), so the visual sits back along -forward.
        transform.position = viewer.position - sunLight.forward * distance;

        if (dayNightCycle != null)
            SetVisible(dayNightCycle.IsDaytime);
    }

    void SetVisible(bool visible)
    {
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible;
    }
}
