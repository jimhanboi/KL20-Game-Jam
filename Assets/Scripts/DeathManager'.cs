using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
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

    [Header("Death Screen")]
    [Tooltip("The panel INSIDE the canvas that slides down from above. Assign a child of the canvas, not the canvas root, which can't be moved.")]
    [SerializeField] private RectTransform deathScreen;
    [Tooltip("Optional. The canvas holding the death screen. It never moves; it's only switched on when the slide starts, so it can begin disabled.")]
    [SerializeField] private GameObject deathCanvas;

    [Header("Death Screen Buttons")]
    [Tooltip("Restarts the current scene.")]
    [SerializeField] private Button continueButton;
    [Tooltip("Quits the game (stops Play mode in the editor).")]
    [SerializeField] private Button giveUpButton;
    [Tooltip("Seconds to wait after the crack and blur have finished before the screen starts sliding in.")]
    [SerializeField] private float screenDelay = 1f;
    [SerializeField] private float screenDuration = 0.6f;

    [Header("Audio")]
    [Tooltip("Played once at the moment of death. Leave empty for no sound.")]
    [SerializeField] private AudioClip deathClip;
    [SerializeField] float clipStartPoint;
    [Range(0f, 1f)]
    [SerializeField] private float deathVolume = 1f;
    [Tooltip("Optional. If empty, an AudioSource is added to this object (2D, no play on awake).")]
    [SerializeField] private AudioSource audioSource;

    [Header("Input")]
    [Tooltip("Disabled on death: stops toggle keys, eye swapping and the candidate/raycast selection.")]
    [SerializeField] private EyeControllerManager eyeManager;

    [Header("Timing")]
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector2 screenOnPos;   // where the screen rests in the scene (its authored position)
    private Vector2 screenOffPos;  // same position shifted up past the top of the canvas

    private bool hasPlayed;
    private bool leaving; // set once a button is pressed, so it can't fire twice
    public bool HasPlayed => hasPlayed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SetupAudio();
        Prepare(leftEye);
        Prepare(rightEye);
        PrepareDeathScreen();
        SetButtonsInteractable(false);
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

        if (continueButton != null) continueButton.onClick.AddListener(Continue);
        if (giveUpButton != null) giveUpButton.onClick.AddListener(GiveUp);
    }

    private void OnDisable()
    {
        if (leftEye.freeRoam != null) leftEye.freeRoam.OnDeath -= HandleLeftDeath;
        if (rightEye.freeRoam != null) rightEye.freeRoam.OnDeath -= HandleRightDeath;

        if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
        if (giveUpButton != null) giveUpButton.onClick.RemoveListener(GiveUp);
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
        PlayDeathSound();
        StartCoroutine(PlayDeath(side == EyeSide.Left ? leftEye : rightEye));
    }

    // ---------- Audio ----------

    private void SetupAudio()
    {
        if (audioSource != null) return; // use the one you assigned as-is

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D, so it isn't affected by where the eyes are
    }

    private void PlayDeathSound()
    {
        if (deathClip == null || audioSource == null) return;

        audioSource.clip = deathClip;
        audioSource.volume = deathVolume;
        audioSource.time = Mathf.Clamp(clipStartPoint, 0f, deathClip.length - 0.01f);
        audioSource.Play();
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

    private void PrepareDeathScreen()
    {
        if (deathScreen == null) return;

        // Whatever position you authored in the scene is where it slides to.
        screenOnPos = deathScreen.anchoredPosition;

        float canvasHeight = Screen.height;
        Canvas canvas = deathScreen.GetComponentInParent<Canvas>(true);
        if (canvas != null)
            canvasHeight = ((RectTransform)canvas.rootCanvas.transform).rect.height;

        // Canvas height + the screen's own height guarantees it's fully clear of the top edge.
        screenOffPos = screenOnPos + Vector2.up * (canvasHeight + deathScreen.rect.height);
        deathScreen.anchoredPosition = screenOffPos;
    }

    private IEnumerator PlayDeath(EyeDeathEffects fx)
    {
        if (fx.crackImage != null)
            fx.crackImage.gameObject.SetActive(true);

        if (fx.cameraShake != null && fx.flashDaze != null)
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

        // Everything above has finished: wait, then slide the death screen down from the top.
        if (deathScreen != null)
        {
            if (screenDelay > 0f)
                yield return new WaitForSecondsRealtime(screenDelay);

            yield return SlideDeathScreen();
        }
    }

    private IEnumerator SlideDeathScreen()
    {
        if (deathCanvas != null) deathCanvas.SetActive(true);
        deathScreen.gameObject.SetActive(true);

        // Gameplay usually locks/hides the cursor; the buttons need it back.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        float t = 0f;
        while (t < screenDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = ease.Evaluate(Progress(t, 0f, screenDuration));
            deathScreen.anchoredPosition = Vector2.LerpUnclamped(screenOffPos, screenOnPos, k);
            yield return null;
        }

        deathScreen.anchoredPosition = screenOnPos;
        SetButtonsInteractable(true);
    }

    // ---------- Buttons ----------

    private void SetButtonsInteractable(bool value)
    {
        if (continueButton != null) continueButton.interactable = value;
        if (giveUpButton != null) giveUpButton.interactable = value;
    }

    /// <summary>Restarts the current scene.</summary>
    public void Continue()
    {
        if (leaving) return;
        leaving = true;
        SetButtonsInteractable(false);

        Time.timeScale = 1f; // in case time was slowed or paused on death
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Quits the game. In the editor this stops Play mode instead.</summary>
    public void GiveUp()
    {
        if (leaving) return;
        leaving = true;
        SetButtonsInteractable(false);

        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static float Progress(float t, float delay, float duration)
    {
        if (duration <= 0f) return t >= delay ? 1f : 0f;
        return Mathf.Clamp01((t - delay) / duration);
    }
}