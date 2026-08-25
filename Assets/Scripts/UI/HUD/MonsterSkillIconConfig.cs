using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能 HUD 图标配置：按怪物罪类型配置三张战斗槽图标，按玩家配置两张玩家槽图标。
/// 资产放在 Resources/UI/MonsterSkillIconConfig.asset，AbilityCooldownUI 会自动加载；
/// 也可以在 AbilityCooldownUI 的 Icon Config 字段显式覆盖。
/// </summary>
[CreateAssetMenu(fileName = "MonsterSkillIconConfig", menuName = "Kepler/UI/Monster Skill Icon Config")]
public class MonsterSkillIconConfig : ScriptableObject
{
    /// <summary>怪物附身后当前 Ability HUD 的三个可见槽位。</summary>
    public enum MonsterSlot
    {
        BasicAttack,
        Skill,
        Possess,
    }

    /// <summary>灵魂态玩家当前 Ability HUD 的两个可见槽位。</summary>
    public enum PlayerSlot
    {
        BasicAttack,
        Possess,
    }

    [Serializable]
    public class MonsterEntry
    {
        [Tooltip("怪物身份（七罪类型）。")]
        public SinType sin = SinType.None;
        [Tooltip("怪物附身态 HUD 槽位。")]
        public MonsterSlot slot = MonsterSlot.BasicAttack;
        [Tooltip("该怪物该槽位的 HUD 图片；留空则保留场景中的默认图片。")]
        public Sprite icon;
    }

    [Serializable]
    public class PlayerEntry
    {
        [Tooltip("灵魂态玩家 HUD 槽位。")]
        public PlayerSlot slot = PlayerSlot.BasicAttack;
        [Tooltip("该玩家槽位的 HUD 图片；留空则保留场景中的默认图片。")]
        public Sprite icon;
    }

    [Tooltip("怪物条目：每种罪类型配置 BasicAttack / Skill / Possess 三行。")]
    public List<MonsterEntry> monsterEntries = new List<MonsterEntry>();

    [Tooltip("玩家条目：配置 BasicAttack / Possess 两行。")]
    public List<PlayerEntry> playerEntries = new List<PlayerEntry>();

    Dictionary<(SinType, MonsterSlot), Sprite> monsterCache;
    Dictionary<PlayerSlot, Sprite> playerCache;

    /// <summary>查询怪物附身态指定槽位的图标。</summary>
    public bool TryGetMonsterIcon(SinType sin, MonsterSlot slot, out Sprite icon)
    {
        icon = null;
        if (sin == SinType.None) return false;
        BuildCache();
        return monsterCache.TryGetValue((sin, slot), out icon) && icon != null;
    }

    /// <summary>查询灵魂态玩家指定槽位的图标。</summary>
    public bool TryGetPlayerIcon(PlayerSlot slot, out Sprite icon)
    {
        icon = null;
        BuildCache();
        return playerCache.TryGetValue(slot, out icon) && icon != null;
    }

    void BuildCache()
    {
        if (monsterCache != null && playerCache != null) return;

        monsterCache = new Dictionary<(SinType, MonsterSlot), Sprite>();
        playerCache = new Dictionary<PlayerSlot, Sprite>();

        if (monsterEntries != null)
        {
            foreach (var entry in monsterEntries)
            {
                if (entry == null || entry.sin == SinType.None || entry.icon == null) continue;
                var key = (entry.sin, entry.slot);
                if (!monsterCache.ContainsKey(key)) monsterCache[key] = entry.icon;
            }
        }

        if (playerEntries != null)
        {
            foreach (var entry in playerEntries)
            {
                if (entry == null || entry.icon == null) continue;
                if (!playerCache.ContainsKey(entry.slot)) playerCache[entry.slot] = entry.icon;
            }
        }
    }

    void OnValidate()
    {
        var monsterSeen = new HashSet<(SinType, MonsterSlot)>();
        if (monsterEntries != null)
        {
            foreach (var entry in monsterEntries)
            {
                if (entry == null || entry.sin == SinType.None) continue;
                if (!monsterSeen.Add((entry.sin, entry.slot)))
                    Debug.LogWarning($"[MonsterSkillIconConfig] 重复条目 sin={entry.sin} slot={entry.slot}，运行时取首个，请清理资产。", this);
            }
        }

        var playerSeen = new HashSet<PlayerSlot>();
        if (playerEntries != null)
        {
            foreach (var entry in playerEntries)
            {
                if (entry == null) continue;
                if (!playerSeen.Add(entry.slot))
                    Debug.LogWarning($"[MonsterSkillIconConfig] 重复玩家条目 slot={entry.slot}，运行时取首个，请清理资产。", this);
            }
        }

        monsterCache = null;
        playerCache = null;
    }
}
