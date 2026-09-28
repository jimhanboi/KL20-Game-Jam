using UnityEngine;

public class LookAtFadeText : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Camera playerCamera;

    [Header("Look Settings")]
    [SerializeField] private float maxDistance = 15f;
    [SerializeField] private float lookAngle = 15f;   // how close to the center of the screen it must be
    [SerializeField] private float lookDelay = 1f;    // seconds of looking before fade-in

    [Header("Fade Settings")]
    [SerializeField] private float fadeInSpeed = 2f;
    [SerializeField] private float fadeOutSpeed = 3f;

    [Header("Optional")]
    [SerializeField] private bool faceCamera = true;

    private float lookTimer;

    void Start()
    {
        if (playerCamera == null)
        {
            Debug.LogError("LookAtFadeText: PlayerCamera is not assigned.", this);
            enabled = false;
            return;
        }

        canvasGroup.alpha = 0f;
    }

    void Update()
    {
        bool isLooking = IsLookingAtText();

        if (isLooking)
            lookTimer += Time.deltaTime;
        else
            lookTimer = 0f;

        float targetAlpha = (isLooking && lookTimer >= lookDelay) ? 1f : 0f;
        float speed = targetAlpha > canvasGroup.alpha ? fadeInSpeed : fadeOutSpeed;

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, speed * Time.deltaTime);

        if (faceCamera)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - playerCamera.transform.position);
        }
    }

    bool IsLookingAtText()
    {
        Vector3 toText = transform.position - playerCamera.transform.position;

        if (toText.magnitude > maxDistance)
            return false;

        float angle = Vector3.Angle(playerCamera.transform.forward, toText);
        return angle <= lookAngle;
    }
}