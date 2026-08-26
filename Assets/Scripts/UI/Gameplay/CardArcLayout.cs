using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 把子物体沿一段圆弧扇形排布，模拟"人手抓牌"的姿态。
/// 弧心位于容器下方，卡片中间最高、两端下垂并向外翻转。
/// 仅在 Rebuild 时计算一次（静态布局，无需每帧更新）。
/// </summary>
public class CardArcLayout : MonoBehaviour
{
    [Tooltip("弧半径（参考分辨率像素）。越大越平缓。")]
    public float radius = 1000f;
    [Tooltip("扇形总张角上限（度）。卡很多时自动收敛到该上限避免溢出。")]
    public float maxSpreadDeg = 100f;
    [Tooltip("相邻两张卡的张角（度）。决定卡片重叠程度。")]
    public float perCardDeg = 16f;
    [Tooltip("整段扇形相对容器原点的竖直偏移（参考像素），把扇形抬到屏幕中下部。")]
    public float baseYOffset = 360f;
    [Tooltip("屏幕边缘安全边距（参考像素）：最外侧卡的任何部分都不允许进入该边距内。")]
    public float safeMargin = 40f;
    [Tooltip("卡片整体缩放倍率（1=原始基准；构筑界面调大让卡更大）。")]
    public float scaleMultiplier = 1f;

    public void Rebuild(List<GameObject> items)
    {
        if (items == null || items.Count == 0) return;
        int n = items.Count;

        // 容器自身局部尺寸（CardContainer 是 1920×1080，卡片 localPosition 即此坐标系，不直接依赖屏幕分辨率）
        var parentRT = transform as RectTransform;
        float halfW = (parentRT != null && parentRT.rect.width > 1f) ? parentRT.rect.width * 0.5f : 960f;
        float parentH = (parentRT != null && parentRT.rect.height > 1f) ? parentRT.rect.height : 1080f;
        // 非 16:9 分辨率（4:3 / 16:10 等）经 CanvasScaler 适配后，可见逻辑区域比 CardContainer(1920×1080) 更窄，
        // 若不收紧安全区，弧形最外侧卡片在窄比例屏幕上会横向越界。取可见区域与容器二者更严格者。
        var canvas = transform.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.scaleFactor > 0.001f)
        {
            float visW = Screen.width / canvas.scaleFactor;
            float visH = Screen.height / canvas.scaleFactor;
            if (visW > 1f) halfW = Mathf.Min(halfW, visW * 0.5f);
            if (visH > 1f) parentH = Mathf.Min(parentH, visH);
        }
        float margin = safeMargin;

        // ① 测每张卡的【真实视觉尺寸】（含子物体立绘）。不能用 root rect——实测 root 只有 100×100，实际视觉约 300×450，
        //    之前一直读 root rect 导致"以为卡很小"、半径不收缩、两侧溢出。
        float cardW = 0f, cardH = 0f;
        foreach (var it in items)
        {
            var rt = it.GetComponent<RectTransform>();
            if (rt == null) continue;
            var lp = rt.localPosition; var lr = rt.localRotation; var ls = rt.localScale;
            rt.localPosition = Vector3.zero; rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
            Canvas.ForceUpdateCanvases();
            Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(transform, rt);
            float w = Mathf.Abs(b.size.x), h = Mathf.Abs(b.size.y);
            if (w > cardW) cardW = w;
            if (h > cardH) cardH = h;
            rt.localPosition = lp; rt.localRotation = lr; rt.localScale = ls;
        }
        if (cardW < 1f) cardW = 100f;
        if (cardH < 1f) cardH = 140f;

        float spread = (n == 1) ? 0f : Mathf.Min(maxSpreadDeg, perCardDeg * (n - 1));
        float scale = (n > 8 ? Mathf.Clamp(1f - (n - 8) * 0.05f, 0.6f, 1f) : 1f) * scaleMultiplier;
        float minScale = 0.35f * scaleMultiplier;
        float by0 = baseYOffset;

        // ② 由大到小试缩放：保证横向(由 r 公式保证)与纵向都落在安全区内，尽量保留靠下位置
        float r = radius, cw = cardW * scale, ch = cardH * scale, edgeRad = (spread * 0.5f) * Mathf.Deg2Rad;
        float edgeHalfW = 0f, edgeHalfH = 0f;
        for (int s = 0; s < 12; s++)
        {
            cw = cardW * scale; ch = cardH * scale;
            edgeRad = (spread * 0.5f) * Mathf.Deg2Rad;
            edgeHalfW = (Mathf.Abs(cw * Mathf.Cos(edgeRad)) + Mathf.Abs(ch * Mathf.Sin(edgeRad))) * 0.5f;
            edgeHalfH = (Mathf.Abs(cw * Mathf.Sin(edgeRad)) + Mathf.Abs(ch * Mathf.Cos(edgeRad))) * 0.5f;
            r = radius;
            if (n >= 2 && Mathf.Sin(edgeRad) > 1e-4f)
            {
                float maxSafeR = (halfW - margin - edgeHalfW) / Mathf.Sin(edgeRad);
                r = Mathf.Min(radius, maxSafeR);
                if (r < 100f) r = 100f;
            }
            float bottomY = by0 - r * (1f - Mathf.Cos(edgeRad)) - edgeHalfH;
            float topY = by0 + ch * 0.5f;
            if (bottomY >= margin && topY <= parentH - margin) break; // 都装下，停止缩放
            scale *= 0.9f;
            if (scale < minScale) { scale = minScale; break; }
        }

        // ③ 竖直：尽量保留 by0（靠下），仅当确实溢出时做最小贴边微调
        float by = by0;
        float bottomY2 = by - r * (1f - Mathf.Cos(edgeRad)) - edgeHalfH;
        float topY2 = by + ch * 0.5f;
        if (bottomY2 < margin) by += (margin - bottomY2);
        else if (topY2 > parentH - margin) by -= (topY2 - (parentH - margin));

        // ④ 摆放
        for (int i = 0; i < n; i++)
        {
            float t = (n == 1) ? 0.5f : i / (float)(n - 1);
            float ang = Mathf.Lerp(-spread / 2f, spread / 2f, t);
            float rad = ang * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * r;
            float y = Mathf.Cos(rad) * r;

            var rt = items[i].GetComponent<RectTransform>();
            if (rt == null) continue;
            rt.localPosition = new Vector3(x, y - r + by, 0f);
            rt.localRotation = Quaternion.Euler(0f, 0f, -ang);
            rt.localScale = new Vector3(scale, scale, 1f);
        }

        Debug.Log($"[CardArcLayout] n={n} cardVisual=({cardW:F0}x{cardH:F0}) spread={spread:F1} r={r:F1} scale={scale:F2} by={by:F1} edgeHalfW={edgeHalfW:F1} (limit {halfW - margin:F1})");
    }
}
