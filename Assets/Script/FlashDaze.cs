using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FlashDaze : MonoBehaviour
{
    [Header("References")]
    public Volume distortionVolume;

    [Header("Flash Settings")]
    public float flashExposure = 3f;      // how bright the white flash spikes
    public float flashInDuration = 0.08f; // quick snap to white
    public float flashOutDuration = 0.6f; // fade back to normal

    [Header("Daze Settings")]
    public float desaturateAmount = -80f; // negative = washed out / grey
    public float dazeDuration = 1f;

    [Header("Shake Settings")]
    public float shakeStrength = 0.15f;
    public float shakeDuration = 0.3f;

    private ColorAdjustments colorAdjustments;

    void Start()
    {
        distortionVolume.profile.TryGet(out colorAdjustments);
    }

    public void TriggerDaze(CameraShake cameraShake)
    {
        // shake (lighter/quicker than a big impact shake)
        if (cameraShake != null)
        {
            cameraShake.Shake(shakeDuration, shakeStrength);
            
        }

        // white flash
        DOTween.To(() => colorAdjustments.postExposure.value,
                    x => colorAdjustments.postExposure.value = x,
                    flashExposure, flashInDuration)
               .SetEase(Ease.OutQuad)
               .OnComplete(() =>
               {
                   DOTween.To(() => colorAdjustments.postExposure.value,
                               x => colorAdjustments.postExposure.value = x,
                               0f, flashOutDuration)
                          .SetEase(Ease.InQuad);
               });

        // desaturate then recover ("dazed" feeling)
        DOTween.To(() => colorAdjustments.saturation.value,
                    x => colorAdjustments.saturation.value = x,
                    desaturateAmount, 0.1f)
               .SetEase(Ease.OutQuad)
               .OnComplete(() =>
               {
                   DOTween.To(() => colorAdjustments.saturation.value,
                               x => colorAdjustments.saturation.value = x,
                               0f, dazeDuration)
                          .SetEase(Ease.InOutQuad);
               });
    }
}