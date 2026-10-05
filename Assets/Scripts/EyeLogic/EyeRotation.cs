using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(Rigidbody))]
public class EyeRotation : MonoBehaviour
{
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private Rigidbody rb;
    private float yaw;
    private float pitch;
    private Vector2 pendingDelta;

    public bool CanRotate = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        // Re-sync to the eye's current rotation (e.g. reset to the socket's orientation
        // by EyeDeployController on dock) so re-enabling on the next deploy doesn't snap
        // back to stale yaw/pitch left over from before this was last disabled.
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x > 180f ? e.x - 360f : e.x;
        pendingDelta = Vector2.zero;
    }

    private void Update()
    {
        if (!CanRotate) return;
        HandleInput(); 
    }

    void HandleInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        pendingDelta += mouse.delta.ReadValue() * mouseSensitivity;
    }


    private void FixedUpdate()
    {
        if(!CanRotate) return; 
        HandleRotation();
    }

    public void HandleRotation()
    {
        yaw += pendingDelta.x;
        pitch = Mathf.Clamp(pitch - pendingDelta.y, minPitch, maxPitch);
        pendingDelta = Vector2.zero;

        rb.MoveRotation(Quaternion.Euler(pitch, yaw, 0f));
    }
}