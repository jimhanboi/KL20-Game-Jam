using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class LevelSelect : MonoBehaviour
{
    public Camera PlayerCamera;
    public Transform PlayerRoot; // the object that actually moves (parent of the camera, e.g. your "Player" object)
    public CharacterController playerController; // optional, only if your player uses one

    public float interactDistance = 3f;
    public Transform destinationPoint; // where the player teleports to
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

    [Header("Flash / Teleport Sync")]
    public FlashDaze flashDaze;
    public float delayBeforeTeleport = 0.08f;
    public float fadeInDuration = 0.3f;
    public float fadeOutDuration = 0.3f;

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

        if (isLookingAtObject && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
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
        StartCoroutine(TravelToPlayerThenTeleport());
    }

    IEnumerator TravelToPlayerThenTeleport()
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

        yield return new WaitForSeconds(delayBeforeTeleport);

        // Only fade if the fader exists and is intact
        ScreenFader fader = ScreenFader.Instance;
        bool canFade = fader != null && fader.fadeCanvasGroup != null;

        // fade to white
        if (canFade) yield return fader.FadeIn(fadeInDuration);

        // teleport the player
        TeleportPlayer();

        // fade back to normal
        if (canFade) yield return fader.FadeOut(fadeOutDuration);

        // reset portal so it can be used again
        transform.position = startWorldPos;
        transform.localScale = startScale;
        isTransitioning = false;
    }

    void TeleportPlayer()
    {
        if (playerController != null)
        {
            // CharacterController must be disabled before moving its transform directly,
            // otherwise it fights against the manual position change
            playerController.enabled = false;
            PlayerRoot.position = destinationPoint.position;
            PlayerRoot.rotation = destinationPoint.rotation;
            playerController.enabled = true;
        }
        else
        {
            PlayerRoot.position = destinationPoint.position;
            PlayerRoot.rotation = destinationPoint.rotation;
        }
    }
}