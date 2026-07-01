using UnityEngine;

/// <summary>
/// Handles prototype first-person movement on the active HO player stack.
/// Keeps walking, sprinting, jumping, and gravity in one CharacterController-driven class.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class HO_PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float sprintSpeed = 15f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = 18.0f;
    [SerializeField] private float groundSnapForce = 1.0f;
    [SerializeField] private float mouseSensitivity = 10f;
    [SerializeField] private CharacterController characterController;

    private float verticalVelocity;

    /// <summary>
    /// Caches the colocated CharacterController when the component is first added.
    /// </summary>
    private void Reset()
    {
        CacheCharacterController();
    }

    /// <summary>
    /// Caches required references before runtime movement starts.
    /// </summary>
    private void Awake()
    {
        CacheCharacterController();
    }

    /// <summary>
    /// Reads Old Input every frame and applies movement through CharacterController.
    /// </summary>
    private void Update()
    {
        if (characterController == null)
        {
            return;
        }

        ApplyMovement();
    }

    /// <summary>
    /// Finds the CharacterController on the same GameObject when not assigned yet.
    /// </summary>
    private void CacheCharacterController()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
    }

    /// <summary>
    /// Combines planar movement with jump and gravity before moving the controller.
    /// </summary>
    private void ApplyMovement()
    {
        Vector3 localMove = ReadMoveInput();
        float currentSpeed = IsSprintPressed() ? sprintSpeed : moveSpeed;
        Vector3 worldMove = transform.TransformDirection(localMove) * currentSpeed;

        UpdateVerticalVelocity();
        worldMove.y = verticalVelocity;

        characterController.Move(worldMove * Time.deltaTime);
    }

    /// <summary>
    /// Reads horizontal and vertical axes from the Old Input Manager.
    /// Uses raw values so the active HO stack reacts like the prototype controller did.
    /// </summary>
    private static Vector3 ReadMoveInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 moveInput = new Vector3(horizontal, 0.0f, vertical);
        return Vector3.ClampMagnitude(moveInput, 1.0f);
    }

    /// <summary>
    /// Applies ground snapping, jump impulse, and gravity accumulation.
    /// Gravity is stored as a positive magnitude and applied downward each frame.
    /// </summary>
    private void UpdateVerticalVelocity()
    {
        bool grounded = characterController.isGrounded;
        if (grounded && verticalVelocity < 0.0f)
        {
            verticalVelocity = -groundSnapForce;
        }

        if (grounded && IsJumpPressed() && jumpHeight > 0.0f)
        {
            verticalVelocity = Mathf.Sqrt(2.0f * jumpHeight * gravity);
        }

        verticalVelocity -= gravity * Time.deltaTime;
    }

    /// <summary>
    /// Returns whether the sprint modifier is currently held.
    /// </summary>
    private static bool IsSprintPressed()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    /// <summary>
    /// Returns whether jump was requested on this frame.
    /// </summary>
    private static bool IsJumpPressed()
    {
        return Input.GetKeyDown(KeyCode.Space);
    }
}
