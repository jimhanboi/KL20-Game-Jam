public interface IEyeDetect
{
    void OnGazeEnter();
    void OnGazeExit();
}

public interface ILeftEyeVisible : IEyeDetect { }
public interface IRightEyeVisible : IEyeDetect { }