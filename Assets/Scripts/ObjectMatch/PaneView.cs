using UnityEngine;

public class PaneView : MonoBehaviour
{
    public Camera cam;
    public bool seamIsOnRight;            // tick for the left pane, untick for the right pane

    [SerializeField] float xTolerance = 0.06f;     // how close to the edge counts (viewport units)
    [SerializeField] float minY = 0.25f;           // piece must be in the middle band vertically
    [SerializeField] float maxY = 0.75f;
    [SerializeField] LayerMask blockingLayers = ~0; // things that can hide the piece



    // Returns true if this pane is framing the piece's seam correctly.
    // Also hands back how big the piece looks and where it sits vertically.
    public bool Frames(SeamPiece piece, out float apparentSize, out float viewportY)
    {
        apparentSize = 0f;
        viewportY = 0f;

        // Step 1: where is the seam anchor on this camera's view?
        // x and y go from 0 to 1 across the view. z is the distance from the camera.
        Vector3 viewportPos = cam.WorldToViewportPoint(piece.seamAnchor.position);
        viewportY = viewportPos.y;

        // Step 2: is it in front of the camera (not behind it)?
        if (viewportPos.z <= 0f)
        {
            return false;
        }

        // Step 3: is it near this pane's seam edge?
        float targetX = 0f;
        if (seamIsOnRight)
        {
            targetX = 1f;
        }

        float distanceFromEdge = Mathf.Abs(viewportPos.x - targetX);
        if (distanceFromEdge > xTolerance)
        {
            return false;
        }

        // Step 4: is it in the middle band vertically?
        if (viewportPos.y < minY || viewportPos.y > maxY)
        {
            return false;
        }

        // Step 5: does the cut face point toward the seam edge?
        // For the right edge we want the face pointing along the camera's right.
        // For the left edge we want it pointing the opposite way.
        float facing = Vector3.Dot(piece.seamAnchor.forward, cam.transform.right);
        if (!seamIsOnRight)
        {
            facing = -facing;
        }
        float facingAngle = GetFacingAngle(piece);
        if (facingAngle > piece.maxFacingAngle)
        {
            return false;
        }
        // Step 6: is anything blocking the camera's line of sight to the anchor?
        Vector3 camPos = cam.transform.position;
        Vector3 toAnchor = piece.seamAnchor.position - camPos;

        RaycastHit hit;
        bool hitSomething = Physics.Raycast(camPos, toAnchor.normalized, out hit, toAnchor.magnitude, blockingLayers);

        if (hitSomething)
        {
            bool hitIsPartOfPiece = hit.transform.IsChildOf(piece.transform);
            if (!hitIsPartOfPiece)
            {
                return false;
            }
        }

        // Step 7: how big does the piece look? (fraction of the view it fills)
        apparentSize = GetSize(piece, viewportPos.z);
        return true;
    }

    public float GetSize(SeamPiece piece, float distance)
    {
        if (cam.orthographic)
        {
            return piece.radius / cam.orthographicSize;
        }

        float halfFovRadians = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float visibleHalfHeight = distance * Mathf.Tan(halfFovRadians);
        return piece.radius / visibleHalfHeight;
    }

    // Returns the angle in degrees between the cut face direction and this pane's seam edge direction,
    // measured on the horizontal plane so camera tilt and roll don't affect it.
    float GetFacingAngle(SeamPiece piece)
    {
        Vector3 seamDirection = cam.transform.right;
        if (!seamIsOnRight)
        {
            seamDirection = -seamDirection;
        }

        Vector3 faceDirection = piece.seamAnchor.forward;

        seamDirection.y = 0f;
        faceDirection.y = 0f;

        if (seamDirection.sqrMagnitude < 0.0001f || faceDirection.sqrMagnitude < 0.0001f)
        {
            return 180f;
        }

        return Vector3.Angle(seamDirection, faceDirection);
    }


}