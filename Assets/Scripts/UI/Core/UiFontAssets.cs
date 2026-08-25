using TMPro;
using UnityEngine;

/// <summary>
/// 跨模块共享的 UI 字体资产引用（运行时注入 → 已升级为 FontRegistry 驱动）。
///
/// 背景：项目主 UI 字体 rocky-rockin-2 无中文字形，代码动态创建的 TMP 文本
/// （如精英网络状态提示）无法走场景 YAML 注入字体，需经此静态点取中文字体。
///
/// 现状：字体统一由 FontRegistry（ScriptableObject）管理。本静态类只保留旧 API 作为兼容代理，
/// 不允许调用方通过静态字段绕过 FontRegistry 覆盖全局字体。
/// </summary>
public static class UiFontAssets
{
    /// <summary>
    /// 旧版注入入口，仅为兼容已有场景序列化和调用方保留；实际字体始终由 FontRegistry 决定。
    /// </summary>
    public static TMP_FontAsset Chinese { get; set; }

    /// <summary>取中文字体；FontRegistry 缺失时仅回退 TMP 默认字体，不绕过注册表。</summary>
    public static TMP_FontAsset ChineseOrDefault
    {
        get
        {
            var registry = FontRegistry.Instance;
            return registry != null && registry.DefaultFont != null
                ? registry.DefaultFont
                : TMP_Settings.defaultFontAsset;
        }
    }

    /// <summary>通过 FontRegistry 为动态创建的 TMP 文本应用字体和匹配材质。</summary>
    public static bool ApplyTo(TMP_Text text, string slot = FontSlots.Default)
    {
        if (text == null) return false;
        var registry = FontRegistry.Instance;
        if (registry == null)
        {
            Debug.LogError("[UiFontAssets] 未找到 Resources/Text/FontRegistry，动态 TMP 文本无法统一应用字体。");
            return false;
        }

        return registry.ApplyFontToText(text, slot);
    }
}
