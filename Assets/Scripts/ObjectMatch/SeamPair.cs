using UnityEngine;

public class SeamPair : MonoBehaviour
{
    public SeamPiece pieceA;
    public SeamPiece pieceB;

    [SerializeField] Vector3 bInAPosition;
    [SerializeField] Quaternion bInARotation = Quaternion.identity;
    [SerializeField] Vector3 aInBPosition;
    [SerializeField] Quaternion aInBRotation = Quaternion.identity;

    // Records where each half sits relative to the other while they are placed together.
    [ContextMenu("Capture Assembled Pose")]
    void CaptureAssembledPose()
    {
        if (pieceA == null || pieceB == null)
        {
            Debug.LogError("SeamPair: assign both pieces first.");
            return;
        }

        Transform a = pieceA.transform;
        Transform b = pieceB.transform;

        bInAPosition = a.InverseTransformPoint(b.position);
        bInARotation = Quaternion.Inverse(a.rotation) * b.rotation;
        aInBPosition = b.InverseTransformPoint(a.position);
        aInBRotation = Quaternion.Inverse(b.rotation) * a.rotation;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("SeamPair: assembled pose captured.");
    }

    // Returns the other half of the pair, or null if the piece isn't part of this pair.
    public SeamPiece GetPartner(SeamPiece piece)
    {
        if (piece == pieceA)
        {
            return pieceB;
        }

        if (piece == pieceB)
        {
            return pieceA;
        }

        return null;
    }

    // Gives the world pose the partner would have if it were attached to "from" in its current position.
    public bool GetPartnerPose(SeamPiece from, out Vector3 position, out Quaternion rotation)
    {
        if (from == pieceA)
        {
            position = pieceA.transform.TransformPoint(bInAPosition);
            rotation = pieceA.transform.rotation * bInARotation;
            return true;
        }

        if (from == pieceB)
        {
            position = pieceB.transform.TransformPoint(aInBPosition);
            rotation = pieceB.transform.rotation * aInBRotation;
            return true;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }
}