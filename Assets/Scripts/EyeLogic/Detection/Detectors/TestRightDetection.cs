using UnityEngine;

/// <summary>
/// Example gaze-reactive object: turns green while gazed at by the left eye, blue while
/// gazed at by the right eye, and reverts to its original color once neither eye is
/// gazing at it. Requires a Collider on this object (or a child) for EyeDetection to
/// find it - not enforced here since one may already exist on the mesh.
///
/// Implements both ILeftEyeVisible and IRightEyeVisible using EXPLICIT interface
/// implementation. Both interfaces declare identically-signed OnGazeEnter/OnGazeExit
/// (inherited from IGazeReceiver), so a normal "public void OnGazeEnter()" would
/// satisfy both interfaces with the SAME method - making it impossible to tell which
/// eye triggered it. Writing "void ILeftEyeVisible.OnGazeEnter()" keeps the two paths
/// genuinely separate.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class TestRightDetection : MonoBehaviour, IRightEyeVisible
{
    [SerializeField] private Color rightEyeColor = Color.blue;

    private Renderer targetRenderer;
    private Color originalColor;
    private bool rightGazing;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        originalColor = targetRenderer.material.color; // .material auto-instances a copy, so this won't affect other objects sharing the same material asset
    }


    private void RefreshColor()
    {

        if (rightGazing)
            targetRenderer.material.color = rightEyeColor;
        else
            targetRenderer.material.color = originalColor;
    }

    public void OnGazeEnter()
    {
        rightGazing = true;
        RefreshColor();
    }

    public void OnGazeExit()
    {
        rightGazing = false;
        RefreshColor();
    }
}