using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能教学面板：模仿附身后的 Ability Cooldown UI，展示某一七宗罪怪物的三个技能图标
/// （普攻 / 技能 / 位移），鼠标悬浮图标时弹出该技能的详情（复用 HUD 的悬浮提示）。
///
/// 用法：
///   1. 挂到任意 UI GameObject 上；
///   2. 在 Inspector 选择 Sin Type（七宗罪之一）；
///   3. 在 Monsters 列表里配好该罪的怪物 prefab（用于读取技能名称/描述）；
///   4. 把三个槽位（Basic / Skill / Mobility）的 Image 拖到对应字段（也可留空只用其中几个）；
///   5. 运行后按 Sin Type populate 图标，hover 显示技能详情。
///
/// 图标来源：MonsterSkillIconConfig（Resources/UI/MonsterSkillIconConfig，与 AbilityCooldownUI 共用）。
/// 详情来源：怪物 prefab 上 EnemyAbility 组件的 abilityName / abilityDescription。
/// </summary>
public class AbilityTeachPanel : MonoBehaviour
{
    [Serializable]
    public class TeachMonsterBinding
    {
        [Tooltip("七宗罪类型。")]
        public SinType sin = SinType.None;
        [Tooltip("该罪怪物的 prefab（用于读取技能名称/描述）。")]
        public GameObject monsterPrefab;
    }

    [Header("Monster Selection")]
    [Tooltip("选择要展示技能的七宗罪怪物。")]
    public SinType sinType = SinType.Pride;
    [Tooltip("七宗罪 → 怪物 prefab 映射（用于读取技能名称/描述）。可只配需要展示的几项。")]
    public List<TeachMonsterBinding> monsters = new List<TeachMonsterBinding>();

    [Header("Enemy Icon（怪物身份图标）")]
    [Tooltip("怪物身份图标槽位根节点；无配置则隐藏。")]
    public RectTransform enemyIconRoot;
    [Tooltip("怪物身份图标 Image（MonsterSkillIconConfig.monsterIdentityEntries）。")]
    public Image enemyIconImage;

    [Header("Icon Slots（普攻 / 技能 / 位移）")]
    public RectTransform basicIconRoot;
    public Image basicIconImage;
    public RectTransform skillIconRoot;
    public Image skillIconImage;
    public RectTransform mobilityIconRoot;
    public Image mobilityIconImage;

    [Header("Config & Tooltip")]
    [Tooltip("技能图标配置；留空则自动加载 Resources/UI/MonsterSkillIconConfig。")]
    public MonsterSkillIconConfig iconConfig;
    [Tooltip("悬浮提示面板；留空则自动查找场景中的 PossessionImprintTooltip。")]
    public PossessionImprintTooltip tooltip;

    private EnemyAbility basicAbility;
    private EnemyAbility skillAbility;
    private EnemyAbility mobilityAbility;
    private string monsterDisplayName;

    void Awake()
    {
        if (iconConfig == null)
            iconConfig = Resources.Load<MonsterSkillIconConfig>("UI/MonsterSkillIconConfig");
        if (tooltip == null)
            tooltip = FindObjectOfType<PossessionImprintTooltip>(true);

        // 防止同 GameObject 上的 AbilityCooldownUI（主菜单 HUD）反复 SetActive(false) 教学面板的 UI 元素：
        // 共享 UI 时，在运行时清空 AbilityCooldownUI 对我们 UI 的字段引用，让它不再操作。
        DetachFromCooldownUIIfSharing();
    }

    /// <summary>
    /// 与同 GameObject 的 AbilityCooldownUI 共享 UI 时，运行时清空后者对我们 UI 的字段引用。
    /// 主菜单里教学面板与 HUD 模板可能复用同一组 Basic/Skill/Mobility/EnemyIcon，
    /// HUD 在 Update 里会根据 currentEnemy 把它们 SetActive(false) / 改 color，导致教学面板"Play 全部消失"。
    /// </summary>
    void DetachFromCooldownUIIfSharing()
    {
        var cooldown = GetComponent<AbilityCooldownUI>();
        if (cooldown == null) return;
        var cdType = typeof(AbilityCooldownUI);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        // 检测共享：AbilityCooldownUI 四个 root 任一等于我们的 root 即视为共享。
        // 注意 AbilityCooldownUI 用 possessIconRoot 对应我们的 mobilityIconRoot。
        bool shares = false;
        var pairs = new (string ourField, string cdField)[]
        {
            ("basicIconRoot", "basicIconRoot"),
            ("skillIconRoot", "skillIconRoot"),
            ("mobilityIconRoot", "possessIconRoot"),
            ("enemyIconRoot", "enemyIconRoot"),
        };
        var panelType = typeof(AbilityTeachPanel);
        foreach (var pair in pairs)
        {
            var ourFi = panelType.GetField(pair.ourField, flags);
            var cdFi = cdType.GetField(pair.cdField, flags);
            if (ourFi == null || cdFi == null) continue;
            var ourVal = ourFi.GetValue(this) as UnityEngine.Object;
            var cdVal = cdFi.GetValue(cooldown) as UnityEngine.Object;
            if (ourVal != null && ourVal == cdVal) { shares = true; break; }
        }
        if (!shares) return;

        // 共享：清空 AbilityCooldownUI 的所有 UI 字段引用（HUD 在主菜单本就不需要工作）。
        var fieldsToNull = new[]
        {
            "basicIconRoot", "basicIconImage", "basicCooldownOverlay",
            "skillIconRoot", "skillIconImage", "skillCooldownOverlay",
            "possessIconRoot", "possessIconImage", "possessCooldownOverlay",
            "enemyIconRoot", "enemyIconImage"
        };
        int cleared = 0;
        foreach (var fn in fieldsToNull)
        {
            var fi = cdType.GetField(fn, flags);
            if (fi == null) continue;
            if (typeof(UnityEngine.Object).IsAssignableFrom(fi.FieldType))
            {
                fi.SetValue(cooldown, null);
                cleared++;
            }
        }
        Debug.LogWarning($"[AbilityTeachPanel] 与同 GameObject 的 AbilityCooldownUI 共享 UI 元素，已运行时清空后者的 {cleared} 个 UI 字段以避免冲突。" +
            "建议在场景里给两者配置独立的 UI 元素（或从主菜单的 pride GameObject 上移除 AbilityCooldownUI）。", this);
    }

    void Start()
    {
        Refresh();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 编辑器里切换 Sin Type 时即时预览图标（不在 OnValidate 里改其它对象，延迟到下一帧）
        if (Application.isPlaying) return;
        if (iconConfig == null)
            iconConfig = Resources.Load<MonsterSkillIconConfig>("UI/MonsterSkillIconConfig");
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            ResolveAbilities();
            PopulateIcons();
        };
    }
#endif

    /// <summary>按当前 sinType 重新读取技能、刷新图标并绑定悬浮提示。</summary>
    public void Refresh()
    {
        ResolveAbilities();
        PopulateIcons();
        BindTooltips();
    }

    /// <summary>从怪物 prefab 上读取普攻 / 技能 / 位移三个 EnemyAbility（按 type 分类）。</summary>
    void ResolveAbilities()
    {
        basicAbility = null;
        skillAbility = null;
        mobilityAbility = null;
        monsterDisplayName = null;

        GameObject prefab = ResolveMonsterPrefab();
        if (prefab == null) return;

        MonsterActor actor = prefab.GetComponentInChildren<MonsterActor>(true);
        if (actor != null && !string.IsNullOrWhiteSpace(actor.displayName))
            monsterDisplayName = actor.displayName;

        EnemyAbility[] all = prefab.GetComponentsInChildren<EnemyAbility>(true);
        for (int i = 0; i < all.Length; i++)
        {
            EnemyAbility ability = all[i];
            if (ability == null) continue;
            switch (ability.type)
            {
                case EnemyAbility.AbilityType.BasicAttack:
                    if (basicAbility == null) basicAbility = ability;
                    break;
                case EnemyAbility.AbilityType.Skill:
                    if (skillAbility == null) skillAbility = ability;
                    break;
                case EnemyAbility.AbilityType.Mobility:
                    if (mobilityAbility == null) mobilityAbility = ability;
                    break;
            }
        }
    }

    GameObject ResolveMonsterPrefab()
    {
        if (monsters == null) return null;
        for (int i = 0; i < monsters.Count; i++)
        {
            TeachMonsterBinding binding = monsters[i];
            if (binding != null && binding.sin == sinType) return binding.monsterPrefab;
        }
        return null;
    }

    void PopulateIcons()
    {
        ApplyMonsterIcon(MonsterSkillIconConfig.MonsterSlot.BasicAttack, basicIconImage, basicIconRoot);
        ApplyMonsterIcon(MonsterSkillIconConfig.MonsterSlot.Skill, skillIconImage, skillIconRoot);
        ApplyMonsterIcon(MonsterSkillIconConfig.MonsterSlot.Mobility, mobilityIconImage, mobilityIconRoot);
        ApplyMonsterIdentityIcon();
    }

    /// <summary>从 MonsterSkillIconConfig.monsterIdentityEntries 取怪物身份图标填充 enemy icon 槽。</summary>
    void ApplyMonsterIdentityIcon()
    {
        if (enemyIconImage == null) return;
        Sprite icon = null;
        Color color = Color.white;
        bool has = iconConfig != null && iconConfig.TryGetMonsterIdentity(sinType, out icon, out color);
        if (has)
        {
            enemyIconImage.sprite = icon;
            enemyIconImage.color = color;
        }
        if (enemyIconRoot != null) enemyIconRoot.gameObject.SetActive(has);
    }

    void ApplyMonsterIcon(MonsterSkillIconConfig.MonsterSlot slot, Image image, RectTransform root)
    {
        if (image == null) return;
        Sprite icon = null;
        Color color = Color.white;
        bool has = iconConfig != null && iconConfig.TryGetMonsterIcon(sinType, slot, out icon, out color);
        if (has)
        {
            image.sprite = icon;
            image.color = color;
        }
        if (root != null) root.gameObject.SetActive(has);
    }

    void BindTooltips()
    {
        BindIconDescription(basicIconRoot, MonsterSkillIconConfig.MonsterSlot.BasicAttack, basicAbility);
        BindIconDescription(skillIconRoot, MonsterSkillIconConfig.MonsterSlot.Skill, skillAbility);
        BindIconDescription(mobilityIconRoot, MonsterSkillIconConfig.MonsterSlot.Mobility, mobilityAbility);
        BindIdentityDescription(enemyIconRoot);
    }

    /// <summary>
    /// 给技能图标挂通用 HoverTooltipText，并用 MonsterSkillIconConfig 对应槽位的 description 联动填充。
    /// description 为空时回退到 EnemyAbility 的描述/伤害冷却摘要。
    /// </summary>
    void BindIconDescription(RectTransform root, MonsterSkillIconConfig.MonsterSlot slot, EnemyAbility ability)
    {
        if (root == null) return;
        RemoveLegacyTooltip(root);

        string title = ability != null && !string.IsNullOrWhiteSpace(ability.abilityName)
            ? ability.abilityName : SlotDisplayName(slot);

        string desc = null;
        if (iconConfig != null) iconConfig.TryGetMonsterDescription(sinType, slot, out desc);
        if (string.IsNullOrWhiteSpace(desc) && ability != null) desc = BuildAbilitySummary(ability);

        var hover = root.GetComponent<HoverTooltipText>();
        if (hover == null) hover = root.gameObject.AddComponent<HoverTooltipText>();
        hover.tooltip = tooltip;
        hover.SetText(title, desc);
    }

    /// <summary>enemy icon 悬浮时显示怪物显示名 + MonsterSkillIconConfig 身份 description。</summary>
    void BindIdentityDescription(RectTransform root)
    {
        if (root == null) return;
        RemoveLegacyTooltip(root);

        string desc = null;
        if (iconConfig != null) iconConfig.TryGetMonsterIdentityDescription(sinType, out desc);

        var hover = root.GetComponent<HoverTooltipText>();
        if (hover == null) hover = root.gameObject.AddComponent<HoverTooltipText>();
        hover.tooltip = tooltip;
        hover.SetText(monsterDisplayName, desc);
    }

    /// <summary>移除旧的 GameplayTooltipTarget，避免与 HoverTooltipText 双 hover 触发。</summary>
    void RemoveLegacyTooltip(RectTransform root)
    {
        var legacy = root.GetComponent<GameplayTooltipTarget>();
        if (legacy != null) Destroy(legacy);
    }

    static string SlotDisplayName(MonsterSkillIconConfig.MonsterSlot slot)
    {
        switch (slot)
        {
            case MonsterSkillIconConfig.MonsterSlot.Skill: return "技能";
            case MonsterSkillIconConfig.MonsterSlot.Mobility: return "位移";
            default: return "普攻";
        }
    }

    static string BuildAbilitySummary(EnemyAbility ability)
    {
        if (!string.IsNullOrWhiteSpace(ability.abilityDescription))
            return ability.abilityDescription;
        var lines = new List<string>();
        if (ability.damage > 0f) lines.Add("伤害：" + ability.damage.ToString("0.##"));
        if (ability.cooldown > 0f) lines.Add("冷却：" + ability.cooldown.ToString("0.##") + " 秒");
        if (lines.Count == 0) lines.Add("施放该技能以触发其战斗效果。");
        return string.Join("\n", lines);
    }
}
