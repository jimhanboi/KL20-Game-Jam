using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fades an image in while this eye is roaming and not grounded,
/// and fades it back to 0 when it lands (or is no longer roaming).
/// Attach one per eye, each with its own image.
/// </summary>
public class EyeAirborneIndicator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EyeDeployController deployController;
    [SerializeField] private EyeFreeRoamNonVert freeRoam;
    [SerializeField] private Image image;

    [Header("Fade")]
    [Range(0f, 1f)]
    [SerializeField] private float targetAlpha = 0.5f;
    [Tooltip("Seconds to fade from 0 to target alpha once airborne. 0 = instant.")]
    [SerializeField] private float fadeInDuration = 0.2f;
    [Tooltip("Seconds to fade from target alpha back to 0 once grounded. 0 = instant.")]
    [SerializeField] private float fadeOutDuration = 0.15f;
    [Tooltip("How long the eye must be airborne before the fade-in starts. Stops flicker from tiny bumps/steps. 0 = immediate.")]
    [SerializeField] private float airTimeBeforeShow = 0.1f;

    private float alpha;
    private float airTime;

    private void Awake()
    {
        SetAlpha(0f);
    }

    private void OnDisable()
    {
        airTime = 0f;
        SetAlpha(0f);
    }

    private void Update()
    {
        if (deployController == null || freeRoam == null || image == null) return;

        bool roaming = deployController.State == EyeDeployState.Roaming;
        bool airborne = roaming && !freeRoam.IsGrounded();

        airTime = airborne ? airTime + Time.deltaTime : 0f;
        bool show = airborne && airTime >= airTimeBeforeShow;

        // Move toward the current target every frame. If the state flips mid-fade,
        // it reverses smoothly from wherever alpha currently is.
        float target = show ? targetAlpha : 0f;
        float duration = show ? fadeInDuration : fadeOutDuration;
        float speed = duration > 0f ? targetAlpha / duration : float.MaxValue;

        alpha = Mathf.MoveTowards(alpha, target, speed * Time.deltaTime);
        SetAlpha(alpha);
    }

    private void SetAlpha(float a)
    {
        alpha = a;
        if (image == null) return;

        Color c = image.color;
        c.a = a;
        image.color = c;
    }
}