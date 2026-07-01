using UnityEngine;

public class RotatingExhibit : MonoBehaviour
{
    public Vector3 rotationAxis = Vector3.up;
    public float degreesPerSecond = 24.0f;
    public bool rotateAutomatically = true;
    public bool rotateWhenInteracted = true;

    private bool isRotating;

    private void Awake()
    {
        isRotating = rotateAutomatically;
    }

    private void Update()
    {
        if (!isRotating)
        {
            return;
        }

        transform.Rotate(rotationAxis.normalized, degreesPerSecond * Time.deltaTime, Space.Self);
    }

    public void ToggleRotation()
    {
        if (rotateWhenInteracted)
        {
            isRotating = !isRotating;
        }
    }

    public void SpinOnce()
    {
        isRotating = true;
    }
}
