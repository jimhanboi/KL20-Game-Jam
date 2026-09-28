using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // swap to UnityEngine.Rendering.HighDefinition if you're on HDRP

[RequireComponent(typeof(Rigidbody))]
public class EyeFreeRoamNonVert : MonoBehaviour
{
    [Header("Movement Stats")]
    [SerializeField] private float roamSpeed = 8f;
    [Tooltip("How quickly the eye speeds up while input is held (units/sec^2).")]
    [SerializeField] private float acceleration = 20f;
    [Tooltip("How quickly the eye slows down when there is no input, or the eye is not the selected one (units/sec^2).")]
    [SerializeField] private float deceleration = 12f;
    [SerializeField] private float maxRange = 10f;

    [Header("Jump")]
    [SerializeField] private Key jumpKey = Key.Space;
    [Tooltip("How high the eye rises on a jump, in world units. Launch speed is derived from this and gravity.")]
    [SerializeField] private float jumpHeight = 1.5f;
    [Tooltip("Layers that count as ground.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Extra distance below the collider's bottom that still counts as grounded.")]
    [SerializeField] private float groundCheckDistance = 0.1f;

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

    [Header("Warning / Death")]
    [SerializeField] private float minWarningRange;

    [Header("Vignette Warning")]
    [SerializeField] private Volume vignetteVolume;
    [SerializeField] private float minVignetteIntensity = 0.2f;
    [SerializeField] private float maxVignetteIntensity = 0.6f;
    [Tooltip("Seconds to fade the vignette in after entering the warning range. It scales the distance-driven intensity, so it never caps or delays it.")]
    [SerializeField] private float enterTweenDuration = 0.3f;
    [Tooltip("Shape of the ramp from min to max intensity across the warning range. 1 = linear, below 1 = appears sooner, above 1 = appears later.")]
    [SerializeField] private float rampExponent = 0.5f;

    private Vignette vignette;
    private Coroutine enterTweenRoutine;
    private float warningBlend; // 0..1, eased in on entering the warning range

    private bool underWarning;
    private bool isDead;
    public event Action OnDeath;

    private Rigidbody rb;
    private Vector3 currentVelocity;

    // True only for the eye the player is currently controlling.
    // The component stays enabled while the eye is deployed so it can keep decelerating.
    private bool isControlled;

    public void SetControlled(bool controlled) => isControlled = controlled;

    private Collider col;
    private bool jumpQueued; // set in Update, consumed in FixedUpdate

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        col = GetComponentInChildren<Collider>();

        if (attachedCamera == null)
            attachedCamera = GetComponentInChildren<Camera>();

        if (vignetteVolume != null && vignetteVolume.profile.TryGet(out vignette))
        {
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0f;
        }
    }

    private void OnDisable()
    {
        // Only reached on recall/dock, so don't leave stale motion behind.
        currentVelocity = Vector3.zero;
        isControlled = false;
        jumpQueued = false;
        if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    private void Update()
    {
        if (isDead) return;

        // Input is read here (wasPressedThisFrame is per-frame) and applied in FixedUpdate.
        var kb = Keyboard.current;
        if (isControlled && kb != null && kb[jumpKey].wasPressedThisFrame)
            jumpQueued = true;

        float distance = Vector3.Distance(rangeAnchor.position, rb.position);

        UpdateVignetteIntensity(distance);

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

        if (underWarning)
        {
            Debug.Log("Warning");

        }
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        // Must run before HandleMovement: it reads rb.linearVelocity.y and preserves it.
        HandleJump();
        HandleMovement();
    }

    public void HandleMovement()
    {
        currentVelocity.y = rb.linearVelocity.y;

        Vector3 inputDir = ReadInput();
        Vector3 desiredMoveDir = useCameraRelativeMovement ? CameraRelative(inputDir) : inputDir;

        Vector3 targetVelocity = desiredMoveDir * roamSpeed;
        targetVelocity.y = currentVelocity.y;

        // Speed up while there is input, slow down when there is none.
        bool hasInput = inputDir.sqrMagnitude > 0.0001f;
        float rate = hasInput ? acceleration : deceleration;

        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);

        rb.linearVelocity = currentVelocity;
    }

    private void HandleJump()
    {
        if (!jumpQueued) return;
        jumpQueued = false; // consume the press whether or not the jump succeeds

        if (!isControlled || !IsGrounded()) return;

        // v = sqrt(2 * g * h): the launch speed that peaks at exactly jumpHeight.
        float gravity = Mathf.Abs(Physics.gravity.y);
        float launchSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);

        // Set Y directly; X/Z are left to HandleMovement.
        Vector3 v = rb.linearVelocity;
        v.y = launchSpeed;
        rb.linearVelocity = v;
    }

    public bool IsGrounded()
    {
        if (col == null) return false;

        Bounds b = col.bounds;
        float distance = b.extents.y + groundCheckDistance;

        // The ray starts inside our own collider, so it won't hit it.
        return Physics.Raycast(b.center, Vector3.down, distance, groundMask, QueryTriggerInteraction.Ignore);
    }

    private Vector3 ReadInput()
    {
        if (!isControlled) return Vector3.zero; // not selected -> no input -> decelerates

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

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // Update stops after this, so don't leave the vignette frozen partway through its fade.
        if (enterTweenRoutine != null)
        {
            StopCoroutine(enterTweenRoutine);
            enterTweenRoutine = null;
        }
        warningBlend = 1f;
        if (vignette != null) vignette.intensity.value = maxVignetteIntensity;

        OnDeath?.Invoke();
    }

    private void OnEnterWarning()
    {
        if (enterTweenRoutine != null)
            StopCoroutine(enterTweenRoutine);

        warningBlend = 0f;
        enterTweenRoutine = StartCoroutine(TweenWarningBlend(enterTweenDuration));
    }

    private void OnExitWarning()
    {
        if (enterTweenRoutine != null)
        {
            StopCoroutine(enterTweenRoutine);
            enterTweenRoutine = null;
        }
        warningBlend = 0f;

        if (vignette != null)
            vignette.intensity.value = 0f;
    }

    // Eases the vignette in without ever overriding the distance-driven value.
    private IEnumerator TweenWarningBlend(float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            warningBlend = Mathf.Clamp01(t / duration);
            yield return null;
        }

        warningBlend = 1f;
        enterTweenRoutine = null;
    }

    private void UpdateVignetteIntensity(float distance)
    {
        if (vignette == null) return;

        if (distance <= minWarningRange)
        {
            vignette.intensity.value = 0f;
            return;
        }

        float clampedDistance = Mathf.Min(distance, maxRange);
        float percent = Mathf.InverseLerp(minWarningRange, maxRange, clampedDistance);
        float target = Mathf.Lerp(minVignetteIntensity, maxVignetteIntensity, Mathf.Pow(percent, rampExponent));

        // Always follows distance; the blend only softens the moment of entering the zone.
        vignette.intensity.value = target * warningBlend;
    }
}