using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance;

    public CanvasGroup fadeCanvasGroup;
    public float defaultFadeOutDuration = 0.6f;

    private int fadeId;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log("Duplicate ScreenFader destroyed: " + gameObject.scene.name);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        fadeCanvasGroup.alpha = 0f; // ensure it starts invisible

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (this != Instance || fadeCanvasGroup == null) return;
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
        if (fadeCanvasGroup == null) yield break;

        int myId = ++fadeId; // any newer fade cancels this one
        float t = 0f;
        fadeCanvasGroup.alpha = from;

        while (t < duration)
        {
            if (fadeCanvasGroup == null || myId != fadeId) yield break;

            t += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }

        if (fadeCanvasGroup != null && myId == fadeId)
            fadeCanvasGroup.alpha = to;
    }
}