using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 精英怪目录：Sin → 怪物 prefab / 显示名 映射（单文件 SO，同 CardLibrary 模式）。
///   - 上传端：取 displayName 作快照 monsterType（透传字段，注入端展示用）；
///   - 注入端：按快照 sin 解析 prefab 刷出精英。
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

    [Tooltip("七罪条目列表。同一 Sin 重复配置时首个生效。")]
    public List<Entry> entries = new List<Entry>();

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
}
