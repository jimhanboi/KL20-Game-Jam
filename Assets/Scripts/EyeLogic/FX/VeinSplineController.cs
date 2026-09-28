using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the vein's authoritative target spline from EyeReturnPath's recorded/return data.
/// Owns all curvature, noise, and Catmull-Rom logic - EyeReturnPath knows nothing about this.
/// Feed the result into VeinSpringChain.SetPathTargets() for the physical secondary-motion layer,
/// or read it directly if you don't need the spring/inertia effects.
/// </summary>
public class VeinSplineController : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private EyeReturnPath eyeReturnPath;

    [Header("Curvature")]
    [Tooltip("Should match the roam script's maxRange - normalizes how 'far out' a point is for curvature purposes.")]
    [SerializeField] private float maxExpectedRange = 10f;
    [Tooltip("Lateral wobble amount at the start of a return (path still long/taut).")]
    [SerializeField] private float baseCurveAmount = 0.1f;
    [Tooltip("Lateral wobble amount as the return finishes (path mostly reeled in/slack).")]
    [SerializeField] private float maxCurveAmount = 0.6f;
    [Tooltip("Maps normalized distance-from-anchor (0-1) to a curvature multiplier for that point.")]
    [SerializeField] private AnimationCurve curveByDistance = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private float curveNoiseFrequency = 0.5f;
    [SerializeField] private int splineResolutionPerSegment = 8;

    private float noiseSeed;

    private void Awake()
    {
        noiseSeed = Random.Range(0f, 1000f);
    }

    /// <summary>
    /// Builds a smooth curve through [anchor -> remaining/recorded path -> eye tip].
    /// Curvature grows toward maxCurveAmount as EyeReturnPath.ReturnProgress01 advances,
    /// weighted per-point by distance from the anchor via curveByDistance.
    /// </summary>
    public List<Vector3> BuildSpline()
    {
        if (eyeReturnPath == null)
            return new List<Vector3>();

        List<Vector3> controlPoints = new List<Vector3>();

        if (eyeReturnPath.IsReturning)
        {
            var remaining = eyeReturnPath.RemainingReturnWaypointsNewestToOldest;
            // remaining is newest->oldest; spline should read anchor-end -> eye-end.
            for (int i = remaining.Count - 1; i >= 0; i--)
                controlPoints.Add(remaining[i]);
        }
        else
        {
            controlPoints.AddRange(eyeReturnPath.HistoryOldestToNewest);
        }

        Transform anchor = eyeReturnPath.PlayerAnchor;
        if (anchor != null)
            controlPoints.Insert(0, anchor.position);

        controlPoints.Add(eyeReturnPath.CurrentPosition); // eye tip, always current

        if (controlPoints.Count < 2)
            return controlPoints;

        float curveAmount = Mathf.Lerp(baseCurveAmount, maxCurveAmount, eyeReturnPath.ReturnProgress01);
        return CatmullRomSpline(controlPoints, splineResolutionPerSegment, curveAmount, anchor);
    }

    private List<Vector3> CatmullRomSpline(List<Vector3> pts, int resolution, float curveAmount, Transform anchor)
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
                Vector3 point = CatmullRomPoint(p0, p1, p2, p3, t);

                float distFromAnchor = anchor != null ? Vector3.Distance(point, anchor.position) : 0f;
                float normalizedDist = maxExpectedRange > 0f
                    ? Mathf.Clamp01(distFromAnchor / maxExpectedRange)
                    : 0f;
                float localCurve = curveByDistance.Evaluate(normalizedDist) * curveAmount;

                if (localCurve > 0f)
                {
                    Vector3 tangent = (p2 - p0);
                    if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.forward;
                    tangent.Normalize();

                    Vector3 wobbleAxis = Vector3.Cross(tangent, Vector3.up);
                    if (wobbleAxis.sqrMagnitude < 0.0001f)
                        wobbleAxis = Vector3.Cross(tangent, Vector3.right);
                    wobbleAxis.Normalize();

                    float noise = Mathf.PerlinNoise(noiseSeed + i * 0.37f, t * curveNoiseFrequency + noiseSeed) * 2f - 1f;
                    point += wobbleAxis * noise * localCurve;
                }

                result.Add(point);
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