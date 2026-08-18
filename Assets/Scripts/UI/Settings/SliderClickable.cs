using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 让 Slider 支持"点击轨道任意位置直接跳转"（Unity 原生 Slider 只能拖 thumb，
/// 轨道点击无反应，导致拇指目标小、难精确操作）。
/// 挂在与 Slider 同对象上即可；点击后同时触发 onValueChanged（与拖动一致）。
/// </summary>
[RequireComponent(typeof(Slider))]
public class SliderClickable : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        var slider = GetComponent<Slider>();
        if (slider == null || !slider.interactable) return;

        var rect = (RectTransform)transform;
        // 把屏幕点击点转到本地坐标
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        Rect r = rect.rect;
        float pct;
        switch (slider.direction)
        {
            case Slider.Direction.LeftToRight:
                pct = r.width > 0f ? (local.x - r.x) / r.width : 0f;
                break;
            case Slider.Direction.RightToLeft:
                pct = r.width > 0f ? 1f - (local.x - r.x) / r.width : 0f;
                break;
            case Slider.Direction.BottomToTop:
                pct = r.height > 0f ? (local.y - r.y) / r.height : 0f;
                break;
            default: // TopToBottom
                pct = r.height > 0f ? 1f - (local.y - r.y) / r.height : 0f;
                break;
        }

        slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, Mathf.Clamp01(pct));
    }
}
