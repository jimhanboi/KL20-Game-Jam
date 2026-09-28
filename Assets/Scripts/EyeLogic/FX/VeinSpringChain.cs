using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Secondary-motion layer for a vein/tether. Sits on top of an authoritative target path
/// (e.g. VeinSplineController's spline, or raw recorded history) and simulates a coarse
/// Verlet chain that lags behind, overshoots on sudden moves, and settles when the eye stops.
///
/// Usage: call SetPathTargets(...) every frame/FixedUpdate with the current authoritative
/// path (anchor-end -> eye-end, any resolution). This resamples it down to nodeCount targets.
/// Read GetSmoothedPositions() to get the final high-res curve for your mesh/LineRenderer.
/// </summary>
public class VeinSpringChain : MonoBehaviour
{
    [Header("Chain Setup")]
    [Tooltip("Number of physically simulated nodes. Keep this small (10-15) - the visual resolution comes from re-splining afterward, not from node count.")]
    [SerializeField] private int nodeCount = 12;
    [Tooltip("Root of the chain - pinned directly, no spring lag.")]
    [SerializeField] private Transform anchor;
    [Tooltip("Tip of the chain (the eye) - pinned directly, no spring lag.")]
    [SerializeField] private Transform tip;

    [Header("Spring")]
    [SerializeField] private float springStiffness = 45f;
    [Tooltip("Damping applied while the chain is moving fast - keep low for visible jiggle/overshoot.")]
    [SerializeField] private float dampingWhileMoving = 2.5f;
    [Tooltip("Damping applied once the chain's average speed drops below settleSpeedThreshold - keep high so it stops quickly instead of jiggling forever.")]
    [SerializeField] private float dampingWhenSettled = 14f;
    [Tooltip("Average node speed (units/sec) below which the chain is considered 'settling'.")]
    [SerializeField] private float settleSpeedThreshold = 0.15f;
    [Tooltip("How quickly damping blends between the moving/settled values. Higher = snappier transition.")]
    [SerializeField] private float dampingBlendSpeed = 4f;

    [Header("Constraints")]
    [Tooltip("Distance-constraint solve iterations per FixedUpdate. More = stiffer/more rope-like, fewer = looser/springier.")]
    [SerializeField] private int constraintIterations = 4;
    [Tooltip("Allowed stretch multiplier before a constraint kicks in (1 = perfectly rigid segment length).")]
    [SerializeField] private float segmentSlack = 1.02f;

    [Header("Stability")]
    [Tooltip("Clamps how far any single node can move in one FixedUpdate step - prevents the Verlet integration from exploding on a very large yank or a big Time.fixedDeltaTime spike.")]
    [SerializeField] private float maxStepDisplacement = 1.5f;

    [Header("Visual Resample")]
    [SerializeField] private int splineResolutionPerSegment = 6;

    [Header("Wall Collision (overlap-based - resolves resting/pressing against walls, not guaranteed against fast tunneling through thin geometry)")]
    [SerializeField] private LayerMask obstacleMask;
    [Tooltip("Should roughly match the rendered tube's radius plus a small margin, so the visual surface doesn't clip even when the centerline is technically clear.")]
    [SerializeField] private float collisionRadius = 0.1f;
    [SerializeField] private float collisionSkin = 0.01f;
    private readonly Collider[] overlapBuffer = new Collider[8];

    private struct Node
    {
        public Vector3 position;
        public Vector3 previousPosition;
        public Vector3 target;      // authoritative target this node is spring-pulled toward
        public float restLength;    // rest distance to the NEXT node
    }

    private Node[] nodes;
    private float currentDamping;

    private void Awake()
    {
        nodes = new Node[Mathf.Max(nodeCount, 2)];
        currentDamping = dampingWhileMoving;

        Vector3 start = anchor != null ? anchor.position : transform.position;
        Vector3 end = tip != null ? tip.position : transform.position;

        for (int i = 0; i < nodes.Length; i++)
        {
            float t = i / (float)(nodes.Length - 1);
            Vector3 p = Vector3.Lerp(start, end, t);
            nodes[i] = new Node
            {
                position = p,
                previousPosition = p,
                target = p,
                restLength = 0f
            };
        }

        RecalculateRestLengths();
    }

    /// <summary>
    /// Feed the current authoritative path (any resolution, ordered anchor-end -> eye-end).
    /// This resamples it evenly by arc length into per-node targets. Call once per frame
    /// before/at the same cadence as your FixedUpdate physics step.
    /// </summary>
    public void SetPathTargets(IReadOnlyList<Vector3> authoritativePath)
    {
        if (authoritativePath == null || authoritativePath.Count < 2 || nodes == null)
            return;

        float totalLength = 0f;
        for (int i = 1; i < authoritativePath.Count; i++)
            totalLength += Vector3.Distance(authoritativePath[i - 1], authoritativePath[i]);

        if (totalLength <= 0.0001f)
        {
            for (int i = 0; i < nodes.Length; i++)
                nodes[i].target = authoritativePath[0];
            return;
        }

        for (int n = 0; n < nodes.Length; n++)
        {
            float targetDist = totalLength * (n / (float)(nodes.Length - 1));
            nodes[n].target = SampleAtArcLength(authoritativePath, targetDist);
        }
    }

    private Vector3 SampleAtArcLength(IReadOnlyList<Vector3> path, float targetDist)
    {
        float accumulated = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            float segLength = Vector3.Distance(path[i - 1], path[i]);
            if (accumulated + segLength >= targetDist || i == path.Count - 1)
            {
                float remaining = targetDist - accumulated;
                float t = segLength > 0.0001f ? Mathf.Clamp01(remaining / segLength) : 0f;
                return Vector3.Lerp(path[i - 1], path[i], t);
            }
            accumulated += segLength;
        }
        return path[path.Count - 1];
    }

    private void FixedUpdate()
    {
        if (nodes == null || nodes.Length < 2) return;

        float dt = Time.fixedDeltaTime;

        // Pin the ends directly to the live transforms - no spring lag on anchor/tip themselves,
        // all the jiggle happens in the middle nodes.
        if (anchor != null)
        {
            nodes[0].previousPosition = nodes[0].position;
            nodes[0].position = anchor.position;
        }
        if (tip != null)
        {
            int last = nodes.Length - 1;
            nodes[last].previousPosition = nodes[last].position;
            nodes[last].position = tip.position;
        }

        UpdateAdaptiveDamping(dt);
        IntegrateSpring(dt);
        SolveDistanceConstraints();
        ResolveWallCollisions();
    }

    /// <summary>
    /// Pushes free (non-pinned) nodes back out of any overlapping wall geometry.
    /// Overlap-based: reliably resolves shallow penetration/resting-against-walls, but
    /// can miss a node that fully tunnels through thin geometry in a single fast step -
    /// that would need a sweep (SphereCast) check instead, which costs more per node.
    /// </summary>
    private void ResolveWallCollisions()
    {
        if (obstacleMask == 0) return;

        for (int i = 1; i < nodes.Length - 1; i++) // skip pinned anchor (0) and tip (last)
        {
            Vector3 pos = nodes[i].position;
            int hitCount = Physics.OverlapSphereNonAlloc(pos, collisionRadius, overlapBuffer, obstacleMask);

            for (int h = 0; h < hitCount; h++)
            {
                Collider col = overlapBuffer[h];
                if (col == null) continue;

                Vector3 closest = col.ClosestPoint(pos);
                Vector3 diff = pos - closest;
                float dist = diff.magnitude;

                if (dist < collisionRadius)
                {
                    Vector3 pushDir = dist > 0.0001f ? diff / dist : Vector3.up;
                    pos = closest + pushDir * (collisionRadius + collisionSkin);
                }
            }

            nodes[i].position = pos;
        }
    }

    private void UpdateAdaptiveDamping(float dt)
    {
        float avgSpeed = 0f;
        for (int i = 0; i < nodes.Length; i++)
            avgSpeed += Vector3.Distance(nodes[i].position, nodes[i].previousPosition) / Mathf.Max(dt, 0.0001f);
        avgSpeed /= nodes.Length;

        float targetDamping = avgSpeed > settleSpeedThreshold ? dampingWhileMoving : dampingWhenSettled;
        currentDamping = Mathf.Lerp(currentDamping, targetDamping, dampingBlendSpeed * dt);
    }

    private void IntegrateSpring(float dt)
    {
        // Skip index 0 and last - those are pinned directly above.
        for (int i = 1; i < nodes.Length - 1; i++)
        {
            Node n = nodes[i];

            Vector3 velocity = (n.position - n.previousPosition) * Mathf.Clamp01(1f - currentDamping * dt);
            Vector3 springAccel = (n.target - n.position) * springStiffness;

            Vector3 nextPos = n.position + velocity + springAccel * dt * dt;

            Vector3 displacement = nextPos - n.position;
            if (displacement.magnitude > maxStepDisplacement)
                displacement = displacement.normalized * maxStepDisplacement;

            n.previousPosition = n.position;
            n.position = n.position + displacement;
            nodes[i] = n;
        }
    }

    private void SolveDistanceConstraints()
    {
        for (int iter = 0; iter < constraintIterations; iter++)
        {
            for (int i = 0; i < nodes.Length - 1; i++)
            {
                Vector3 a = nodes[i].position;
                Vector3 b = nodes[i + 1].position;
                float rest = nodes[i].restLength * segmentSlack;

                Vector3 delta = b - a;
                float dist = delta.magnitude;
                if (dist < 0.0001f || dist <= rest) continue;

                float error = dist - rest;
                Vector3 correction = delta.normalized * error;

                bool aPinned = i == 0;
                bool bPinned = i + 1 == nodes.Length - 1;

                if (aPinned && bPinned) continue;
                if (aPinned) { nodes[i + 1].position = b - correction; continue; }
                if (bPinned) { nodes[i].position = a + correction; continue; }

                nodes[i].position = a + correction * 0.5f;
                nodes[i + 1].position = b - correction * 0.5f;
            }
        }
    }

    private void RecalculateRestLengths()
    {
        for (int i = 0; i < nodes.Length - 1; i++)
            nodes[i].restLength = Vector3.Distance(nodes[i].position, nodes[i + 1].position);
    }

    /// <summary>Raw simulated node positions (anchor -> eye), no re-splining.</summary>
    public Vector3[] GetNodePositions()
    {
        Vector3[] result = new Vector3[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
            result[i] = nodes[i].position;
        return result;
    }

    /// <summary>Smoothed high-resolution curve through the current (settled/overshooting) node positions - feed this to your mesh/LineRenderer.</summary>
    public List<Vector3> GetSmoothedPositions()
    {
        List<Vector3> pts = new List<Vector3>(nodes.Length);
        for (int i = 0; i < nodes.Length; i++)
            pts.Add(nodes[i].position);

        return CatmullRomSpline(pts, splineResolutionPerSegment);
    }

    private List<Vector3> CatmullRomSpline(List<Vector3> pts, int resolution)
    {
        List<Vector3> result = new List<Vector3>();
        int count = pts.Count;
        if (count < 2) return new List<Vector3>(pts);

        for (int i = 0; i < count - 1; i++)
        {
            Vector3 p0 = pts[Mathf.Max(i - 1, 0)];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[Mathf.Min(i + 1, count - 1)];
            Vector3 p3 = pts[Mathf.Min(i + 2, count - 1)];

            for (int j = 0; j < resolution; j++)
            {
                float t = j / (float)resolution;
                result.Add(CatmullRomPoint(p0, p1, p2, p3, t));
            }
        }

        result.Add(pts[count - 1]);
        return result;
    }

    private Vector3 CatmullRomPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }
}