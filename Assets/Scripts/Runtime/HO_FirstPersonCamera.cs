using UnityEngine;

/// <summary>
/// Owns the active HO first-person look stack.
/// Separates yaw on the player body from pitch on the camera pivot and manages cursor lock for mouse look.
/// </summary>
public class HO_FirstPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float mouseSensitivity = 1.0f;
    [SerializeField] private float minPitch = -75.0f;
    [SerializeField] private float maxPitch = 75.0f;
    [SerializeField] private bool lockCursorOnStart = true;

    private float pitch;
    private bool cursorLocked;

    /// <summary>
    /// Rebuilds default references when the component is first added.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Caches references and restores the starting pitch from the camera pivot.
    /// </summary>
    private void Awake()
    {
        CacheReferences();

        if (cameraPivot != null)
        {
            pitch = NormalizeAngle(cameraPivot.localEulerAngles.x);
        }
    }

    /// <summary>
    /// Applies the prototype cursor-lock startup behavior on the active HO path.
    /// </summary>
    private void Start()
    {
        SetCursorLocked(lockCursorOnStart);
    }

    /// <summary>
    /// Handles Escape cursor toggling and rotates the view only while the cursor is locked.
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorLocked(!cursorLocked);
        }

        if (playerBody == null || cameraPivot == null || !cursorLocked)
        {
            return;
        }

        RotateView();
    }

    /// <summary>
    /// Restores a visible cursor when the component is disabled.
    /// </summary>
    private void OnDisable()
    {
        if (cursorLocked)
        {
            SetCursorLocked(false);
        }
    }

    /// <summary>
    /// Finds the player body and camera pivot from the expected runtime hierarchy.
    /// </summary>
    private void CacheReferences()
    {
        if (playerBody == null)
        {
            playerBody = transform;
        }

        if (cameraPivot == null)
        {
            Transform pivot = transform.Find("PlayerCameraPivot");
            if (pivot != null)
            {
                cameraPivot = pivot;
            }
        }
    }

    /// <summary>
    /// Reads Old Input mouse axes and applies yaw and pitch to the active HO player stack.
    /// </summary>
    private void RotateView()
    {
        float yawInput = Input.GetAxis("Mouse X") * mouseSensitivity;
        float pitchInput = Input.GetAxis("Mouse Y") * mouseSensitivity;

        playerBody.Rotate(Vector3.up * yawInput, Space.Self);

        pitch -= pitchInput;
        float clampedMinPitch = Mathf.Min(minPitch, maxPitch);
        float clampedMaxPitch = Mathf.Max(minPitch, maxPitch);
        pitch = Mathf.Clamp(pitch, clampedMinPitch, clampedMaxPitch);

        cameraPivot.localRotation = Quaternion.Euler(pitch, 0.0f, 0.0f);
    }

    /// <summary>
    /// Applies the current cursor visibility and lock state for prototype mouse look.
    /// </summary>
    private void SetCursorLocked(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    /// <summary>
    /// Converts Unity's 0..360 angle range into -180..180 for pitch restoration.
    /// </summary>
    private static float NormalizeAngle(float angle)
    {
        if (angle > 180.0f)
        {
            angle -= 360.0f;
        }

        return angle;
    }
}
