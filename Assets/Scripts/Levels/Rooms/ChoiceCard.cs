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

    private RectTransform rect;
    private bool isHovered;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        Vector3 target = isHovered ? Vector3.one * hoverScale : Vector3.one;
        rect.localScale = Vector3.Lerp(rect.localScale, target, Time.unscaledDeltaTime * animSpeed);
    }

    public void OnPointerEnter(PointerEventData e) { isHovered = true; }
    public void OnPointerExit(PointerEventData e) { isHovered = false; }
}
