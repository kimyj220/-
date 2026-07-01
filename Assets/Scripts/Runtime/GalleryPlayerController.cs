using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class GalleryPlayerController : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;

    [Header("Movement")]
    public float walkSpeed = 10f;
    public float sprintSpeed = 15f;
    public float jumpHeight = 5f;
    public float gravity = -18.0f;

    [Header("Look")]
    public float mouseSensitivity = 10f;
    public float minPitch = -82.0f;
    public float maxPitch = 82.0f;
    public bool lockCursorOnStart = true;

    private CharacterController controller;
    private float verticalVelocity;
    private float pitch;
    private bool cursorLocked;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            SetCursorLocked(true);
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorLocked(!cursorLocked);
        }

        UpdateLook();
        UpdateMovement();
    }

    private void UpdateLook()
    {
        if (!cursorLocked || Mouse.current == null || playerCamera == null)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float yaw = mouseDelta.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, minPitch, maxPitch);

        transform.Rotate(Vector3.up * yaw, Space.Self);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 3f, 0.0f);
    }

    private void UpdateMovement()
    {
        Vector2 input = ReadMoveInput();
        Vector3 move = transform.right * input.x + transform.forward * input.y;

        if (move.sqrMagnitude > 1.0f)
        {
            move.Normalize();
        }

        bool sprinting = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        float speed = sprinting ? sprintSpeed : walkSpeed;

        if (controller.isGrounded && verticalVelocity < 0.0f)
        {
            verticalVelocity = -2.0f;
        }

        bool jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
        if (jumpPressed && controller.isGrounded && jumpHeight > 0.0f)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * speed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private static Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            input.x -= 1.0f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            input.x += 1.0f;
        }

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            input.y += 1.0f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            input.y -= 1.0f;
        }

        return input;
    }

    private void SetCursorLocked(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
