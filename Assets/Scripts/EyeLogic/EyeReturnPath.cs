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
    [SerializeField] private Transform playerAnchor;

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

    private bool HasClearLine(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.001f) return true;

        Vector3 dir = delta / distance;
        return !Physics.Raycast(from, dir, distance - raycastSkinWidth, obstacleMask);
    }
}