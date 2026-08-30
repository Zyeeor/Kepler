using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 卡牌图鉴专用卡片视图——与局内选卡 UI（choice1/CoreChoiceCard）完全独立的展示预制体。
///
/// 之前图鉴直接复用 choice1 预制体：其内部 foreground/middleground/background 三个图层
/// 在预制体里的画布尺寸本就互不相同（如 middleground 500×800 明显大于 foreground/background
/// 的 300×600），不同卡牌又按 CardData 配置各自启用其中若干层，导致按"实际可见图层内容"
/// 测量包围盒来定缩放比例时，每张卡换算出的倍率都不一样——表现为卡与卡之间大小不一致，
/// 且已知/未知两种状态也很难对齐。
///
/// 本预制体改为固定同一块画布（frame 图层尺寸），插画图层（background/middleground/foreground）
/// 铺满整个画布（不留白边），边框（borderOverlayImage）作为独立图层叠加在插画之上——
/// 边框素材是从 Unknown Card.png 抠出内部纯色区域后得到的"镂空"版本，只保留描边，
/// 因此叠在插画上方时插画能透过中间完整显示。未知卡则直接显示 frameImage（Unknown Card.png
/// 本体，带底色的占位卡），与已知/已解锁共享同一块画布尺寸，两种状态天然一样大。
///
/// 每层还支持 CardData 的"额外并列素材"列表（extraForegroundSprites 等，局内 CoreChoiceCard
/// 用于同层视差叠加）——图鉴不需要视差动效，但仍需要把这些素材实际显示出来，否则配置了额外
/// 素材的卡在图鉴里会比局内选卡界面缺内容。额外素材动态生成，尺寸与所属层一致（同样铺满画布），
/// 按列表顺序叠在该层基础素材之上，插在该层与下一层之间（保持 background→middleground→foreground
/// 的整体前后顺序不变）。
/// </summary>
public class CardArchiveTileView : MonoBehaviour
{
    [Header("未知态占位（Unknown Card.png 本体，未知时显示）")]
    [SerializeField] Image frameImage;

    [Header("插画图层（铺满画布，已知/已解锁时显示）")]
    [SerializeField] RectTransform artRoot;
    [SerializeField] Image backgroundImage;
    [SerializeField] Image middlegroundImage;
    [SerializeField] Image foregroundImage;

    [Header("边框叠加层（镂空描边，叠在插画之上，已知/已解锁时显示）")]
    [SerializeField] Image borderOverlayImage;

    readonly List<Image> _extraBackgroundImages = new List<Image>();
    readonly List<Image> _extraMiddlegroundImages = new List<Image>();
    readonly List<Image> _extraForegroundImages = new List<Image>();

    /// <summary>state: 0=未知 1=已知（灰） 2=已解锁（彩色）。</summary>
    public void Bind(CardData card, int state, Sprite frameSprite)
    {
        bool known = state >= 1;

        // 未知态：只显示占位卡本体；已知/已解锁态：只显示插画 + 边框叠加层。
        if (frameImage != null)
        {
            frameImage.sprite = frameSprite;
            frameImage.enabled = !known && frameSprite != null;
        }
        if (artRoot != null) artRoot.gameObject.SetActive(known);
        if (borderOverlayImage != null) borderOverlayImage.gameObject.SetActive(known);

        ClearExtraLayers();
        if (!known) return;

        bool bgOn = card != null && !card.hideBackgroundLayer;
        bool mgOn = card != null && !card.hideMiddlegroundLayer;
        bool fgOn = card != null && !card.hideForegroundLayer;

        SetLayer(backgroundImage, bgOn ? card.backgroundSprite : null);
        SetLayer(middlegroundImage, mgOn ? card.middlegroundSprite : null);
        SetLayer(foregroundImage, fgOn ? card.foregroundSprite : null);

        ApplyExtraLayers(backgroundImage, bgOn ? card.extraBackgroundSprites : null, _extraBackgroundImages);
        ApplyExtraLayers(middlegroundImage, mgOn ? card.extraMiddlegroundSprites : null, _extraMiddlegroundImages);
        ApplyExtraLayers(foregroundImage, fgOn ? card.extraForegroundSprites : null, _extraForegroundImages);

        // Known（已遇见未解锁）整体置灰，仅改 RGB，不动 alpha；边框叠加层与额外素材同步置灰。
        var tint = state == 2 ? Color.white : new Color(0.55f, 0.55f, 0.55f);
        SetTint(backgroundImage, tint);
        SetTint(middlegroundImage, tint);
        SetTint(foregroundImage, tint);
        foreach (var img in _extraBackgroundImages) img.color = tint;
        foreach (var img in _extraMiddlegroundImages) img.color = tint;
        foreach (var img in _extraForegroundImages) img.color = tint;
        if (borderOverlayImage != null) borderOverlayImage.color = tint;
    }

    static void SetLayer(Image img, Sprite sprite)
    {
        if (img == null) return;
        img.sprite = sprite;
        img.enabled = sprite != null;
    }

    static void SetTint(Image img, Color tint)
    {
        if (img == null || img.sprite == null) return;
        img.color = tint;
    }

    /// <summary>
    /// 为一个图层生成"额外并列素材"的动态子层：与所属基础层同尺寸（铺满画布），
    /// 按列表顺序插在该层紧后方，保持 background/middleground/foreground 的整体叠放顺序。
    /// </summary>
    void ApplyExtraLayers(Image baseImage, List<Sprite> extraSprites, List<Image> tracked)
    {
        if (baseImage == null || !baseImage.enabled) return;
        if (extraSprites == null || extraSprites.Count == 0) return;

        int insertAt = baseImage.transform.GetSiblingIndex() + 1;
        int inserted = 0;
        for (int i = 0; i < extraSprites.Count; i++)
        {
            var sprite = extraSprites[i];
            if (sprite == null) continue;

            var go = new GameObject("Extra_" + baseImage.name + "_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(baseImage.transform.parent, false);
            rt.anchorMin = baseImage.rectTransform.anchorMin;
            rt.anchorMax = baseImage.rectTransform.anchorMax;
            rt.offsetMin = baseImage.rectTransform.offsetMin;
            rt.offsetMax = baseImage.rectTransform.offsetMax;
            rt.pivot = baseImage.rectTransform.pivot;
            rt.localScale = Vector3.one;
            rt.SetSiblingIndex(insertAt + inserted);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.preserveAspect = false;

            tracked.Add(img);
            inserted++;
        }
    }

    void ClearExtraLayers()
    {
        DestroyExtras(_extraBackgroundImages);
        DestroyExtras(_extraMiddlegroundImages);
        DestroyExtras(_extraForegroundImages);
    }

    static void DestroyExtras(List<Image> list)
    {
        foreach (var img in list)
        {
            if (img == null) continue;
            if (Application.isPlaying) Destroy(img.gameObject);
            else DestroyImmediate(img.gameObject);
        }
        list.Clear();
    }
}
