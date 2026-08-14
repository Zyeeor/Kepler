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
