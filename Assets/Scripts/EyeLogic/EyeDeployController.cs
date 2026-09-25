using UnityEngine;
using UnityEngine.InputSystem;

public enum EyeDeployState { Docked, Roaming, Returning }

public class EyeDeployController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform eyeTransform;
    [SerializeField] private EyeDetection gazeDetection;
    [SerializeField] private EyeFreeRoam freeRoam;
    [SerializeField] private EyeRotation eyeLook;
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
                break;
        }
    }



    private void Deploy()
    {
        eyeTransform.SetParent(null); // free from the socket so physics can move it independently
        eyeTransform.position = socket.position;

        returnPath.StartTracking(socket.position);
        if (freeRoam != null)
        {
            freeRoam.enabled = true;
        }
        if (eyeLook != null)
        {
            eyeLook.enabled = true;
        }
        if (gazeDetection != null)
        {
            gazeDetection.enabled = true;
        }
        state = EyeDeployState.Roaming;

        splitScreenView.Show();
    }

    private void Recall()
    {
        if (freeRoam != null)
        {
            freeRoam.enabled = false;
        }
        if (eyeLook != null)
        {
            eyeLook.enabled = false;
        }
        if (gazeDetection != null)
        {
            gazeDetection.enabled = false;
        }

            returnPath.BeginReturn();
        state = EyeDeployState.Returning;
    }

    private void HandleReturnComplete()
    {
        eyeTransform.SetParent(socket);
        eyeTransform.localPosition = Vector3.zero;
        eyeTransform.localRotation = Quaternion.identity;
        state = EyeDeployState.Docked;

        splitScreenView.Hide();
    }
}