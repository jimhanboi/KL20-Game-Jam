using System;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Rigidbody))]
public class EyeReturnPath : MonoBehaviour
{
    [Header("Return Movement")]
    [SerializeField] private float returnSpeed = 12f;
    [SerializeField] private float waypointReachThreshold = 0.3f;
    [Tooltip("Live socket on the player - final destination once the path is exhausted.")]
    [SerializeField] private Transform playerAnchor;

    [Header("Return Rotation")]
    [Tooltip("Degrees per second the eye straightens out toward the socket's orientation while returning.")]
    [SerializeField] private float rotationReturnSpeed = 180f;

    [Header("Sampling")]
    [Tooltip("Only record a new point after moving at least this far from the last one. No simplification is applied afterward - every sampled point is kept for accurate path following.")]
    [SerializeField] private float minSampleDistance = 0.5f;

    private Rigidbody rb;

    // Full-resolution recorded path, oldest (near anchor/start) -> newest (current eye position).
    private readonly List<Vector3> recordedPath = new List<Vector3>();

    // Built once on BeginReturn: newest -> oldest. Consumed from the front as waypoints are reached.
    private List<Vector3> returnWaypoints;
    private int returnPathStartCount;

    private bool isRecording;
    private bool isReturning;

    public bool IsRecording => isRecording;
    public bool IsReturning => isReturning;
    public event Action OnReturnComplete;

    // --- Read-only data for external consumers (e.g. a spline/vein controller) ---
    public Transform PlayerAnchor => playerAnchor;
    public Vector3 CurrentPosition => rb.position;

    /// <summary>Full recorded history while roaming, oldest -> newest. Empty during/after a return.</summary>
    public IReadOnlyList<Vector3> HistoryOldestToNewest => recordedPath;

    /// <summary>Remaining waypoints during a return, newest (near eye) -> oldest (near anchor). Shrinks toward empty.</summary>
    public IReadOnlyList<Vector3> RemainingReturnWaypointsNewestToOldest => returnWaypoints;

    public int RemainingReturnPoints => returnWaypoints?.Count ?? 0;
    public int ReturnPathStartCount => returnPathStartCount;

    /// <summary>0 at the start of a return, 1 once every waypoint has been consumed.</summary>
    public float ReturnProgress01 =>
        isReturning && returnPathStartCount > 0
            ? 1f - (float)RemainingReturnPoints / returnPathStartCount
            : 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>Call when roaming starts. Seed with the eye's origin (e.g. the socket it was thrown from).</summary>
    public void StartTracking(Vector3 startPosition)
    {
        recordedPath.Clear();
        recordedPath.Add(startPosition);
        isRecording = true;
    }

    private void Update()
    {
        if (!isRecording) return;

        Vector3 pos = transform.position;
        if (Vector3.Distance(pos, recordedPath[recordedPath.Count - 1]) >= minSampleDistance)
            recordedPath.Add(pos);
    }

    /// <summary>Stops recording and starts walking back along the exact recorded path (no simplification).</summary>
    public void BeginReturn()
    {
        isRecording = false;

        returnWaypoints = new List<Vector3>(recordedPath);
        returnWaypoints.Reverse(); // newest (near current position) -> oldest (near anchor)

        // Drop the point nearest the eye's current position - redundant with rb.position itself.
        if (returnWaypoints.Count > 0)
            returnWaypoints.RemoveAt(0);

        returnPathStartCount = returnWaypoints.Count;
        isReturning = true;
    }

    private void FixedUpdate()
    {
        if (!isReturning) return;

        Vector3 target = CurrentTarget();
        Vector3 toTarget = target - rb.position;
        float distance = toTarget.magnitude;

        if (distance <= waypointReachThreshold)
        {
            if (returnWaypoints.Count > 0)
            {
                // Passed this waypoint - remove it from the front so the remaining/visible
                // path shrinks as the eye is reeled in.
                returnWaypoints.RemoveAt(0);
                return;
            }

            rb.linearVelocity = Vector3.zero;
            isReturning = false;
            OnReturnComplete?.Invoke();
            return;
        }

        Vector3 dir = toTarget / distance;
        rb.linearVelocity = dir * returnSpeed;

        Quaternion targetRotation = playerAnchor != null ? playerAnchor.rotation : Quaternion.identity;
        Quaternion newRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, rotationReturnSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(newRotation);
    }

    private Vector3 CurrentTarget()
    {
        if (returnWaypoints.Count > 0)
            return returnWaypoints[0];

        return playerAnchor != null ? playerAnchor.position : rb.position;
    }
}