using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 卡池资产：1 个 SO 资产 = 整个卡池。
/// 把卡池从场景内联（CardManager.allCards）抽离为独立资产，便于配置/修改/复用。
/// OnValidate 编辑期查重，重复 effectId 给出警告。
/// </summary>
[CreateAssetMenu(fileName = "CardLibrary", menuName = "Possession/Progression/Card Library")]
public class CardLibrary : ScriptableObject
{
    [Tooltip("卡池所有卡。effectId 需全局唯一（OnValidate 查重，运行时重复项忽略）。")]
    public List<CardData> cards = new List<CardData>();
    [Tooltip("Temporarily retired effect IDs. Their legacy records stay available for migration, but cannot be offered, found, or applied at runtime.")]
    public List<string> disabledEffectIds = new List<string>();
    [Tooltip("渲染单张卡的预制体（如选卡 Choice1 prefab）。图鉴等无 CoreChoiceUI 场景用来按原始布局渲染完整卡面。")]
    public GameObject cardPrefab;

    public bool IsEffectEnabled(string effectId)
    {
        return !string.IsNullOrEmpty(effectId) &&
            (disabledEffectIds == null || !disabledEffectIds.Contains(effectId));
    }

    // 资产路径：Assets/Configs/CardLibrary.asset（与 MonsterAIConfig 等配置同级）
    const string AssetPath = "Assets/Configs/CardLibrary.asset";

    static CardLibrary instance;
    /// <summary>
    /// 运行时单例。主菜单/图鉴等无 CardManager 的场景用它读取卡面等数据。
    /// 资产位于 Assets/Configs（非 Resources），因此：
    ///   - 编辑器（含 Play 模式）：用 AssetDatabase 按路径加载；
    ///   - 打包构建：回退到 Resources.Load（需确保构建时该资产被打包进 Resources）。
    /// </summary>
    public static CardLibrary Instance
    {
        get
        {
            if (instance == null) instance = LoadAsset();
            return instance;
        }
    }

    static CardLibrary LoadAsset()
    {
#if UNITY_EDITOR
        var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<CardLibrary>(AssetPath);
        if (loaded != null) return loaded;
#endif
        return Resources.Load<CardLibrary>("UI/CardLibrary");
    }

    /// <summary>按 effectId 查卡（图鉴/调试用）。未找到返回 null。</summary>
    public CardData FindCard(string effectId)
    {
        if (cards == null) return null;
        for (int i = 0; i < cards.Count; i++)
            if (cards[i] != null && cards[i].effectId == effectId) return cards[i];
        return null;
    }

#if UNITY_EDITOR
    void OnValidate()   // editor-side guard: warn on duplicate effectId (runtime ignores later entries)
    {
        var seen = new HashSet<string>();
        foreach (var c in cards)
        {
            if (c == null || string.IsNullOrEmpty(c.effectId)) continue;
            if (!seen.Add(c.effectId))
                Debug.LogWarning($"[CardLibrary] duplicate effectId '{c.effectId}' in {name}; later entries ignored at runtime.", this);
        }
    }
#endif
}
