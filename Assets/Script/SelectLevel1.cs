using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class LevelSelectCube : MonoBehaviour
{
    [Header("Level Settings")]
    public string sceneToLoad; // e.g. "Level1" - set this in Inspector per cube

    [Header("Hover Bob Settings")]
    public float bobHeight = 0.2f;
    public float bobSpeed = 4f;
    public float scaleOnHover = 1.1f;
    public float transitionSpeed = 8f;

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
            bobTimer += Time.deltaTime * bobSpeed;
            float yOffset = Mathf.Sin(bobTimer) * bobHeight;
            transform.position = startPos + new Vector3(0, yOffset, 0);

            transform.localScale = Vector3.Lerp(
                transform.localScale,
                baseScale * scaleOnHover,
                Time.deltaTime * transitionSpeed
            );
        }
        else
        {
            transform.position = Vector3.Lerp(
                transform.position,
                startPos,
                Time.deltaTime * transitionSpeed
            );

            transform.localScale = Vector3.Lerp(
                transform.localScale,
                baseScale,
                Time.deltaTime * transitionSpeed
            );

            bobTimer = 0f;
        }
    }

    void OnMouseEnter()
    {
        isHovering = true;
    }

    void OnMouseExit()
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
            Debug.LogWarning("No scene name set for " + gameObject.name);
        }
    }
}