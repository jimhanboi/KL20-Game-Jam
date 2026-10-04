using System;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Rigidbody))]
public class EyeReturnPath : MonoBehaviour
{
    [Header("Return Movement")]
    [SerializeField] private float returnSpeed = 12f;
    [SerializeField] private float waypointReachThreshold = 0.3f;
    [Tooltip("Live socket on the player - final destination once recorded waypoints are exhausted.")]
    [SerializeField] public Transform playerAnchor;

    [Header("Return Rotation")]
    [Tooltip("Degrees per second the eye straightens out toward the socket's orientation while returning.")]
    [SerializeField] private float rotationReturnSpeed = 180f;

    [Header("Sampling")]
    [Tooltip("Only record a new point after moving at least this far from the last one.")]
    [SerializeField] private float minSampleDistance = 0.5f;

    [Header("Line-of-Sight Simplification")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float raycastSkinWidth = 0.05f;

    private Rigidbody rb;
    private readonly List<Vector3> recordedPath = new List<Vector3>();
    private List<Vector3> activeWaypoints;
    private int currentIndex;

    private bool isRecording;
    private bool isReturning;

    public bool IsRecording => isRecording;
    public bool IsReturning => isReturning;
    public event Action OnReturnComplete;

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

    /// <summary>Stops recording, simplifies the recorded path, and starts moving back along it.</summary>
    public void BeginReturn()
    {
        isRecording = false;

        activeWaypoints = BuildSimplifiedReturnPath();
        if (activeWaypoints.Count > 0)
            activeWaypoints.RemoveAt(activeWaypoints.Count - 1); // drop static endpoint - chase live anchor instead

        currentIndex = 0;
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
            if (currentIndex < activeWaypoints.Count)
            {
                currentIndex++;
                return;
            }

            rb.linearVelocity = Vector3.zero;
            isReturning = false;
            OnReturnComplete?.Invoke();
            return;
        }

        Vector3 dir = toTarget / distance;
        rb.linearVelocity = dir * returnSpeed;

        // Ease rotation back toward the socket's orientation (0,0,0 relative to the
        // socket) over the course of the return, rather than snapping it on arrival.
        Quaternion targetRotation = playerAnchor != null ? playerAnchor.rotation : Quaternion.identity;
        Quaternion newRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, rotationReturnSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(newRotation);
    }

    private Vector3 CurrentTarget()
    {
        if (currentIndex < activeWaypoints.Count)
            return activeWaypoints[currentIndex];

        return playerAnchor != null ? playerAnchor.position : rb.position;
    }

    private List<Vector3> BuildSimplifiedReturnPath()
    {
        List<Vector3> reversed = new List<Vector3>(recordedPath);
        reversed.Reverse();

        if (reversed.Count <= 2)
            return reversed;

        List<Vector3> simplified = new List<Vector3> { reversed[0] };
        int i = 0;

        while (i < reversed.Count - 1)
        {
            int farthestVisible = -1;
            for (int j = reversed.Count - 1; j > i; j--)
            {
                if (HasClearLine(reversed[i], reversed[j]))
                {
                    farthestVisible = j;
                    break;
                }
            }

            i = farthestVisible == -1 ? i + 1 : farthestVisible;
            simplified.Add(reversed[i]);
        }

        return simplified;
    }

    /// <summary>Fills dest with the route the eye is on, from the anchor to the eye. Used by the vein.</summary>
    public void GetRoutePoints(List<Vector3> dest)
    {
        dest.Clear();
        dest.Add(playerAnchor != null ? playerAnchor.position
               : recordedPath.Count > 0 ? recordedPath[0] : rb.position);

        if (isReturning && activeWaypoints != null)
        {
            // activeWaypoints runs eye -> anchor; the vein wants anchor -> eye.
            for (int i = activeWaypoints.Count - 1; i >= currentIndex; i--)
                dest.Add(activeWaypoints[i]);
        }
        else if (isRecording)
        {
            for (int i = 1; i < recordedPath.Count; i++)
                dest.Add(recordedPath[i]);
        }

        dest.Add(rb.position);
    }


    private bool HasClearLine(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.001f) return true;

        Vector3 dir = delta / distance;
        return !Physics.Raycast(from, dir, distance - raycastSkinWidth, obstacleMask);
    }
}