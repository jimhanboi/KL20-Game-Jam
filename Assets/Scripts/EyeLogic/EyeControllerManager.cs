using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class EyeControllerManager : MonoBehaviour
{
    [SerializeField] EyeDeployController LeftEye;
    [SerializeField] EyeDeployController RightEye;

    [SerializeField] private Key leftEyeToggle = Key.Digit1;
    [SerializeField] private Key rightEyeToggle = Key.Digit2;

    [SerializeField] GameObject rightSelect, leftSelect;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[leftEyeToggle].wasPressedThisFrame)
        {
            ToggleEye(LeftEye);
        }
        if (kb != null && kb[rightEyeToggle].wasPressedThisFrame)
        {
            ToggleEye(RightEye);
        }
    }

    private void OnEnable()
    {
        LeftEye.OnReturnedEye += HandleEyeReturn;
        RightEye.OnReturnedEye += HandleEyeReturn;
    }

    void HandleEyeReturn(EyeDeployController returnedEye)
    {
        if (returnedEye == LeftEye)
        {
            RightEye.enabled = true;
            if (RightEye.IsOut())
            {
                RightEye.enabled = true;
                RightEye.ActivateControl(true, rightSelect);
            }
            else
            {
                LeftEye.ActivateControl(false, leftSelect);
                RightEye.enabled = false;
            }
        }
        if (returnedEye == RightEye)
        {
            LeftEye.enabled = true;
            if (LeftEye.IsOut())
            {
                LeftEye.enabled = true;
                LeftEye.ActivateControl(true, leftSelect);
            }
            else
            {
                // FIX: was LeftEye.ActivateControl(false, leftSelect) + LeftEye.enabled = false,
                // which deactivated the wrong eye when RightEye was the one returning.
                RightEye.ActivateControl(false, rightSelect);
                RightEye.enabled = false;
            }
        }
    }

    void ToggleEye(EyeDeployController selectedEye)
    {
        if (selectedEye == LeftEye)
        {
            if (RightEye.enabled == false)
            {
                RightEye.enabled = true;
            }
            RightEye.ActivateControl(false, rightSelect);
            RightEye.enabled = false;
            if (LeftEye.enabled == false)
            {
                LeftEye.enabled = true;
            }
            LeftEye.ActivateControl(true, leftSelect);
        }
        else
        {
            if (LeftEye.enabled == false)
            {
                LeftEye.enabled = true;
            }
            LeftEye.ActivateControl(false, leftSelect);
            LeftEye.enabled = false;
            if (RightEye.enabled == false)
            {
                RightEye.enabled = true;
            }
            RightEye.ActivateControl(true, rightSelect);
        }
    }
}