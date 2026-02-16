using UnityEngine;

public class Farmland : MonoBehaviour
{
    public enum LandStatus
    {
        Grass,
        Tilled,
        Watered
    }

    public LandStatus landStatus;

    public GameObject grassModel;   // child object for grass
    public GameObject tilledModel;  // child object for tilled soil

    private MeshRenderer tilledRenderer;

    void Start()
    {
        // Get the renderer for tilled so we can change its color
        tilledRenderer = tilledModel.GetComponent<MeshRenderer>();
        landStatus = LandStatus.Grass;
        grassModel.SetActive(true);   // grass visible
        tilledModel.SetActive(false); // tilled hidden until hoe hits

    }

    private void Update()
    {
        
    }


    public void SetStatus(LandStatus newStatus)
    {
        landStatus = newStatus;

        // Grass vs tilled model swap
        grassModel.SetActive(newStatus == LandStatus.Grass);
        tilledModel.SetActive(newStatus == LandStatus.Tilled || newStatus == LandStatus.Watered);

        // Only change color if tilled/watered
        if (newStatus == LandStatus.Tilled)
        {
            tilledRenderer.material.color = new Color(0.55f, 0.27f, 0.07f); // light brown
        }
        else if (newStatus == LandStatus.Watered)
        {
            tilledRenderer.material.color = new Color(0.35f, 0.2f, 0.05f); // darker brown
        }
    }

    // Hoe collision -> Tilled
    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Hoe") && landStatus == LandStatus.Grass)
        {
            SetStatus(LandStatus.Tilled);
        }
    }

    // Water particle collision -> Watered
    private void OnParticleCollision(GameObject other)
    {
        if (other.CompareTag("Water") && landStatus == LandStatus.Tilled)
        {
            SetStatus(LandStatus.Watered);
        }
    }
}
