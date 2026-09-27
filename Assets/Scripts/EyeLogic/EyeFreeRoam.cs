using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class EyeFreeRoam : MonoBehaviour
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
    [SerializeField] private Key ascendKey = Key.E;
    [SerializeField] private Key descendKey = Key.Q;

    [Header("Range Anchor")]
    [Tooltip("The eye's socket/attachment point on the player - range is measured from here.")]
    [SerializeField] private Transform rangeAnchor;

    [Header("Warning / Death")]
    [SerializeField] private float minWarningRange;
    [SerializeField] private Image dyingImg;

    private bool underWarning;
    private bool isDead;
    public event Action OnDeath;

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

    private void Update()
    {
        if (isDead) return;

        float distance = Vector3.Distance(rangeAnchor.position, rb.position);

        UpdateDyingImageAlpha(distance);

        if (!underWarning && distance >= minWarningRange)
        {
            underWarning = true;
            OnEnterWarning();
        }
        else if (underWarning && distance < minWarningRange)
        {
            underWarning = false;
            OnExitWarning();
        }

        if (distance >= maxRange)
        {
            Die();
        }
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        HandleMovement();
    }

    public void HandleMovement()
    {
        Vector3 inputDir = ReadInput();
        Vector3 desiredMoveDir = useCameraRelativeMovement ? CameraRelative(inputDir) : inputDir;

        Vector3 targetVelocity = desiredMoveDir * roamSpeed;
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

        float y = 0f;
        if (kb[ascendKey].isPressed) y += 1f;
        if (kb[descendKey].isPressed) y -= 1f;

        Vector3 dir = new Vector3(x, y, z);
        return dir.sqrMagnitude > 1f ? dir.normalized : dir;
    }

    private Vector3 CameraRelative(Vector3 inputDir)
    {
        if (attachedCamera == null) return inputDir;

        Vector3 camForward = attachedCamera.transform.forward.normalized;
        Vector3 camRight = attachedCamera.transform.right.normalized;

        Vector3 result = camRight * inputDir.x + Vector3.up * inputDir.y + camForward * inputDir.z;
        return result.sqrMagnitude > 1f ? result.normalized : result;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        OnDeath?.Invoke();
    }

    private void OnEnterWarning()
    {
        // Hook for entering the warning zone (SFX, haptics, etc.)
    }

    private void OnExitWarning()
    {
        // Hook for exiting the warning zone back to safety.
    }

    private void UpdateDyingImageAlpha(float distance)
    {
        if (dyingImg == null) return;

        float alpha;
        if (distance <= minWarningRange)
        {
            alpha = 0f;
        }
        else if (distance >= maxRange)
        {
            alpha = 1f;
        }
        else
        {
            alpha = (distance - minWarningRange) / (maxRange - minWarningRange);
        }

        Color c = dyingImg.color;
        c.a = alpha;
        dyingImg.color = c;
    }

    private void OnDrawGizmosSelected()
    {
        if (rangeAnchor == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(rangeAnchor.position, minWarningRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(rangeAnchor.position, maxRange);
    }
}