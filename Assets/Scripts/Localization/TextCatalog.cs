using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一文本目录（Text Catalog）—— 玩家可见文本的唯一真源。
///
/// 背景：项目此前文本散落（CardData 卡名/描述、TutorialStepConfig 内联中文、UI 脚本硬编码 .text=、场景 TMP 默认文本），
/// 无法统一查看、无法统一换字体/换文案。本系统按 Canonical 要求（Dual_Line_Text_Requirements_v1.0 §1）：
/// "玩家可见文本通过稳定 Text Key 与 Concept Key 读取，不在 Gameplay 代码中写死最终词汇"。
///
/// 设计：
///   - 每条目 = 稳定 textKey + 默认文本（v1 中文）；预留 Mythic/System 双线字段（Display Profile 启用后使用）；
///   - 代码/UI 通过 TextCatalog.Get(key) 取文本，文案修改只动资产不动代码；
///   - 支持 {PLACEHOLDER} 占位符（string.Format 风格，见 Get(key, args)）；
///   - 资产路径建议：Assets/Settings/Text/TextCatalog.asset。
/// </summary>
[CreateAssetMenu(menuName = "Kepler/Text/Text Catalog", fileName = "TextCatalog")]
public class TextCatalog : ScriptableObject
{
    [Tooltip("文本条目（key 必须唯一；建议命名空间前缀，如 ui.pause.resume / tut.step.01.title）")]
    public List<TextEntry> entries = new List<TextEntry>();

    /// <summary>运行时已加载的实例（Editor 预览 / 运行时解析共用）。</summary>
    static TextCatalog _loaded;

    /// <summary>文本目录资产（外部注入或资源加载）。</summary>
    public static TextCatalog Instance
    {
        get
        {
            if (_loaded == null)
                _loaded = Resources.Load<TextCatalog>("Text/TextCatalog");
            return _loaded;
        }
        set => _loaded = value;
    }

    /// <summary>按 key 查文本（无命中返回 key 本身，保证不抛空、便于排查漏配）。</summary>
    public string Get(string key, params object[] args)
    {
        string raw = Lookup(key);
        return (args == null || args.Length == 0) ? raw : SafeFormat(raw, args);
    }

    /// <summary>按 key 查原始文本（不做格式化）。</summary>
    public string Lookup(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        var list = entries;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].key == key)
                return list[i].DefaultText;
        }
#if UNITY_EDITOR
        // Editor 下提示漏配（运行时静默返回 key，避免刷屏）
        Debug.LogWarning($"[TextCatalog] 未找到文本 Key：'{key}'");
#endif
        return key;
    }

    /// <summary>占位符格式化（{0}/{1}…）；格式异常时回退原文，不抛异常。</summary>
    static string SafeFormat(string template, object[] args)
    {
        try { return string.Format(template, args); }
        catch (FormatException) { return template; }
    }

    /// <summary>静态快捷入口：TextCatalog.Get("ui.resume")。</summary>
    public static string Get(string key) => Instance != null ? Instance.Lookup(key) : key;
}

/// <summary>单条文本（Key → 默认文本；双线字段预留）。</summary>
[Serializable]
public class TextEntry
{
    [Tooltip("稳定文本 Key（唯一，全局命名空间前缀）")]
    public string key;

    [Tooltip("默认文本（v1 中文）")]
    [TextArea(1, 4)]
    public string text;

    [Tooltip("Mythic 线（双线 Canonical 预留；未启用时用默认文本）")]
    [TextArea(1, 4)]
    public string mythicText;

    [Tooltip("System 线（双线 Canonical 预留；未启用时用默认文本）")]
    [TextArea(1, 4)]
    public string systemText;

    /// <summary>当前生效文本（按 Display Profile；v1 恒为默认文本）。</summary>
    public string DefaultText
    {
        get
        {
            // TODO(display-profile)：接入 Narrative Access 后按 Access 返回 mythic/system
            return string.IsNullOrEmpty(text) ? key : text;
        }
    }
}
