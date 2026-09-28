using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates an actual volumetric tube mesh along the vein's current point list (rebuilt every
/// frame, since the point count/positions change as the vein moves, grows, and reels in).
/// Reads from VeinSpringChain (animated/settled positions - recommended) or falls back to
/// VeinSplineController's raw target spline if you want to preview without the spring layer.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VeinTubeMeshRenderer : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private VeinSpringChain springChain;
    [SerializeField] private VeinSplineController splineController;
    [Tooltip("True: use the animated/settled spring chain output. False: use the raw target spline directly (no inertia) - useful for isolating rendering bugs from physics bugs.")]
    [SerializeField] private bool useSpringChain = true;

    [Header("Tube Shape")]
    [SerializeField] private float radius = 0.05f;
    [SerializeField, Range(3, 24)] private int radialSegments = 8;
    [Tooltip("Radius multiplier along the tube's length: time 0 = anchor end, time 1 = eye-tip end.")]
    [SerializeField] private AnimationCurve radiusByLength = AnimationCurve.Constant(0f, 1f, 1f);
    [SerializeField] private bool capEnds = true;

    [Header("Update")]
    [Tooltip("1 = rebuild every frame. Raise this to throttle mesh rebuilds if a very long vein ever shows up in profiling.")]
    [SerializeField] private int rebuildEveryNFrames = 1;

    private Mesh mesh;
    private MeshFilter meshFilter;
    private int frameCounter;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        mesh = new Mesh { name = "VeinTubeMesh" };
        mesh.MarkDynamic();
        meshFilter.mesh = mesh;
    }

    private void LateUpdate()
    {
        frameCounter++;
        if (frameCounter % Mathf.Max(rebuildEveryNFrames, 1) != 0) return;

        List<Vector3> points = GetSourcePoints();
        if (points == null || points.Count < 2)
        {
            mesh.Clear();
            return;
        }

        RebuildMesh(points);
    }

    private List<Vector3> GetSourcePoints()
    {
        if (useSpringChain && springChain != null)
            return springChain.GetSmoothedPositions();
        if (splineController != null)
            return splineController.BuildSpline();
        return null;
    }

    private void RebuildMesh(List<Vector3> worldPoints)
    {
        int count = worldPoints.Count;

        // Mesh.vertices is always local-space relative to this GameObject's transform -
        // unlike LineRenderer there is no "world space" toggle, so world-space input points
        // must be converted here or the whole mesh renders offset by this object's transform.
        Vector3[] points = new Vector3[count];
        for (int i = 0; i < count; i++)
            points[i] = transform.InverseTransformPoint(worldPoints[i]);

        Vector3[] tangents = new Vector3[count];
        Vector3[] normals = new Vector3[count];
        Vector3[] binormals = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            Vector3 prev = points[Mathf.Max(i - 1, 0)];
            Vector3 next = points[Mathf.Min(i + 1, count - 1)];
            Vector3 t = next - prev;
            tangents[i] = t.sqrMagnitude > 0.0001f ? t.normalized : Vector3.forward;
        }

        // Propagate a twist-minimizing frame along the tangents instead of recomputing
        // "up" independently per ring - independent computation causes visible flipping/
        // twisting wherever the tangent direction crosses certain axes.
        Vector3 initialNormal = Vector3.Cross(tangents[0], Vector3.up);
        if (initialNormal.sqrMagnitude < 0.0001f)
            initialNormal = Vector3.Cross(tangents[0], Vector3.right);
        normals[0] = initialNormal.normalized;
        binormals[0] = Vector3.Cross(tangents[0], normals[0]).normalized;

        for (int i = 1; i < count; i++)
        {
            Quaternion rot = Quaternion.FromToRotation(tangents[i - 1], tangents[i]);
            Vector3 propagated = Vector3.ProjectOnPlane(rot * normals[i - 1], tangents[i]);
            if (propagated.sqrMagnitude < 0.0001f)
                propagated = Vector3.Cross(tangents[i], Vector3.up);
            normals[i] = propagated.normalized;
            binormals[i] = Vector3.Cross(tangents[i], normals[i]).normalized;
        }

        float[] cumulativeLength = new float[count];
        float totalLength = 0f;
        for (int i = 1; i < count; i++)
        {
            totalLength += Vector3.Distance(points[i - 1], points[i]);
            cumulativeLength[i] = totalLength;
        }
        if (totalLength <= 0f) totalLength = 1f;

        int vertCount = radialSegments * count + (capEnds ? 2 : 0);
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];

        for (int i = 0; i < count; i++)
        {
            float tAlong = cumulativeLength[i] / totalLength;
            float r = radius * radiusByLength.Evaluate(tAlong);

            for (int s = 0; s < radialSegments; s++)
            {
                float angle = (s / (float)radialSegments) * Mathf.PI * 2f;
                Vector3 offset = (Mathf.Cos(angle) * normals[i] + Mathf.Sin(angle) * binormals[i]) * r;
                int idx = i * radialSegments + s;
                vertices[idx] = points[i] + offset;
                uvs[idx] = new Vector2(s / (float)radialSegments, tAlong);
            }
        }

        List<int> triangles = new List<int>((count - 1) * radialSegments * 6 + (capEnds ? radialSegments * 6 : 0));

        for (int i = 0; i < count - 1; i++)
        {
            int ringStart = i * radialSegments;
            int nextRingStart = (i + 1) * radialSegments;

            for (int s = 0; s < radialSegments; s++)
            {
                int sNext = (s + 1) % radialSegments;

                int a = ringStart + s;
                int b = ringStart + sNext;
                int c = nextRingStart + s;
                int d = nextRingStart + sNext;

                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
        }

        if (capEnds)
        {
            int startCapIndex = radialSegments * count;
            int endCapIndex = startCapIndex + 1;

            vertices[startCapIndex] = points[0];
            vertices[endCapIndex] = points[count - 1];
            uvs[startCapIndex] = new Vector2(0.5f, 0f);
            uvs[endCapIndex] = new Vector2(0.5f, 1f);

            int lastRingStart = (count - 1) * radialSegments;
            for (int s = 0; s < radialSegments; s++)
            {
                int sNext = (s + 1) % radialSegments;
                triangles.Add(startCapIndex); triangles.Add(s); triangles.Add(sNext);
                triangles.Add(endCapIndex); triangles.Add(lastRingStart + sNext); triangles.Add(lastRingStart + s);
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}