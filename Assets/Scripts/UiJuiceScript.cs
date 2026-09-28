using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIButtonJuice : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Hover - Scale")]
    public bool animateScale = true;
    public float hoverScale = 1.1f;

    [Header("Hover - Size (sizeDelta)")]
    public bool animateSize = false;
    public Vector2 hoverSizeIncrease = new Vector2(20f, 10f);

    [Header("Hover - Color")]
    public bool animateColor = true;
    [Tooltip("Leave empty to auto-find an Image/Text/TMP on this object")]
    public Graphic targetGraphic;
    public Color hoverColor = new Color(1f, 0.9f, 0.6f, 1f);

    [Header("Hover Timing")]
    public float hoverDuration = 0.15f;
    public AnimationCurve hoverCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Click Bounce")]
    public bool bounceOnClick = true;
    [Tooltip("Peak scale change as a fraction (0.15 = 15%)")]
    public float bounceStrength = 0.15f;
    [Tooltip("How fast it oscillates")]
    public float bounceFrequency = 14f;
    [Tooltip("How quickly the bounce dies out")]
    public float bounceDamping = 7f;
    public float bounceDuration = 0.6f;

    RectTransform rect;
    Selectable selectable;
    Vector3 baseScale;
    Vector2 baseSizeDelta;
    Color baseColor;

    bool hovered;
    float hoverT;
    float bounceTime;
    bool bouncing;

    void Awake()
    {
        rect = (RectTransform)transform;
        selectable = GetComponent<Selectable>();

        if (targetGraphic == null)
            targetGraphic = selectable != null && selectable.targetGraphic != null
                ? selectable.targetGraphic
                : GetComponent<Graphic>();

        baseScale = rect.localScale;
        baseSizeDelta = rect.sizeDelta;
        if (targetGraphic != null) baseColor = targetGraphic.color;
    }

    void OnEnable()
    {
        hovered = false;
        hoverT = 0f;
        bouncing = false;
        Apply(0f, 0f);
    }

    void OnDisable() => Apply(0f, 0f);

    public void OnPointerEnter(PointerEventData e) { if (CanInteract()) hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = false; }

    public void OnPointerClick(PointerEventData e)
    {
        if (!bounceOnClick || !CanInteract()) return;
        bouncing = true;
        bounceTime = 0f;
    }

    bool CanInteract() => selectable == null || selectable.IsInteractable();

    void Update()
    {
        float target = hovered ? 1f : 0f;
        bool hoverIdle = Mathf.Approximately(hoverT, target);
        if (hoverIdle && !bouncing) return;

        float dt = Time.unscaledDeltaTime;

        hoverT = Mathf.MoveTowards(hoverT, target, dt / Mathf.Max(0.0001f, hoverDuration));

        float punch = 0f;
        if (bouncing)
        {
            bounceTime += dt;
            if (bounceTime >= bounceDuration) bouncing = false;
            else
            {
                punch = -Mathf.Sin(bounceTime * bounceFrequency)
                        * Mathf.Exp(-bounceDamping * bounceTime)
                        * bounceStrength;
            }
        }

        Apply(hoverCurve.Evaluate(hoverT), punch);
    }

    void Apply(float hover, float punch)
    {
        float s = animateScale ? Mathf.Lerp(1f, hoverScale, hover) : 1f;
        rect.localScale = baseScale * (s * (1f + punch));

        if (animateSize)
            rect.sizeDelta = baseSizeDelta + hoverSizeIncrease * hover;

        if (animateColor && targetGraphic != null)
            targetGraphic.color = Color.Lerp(baseColor, hoverColor, hover);
    }
}