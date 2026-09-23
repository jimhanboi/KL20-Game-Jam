using UnityEngine;
using UnityEngine.InputSystem;

public enum EyeDeployState { Docked, Roaming, Returning }

public class EyeDeployController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform eyeTransform;
    [SerializeField] private EyeFreeRoam freeRoam;
    [SerializeField] private EyeReturnPath returnPath;
    [Tooltip("The eye's resting attachment point on the player.")]
    [SerializeField] private Transform socket;
    [Tooltip("This eye's own camera/panel toggle - not shared with the other eye.")]
    [SerializeField] private EyeSplitScreenView splitScreenView;

    [Header("Input")]
    [Tooltip("Pressed once to deploy (throw out) this eye; pressed again to recall it.")]
    [SerializeField] private Key toggleKey = Key.Digit1;

    private EyeDeployState state = EyeDeployState.Docked;
    public EyeDeployState State => state;

    private void OnEnable()
    {
        returnPath.OnReturnComplete += HandleReturnComplete;
    }

    private void OnDisable()
    {
        returnPath.OnReturnComplete -= HandleReturnComplete;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame)
            Toggle();
    }

    private void Toggle()
    {
        switch (state)
        {
            case EyeDeployState.Docked:
                Deploy();
                break;
            case EyeDeployState.Roaming:
                Recall();
                break;
            case EyeDeployState.Returning:
                // Already coming back - ignore input until OnReturnComplete fires.
                break;
        }
    }

    private void Deploy()
    {
        eyeTransform.SetParent(null); // free from the socket so physics can move it independently
        eyeTransform.position = socket.position;

        returnPath.StartTracking(socket.position);
        freeRoam.enabled = true;
        state = EyeDeployState.Roaming;

        splitScreenView.Show();
    }

    private void Recall()
    {
        freeRoam.enabled = false;
        returnPath.BeginReturn();
        state = EyeDeployState.Returning;
    }

    private void HandleReturnComplete()
    {
        eyeTransform.SetParent(socket);
        eyeTransform.localPosition = Vector3.zero;
        state = EyeDeployState.Docked;

        splitScreenView.Hide();
    }
}