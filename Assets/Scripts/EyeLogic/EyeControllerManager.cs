using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EyeControllerManager : MonoBehaviour
{
    public EyeDeployController LeftEye;
    public EyeDeployController RightEye;

    [SerializeField] private Key leftEyeToggle = Key.Digit1;
    [SerializeField] private Key rightEyeToggle = Key.Digit2;



    [Header("Selection Indicators")]
    [SerializeField] Image rightSelect, leftSelect;
    [SerializeField] Color selectedColor = Color.green;
    [SerializeField] Color unselectedColor = Color.white;

    [Header("Swap")]
    [Tooltip("Minimum seconds between swaps per eye. Stops spam-click flip-flopping. 0 = no limit.")]
    [SerializeField] float swapCooldown = 0.25f;

    readonly HashSet<EyePickupable> leftCandidates = new HashSet<EyePickupable>();
    readonly HashSet<EyePickupable> rightCandidates = new HashSet<EyePickupable>();
    Camera leftCam, rightCam;
    float leftLastSwap = -999f, rightLastSwap = -999f;

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (kb != null && kb[leftEyeToggle].wasPressedThisFrame)
            ToggleEye(LeftEye);
        if (kb != null && kb[rightEyeToggle].wasPressedThisFrame)
            ToggleEye(RightEye);

        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame && Time.time >= leftLastSwap + swapCooldown)
        {
            var target = GetBestAny();
            if (target)
            {
                Swap(LeftEye.GetComponentInChildren<EyeRenderersData>(), target.GetComponent<EyeRenderersData>());
                target.LiftAndDrop();
                leftLastSwap = Time.time;
            }
        }

        if (mouse.rightButton.wasPressedThisFrame && Time.time >= rightLastSwap + swapCooldown)
        {
            var target = GetBestAny();
            if (target)
            {
                Swap(RightEye.GetComponentInChildren<EyeRenderersData>(), target.GetComponent<EyeRenderersData>());
                target.LiftAndDrop();
                rightLastSwap = Time.time;
            }
        }
    }

    // ---------- Candidate tracking ----------

    public void AddCandidate(EyePickupable p, EyeDetection d)
    {
        if (d.IsLeftEye) { leftCam = d.EyeCamera; leftCandidates.Add(p); }
        else { rightCam = d.EyeCamera; rightCandidates.Add(p); }
    }

    public void RemoveCandidate(EyePickupable p, EyeDetection d)
    {
        (d.IsLeftEye ? leftCandidates : rightCandidates).Remove(p);
    }

    // Best candidate across both eyes, judged by whichever eye is actually seeing it.
    EyePickupable GetBestAny()
    {
        float dotL, dotR;
        var l = GetBest(leftCandidates, leftCam, out dotL);
        var r = GetBest(rightCandidates, rightCam, out dotR);

        if (!l) return r;
        if (!r) return l;
        return dotL >= dotR ? l : r;
    }

    EyePickupable GetBest(HashSet<EyePickupable> set, Camera cam, out float bestDot)
    {
        bestDot = -1f;
        if (cam == null) return null;

        EyePickupable best = null;
        foreach (var p in set)
        {
            if (!p) continue;
            Vector3 dir = (p.transform.position - cam.transform.position).normalized;
            float dot = Vector3.Dot(cam.transform.forward, dir);
            if (dot > bestDot) { bestDot = dot; best = p; }
        }
        return best;
    }
    // ---------- Swap ----------

    public void Swap(EyeRenderersData a, EyeRenderersData b)
    {
        Debug.Log(a.name);
        Debug.Log(b.name);

        if (!a || !b) return;

        (a.material, b.material) = (b.material, a.material);

        a.Apply();
        b.Apply();
    }

    // ---------- Eye control (unchanged) ----------

    private void OnEnable()
    {
        LeftEye.OnReturnedEye += HandleEyeReturn;
        RightEye.OnReturnedEye += HandleEyeReturn;
    }

    private void OnDisable()
    {
        LeftEye.OnReturnedEye -= HandleEyeReturn;
        RightEye.OnReturnedEye -= HandleEyeReturn;
    }

    void HandleEyeReturn(EyeDeployController returnedEye)
    {
        if (returnedEye == LeftEye)
        {
            HideSelect(leftSelect);

            if (RightEye.IsOut())
            {
                RightEye.enabled = true;
                RightEye.ActivateControl(true, rightSelect, selectedColor, unselectedColor);
            }
            else RightEye.enabled = false;
        }
        else if (returnedEye == RightEye)
        {
            HideSelect(rightSelect);

            if (LeftEye.IsOut())
            {
                LeftEye.enabled = true;
                LeftEye.ActivateControl(true, leftSelect, selectedColor, unselectedColor);
            }
            else LeftEye.enabled = false;
        }
    }

    void ToggleEye(EyeDeployController selectedEye)
    {
        if (selectedEye == LeftEye)
        {
            if (!RightEye.enabled) RightEye.enabled = true;
            RightEye.ActivateControl(false, rightSelect, selectedColor, unselectedColor);
            RightEye.enabled = false;

            if (!LeftEye.enabled) LeftEye.enabled = true;
            LeftEye.ActivateControl(true, leftSelect, selectedColor, unselectedColor);
        }
        else
        {
            if (!LeftEye.enabled) LeftEye.enabled = true;
            LeftEye.ActivateControl(false, leftSelect, selectedColor, unselectedColor);
            LeftEye.enabled = false;

            if (!RightEye.enabled) RightEye.enabled = true;
            RightEye.ActivateControl(true, rightSelect, selectedColor, unselectedColor);
        }
    }

    void HideSelect(Image img)
    {
        if (img != null) img.gameObject.SetActive(false);
    }
}