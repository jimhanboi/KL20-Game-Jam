using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum EyeDeployState { Docked, Roaming, Returning }

public class EyeDeployController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform eyeTransform;
    [SerializeField] private EyeDetection gazeDetection;
    [SerializeField] private EyeFreeRoamNonVert freeRoam;
    [SerializeField] private EyeRotation eyeLook;
    [SerializeField] private EyeReturnPath returnPath;
    [SerializeField] private Rigidbody rb;
    [Tooltip("The eye's resting attachment point on the player.")]
    [SerializeField] private Transform socket;
    [Tooltip("This eye's own camera/panel toggle - not shared with the other eye.")]
    [SerializeField] private EyeSplitScreenView splitScreenView;

    [Header("Input")]
    [Tooltip("Pressed once to deploy (throw out) this eye; pressed again to recall it.")]
    [SerializeField] private Key toggleKey = Key.Digit1;
    public event Action<EyeDeployController> OnReturnedEye;

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

    private bool inputLocked;


    public void ChangeAnchor(Transform newAnchor)
    {
        socket = newAnchor;
        freeRoam.rangeAnchor = newAnchor;
        returnPath.playerAnchor = newAnchor;

    }

    // Called by DeathManager. Stops this eye reading input and shuts off look + detection.
    // Free roam stays enabled (just uncontrolled) so the eye decelerates rather than freezing mid-air.
    public void LockInput()
    {
        inputLocked = true;
        if (freeRoam != null) freeRoam.SetControlled(false);
        if (eyeLook != null) eyeLook.enabled = false;
        if (gazeDetection != null) gazeDetection.enabled = false;
    }

    private void Update()
    {
        if (inputLocked) return;

        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame && state == EyeDeployState.Roaming)
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

    public void Deploy()
    {
        rb.useGravity = true;
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
            freeRoam.enabled = false; // OnDisable clears velocity and the controlled flag
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
        rb.useGravity = false;
        splitScreenView.Hide();

        // Hiding the select indicator is handled by the manager (HandleEyeReturn),
        // since it owns the Image references. This just announces the docking.
        OnReturnedEye?.Invoke(this);
    }

    public bool IsOut()
    {
        return state == EyeDeployState.Roaming;
    }

    public void ActivateControl(bool activate, Image selectImg, Color selectedColor, Color unselectedColor)
    {
        if (selectImg != null)
        {
            bool shouldShow = activate || IsOut();   // show if being selected, OR already deployed
            selectImg.gameObject.SetActive(shouldShow);
            selectImg.color = activate ? selectedColor : unselectedColor;
        }

        if (activate)
        {
            Select();
        }

        if (freeRoam != null)
        {
            // Stays enabled while deployed so it can decelerate; only input is switched.
            freeRoam.SetControlled(activate);
        }
        if (eyeLook != null)
        {
            eyeLook.enabled = activate;
        }
    }

    public void DeployAt(Vector3 position, Quaternion rotation)
    {
        if (inputLocked) return;
        if (state == EyeDeployState.Returning) return; // cancel the return first if you want to allow this

        eyeTransform.SetParent(null);
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;   // rb.velocity on pre-Unity 6
        rb.angularVelocity = Vector3.zero;

        eyeTransform.SetPositionAndRotation(position, rotation);
        rb.position = position;
        rb.rotation = rotation;
        Physics.SyncTransforms();

        returnPath.StartTracking(position); // restarts the trail so it doesn't include the old location

        if (freeRoam != null) freeRoam.enabled = true;
        if (eyeLook != null) eyeLook.enabled = true;
        if (gazeDetection != null) gazeDetection.enabled = true;

        state = EyeDeployState.Roaming;
        splitScreenView.Show(); // make sure Show() is safe to call twice
    }

    void Select()
    {
        if (state == EyeDeployState.Docked)
        {
            Toggle();
        }
    }
}