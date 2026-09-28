public interface IEyeDetect
{
    float GazeRange { get; }

    void OnRightEnter(EyeDetection detectingCam);
    void OnRightExit(EyeDetection detectionCam);
    void OnLeftEnter(EyeDetection detectingCam);
    void OnLeftExit(EyeDetection detectionCam);
}