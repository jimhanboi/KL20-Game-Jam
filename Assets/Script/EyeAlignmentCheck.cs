using UnityEngine;

public class EyeAlignmentCheck : MonoBehaviour
{
    public Transform eyeballCamera;
    public Transform target;

    public float positionTolerance = 10f;
    public float angleTolerance = 30f;

    [Range(0f, 1f)]
    public float alignThreshold = 0.7f;   // this is your "70%"

    public float currentPercent; // shows live in Inspector for debugging

    public bool IsAligned()
    {
        float posDiff = Vector3.Distance(eyeballCamera.position, target.position);
        float angleDiff = Quaternion.Angle(eyeballCamera.rotation, target.rotation);

        float posScore = Mathf.Clamp01(1f - (posDiff / positionTolerance));
        float angleScore = Mathf.Clamp01(1f - (angleDiff / angleTolerance));

        currentPercent = (posScore + angleScore) / 2f;

        return currentPercent >= alignThreshold;
    }
}