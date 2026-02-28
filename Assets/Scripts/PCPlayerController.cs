using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PCPlayerController : MonoBehaviour
{
    private float moveSpeed = 5f;
    private float mouseSensitivity = 0.15f;
    private float jumpHeight = 1.2f;
    private float gravity = -9.81f;

    private CharacterController controller;
    private float yVelocity;
    private float xRotation = 0f;

    public Transform cameraTransform; // Assign your camera here

    private bool cameraEnabled = true; // Alt key toggle

    void Start()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Check Alt key
        if (Keyboard.current != null &&
            (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed))
        {
            cameraEnabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            cameraEnabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        HandleMovement();

        if (cameraEnabled)
            HandleMouseLook();
    }

    void HandleMovement()
    {
        Vector2 input = Keyboard.current != null ?
            new Vector2(
                (Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0),
                (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0)
            )
            : Vector2.zero;

        Vector3 move = transform.right * input.x + transform.forward * input.y;

        if (controller.isGrounded && yVelocity < 0)
            yVelocity = -2f;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && controller.isGrounded)
            yVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        yVelocity += gravity * Time.deltaTime;

        controller.Move(move * moveSpeed * Time.deltaTime + Vector3.up * yVelocity * Time.deltaTime);
    }

    void HandleMouseLook()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        // Yaw rotates player left/right
        transform.Rotate(Vector3.up * mouseDelta.x * mouseSensitivity);

        // Pitch rotates camera up/down
        xRotation -= mouseDelta.y * mouseSensitivity;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
}
