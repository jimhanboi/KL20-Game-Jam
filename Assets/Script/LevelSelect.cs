using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LevelSelect : MonoBehaviour
{
    public Camera PlayerCamera;

    public float interactDistance = 3f;
    public string sceneToLoad;
    private bool isLookingAtObject = false;

    [Header("Bop Settings")]
    public float bobHeight = 0.1f;
    public float bobSpeed = 4f;
    public float returnSpeed = 8f;

    [Header("Portal Travel Settings")]
    public float travelDuration = 2f;
    public float distanceFromCamera = 0.1f;
    public float targetScale = 15f;
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Flash / Load Sync")]
    public FlashDaze flashDaze;
    public float delayBeforeLoad = 0.08f; // should match flashDaze's flashInDuration, so scene loads right as screen goes white

    private Vector3 startPos;
    private Vector3 startScale;
    private bool isTransitioning = false;

    void Start()
    {
        startPos = transform.localPosition;
        startScale = transform.localScale;
    }

    void Update()
    {
        if (isTransitioning) return;

        CheckIfLookedAt();
        HandleBop();

        if (isLookingAtObject && Mouse.current.leftButton.wasPressedThisFrame)
        {
            SelectLevel();
        }
    }

    void CheckIfLookedAt()
    {
        Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            if (hit.transform == transform)
            {
                isLookingAtObject = true;
                return;
            }
        }

        isLookingAtObject = false;
    }

    void HandleBop()
    {
        if (isLookingAtObject)
        {
            float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.localPosition = new Vector3(startPos.x, newY, startPos.z);
        }
        else
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, startPos, Time.deltaTime * returnSpeed);
        }
    }

    void SelectLevel()
    {
        isTransitioning = true;
        StartCoroutine(TravelToPlayerThenLoad());
    }

    IEnumerator TravelToPlayerThenLoad()
    {
        Vector3 startWorldPos = transform.position;
        Vector3 targetWorldPos = PlayerCamera.transform.position +
                                   (transform.position - PlayerCamera.transform.position).normalized * distanceFromCamera;

        float t = 0f;
        while (t < travelDuration)
        {
            t += Time.deltaTime;
            float rawProgress = t / travelDuration;
            float easedProgress = easeCurve.Evaluate(rawProgress);

            transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, easedProgress);
            transform.localScale = Vector3.Lerp(startScale, startScale * targetScale, easedProgress);

            yield return null;
        }

        transform.position = targetWorldPos;
        transform.localScale = startScale * targetScale;

        yield return ScreenFader.Instance.FadeIn(0.3f);

        SceneManager.LoadScene(sceneToLoad);
    }
}