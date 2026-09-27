using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class EyeFreeRoamNonVert : MonoBehaviour
{
    [Header("Movement Stats")]
    [SerializeField] private float roamSpeed = 8f;
    [SerializeField] private float roamAcceleration = 20f;
    [SerializeField] private float maxRange = 10f;

    [Header("Movement Input")]
    [SerializeField] private bool useCameraRelativeMovement = true;
    [SerializeField] private Camera attachedCamera;
    [SerializeField] private Key forwardKey = Key.W;
    [SerializeField] private Key backwardKey = Key.S;
    [SerializeField] private Key leftKey = Key.A;
    [SerializeField] private Key rightKey = Key.D;

    [Header("Range Anchor")]
    [Tooltip("The eye's socket/attachment point on the player - range is measured from here.")]
    [SerializeField] private Transform rangeAnchor;

    private Rigidbody rb;
    private Vector3 currentVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (attachedCamera == null)
            attachedCamera = GetComponentInChildren<Camera>();
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    public void HandleMovement()
    {
        currentVelocity.y = rb.linearVelocity.y;

        Vector3 inputDir = ReadInput();
        Vector3 desiredMoveDir = useCameraRelativeMovement ? CameraRelative(inputDir) : inputDir;
        desiredMoveDir = ClampToRange(desiredMoveDir);

        Vector3 targetVelocity = desiredMoveDir * roamSpeed;

        targetVelocity.y = currentVelocity.y;

        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, roamAcceleration * Time.fixedDeltaTime);

        rb.linearVelocity = currentVelocity;
    }

    private Vector3 ReadInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return Vector3.zero; // no keyboard connected

        float x = 0f;
        if (kb[rightKey].isPressed) x += 1f;
        if (kb[leftKey].isPressed) x -= 1f;

        float z = 0f;
        if (kb[forwardKey].isPressed) z += 1f;
        if (kb[backwardKey].isPressed) z -= 1f;


        Vector3 dir = new Vector3(x, 0, z);
        return dir.sqrMagnitude > 1f ? dir.normalized : dir;
    }

    private Vector3 CameraRelative(Vector3 inputDir)
    {
        if (attachedCamera == null) return inputDir;

        Vector3 camForward = attachedCamera.transform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = attachedCamera.transform.right;
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 result = camRight * inputDir.x + Vector3.up * inputDir.y + camForward * inputDir.z;
        return result.sqrMagnitude > 1f ? result.normalized : result;
    }

    private Vector3 ClampToRange(Vector3 moveDir)
    {
        if (rangeAnchor == null || moveDir.sqrMagnitude < 0.0001f)
            return moveDir;

        Vector3 fromAnchor = rb.position - rangeAnchor.position;
        float distance = fromAnchor.magnitude;

        if (distance < maxRange)
            return moveDir;

        Vector3 outwardDir = fromAnchor.normalized;
        float outwardComponent = Vector3.Dot(moveDir, outwardDir);

        if (outwardComponent > 0f)
            moveDir -= outwardDir * outwardComponent;

        return moveDir;
    }

    private void OnDrawGizmosSelected()
    {
        if (rangeAnchor == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(rangeAnchor.position, maxRange);
    }
}