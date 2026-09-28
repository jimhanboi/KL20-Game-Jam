using UnityEngine;

/// <summary>
/// Feeds VeinSplineController's authoritative spline into VeinSpringChain every frame.
/// Kept separate so each piece stays independently swappable/testable.
/// </summary>
public class VeinPipeline : MonoBehaviour
{
    [SerializeField] private VeinSplineController splineController;
    [SerializeField] private VeinSpringChain springChain;

    private void Update()
    {
        if (splineController == null || springChain == null) return;
        springChain.SetPathTargets(splineController.BuildSpline());
    }
}