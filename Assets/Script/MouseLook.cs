using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour
{
    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 0.02f;
    public float lookXLimit = 80f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void OnEnable()
    {
        Vector3 angles = transform.localEulerAngles;
        rotationY = angles.y;
        rotationX = Mathf.DeltaAngle(0f, angles.x);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        rotationY += mouseDelta.x * mouseSensitivity;
        rotationX -= mouseDelta.y * mouseSensitivity;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);

        transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);
    }
}