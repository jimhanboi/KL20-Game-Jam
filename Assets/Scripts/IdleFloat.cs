using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class IdleFloat : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Movement")]
    [Tooltip("How far it travels from the resting position, in pixels (UI) or units (world)")]
    public float amplitude = 10f;
    [Tooltip("Full up-and-down cycles per second")]
    public float speed = 0.5f;
    [Tooltip("Direction of the movement. Default is vertical.")]
    public Vector2 direction = Vector2.up;

    [Header("Variation")]
    [Tooltip("Random phase per instance so a group of elements doesn't move in sync")]
    public bool randomizePhase = true;
    [Range(0f, 1f)] public float phaseOffset = 0f;

    [Header("Hover (Buttons only)")]
    [Tooltip("Only takes effect if this object has a Button component")]
    public bool pauseOnHover = true;
    [Tooltip("How quickly it slows to a stop / speeds back up (seconds)")]
    public float pauseBlendTime = 0.15f;

    [Header("Timing")]
    public bool useUnscaledTime = true;

    RectTransform rect;   // null for non-UI objects
    Button button;        // null if this isn't a button
    Vector3 basePosition;
    float phase;
    float cycles;
    float motion = 1f;    // 1 = moving, 0 = frozen
    bool hovered;

    void Awake()
    {
        rect = transform as RectTransform;
        button = GetComponent<Button>();
        phase = randomizePhase ? Random.value : phaseOffset;
    }

    void OnEnable()
    {
        hovered = false;
        motion = 1f;
        Rebase();
    }

    void OnDisable() => SetPosition(basePosition);

    public void OnPointerEnter(PointerEventData e)
    {
        if (button != null) hovered = true;
    }

    public void OnPointerExit(PointerEventData e) => hovered = false;

    /// <summary>Manual override from code. Bypasses the Button check.</summary>
    public void SetHovered(bool value) => hovered = value;

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        bool shouldPause = pauseOnHover && hovered
                           && (button == null || button.IsInteractable());

        float target = shouldPause ? 0f : 1f;
        motion = Mathf.MoveTowards(motion, target, dt / Mathf.Max(0.0001f, pauseBlendTime));

        cycles += dt * speed * motion;

        float wave = Mathf.Sin((cycles + phase) * Mathf.PI * 2f);
        Vector3 offset = (Vector3)(direction.normalized * (wave * amplitude));
        SetPosition(basePosition + offset);
    }

    public void Rebase() => basePosition = rect != null ? (Vector3)rect.anchoredPosition : transform.localPosition;

    void SetPosition(Vector3 p)
    {
        if (rect != null) rect.anchoredPosition = p;
        else transform.localPosition = p;
    }
}