using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class PickUp : MonoBehaviour
{
    public Material outlineMaterial;
    private Material originalMaterial;
    private Renderer rend;

    public TMP_Text pickUpHintUI;

    public Transform holdPosition;
    public Vector3 holdOffset = new Vector3(0.7f, -0.89f, 0.48f);
    public Vector3 holdRotationOffset = new Vector3(0f, 180f, 0f);

    private bool isPickedUp = false;

    public float swingAngle = 10f;
    public float swingSpeed = 12f;
    public float returnSpeed = 10f;

    private bool isSwinging = false;
    private Quaternion originalLocalRotation;

    void Start()
    {
        rend = GetComponent<Renderer>();
        originalMaterial = rend.material;

        if (pickUpHintUI != null)
            pickUpHintUI.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isPickedUp)
        {
            HandleHoverAndPickup();
        }
        else
        {
            HandleSwing();
        }
    }

    void HandleHoverAndPickup()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, 3f))
        {
            if (hit.collider == GetComponent<Collider>())
            {
                if (outlineMaterial != null)
                    rend.material = outlineMaterial;

                if (pickUpHintUI != null)
                    pickUpHintUI.gameObject.SetActive(true);

                if (Keyboard.current.fKey.wasPressedThisFrame)
                {
                    PickUpTool();
                }

                return;
            }
        }

        ResetHighlight();
    }

    void HandleSwing()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame && !isSwinging)
        {
            StartCoroutine(SwingTool());
        }
    }

    void ResetHighlight()
    {
        if (rend != null)
            rend.material = originalMaterial;

        if (pickUpHintUI != null)
            pickUpHintUI.gameObject.SetActive(false);
    }

    void PickUpTool()
    {
        isPickedUp = true;

        transform.SetParent(holdPosition);
        transform.localPosition = new Vector3(0.7f, -0.89f, 0.48f);
        transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        originalLocalRotation = transform.localRotation;

        if (pickUpHintUI != null)
            pickUpHintUI.gameObject.SetActive(false);

        rend.material = originalMaterial;

        Debug.Log("Hoe picked up!");
    }

    IEnumerator SwingTool()
    {
        isSwinging = true;

        Quaternion targetRotation = originalLocalRotation * Quaternion.Euler(-swingAngle, 0, 0);

        while (Quaternion.Angle(transform.localRotation, targetRotation) > 1f)
        {
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                targetRotation,
                swingSpeed * Time.deltaTime
            );
            yield return null;
        }

        yield return new WaitForSeconds(0.05f);

        while (Quaternion.Angle(transform.localRotation, originalLocalRotation) > 1f)
        {
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                originalLocalRotation,
                returnSpeed * Time.deltaTime
            );
            yield return null;
        }

        transform.localRotation = originalLocalRotation;
        isSwinging = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Farmland"))
        {
            // Set farmland to tilled
            other.GetComponent<Farmland>().SetStatus(Farmland.LandStatus.Tilled);
        }
    }

}
