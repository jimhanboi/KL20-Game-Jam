using UnityEngine;

public class TestDetection : MonoBehaviour, IEyeDetect
{
    [SerializeField] private Color leftEyeColor = Color.green;
    [SerializeField] private Color rightEyeColor = Color.blue;
    [SerializeField] FlashDaze rightFlash;
    [SerializeField] FlashDaze leftFlash;
    [SerializeField] float activateRange = 10f;


    public float GazeRange => activateRange;

    private void Awake()
    {

    }

    public void OnRightEnter(EyeDetection detectingCam)
    {
        HandleLook(detectingCam);
    }

    void HandleLook(EyeDetection detectingCam)
    {
        CameraShake shake = detectingCam.GetComponentInChildren<CameraShake>();
        rightFlash.TriggerDaze(shake);
    }

    public void OnRightExit(EyeDetection detectingCam)
    {
    }

    public void OnLeftEnter(EyeDetection detectingCam)
    {
        HandleLook(detectingCam);

    }

    public void OnLeftExit(EyeDetection detectingCam)
    {

    }
}