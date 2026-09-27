using System.Collections.Generic;
using UnityEngine;

public class EyeDetection : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Which callback pair this instance fires: OnLeftEnter/OnLeftExit if true, OnRightEnter/OnRightExit if false.")]
    [SerializeField] private bool isLeftEye = true;

    [Header("Detection")]
    [SerializeField] private Camera eyeCamera;
    [Tooltip("Broad-phase search radius for candidates - not the frustum's own far distance.")]
    [SerializeField] private float gazeRange = 20f;
    [Tooltip("Layer(s) candidate gaze targets live on.")]
    [SerializeField] private LayerMask gazeMask = ~0;
    [Tooltip("Multiplies the frustum's FOV/size for edge-of-view leeway. 1 = exact frustum, >1 = more forgiving.")]
    [SerializeField] private float frustumLeewayScale = 1.1f;

    [Header("Occlusion")]
    [Tooltip("Layer(s) that block line of sight (walls/geometry). Should NOT include the target's own layer, so any hit at all means something is in the way.")]
    [SerializeField] private LayerMask occlusionMask;

    [Header("Dwell")]
    [Tooltip("How long the eye must continuously look at a target before its Enter callback fires.")]
    [SerializeField] private float requiredGazeDuration = 0.5f;

    private readonly Collider[] overlapBuffer = new Collider[32]; // reused each frame to avoid GC allocation
    private readonly HashSet<IEyeDetect> visibleTargets = new HashSet<IEyeDetect>();   // confirmed - Enter already fired
    private readonly HashSet<IEyeDetect> frameVisible = new HashSet<IEyeDetect>();     // this frame's raw qualifying set
    private readonly Dictionary<IEyeDetect, float> dwellTimers = new Dictionary<IEyeDetect, float>(); // unconfirmed, accumulating
    private readonly List<IEyeDetect> scratchRemovalList = new List<IEyeDetect>(); // reused for safe removal during iteration

    private void Update()
    {
        if (eyeCamera == null) return;

        Plane[] frustumPlanes = GetLeewayFrustumPlanes();
        int count = Physics.OverlapSphereNonAlloc(eyeCamera.transform.position, gazeRange, overlapBuffer, gazeMask);

        frameVisible.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider candidate = overlapBuffer[i];

            IEyeDetect target = FindReceiver(candidate);
            if (target == null) continue;

            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, candidate.bounds))
                continue; // outside the (leeway-expanded) view frustum

            if (!HasLineOfSight(eyeCamera.transform.position, candidate))
                continue; // in frustum but something is blocking the view

            frameVisible.Add(target);

            if (!visibleTargets.Contains(target))
            {
                dwellTimers.TryGetValue(target, out float elapsed);
                elapsed += Time.deltaTime;

                if (elapsed >= requiredGazeDuration)
                {
                    dwellTimers.Remove(target);
                    visibleTargets.Add(target);
                    FireEnter(target);
                }
                else
                {
                    dwellTimers[target] = elapsed;
                }
            }
        }

        // Reset dwell progress for anything that dropped out before completing its timer.
        scratchRemovalList.Clear();
        foreach (var kvp in dwellTimers)
        {
            if (!frameVisible.Contains(kvp.Key))
                scratchRemovalList.Add(kvp.Key);
        }
        foreach (var target in scratchRemovalList)
            dwellTimers.Remove(target);

        // Anything confirmed-visible that didn't qualify this frame has left gaze.
        scratchRemovalList.Clear();
        foreach (var target in visibleTargets)
        {
            if (!frameVisible.Contains(target))
                scratchRemovalList.Add(target);
        }
        foreach (var target in scratchRemovalList)
        {
            FireExit(target);
            visibleTargets.Remove(target);
        }
    }

    /// <summary>
    /// Every candidate is now checked against the single IEyeDetect interface -
    /// isLeftEye no longer changes which interface is looked up, only which
    /// callback pair (see FireEnter/FireExit) gets invoked on the result.
    /// </summary>
    private IEyeDetect FindReceiver(Collider candidate)
    {
        return candidate.GetComponent<IEyeDetect>();
    }

    private void FireEnter(IEyeDetect target)
    {
        if (isLeftEye)
            target.OnLeftEnter(this);
        else
            target.OnRightEnter(this);
    }

    private void FireExit(IEyeDetect target)
    {
        if (isLeftEye)
            target.OnLeftExit(this);
        else
            target.OnRightExit(this);
    }

    /// <summary>
    /// Computes frustum planes using a temporarily scaled FOV/orthographic size for
    /// edge-of-view leeway, then immediately restores the camera's real values -
    /// synchronous, so this never affects what's actually rendered this frame.
    /// </summary>
    private Plane[] GetLeewayFrustumPlanes()
    {
        if (Mathf.Approximately(frustumLeewayScale, 1f))
            return GeometryUtility.CalculateFrustumPlanes(eyeCamera);

        float originalFov = eyeCamera.fieldOfView;
        float originalOrthoSize = eyeCamera.orthographicSize;

        if (eyeCamera.orthographic)
            eyeCamera.orthographicSize = originalOrthoSize * frustumLeewayScale;
        else
            eyeCamera.fieldOfView = Mathf.Min(originalFov * frustumLeewayScale, 179f);

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(eyeCamera);

        eyeCamera.fieldOfView = originalFov;
        eyeCamera.orthographicSize = originalOrthoSize;

        return planes;
    }

    private bool HasLineOfSight(Vector3 origin, Collider candidate)
    {
        Vector3 targetPoint = candidate.bounds.center;
        Vector3 delta = targetPoint - origin;
        float distance = delta.magnitude;
        if (distance < 0.001f) return true;

        // occlusionMask should only contain wall/geometry layers - since it excludes the
        // target's own layer, any hit at all here means something is blocking the view.
        return !Physics.Raycast(origin, delta / distance, distance, occlusionMask);
    }

    private void OnDisable()
    {
        // If the eye is recalled mid-gaze, make sure everything it was looking at gets
        // told the gaze ended - otherwise they're stuck reacting as if still watched.
        foreach (var target in visibleTargets)
            FireExit(target);

        visibleTargets.Clear();
        dwellTimers.Clear();
    }
}