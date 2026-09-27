using UnityEngine;
using UnityEngine.SceneManagement;

public class levelSelectCube : MonoBehaviour
{
    public string sceneToLoad;

    public float bobbingHeight = 0.2f;
    public float bobbingSpeed = 4f;
    public float scaleonHover = 1.2f;
    public float transitionSpeed = 5f;

    private Vector3 startPos;
    private Vector3 baseScale;
    private bool isHovering = false;
    private float bobTimer = 0f;

    void Start()
    {
        startPos = transform.position;
        baseScale = transform.localScale;
    }

    void Update()
    {
        if (isHovering)
        {
            bobTimer += Time.deltaTime * bobbingSpeed;
            float yOffset = Mathf.Sin(bobTimer) * bobbingHeight;
            transform.position = startPos + new Vector3(0, yOffset, 0);

            transform.localScale = Vector3.Lerp(
                transform.localScale,
                baseScale * scaleonHover,
                Time.deltaTime * transitionSpeed);
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, startPos, Time.deltaTime * transitionSpeed);
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.deltaTime * transitionSpeed);
        }

        bobTimer = 0f;
    }

    void onMouseEnter()
    {
        isHovering = true;
    }
    void onMouseExit()
    {
        isHovering = false;
    }

    void OnMouseDown()
    {
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogError("Scene " + sceneToLoad + " not found. Please check the scene name and ensure it is added to the build settings.");
        }
    }
}





