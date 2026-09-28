using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public enum EyeSide { Left, Right }

public class DeathManager : MonoBehaviour
{
    public static DeathManager Instance { get; private set; }

    [Serializable]
    public class EyeDeathEffects
    {
        [Header("Eye")]
        [Tooltip("Used to lock this eye's input, and to listen for its OnDeath event.")]
        public EyeDeployController deployController;
        public EyeFreeRoamNonVert freeRoam;

        [Header("Cracked Screen")]
        public Image crackImage;
        public Vector3 targetScale = Vector3.one;
        public float crackDuration = 0.4f;

        [Header("Blur")]
        [Tooltip("Volume for this eye holding a Depth Of Field override. Its weight is tweened 0 -> 1.")]
        public Volume blurVolume;
        public float blurDuration = 1.5f;
        [Tooltip("Seconds after death before the blur starts. 0 = starts with the crack.")]
        public float blurDelay = 0f;

        [Header("Camera Shake")]
        [Tooltip("This eye's CameraShake. Leave empty for no shake.")]
        public CameraShake cameraShake;
        public FlashDaze flashDaze;
    }

    [Header("Per-Eye Effects")]
    [SerializeField] private EyeDeathEffects leftEye;
    [SerializeField] private EyeDeathEffects rightEye;

    [Header("Input")]
    [Tooltip("Disabled on death: stops toggle keys, eye swapping and the candidate/raycast selection.")]
    [SerializeField] private EyeControllerManager eyeManager;

    [Header("Timing")]
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private bool hasPlayed;
    public bool HasPlayed => hasPlayed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Prepare(leftEye);
        Prepare(rightEye);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        if (Instance != this) return;
        if (leftEye.freeRoam != null) leftEye.freeRoam.OnDeath += HandleLeftDeath;
        if (rightEye.freeRoam != null) rightEye.freeRoam.OnDeath += HandleRightDeath;
    }

    private void OnDisable()
    {
        if (leftEye.freeRoam != null) leftEye.freeRoam.OnDeath -= HandleLeftDeath;
        if (rightEye.freeRoam != null) rightEye.freeRoam.OnDeath -= HandleRightDeath;
    }

    private void HandleLeftDeath() => Die(EyeSide.Left);
    private void HandleRightDeath() => Die(EyeSide.Right);

    /// <summary>
    /// Callable from anywhere: DeathManager.Instance.Die(EyeSide.Left).
    /// The eye passed in decides which image / blur profile plays. Only ever plays once.
    /// </summary>
    public void Die(EyeSide side)
    {
        if (hasPlayed) return;
        hasPlayed = true;

        LockAllInput();
        StartCoroutine(PlayDeath(side == EyeSide.Left ? leftEye : rightEye));
    }

    // ---------- Input ----------

    private void LockAllInput()
    {
        if (eyeManager != null) eyeManager.enabled = false;

        if (leftEye.deployController != null) leftEye.deployController.LockInput();
        if (rightEye.deployController != null) rightEye.deployController.LockInput();
    }

    // ---------- Effects ----------

    private void Prepare(EyeDeathEffects fx)
    {
        if (fx.crackImage != null)
            fx.crackImage.rectTransform.localScale = Vector3.zero;

        if (fx.blurVolume == null) return;

        fx.blurVolume.weight = 0f;

        if (fx.blurVolume.profile.TryGet(out DepthOfField dof))
        {
            // The Mode override must be ticked or the effect stays Off no matter what the other values are.
            dof.mode.overrideState = true;
            dof.mode.value = DepthOfFieldMode.Bokeh;
        }
        else
        {
            Debug.LogWarning($"{name}: blur volume '{fx.blurVolume.name}' has no Depth Of Field override.", fx.blurVolume);
        }
    }

    private IEnumerator PlayDeath(EyeDeathEffects fx)
    {
        if (fx.crackImage != null)
            fx.crackImage.gameObject.SetActive(true);

        if (fx.cameraShake != null)
        {
            fx.flashDaze.TriggerDaze(fx.cameraShake);
        }

        // Unscaled time so the effect still plays if you slow or pause time on death.
        float total = Mathf.Max(fx.crackDuration, fx.blurDelay + fx.blurDuration);
        float t = 0f;

        while (t < total)
        {
            t += Time.unscaledDeltaTime;

            if (fx.crackImage != null)
            {
                float k = ease.Evaluate(Progress(t, 0f, fx.crackDuration));
                fx.crackImage.rectTransform.localScale = Vector3.LerpUnclamped(Vector3.zero, fx.targetScale, k);
            }

            if (fx.blurVolume != null)
            {
                float k = ease.Evaluate(Progress(t, fx.blurDelay, fx.blurDuration));
                fx.blurVolume.weight = k;
            }

            yield return null;
        }

        if (fx.crackImage != null) fx.crackImage.rectTransform.localScale = fx.targetScale;
        if (fx.blurVolume != null) fx.blurVolume.weight = 1f;
    }

    private static float Progress(float t, float delay, float duration)
    {
        if (duration <= 0f) return t >= delay ? 1f : 0f;
        return Mathf.Clamp01((t - delay) / duration);
    }
}