using UnityEngine;

/// <summary>
/// Example gaze-reactive object: turns green while gazed at by the left eye, blue while
/// gazed at by the right eye, and reverts to its original color once neither eye is
/// gazing at it. Requires a Collider on this object (or a child) for EyeDetection to
/// find it - not enforced here since one may already exist on the mesh.
///
/// Implements IEyeDetect directly - a single instance now reacts to both eyes, so
/// left/right gazing state is tracked separately and RefreshColor decides priority
/// between them.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class TestDetection : MonoBehaviour, IEyeDetect
{
    [SerializeField] private Color leftEyeColor = Color.green;
    [SerializeField] private Color rightEyeColor = Color.blue;
    [SerializeField] FlashDaze flash;

    private Renderer targetRenderer;
    private Color originalColor;
    private bool leftGazing;
    private bool rightGazing;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        originalColor = targetRenderer.material.color; // .material auto-instances a copy, so this won't affect other objects sharing the same material asset
    }

    private void RefreshColor()
    {
        // Right eye takes priority if both happen to be gazing at once - arbitrary but
        // deterministic; swap the order below if left should win instead.
        if (rightGazing)
            targetRenderer.material.color = rightEyeColor;
        else if (leftGazing)
            targetRenderer.material.color = leftEyeColor;
        else
            targetRenderer.material.color = originalColor;
    }

    public void OnRightEnter(EyeDetection detectingCam)
    {
        rightGazing = true;
        CameraShake shake = detectingCam.GetComponentInChildren<CameraShake>();
        flash.TriggerDaze(shake);
        RefreshColor();
    }

    public void OnRightExit(EyeDetection detectingCam)
    {
        rightGazing = false;
        RefreshColor();
    }

    public void OnLeftEnter(EyeDetection detectingCam)
    {
        leftGazing = true;
        RefreshColor();
    }

    public void OnLeftExit(EyeDetection detectingCam)
    {
        leftGazing = false;
        RefreshColor();
    }
}