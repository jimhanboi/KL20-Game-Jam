using UnityEngine;

public class EyeAlignmentCheck : MonoBehaviour
{
    public Transform eyeballCamera;
    public Transform[] targets;      // any of these poses counts

    public float positionTolerance = 10f;
    public float angleTolerance = 30f;

    [Range(0f, 1f)]
    public float alignThreshold = 0.7f;

    public float currentPercent;     // best score across all targets

    public bool IsAligned()
    {
        float best = 0f;

        foreach (Transform t in targets)
        {
            float posDiff = Vector3.Distance(eyeballCamera.position, t.position);
            float angleDiff = Quaternion.Angle(eyeballCamera.rotation, t.rotation);

            float posScore = Mathf.Clamp01(1f - (posDiff / positionTolerance));
            float angleScore = Mathf.Clamp01(1f - (angleDiff / angleTolerance));

            float score = (posScore + angleScore) / 2f;
            if (score > best) best = score;
        }

        currentPercent = best;
        return currentPercent >= alignThreshold;
    }
}