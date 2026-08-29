using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能 HUD 图标配置：按怪物罪类型配置普攻、技能、位移三张战斗槽图标，按玩家配置两张玩家槽图标。

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
        Mobility,
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
        [Tooltip("该怪物该槽位图标的显示颜色。")]
        public Color iconColor = Color.white;
        [TextArea(2, 6)]
        [Tooltip("该怪物该槽位（普攻/技能/位移）的文字描述，可被悬浮提示/教学面板/卡牌介绍等任意处引用。")]
        public string description;

    }

    [Serializable]
    public class MonsterIdentityEntry
    {
        [Tooltip("怪物身份（七罪类型）。")]
        public SinType sin = SinType.None;
        [Tooltip("附身时显示在技能栏外的怪物身份图片。")]
        public Sprite icon;
        [Tooltip("怪物身份图标的显示颜色。")]
        public Color iconColor = Color.white;
        [TextArea(2, 6)]
        [Tooltip("该怪物身份的文字描述，可被悬浮提示/教学面板/荣誉殿堂等任意处引用。")]
        public string description;
    }

    [Serializable]
    public class PlayerEntry
    {
        [Tooltip("灵魂态玩家 HUD 槽位。")]
        public PlayerSlot slot = PlayerSlot.BasicAttack;
        [Tooltip("该玩家槽位的 HUD 图片；留空则保留场景中的默认图片。")]
        public Sprite icon;
        [TextArea(2, 6)]
        [Tooltip("该玩家槽位的文字描述，可被悬浮提示/教学面板等任意处引用。")]
        public string description;
    }


    [Tooltip("怪物条目：每种罪类型配置 BasicAttack / Skill / Mobility 三行，每行可独立设置图片和颜色。")]


    public List<MonsterEntry> monsterEntries = new List<MonsterEntry>();

    [Tooltip("怪物身份条目：每种罪类型配置一张附身态显示图标及其颜色。")]
    public List<MonsterIdentityEntry> monsterIdentityEntries = new List<MonsterIdentityEntry>();

    [Tooltip("玩家条目：配置 BasicAttack / Possess 两行。")]

    public List<PlayerEntry> playerEntries = new List<PlayerEntry>();

    Dictionary<(SinType, MonsterSlot), MonsterEntry> monsterCache;
    Dictionary<SinType, MonsterIdentityEntry> monsterIdentityCache;
    Dictionary<PlayerSlot, Sprite> playerCache;



    /// <summary>查询怪物附身态指定槽位的图标。</summary>
    public bool TryGetMonsterIcon(SinType sin, MonsterSlot slot, out Sprite icon, out Color color)
    {
        icon = null;
        color = Color.white;
        if (sin == SinType.None) return false;
        BuildCache();
        if (!monsterCache.TryGetValue((sin, slot), out MonsterEntry entry)) return false;
        icon = entry.icon;
        color = entry.iconColor;
        return icon != null;
    }

    /// <summary>查询怪物附身态显示的身份图标。</summary>
    public bool TryGetMonsterIdentity(SinType sin, out Sprite icon, out Color color)
    {
        icon = null;
        color = Color.white;
        if (sin == SinType.None) return false;
        BuildCache();
        if (!monsterIdentityCache.TryGetValue(sin, out MonsterIdentityEntry entry)) return false;
        icon = entry.icon;
        color = entry.iconColor;
        return icon != null;
    }

    /// <summary>查询灵魂态玩家指定槽位的图标。</summary>
    public bool TryGetPlayerIcon(PlayerSlot slot, out Sprite icon)

    {
        icon = null;
        BuildCache();
        return playerCache.TryGetValue(slot, out icon) && icon != null;
    }

    /// <summary>查询怪物附身态指定槽位（普攻/技能/位移）的文字描述。命中且描述非空返回 true。</summary>
    public bool TryGetMonsterDescription(SinType sin, MonsterSlot slot, out string description)
    {
        description = null;
        if (sin == SinType.None) return false;
        BuildCache();
        if (!monsterCache.TryGetValue((sin, slot), out MonsterEntry entry)) return false;
        description = entry.description;
        return !string.IsNullOrEmpty(description);
    }

    /// <summary>查询怪物身份的文字描述。命中且描述非空返回 true。</summary>
    public bool TryGetMonsterIdentityDescription(SinType sin, out string description)
    {
        description = null;
        if (sin == SinType.None) return false;
        BuildCache();
        if (!monsterIdentityCache.TryGetValue(sin, out MonsterIdentityEntry entry)) return false;
        description = entry.description;
        return !string.IsNullOrEmpty(description);
    }

    /// <summary>查询灵魂态玩家指定槽位的文字描述。命中且描述非空返回 true。</summary>
    public bool TryGetPlayerDescription(PlayerSlot slot, out string description)
    {
        description = null;
        BuildCache();
        if (!playerCache.TryGetValue(slot, out Sprite _)) return false;
        // 复用 playerCache 的存在性判断，但实际 description 需从 playerEntries 里读：
        if (playerEntries == null) return false;
        foreach (var entry in playerEntries)
        {
            if (entry != null && entry.slot == slot)
            {
                description = entry.description;
                return !string.IsNullOrEmpty(description);
            }
        }
        return false;
    }

    /// <summary>按图标 sprite 反查其配置颜色（用于卡片/卡面等区域：CardLibrary 的 image 已替换为本配置的技能图标）。
    /// 玩家条目无颜色配置，命中返回 false。</summary>
    public bool TryGetColorByIcon(Sprite icon, out Color color)
    {
        color = Color.white;
        if (icon == null || monsterEntries == null) return false;
        BuildCache();
        foreach (var e in monsterEntries)
        {
            if (e != null && e.icon == icon) { color = e.iconColor; return true; }
        }
        return false;
    }

    void BuildCache()
    {
        if (monsterCache != null && monsterIdentityCache != null && playerCache != null) return;

        monsterCache = new Dictionary<(SinType, MonsterSlot), MonsterEntry>();
        monsterIdentityCache = new Dictionary<SinType, MonsterIdentityEntry>();
        playerCache = new Dictionary<PlayerSlot, Sprite>();



        if (monsterEntries != null)
        {
            foreach (var entry in monsterEntries)
            {
                if (entry == null || entry.sin == SinType.None) continue;
                var key = (entry.sin, entry.slot);
                if (!monsterCache.ContainsKey(key)) monsterCache[key] = entry;

            }
        }

        if (monsterIdentityEntries != null)
        {
            foreach (var entry in monsterIdentityEntries)
            {
                if (entry == null || entry.sin == SinType.None) continue;
                if (!monsterIdentityCache.ContainsKey(entry.sin)) monsterIdentityCache[entry.sin] = entry;
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

        var identitySeen = new HashSet<SinType>();
        if (monsterIdentityEntries != null)
        {
            foreach (var entry in monsterIdentityEntries)
            {
                if (entry == null || entry.sin == SinType.None) continue;
                if (!identitySeen.Add(entry.sin))
                    Debug.LogWarning($"[MonsterSkillIconConfig] 重复身份图标 sin={entry.sin}，运行时取首个，请清理资产。", this);
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
        monsterIdentityCache = null;
        playerCache = null;

    }
}
