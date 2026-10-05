using UnityEngine;

public class SeamPiece : MonoBehaviour
{
    public string matchId;
    public int halfIndex;
    public Transform seamAnchor;
    public float radius = 1f;
    public float maxFacingAngle = 45f;

    [Header("Gizmos")]
    public bool showGizmos = true;
    public float gizmoLength = 2f;

    // Draws the piece's size, the seam anchor, the direction the cut face points,
    // and a cone showing how far off that direction a camera is allowed to be.
    void OnDrawGizmos()
    {
        if (!showGizmos)
        {
            return;
        }

        if (seamAnchor == null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            return;
        }

        Color halfColor = Color.green;
        if (halfIndex == 1)
        {
            halfColor = Color.magenta;
        }

        Vector3 origin = seamAnchor.position;
        Vector3 forward = seamAnchor.forward;

        Gizmos.color = new Color(halfColor.r, halfColor.g, halfColor.b, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);

        Gizmos.color = halfColor;
        Gizmos.DrawSphere(origin, 0.08f);
        Gizmos.DrawLine(transform.position, origin);

        DrawArrow(origin, forward, gizmoLength, Color.blue);
        DrawAngleCone(origin, forward, maxFacingAngle, gizmoLength, halfColor);
    }

    // Draws a line with a small arrowhead showing the direction the cut face points.
    void DrawArrow(Vector3 start, Vector3 direction, float length, Color color)
    {
        Gizmos.color = color;

        Vector3 end = start + direction * length;
        Gizmos.DrawLine(start, end);

        Vector3 sideways = Vector3.Cross(direction, Vector3.up);
        if (sideways.sqrMagnitude < 0.0001f)
        {
            sideways = Vector3.Cross(direction, Vector3.right);
        }
        sideways.Normalize();

        float headSize = length * 0.15f;
        Vector3 headBase = end - direction * headSize;

        Gizmos.DrawLine(end, headBase + sideways * headSize * 0.5f);
        Gizmos.DrawLine(end, headBase - sideways * headSize * 0.5f);
    }

    // Draws a cone around the forward direction. Its edge is maxFacingAngle degrees away
    // from forward, so anything inside the cone counts as "facing correctly".
    void DrawAngleCone(Vector3 origin, Vector3 forward, float angle, float length, Color color)
    {
        Gizmos.color = new Color(color.r, color.g, color.b, 0.6f);

        Vector3 perpendicular = Vector3.Cross(forward, Vector3.up);
        if (perpendicular.sqrMagnitude < 0.0001f)
        {
            perpendicular = Vector3.Cross(forward, Vector3.right);
        }
        perpendicular.Normalize();

        Vector3 edgeDirection = Quaternion.AngleAxis(angle, perpendicular) * forward;

        int segments = 24;
        Vector3 previousPoint = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float spin = (360f / segments) * i;
            Vector3 direction = Quaternion.AngleAxis(spin, forward) * edgeDirection;
            Vector3 point = origin + direction * length;

            if (i > 0)
            {
                Gizmos.DrawLine(previousPoint, point);
            }

            if (i % 6 == 0)
            {
                Gizmos.DrawLine(origin, point);
            }

            previousPoint = point;
        }
    }
}