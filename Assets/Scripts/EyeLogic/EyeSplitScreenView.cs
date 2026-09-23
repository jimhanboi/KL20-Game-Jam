using UnityEngine;


public class EyeSplitScreenView : MonoBehaviour
{
    [SerializeField] private Camera eyeCamera;
    [SerializeField] private GameObject rawImagePanel;

    private void Awake()
    {
        Hide(); // start docked - no eye view visible until deployed
    }

    public void Show()
    {
        if (eyeCamera != null) eyeCamera.enabled = true;
        if (rawImagePanel != null) rawImagePanel.SetActive(true);
    }

    public void Hide()
    {
        if (eyeCamera != null) eyeCamera.enabled = false;
        if (rawImagePanel != null) rawImagePanel.SetActive(false);
    }
}