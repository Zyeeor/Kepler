using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 统一字体注册表（Font Registry）—— 全局字体集中管理与一键替换。
///
/// 背景：此前字体资产分散在场景 28 处 TMP 组件 + 若干动态建字代码，统一换字体需逐个改。
/// 本系统把字体集中到一个 SO：改资产 → ApplyAll 一键替换所有 TMP 文本组件。
///
/// 设计：
///   - slots 按"用途"分槽（默认/标题/数字/粗体…），每种用途一个 TMP_FontAsset；
///   - ApplyAllToScene()：遍历当前活动场景（含未激活对象）所有 TMP_Text，把字体槽内资产套用上去；
///   - 动态建字代码统一走 FontRegistry.Instance.Get(slot)，替代零散取字体逻辑；
///   - 资产路径建议：Assets/Settings/Text/FontRegistry.asset。
/// </summary>
[CreateAssetMenu(menuName = "Kepler/Text/Font Registry", fileName = "FontRegistry")]
public class FontRegistry : ScriptableObject
{
    [Tooltip("字体槽（按用途分槽；至少保留 Default 槽，供全局替换与动态建字回退）")]
    public List<FontSlot> slots = new List<FontSlot>();

    static FontRegistry _loaded;

    /// <summary>运行时实例（外部注入或 Resources 加载）。</summary>
    public static FontRegistry Instance
    {
        get
        {
            if (_loaded == null)
                _loaded = Resources.Load<FontRegistry>("Text/FontRegistry");
            return _loaded;
        }
        set => _loaded = value;
    }

    /// <summary>按槽名取字体（无命中回退 Default 槽；再无回退 TMP 默认字体，保证不 null）。</summary>
    public TMP_FontAsset GetFont(string slot)
    {
        if (string.IsNullOrEmpty(slot)) return DefaultFont;
        var list = slots;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].slotName == slot)
                return list[i].font != null ? list[i].font : DefaultFont;
        }
        return DefaultFont;
    }

    /// <summary>Default 槽字体（回退链末端：TMP_Settings.defaultFontAsset）。</summary>
    public TMP_FontAsset DefaultFont
    {
        get
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].slotName == FontSlots.Default && slots[i].font != null)
                    return slots[i].font;
            }
            return TMP_Settings.defaultFontAsset;
        }
    }

    /// <summary>将字体槽内资产应用到当前活动场景全部 TMP 文本（含 inactive，不含 DontDestroyOnLoad 域）。</summary>
    public void ApplyAllToActiveScene()
    {
        if (Application.isPlaying) return; // 设计期工具；运行时由 FontApplier 处理（防误改预制体/场景运行时状态）
        var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        int applied = 0;
        foreach (var root in rootObjects)
        {
            if (root == null) continue;
            applied += ApplyToTree(root.transform);
        }
        Debug.Log($"[FontRegistry] 已替换 {applied} 个 TMP 文本组件的字体（场景：{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}）。");
    }

    /// <summary>把本资产内所有槽字体应用到指定 transform 子树（工具与运行时共用）。</summary>
    public int ApplyToTree(Transform root)
    {
        if (root == null) return 0;
        int count = 0;
        foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
        {
            string slot = MatchSlotForFont(tmp.font);
            var target = slot != null ? GetFont(slot) : DefaultFont;
            if (target != null && tmp.font != target)
            {
                tmp.font = target;
                count++;
            }
        }
        return count;
    }

    /// <summary>按字体匹配所在槽（用于场景既有字体 → 槽映射；找不到返回 null）。</summary>
    string MatchSlotForFont(TMP_FontAsset font)
    {
        if (font == null) return null;
        var list = slots;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].font == font) return list[i].slotName;
        }
        return null;
    }

    /// <summary>
    /// 把指定槽的字体强制应用到整个子树（含 inactive）。
    /// 用于运行时动态实例化的对象（选卡卡片、动态 UI），它们不在场景加载时被 FontApplier 覆盖。
    /// 卡牌字体统一配置：改本资产 card 槽 → 所有实例化卡片立即生效。
    /// </summary>
    public int ApplyFontToTree(Transform root, string slot)
    {
        if (root == null) return 0;
        var font = GetFont(slot);
        if (font == null) return 0;
        int count = 0;
        foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.font != font)
            {
                tmp.font = font;
                count++;
            }
        }
        return count;
    }
}

/// <summary>字体槽（用途 → 字体资产）。</summary>
[Serializable]
public class FontSlot
{
    [Tooltip("槽名（如 default / title / number；见 FontSlots 常量）")]
    public string slotName = FontSlots.Default;

    [Tooltip("该用途的 TMP 字体资产（须含目标字形，如中文 SDF）")]
    public TMP_FontAsset font;
}

/// <summary>内置字体槽名常量（避免散落字符串）。</summary>
public static class FontSlots
{
    public const string Default = "default";
    public const string Title = "title";
    public const string Number = "number";
    public const string Bold = "bold";
    /// <summary>卡片字体槽（选卡卡片卡名/描述；CardLibrary 文本配此处字体，统一替换）</summary>
    public const string Card = "card";
}
