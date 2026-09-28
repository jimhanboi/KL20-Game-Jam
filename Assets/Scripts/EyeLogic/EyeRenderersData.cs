using UnityEngine;

public class EyeRenderersData : MonoBehaviour
{
    public MeshRenderer targetRenderer;   // only thing you assign

    public Material material; // filled at runtime from slot 0

    void Awake()
    {
        if (targetRenderer)
            material = targetRenderer.sharedMaterial; // slot 0
    }

    public void Apply()
    {
        if (targetRenderer)
            targetRenderer.sharedMaterial = material;
    }
}