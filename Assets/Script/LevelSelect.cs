using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

public class LevelSelect : MonoBehaviour
{
    public Camera PlayerCamera;
    public Transform EyeRoot; // the object that actually moves (parent of the camera, e.g. your "Player" object)
    public CharacterController playerController; // optional, only if your player uses one
    [SerializeField] bool leftEyeRequired;

    public Transform destinationPoint; // where the player teleports to
    private bool isLookingAtObject = false;
    private bool wasLookingAtObject = false;

    [Header("Bop Settings")]
    public float bobHeight = 0.1f;
    public float bobSpeed = 4f;      // same meaning as before: one full up/down cycle takes 2π / bobSpeed seconds
    public float returnSpeed = 8f;   // return takes roughly 1 / returnSpeed seconds
    public Ease bobEase = Ease.InOutSine;
    public Ease returnEase = Ease.OutQuad;

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
    private Tween bobTween;


    [SerializeField] EyeControllerManager manager; // assign in the inspector

    void Start()
    {
        startPos = transform.localPosition;
        startScale = transform.localScale;
    }

    void Update()
    {
        if (isTransitioning) return;
        UpdateBop();

        if (isLookingAtObject && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            SelectLevel();
        }
    }

    void OnDestroy()
    {
        bobTween?.Kill();
    }


    public void OnLook()
    {
        isLookingAtObject = true;
    }

    public void OnStopLook()
    {
        isLookingAtObject= false;
    }

    // Only reacts when the look state changes, instead of every frame
    void UpdateBop()
    {
        if (isLookingAtObject == wasLookingAtObject) return;
        wasLookingAtObject = isLookingAtObject;

        bobTween?.Kill();

        if (isLookingAtObject)
        {
            // Bob up and down forever (yoyo) until the player looks away
            bobTween = transform
                .DOLocalMoveY(startPos.y + bobHeight, Mathf.PI / bobSpeed)
                .SetEase(bobEase)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }
        else
        {
            // Glide back to the resting position from wherever the bob currently is
            bobTween = transform
                .DOLocalMove(startPos, 1f / returnSpeed)
                .SetEase(returnEase)
                .SetLink(gameObject);
        }
    }

    void SelectLevel()
    {
        isTransitioning = true;

        // Stop the bop so it doesn't fight the travel animation
        bobTween?.Kill();

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

        // Force the bop state to re-evaluate on the next frame
        wasLookingAtObject = false;
        isTransitioning = false;
    }

    void TeleportPlayer()
    {
        if (playerController != null)
        {
            // CharacterController must be disabled before moving its transform directly,
            // otherwise it fights against the manual position change
            playerController.enabled = false;
            EyeRoot.position = destinationPoint.position;
            EyeRoot.rotation = destinationPoint.rotation;
            playerController.enabled = true;
        }
        else
        {
            if (leftEyeRequired) manager.TeleportLeftEye(destinationPoint);
            else manager.TeleportRightEye(destinationPoint);
        }
    }
}