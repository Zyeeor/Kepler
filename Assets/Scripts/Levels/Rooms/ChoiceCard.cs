using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to a choice card root GameObject.
/// The card smoothly zooms in when hovered, and back out when unhovered.
/// </summary>
public class ChoiceCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Target scale multiplier when hovered.")]
    public float hoverScale = 1.15f;
    [Tooltip("Animation speed.")]
    public float animSpeed = 8f;

    [Header("Hover Wobble")]
    [Tooltip("Optional empty RectTransform containing only the card elements that should wobble.")]
    public RectTransform wobbleTarget;
    [Range(0f, 10f)] public float hoverWobbleAngle = 1.5f;
    [Min(0.01f)] public float hoverWobbleFrequency = 5f;
    [Min(0.01f)] public float hoverWobbleDuration = 0.35f;

    private RectTransform rect;
    private Quaternion wobbleBaseRotation;
    private float wobbleElapsed;
    private bool isHovered;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        CacheWobbleBaseRotation();
    }

    void OnEnable()
    {
        isHovered = false;
        wobbleElapsed = hoverWobbleDuration;
        CacheWobbleBaseRotation();
    }

    void LateUpdate()
    {
        Vector3 target = isHovered ? Vector3.one * hoverScale : Vector3.one;
        rect.localScale = Vector3.Lerp(rect.localScale, target, Time.unscaledDeltaTime * animSpeed);
        ApplyWobble();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        isHovered = true;
        wobbleElapsed = 0f;
    }

    public void OnPointerExit(PointerEventData e)
    {
        isHovered = false;
        wobbleElapsed = hoverWobbleDuration;
    }

    private void ApplyWobble()
    {
        if (wobbleTarget == null) return;

        float duration = Mathf.Max(0.01f, hoverWobbleDuration);
        float progress = Mathf.Clamp01(wobbleElapsed / duration);
        float wobble = Mathf.Sin(wobbleElapsed * hoverWobbleFrequency * Mathf.PI * 2f)
            * hoverWobbleAngle
            * (1f - progress);
        wobbleTarget.localRotation = wobbleBaseRotation * Quaternion.Euler(0f, 0f, wobble);
        wobbleElapsed = Mathf.Min(duration, wobbleElapsed + Time.unscaledDeltaTime);
    }

    private void CacheWobbleBaseRotation()
    {
        if (wobbleTarget != null) wobbleBaseRotation = wobbleTarget.localRotation;
    }
}
