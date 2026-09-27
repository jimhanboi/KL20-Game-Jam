using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class ScreenDistortion : MonoBehaviour
{
    public Volume distortionVolume; // drag the DistortionVolume object here
    public float maxIntensity = 0.6f;
    public float fadeInTime = 0.5f;
    public float holdTime = 1.5f;
    public float fadeOutTime = 1f;

    private LensDistortion lensDistortion;
    private Coroutine currentEffect;

    void Start()
    {
        distortionVolume.profile.TryGet(out lensDistortion);
    }

    public void TriggerDistortion()
    {
        if (currentEffect != null) StopCoroutine(currentEffect);
        currentEffect = StartCoroutine(DistortRoutine());
    }

    IEnumerator DistortRoutine()
    {
        // fade in
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            lensDistortion.intensity.value = Mathf.Lerp(0f, maxIntensity, t / fadeInTime);
            yield return null;
        }
        lensDistortion.intensity.value = maxIntensity;

        yield return new WaitForSeconds(holdTime);

        // fade out
        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            lensDistortion.intensity.value = Mathf.Lerp(maxIntensity, 0f, t / fadeOutTime);
            yield return null;
        }
        lensDistortion.intensity.value = 0f;
    }
}