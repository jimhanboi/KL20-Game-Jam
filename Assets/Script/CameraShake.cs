using UnityEngine;
using DG.Tweening;

public class CameraShake : MonoBehaviour
{
    [Header("Shake Settings")]
    public float duration = 0.5f;
    public float strength = 0.3f;
    public int vibrato = 10;
    public float randomness = 90f;

    private Vector3 originalPos;
    private Tween shakeTween;

    void Start()
    {
        originalPos = transform.localPosition;
    }

    public void Shake()
    {
        Shake(duration, strength);
    }

    public void Shake(float customDuration, float customStrength)
    {
        if (shakeTween != null && shakeTween.IsActive())
            shakeTween.Kill();

        transform.localPosition = originalPos; // reset before shaking, avoids drift

        shakeTween = transform.DOShakePosition(
            customDuration,
            customStrength,
            vibrato,
            randomness,
            snapping: false,
            fadeOut: true
        ).OnComplete(() => transform.localPosition = originalPos);
    }
}