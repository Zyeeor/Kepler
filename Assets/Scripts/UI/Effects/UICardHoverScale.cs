using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 鼠标扫过/停留时把目标 RectTransform 平滑放大一点点，移开后弹回原大小。
/// 挂在实际接收射线的物体上（比如卡面的点击层），target 留空则缩放自身。
/// </summary>
public class UICardHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("要缩放的物体；不指定则缩放挂载本组件的这个 RectTransform。")]
    public RectTransform target;
    [Tooltip("悬停时的目标缩放倍数。")]
    public float hoverScale = 1.06f;
    [Tooltip("缩放动画速度。")]
    public float animSpeed = 10f;

    bool isHovered;

    void Awake()
    {
        if (target == null) target = transform as RectTransform;
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 dest = isHovered ? Vector3.one * hoverScale : Vector3.one;
        target.localScale = Vector3.Lerp(target.localScale, dest, Time.unscaledDeltaTime * animSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData) => isHovered = true;
    public void OnPointerExit(PointerEventData eventData) => isHovered = false;
}
