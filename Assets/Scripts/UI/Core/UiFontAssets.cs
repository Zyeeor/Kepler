using TMPro;

/// <summary>
/// 跨模块共享的 UI 字体资产引用（运行时注入 → 已升级为 FontRegistry 驱动）。
///
/// 背景：项目主 UI 字体 rocky-rockin-2 无中文字形，代码动态创建的 TMP 文本
/// （如精英网络状态提示）无法走场景 YAML 注入字体，需经此静态点取中文字体。
///
/// 现状：字体统一由 FontRegistry（ScriptableObject）管理。本静态类保留旧 API（Chinese/ChineseOrDefault）
/// 供既有动态建字组件无感使用：未显式注入时回退 FontRegistry.DefaultFont。
/// </summary>
public static class UiFontAssets
{
    /// <summary>显式注入的中文字体（兼容旧调用方；null 时回退 FontRegistry）。</summary>
    public static TMP_FontAsset Chinese { get; set; }

    /// <summary>取中文字体：显式注入 > FontRegistry.DefaultFont > TMP 默认字体。</summary>
    public static TMP_FontAsset ChineseOrDefault
    {
        get
        {
            if (Chinese != null) return Chinese;
            var reg = FontRegistry.Instance;
            if (reg != null) return reg.DefaultFont;
            return TMP_Settings.defaultFontAsset;
        }
    }
}
