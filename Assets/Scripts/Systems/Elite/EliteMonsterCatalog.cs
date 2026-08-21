using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 精英怪目录：Sin → 怪物 prefab / 显示名 映射（单文件 SO，同 CardLibrary 模式）。
///   - 上传端：取 displayName 作快照 monsterType（透传字段，注入端展示用）；
///   - 注入端：按快照 sin 解析 prefab 刷出精英；
///   - 本地 Preset 兜底池（Meta_Progression_Systems_Baseline §6.3）：无网 / 服务器空候选库时投放，
///     内容为 Fake Historical Build Profiles（OD-CAN-001：PRESET CONTENT OPEN，策划可编辑）。
/// 资产建议放 Assets/Resources/EliteMonsterCatalog.asset（EliteBuildDirector 自动 Resources.Load），
/// 或在场景中挂载 EliteBuildDirector 并手动指定。
/// </summary>
[CreateAssetMenu(fileName = "EliteMonsterCatalog", menuName = "Possession/Elite/Elite Monster Catalog")]
public class EliteMonsterCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("该条目对应的 Sin 类型。")]
        public SinType sin = SinType.None;
        [Tooltip("怪物种类显示名（上传 monsterType / 精英命名用），如 '色欲-灵念师'。")]
        public string displayName;
        [Tooltip("该 Sin 注入为精英时刷出的怪物 prefab（须挂 MonsterActor）。")]
        public GameObject prefab;
    }

    /// <summary>
    /// 本地 Preset 精英快照（Fake Historical Build Profile，Meta §6.3 兜底用）：
    /// bdData 为 Card ID 清单（stack 恒 1，§6.1），注入时未知 cardId 静默跳过（§6.6）。
    /// </summary>
    [Serializable]
    public class PresetSnapshot
    {
        [Tooltip("预设名（观测 / 调试用）。")]
        public string presetName;
        [Tooltip("该预设对应的 Sin 类型。")]
        public SinType sin = SinType.None;
        [Tooltip("历史 BD 的 Card ID 清单（只放 MonsterType / TypeGrowth 卡；未知 ID 注入时静默跳过）。")]
        public List<string> cardIds = new List<string>();
        [Tooltip("快照来源波次编码（模拟他人进度；默认 8 = 已完成全部普通波）。")]
        public int sourceWave = 8;
        [Tooltip("抽取权重（>0 参与；≤0 跳过）。")]
        public float weight = 1f;
    }

    [Tooltip("七罪条目列表。同一 Sin 重复配置时首个生效。")]
    public List<Entry> entries = new List<Entry>();

    [Tooltip("本地 Preset 兜底池：无网 / 服务器空候选库时加权随机投放（Meta §6.3；Preset 内容 OD-CAN-001 OPEN，策划可编辑）。")]
    public List<PresetSnapshot> presetSnapshots = new List<PresetSnapshot>();

    public Entry Find(SinType sin)
    {
        foreach (var e in entries)
            if (e != null && e.sin == sin) return e;
        return null;
    }

    /// <summary>按 wire 名（"lust" 等小写 Sin 名）解析条目；解析失败返回 null。</summary>
    public Entry FindByWireName(string wire)
    {
        if (string.IsNullOrEmpty(wire)) return null;
        foreach (SinType sin in Enum.GetValues(typeof(SinType)))
        {
            if (sin == SinType.None) continue;
            if (string.Equals(WireName(sin), wire, StringComparison.OrdinalIgnoreCase))
                return Find(sin);
        }
        return null;
    }

    /// <summary>Sin 的 wire 编码（服务器透传字符串）：小写枚举名，如 SinType.Lust → "lust"。</summary>
    public static string WireName(SinType sin) => sin.ToString().ToLowerInvariant();

    /// <summary>
    /// 从本地 Preset 池加权随机抽取一条并转为投放快照（Meta §6.3 兜底来源）。
    /// 池空或全部条目非法（sin=None / 无卡 / 权重≤0）时返回 null（本波不投放）。
    /// </summary>
    public EliteSnapshotItem PickPresetSnapshot()
    {
        float total = 0f;
        foreach (var p in presetSnapshots)
        {
            if (IsValidPreset(p)) total += p.weight;
        }
        if (total <= 0f) return null;

        PresetSnapshot picked = null;
        float r = UnityEngine.Random.value * total;
        foreach (var p in presetSnapshots)
        {
            if (!IsValidPreset(p)) continue;
            r -= p.weight;
            if (r <= 0f) { picked = p; break; }
        }
        if (picked == null) return null; // 浮点边界兜底（理论不可达）
        return ToSnapshotItem(picked);
    }

    static bool IsValidPreset(PresetSnapshot p)
    {
        return p != null && p.weight > 0f && p.sin != SinType.None
            && p.cardIds != null && p.cardIds.Count > 0;
    }

    /// <summary>Preset 转投放快照：bdData = Card ID 清单（stack 恒 1，§6.1），来源标记 local-preset。</summary>
    EliteSnapshotItem ToSnapshotItem(PresetSnapshot p)
    {
        var bdData = new List<BdCardEntry>(p.cardIds.Count);
        foreach (var id in p.cardIds)
            bdData.Add(new BdCardEntry { cardId = id, stack = 1 });

        var entry = Find(p.sin);
        return new EliteSnapshotItem
        {
            snapshotId = 0,
            sourcePlayerId = "local-preset",
            runId = "preset",
            sin = WireName(p.sin),
            monsterType = entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : WireName(p.sin),
            bdData = bdData,
            bdCount = bdData.Count,
            sourceWave = p.sourceWave,
            gameTime = 0,
        };
    }
}
