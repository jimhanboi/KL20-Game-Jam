using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance;

    public CanvasGroup fadeCanvasGroup;
    public float defaultFadeOutDuration = 0.6f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        fadeCanvasGroup.alpha = 0f; // ensure it starts invisible
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(Fade(1f, 0f, defaultFadeOutDuration));
    }

    public IEnumerator FadeIn(float duration)
    {
        yield return Fade(0f, 1f, duration);
    }

    public IEnumerator FadeOut(float duration)
    {
        yield return Fade(1f, 0f, duration);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        fadeCanvasGroup.alpha = from;

        while (t < duration)
        {
            t += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
    }
}