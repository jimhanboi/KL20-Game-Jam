public interface IEyeDetect
{
    void OnGazeEnter(EyeDetection detectingCam);
    void OnGazeExit(EyeDetection detectionCam);
}

public interface ILeftEyeVisible : IEyeDetect { }
public interface IRightEyeVisible : IEyeDetect { }