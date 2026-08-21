using TMPro;

/// <summary>
/// 跨模块共享的 UI 字体资产引用（运行时注入）。
/// 背景：项目主 UI 字体 rocky-rockin-2 无中文字形，代码动态创建的 TMP 文本
/// （如精英网络状态提示）无法走场景 YAML 注入字体，需经此静态点取中文字体。
/// 注入方：TutorialController.Start（bannerFont 场景字段，SourceHanSansSC-Light-2 SDF）。
/// 消费方：EliteNetworkStatusUI 等动态建字组件（null 时回退 TMP 默认字体，行为同旧）。
/// </summary>
public static class UiFontAssets
{
    /// <summary>中文字体（含 CJK 字形）。由场景初始化方注入；未注入为 null。</summary>
    public static TMP_FontAsset Chinese { get; set; }

    /// <summary>取中文字体；null 时回退 TMP 默认字体。</summary>
    public static TMP_FontAsset ChineseOrDefault
        => Chinese != null ? Chinese : TMP_Settings.defaultFontAsset;
}
