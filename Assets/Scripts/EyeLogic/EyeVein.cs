using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual-only vein between the player's socket and the eye.
///
/// The rope is guided by the route the eye actually took (EyeReturnPath.GetRoutePoints), so it
/// can wrap around buildings and corners instead of trying to stretch in a straight line.
/// Verlet physics, collision and smoothing then act on top of that route.
/// It never pushes the eye; it only reads positions.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class EyeVein : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Reads playerAnchor and the route live from here. Leave empty to find it on this object or a parent.")]
    [SerializeField] private EyeReturnPath eye;
    [Tooltip("Optional. If set, the vein only shows while the eye is deployed or returning.")]
    [SerializeField] private EyeFreeRoamNonVert roam;
    [Tooltip("Optional point at the back of the eye. Falls back to the eye's transform.")]
    [SerializeField] private Transform eyeEnd;

    [Header("Shape")]
    [Tooltip("Target distance between points. Smaller = harder to clip and smoother, but costlier.")]
    [SerializeField, Min(0.05f)] private float pointSpacing = 0.25f;
    [SerializeField, Min(3)] private int minPoints = 8;
    [Tooltip("Hard cap. If the route gets so long it hits this, spacing grows past pointSpacing.")]
    [SerializeField, Min(4)] private int maxPoints = 80;
    [Tooltip("Always use Max Points and never resize. Costs more on short lengths but removes resize pops entirely. Handy for testing.")]
    [SerializeField] private bool fixedPointCount = false;
    [Tooltip("Extra rope beyond the route length, as a fraction. 0.05 = 5% slack for a little sag.")]
    [SerializeField, Min(0f)] private float extraLength = 0.05f;
    [SerializeField] private float minLength = 0.5f;
    [Tooltip("How quickly the rope shortens when the route shortens (per second). It lengthens instantly.")]
    [SerializeField] private float lengthSmoothing = 8f;

    [Header("Route Following")]
    [Tooltip("How strongly each point is pulled toward the eye's route each physics step. Higher = tighter to the route, lower = floppier.")]
    [SerializeField, Range(0f, 1f)] private float routeStiffness = 0.25f;
    [Tooltip("Smooths kinks and zigzags between neighbouring points. 0 = off.")]
    [SerializeField, Range(0f, 0.5f)] private float bendSmoothing = 0.2f;

    [Header("Physics")]
    [SerializeField] private Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    [SerializeField, Range(0.8f, 1f)] private float damping = 0.97f;
    [Tooltip("Iterations = point count * this, clamped to the min/max below.")]
    [SerializeField, Min(0.01f)] private float iterationsPerPoint = 0.25f;
    [SerializeField, Min(1)] private int minIterations = 4;
    [SerializeField, Min(1)] private int maxIterations = 16;

    [Header("Collision")]
    [Tooltip("Must NOT include the eye or player layers.")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [Tooltip("Visual radius of the vein (line width = radius * 2, tube radius = radius).")]
    [SerializeField, Min(0.001f)] private float radius = 0.05f;
    [Tooltip("Extra radius for collision casts only. Cast radius = radius + this. Keeps the vein off surfaces.")]
    [SerializeField, Min(0f)] private float collisionPadding = 0.05f;
    [Tooltip("Gap kept between the vein and surfaces. Too small and SphereCast stops seeing the surface (it ignores anything it starts inside), which causes flicker.")]
    [SerializeField, Min(0.001f)] private float skin = 0.015f;
    [SerializeField, Range(0f, 1f)] private float friction = 0.3f;
    [Tooltip("Also test the gaps between points. It snaps points to the near side of a wall, which can pop when a segment grazes an edge. Route following mostly makes it unnecessary.")]
    [SerializeField] private bool collideSegments = false;

    [Header("Rendering")]
    [Tooltip("Turn off when using EyeVeinTube to draw a real tube mesh instead.")]
    [SerializeField] private bool drawLine = true;
    [Tooltip("The drawn ends are pinned to the live transforms. This is how far along the rope (world units) that correction fades out.")]
    [SerializeField, Min(0.1f)] private float endBlendLength = 0.75f;

    // ---- Read-only API for EyeVeinTube ----
    public int PointCount => pointCount;
    public int MaxPoints => maxPoints;
    public float Radius => radius;
    public bool IsShowing => wasActive && IsActive();

    /// <summary>
    /// Copies the points to draw into dest (length >= PointCount). The physics runs at a fixed rate,
    /// so this interpolates between the last two steps (removes stepping jitter) and then pulls both
    /// ends onto the live transforms. That correction only fades in over the last few points
    /// (endBlendLength); spreading it along the whole rope would shift the body, and push it
    /// through walls, every time the physics step and the render frame disagree.
    /// </summary>
    public void CopyPoints(Vector3[] dest)
    {
        int last = pointCount - 1;
        float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
        for (int i = 0; i <= last; i++)
            dest[i] = Vector3.Lerp(renderPrev[i], pos[i], alpha);

        Vector3 dA = Anchor.position - dest[0];
        Vector3 dB = EyePosLive - dest[last];

        float spacing = Mathf.Max(length / last, 1e-4f);
        float blendPoints = Mathf.Max(1f, endBlendLength / spacing);
        int n = Mathf.Min(Mathf.CeilToInt(blendPoints), last);
        for (int k = 0; k <= n; k++)
        {
            float w = 1f - k / blendPoints;
            if (w <= 0f) break;
            w *= w;
            dest[k] += dA * w;
            dest[last - k] += dB * w;
        }
    }

    private LineRenderer line;
    private Vector3[] pos, prev, safe, tmpPos, tmpPrev, renderPrev, drawBuffer;
    private int pointCount;
    private float length;
    private Transform lastAnchor;
    private bool wasActive;
    private Rigidbody eyeRb;
    private Vector3 eyeEndLocal;

    private readonly List<Vector3> route = new List<Vector3>(128);
    private float[] routeCum = new float[128];
    private float routeLength;

    private float CastRadius => radius + collisionPadding;
    private Transform Anchor => eye != null ? eye.playerAnchor : null;
    // Physics pose of the eye end. Used by the sim so it shares a timeline with the interpolated drawing.
    private Vector3 EyePos => eyeRb != null
        ? eyeRb.position + eyeRb.rotation * Vector3.Scale(eyeEndLocal, eye.transform.lossyScale)
        : EyePosLive;
    // What is on screen right now (interpolated if the eye's Rigidbody interpolates).
    private Vector3 EyePosLive => eyeEnd != null ? eyeEnd.position : eye.transform.position;

    private bool IsActive()
    {
        if (eye == null || Anchor == null) return false;
        if (roam == null) return true;
        return roam.enabled || eye.IsReturning;
    }

    private int DesiredCount(float ropeLength)
    {
        if (fixedPointCount) return maxPoints;
        return Mathf.Clamp(Mathf.CeilToInt(ropeLength / pointSpacing) + 1, minPoints, maxPoints);
    }

    private void Awake()
    {
        if (eye == null) eye = GetComponentInParent<EyeReturnPath>();
        if (roam == null && eye != null) roam = eye.GetComponent<EyeFreeRoamNonVert>();

        if (eye != null)
        {
            eyeRb = eye.GetComponent<Rigidbody>();
            if (eyeEnd != null) eyeEndLocal = eye.transform.InverseTransformPoint(eyeEnd.position);
        }

        maxPoints = Mathf.Max(maxPoints, minPoints);
        pointCount = minPoints;

        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = pointCount;
        ApplyRadius();

        // Buffers are sized once for the worst case, so changing the point count never allocates.
        pos = new Vector3[maxPoints];
        prev = new Vector3[maxPoints];
        safe = new Vector3[maxPoints];
        tmpPos = new Vector3[maxPoints];
        tmpPrev = new Vector3[maxPoints];
        renderPrev = new Vector3[maxPoints];
        drawBuffer = new Vector3[pointCount];
    }

    // The width curve (set in the inspector) still shapes the line, e.g. a taper, on top of this.
    private void ApplyRadius()
    {
        if (line != null) line.widthMultiplier = radius * 2f;
    }

    // Lets you tweak the radius in the inspector during Play mode and see it live.
    private void OnValidate()
    {
        maxPoints = Mathf.Max(maxPoints, minPoints);
        maxIterations = Mathf.Max(maxIterations, minIterations);
        if (line == null) line = GetComponent<LineRenderer>();
        ApplyRadius();
    }

    private void OnEnable()
    {
        wasActive = false; // forces a clean Snap on the next FixedUpdate
    }

    // ---------------------------------------------------------------- route

    // Anchor -> ... -> eye. Falls back to a straight line if the eye has no route yet.
    private void BuildRoute()
    {
        route.Clear();
        if (eye != null) eye.GetRoutePoints(route);
        if (route.Count < 2)
        {
            route.Clear();
            route.Add(Anchor.position);
            route.Add(EyePos);
        }
        route[0] = Anchor.position;
        route[route.Count - 1] = EyePos;

        if (routeCum.Length < route.Count) routeCum = new float[route.Count * 2];
        routeCum[0] = 0f;
        for (int i = 1; i < route.Count; i++)
            routeCum[i] = routeCum[i - 1] + Vector3.Distance(route[i - 1], route[i]);
        routeLength = routeCum[route.Count - 1];
    }

    // Point at arc-length s along the route. seg is a cursor, so calling this with
    // increasing s walks the route once instead of searching from the start every time.
    private Vector3 PointOnRoute(float s, ref int seg)
    {
        while (seg < route.Count - 2 && routeCum[seg + 1] < s) seg++;
        float segLen = routeCum[seg + 1] - routeCum[seg];
        float t = segLen > 1e-5f ? Mathf.Clamp01((s - routeCum[seg]) / segLen) : 0f;
        return Vector3.Lerp(route[seg], route[seg + 1], t);
    }

    /// <summary>Resets the vein onto the current route. Call right after StartTracking when the eye is thrown.</summary>
    public void Snap()
    {
        Transform anchor = Anchor;
        if (anchor == null || eye == null || pos == null) return;

        lastAnchor = anchor;
        BuildRoute();
        length = Mathf.Max(minLength, routeLength * (1f + extraLength));
        pointCount = DesiredCount(length);

        int seg = 0;
        for (int i = 0; i < pointCount; i++)
            pos[i] = prev[i] = PointOnRoute(routeLength * i / (pointCount - 1f), ref seg);

        System.Array.Copy(pos, renderPrev, pointCount);
    }

    // Changes the point count while keeping the rope's current shape and motion.
    private void Resize(int newCount)
    {
        int oldCount = pointCount;
        for (int k = 0; k < newCount; k++)
        {
            float f = k / (newCount - 1f) * (oldCount - 1);
            int i0 = Mathf.Min(Mathf.FloorToInt(f), oldCount - 2);
            float t = f - i0;
            tmpPos[k] = Vector3.Lerp(pos[i0], pos[i0 + 1], t);
            tmpPrev[k] = Vector3.Lerp(prev[i0], prev[i0 + 1], t);
        }

        // Lerping between points cuts corners, which can drop a point inside a wall. SphereCast
        // ignores anything it starts inside, so such a point would never be pushed back out.
        // Put those points on the route instead, which is known to be clear.
        int seg = 0;
        for (int k = 1; k < newCount - 1; k++)
        {
            if (!Physics.CheckSphere(tmpPos[k], CastRadius, collisionMask, QueryTriggerInteraction.Ignore))
                continue;
            tmpPos[k] = tmpPrev[k] = PointOnRoute(routeLength * k / (newCount - 1f), ref seg);
        }

        System.Array.Copy(tmpPos, pos, newCount);
        System.Array.Copy(tmpPrev, prev, newCount);
        pointCount = newCount;
    }

    // ------------------------------------------------------------ simulation

    private void FixedUpdate()
    {
        if (!IsActive()) { wasActive = false; return; }

        Transform anchor = Anchor;
        if (!wasActive || anchor != lastAnchor)
        {
            Snap();
            wasActive = true;
        }

        float dt = Time.fixedDeltaTime;
        Vector3 a = anchor.position, b = EyePos;

        BuildRoute();

        // Rope length follows the route. It grows instantly (so it never lags behind the eye)
        // and shrinks smoothly. It can never be shorter than the straight distance.
        float straight = Vector3.Distance(a, b);
        float target = Mathf.Max(minLength, routeLength * (1f + extraLength));
        length = target > length ? target : Mathf.Lerp(length, target, 1f - Mathf.Exp(-lengthSmoothing * dt));
        length = Mathf.Max(length, straight);

        // Point count follows length, but only changes on a ~20% difference. Every resize
        // resamples the whole rope, so it should be rare rather than every half metre.
        int desired = DesiredCount(length);
        if (Mathf.Abs(desired - pointCount) >= Mathf.Max(3, pointCount / 5)) Resize(desired);

        int last = pointCount - 1;
        int iterations = Mathf.Clamp(Mathf.CeilToInt(pointCount * iterationsPerPoint), minIterations, maxIterations);
        float segLen = length / last;

        // The renderer interpolates from this state to the one we are about to compute.
        System.Array.Copy(pos, renderPrev, pointCount);

        pos[0] = a;
        pos[last] = b;

        // 1) Integrate, with a swept collision so fast motion can't tunnel.
        for (int i = 1; i < last; i++)
        {
            Vector3 from = pos[i];
            Vector3 vel = (pos[i] - prev[i]) * damping;
            prev[i] = pos[i];
            pos[i] += vel + gravity * (dt * dt);
            Collide(i, from);
        }

        // 2) Pull each point toward where the eye's route says it should be.
        if (routeStiffness > 0f)
        {
            int seg = 0;
            for (int i = 1; i < last; i++)
            {
                Vector3 guide = PointOnRoute(routeLength * i / last, ref seg);
                Vector3 from = pos[i];
                pos[i] = Vector3.Lerp(from, guide, routeStiffness);
                Collide(i, from);
            }
        }

        // 3) Smooth out kinks and zigzags (uses a copy, so the order of points doesn't matter).
        if (bendSmoothing > 0f)
        {
            System.Array.Copy(pos, safe, pointCount);
            for (int i = 1; i < last; i++)
            {
                pos[i] = Vector3.Lerp(safe[i], (safe[i - 1] + safe[i + 1]) * 0.5f, bendSmoothing);
                Collide(i, safe[i]);
            }
        }

        // 4) Distance constraints, with collision resolved after every pass so the
        //    constraints can't drag points back into walls.
        for (int it = 0; it < iterations; it++)
        {
            pos[0] = a;
            pos[last] = b;
            System.Array.Copy(pos, safe, pointCount);

            for (int i = 0; i < last; i++)
            {
                Vector3 d = pos[i + 1] - pos[i];
                float dist = d.magnitude;
                if (dist < 1e-5f) continue;

                Vector3 corr = d * ((dist - segLen) / dist);
                float w0 = i == 0 ? 0f : 1f;                    // pinned ends don't move
                float w1 = i + 1 == last ? 0f : 1f;
                float total = w0 + w1;
                if (total == 0f) continue;

                pos[i] += corr * (w0 / total);
                pos[i + 1] -= corr * (w1 / total);
            }

            for (int i = 1; i < last; i++)
                Collide(i, safe[i]);
        }

        // 5) Final pass: catch segments that cross a wall between two valid points.
        if (collideSegments) CollideSegments();
    }

    // Sweeps point i from a known-good position to where it wants to be.
    private void Collide(int i, Vector3 from)
    {
        Vector3 to = pos[i];
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 1e-5f) return;

        Vector3 dir = delta / dist;
        if (!Physics.SphereCast(from, CastRadius, dir, out RaycastHit hit, dist,
                collisionMask, QueryTriggerInteraction.Ignore))
            return;

        if (hit.distance <= 0f) { pos[i] = from; return; }   // started overlapping; stay put

        pos[i] = from + dir * hit.distance + hit.normal * skin;

        // Kill the velocity into the surface, and apply friction to what remains.
        Vector3 vel = pos[i] - prev[i];
        vel = Vector3.ProjectOnPlane(vel, hit.normal) * (1f - friction);
        prev[i] = pos[i] - vel;
    }

    // Tests the gap between each pair of points. If something is in the way, the free point
    // on the far side is pulled back to this side of it. A pinned end is never moved.
    private void CollideSegments()
    {
        float r = CastRadius;
        int last = pointCount - 1;

        for (int i = 0; i < last; i++)
        {
            Vector3 from = pos[i], to = pos[i + 1];
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 1e-5f) continue;
            Vector3 dir = d / dist;

            bool nextFree = i + 1 != last;
            bool prevFree = i != 0;

            if (nextFree
                && Physics.SphereCast(from, r, dir, out RaycastHit h1, dist, collisionMask, QueryTriggerInteraction.Ignore)
                && h1.distance > 0f)
            {
                pos[i + 1] = from + dir * h1.distance + h1.normal * skin;
                prev[i + 1] = pos[i + 1];                       // no leftover velocity into the wall
            }
            else if (prevFree
                && Physics.SphereCast(to, r, -dir, out RaycastHit h2, dist, collisionMask, QueryTriggerInteraction.Ignore)
                && h2.distance > 0f)
            {
                pos[i] = to - dir * h2.distance + h2.normal * skin;
                prev[i] = pos[i];
            }
        }
    }

    // ------------------------------------------------------------- rendering

    private void LateUpdate()
    {
        bool show = IsShowing;
        line.enabled = show && drawLine;
        if (!show || !drawLine) return;

        if (drawBuffer.Length != pointCount)
        {
            drawBuffer = new Vector3[pointCount];            // only when the count changes
            line.positionCount = pointCount;
        }

        CopyPoints(drawBuffer);
        line.SetPositions(drawBuffer);
    }

    // Select the vein in Play mode to see the route it is following (yellow).
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || route.Count < 2) return;
        Gizmos.color = Color.yellow;
        for (int i = 1; i < route.Count; i++)
            Gizmos.DrawLine(route[i - 1], route[i]);
    }
}