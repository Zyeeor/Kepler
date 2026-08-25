using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 精英怪历史 BD 快照载体（Canonical §23：Elite = Base Monster + External Historical Build Snapshot）。
///
/// 挂载时（Init）：
///   1) 剥离该实例上当前 Run 的卡层状态——池化实例可能带有 CardManager.UnlockEffect 写入的
///      upgrades 解锁槽位与 grantedEffectTags，先捕获现状再清空；
///   2) 只应用快照 bdData 内的卡（解锁槽位 + 效果 Tag；未知 cardId 静默跳过，策划案 F11）。
///
/// 存续期间：能力参数读取（EnemyAbility.GetCardParameter）与全局解锁判定（如 LustBodyState 的
/// LU-TG01）经 EliteBuildCarrier.Get 路由到本载体，不读 CardManager 全局 Run 卡层。
/// 附身 / 倒地期间 GameObject 保持 active，构筑全程保留（§23：击杀并 Possess 后仍保持 Historical Build）。
///
/// 回池时（OnDisable，由 MonsterPool.Return 的 SetActive(false) 触发）：还原捕获状态并自毁，
/// 保证池复用不残留精英构筑。
/// </summary>
[DisallowMultipleComponent]
public class EliteBuildCarrier : MonoBehaviour
{
    /// <summary>来源快照 ID（观测 / 调试）。</summary>
    public long SnapshotId { get; private set; }
    /// <summary>来源玩家设备特征码（观测 / 调试）。</summary>
    public string SourcePlayerId { get; private set; }
    /// <summary>来源玩家的 Run ID（战果回传聚合键组成，Meta §6.5）。</summary>
    public string RunId { get; private set; }
    /// <summary>快照 Sin 的 wire 名（观测 / 调试）。</summary>
    public string Sin { get; private set; }
    /// <summary>快照来源波次（观测 / 调试）。</summary>
    public int SourceWave { get; private set; }

    readonly HashSet<string> cardIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    readonly List<CapturedSlot> capturedSlots = new List<CapturedSlot>();
    readonly List<CapturedTags> capturedTags = new List<CapturedTags>();
    readonly List<AddedSlot> addedSlots = new List<AddedSlot>();
    bool applied;
    MonsterActor actor;
    string originalDisplayName;

    class CapturedSlot { public EnemyAbility.UpgradeSlot slot; public bool wasUnlocked; }
    class CapturedTags { public EnemyAbility ability; public List<string> tags; }
    class AddedSlot { public EnemyAbility ability; public EnemyAbility.UpgradeSlot slot; }

    /// <summary>从任意子组件（EnemyAbility / LustBodyState 等）向上查找精英载体；非精英返回 null。</summary>
    public static EliteBuildCarrier Get(Component component)
    {
        return component != null ? component.GetComponentInParent<EliteBuildCarrier>() : null;
    }

    /// <summary>
    /// 以服务器快照初始化精英构筑。重复调用仅首次生效。
    /// </summary>
    /// <param name="snapshot">pick 返回的投放快照。</param>
    /// <param name="displayName">Catalog 配置的怪物显示名（用于"精英·"前缀命名；空则保留原名）。</param>
    public void Init(EliteSnapshotItem snapshot, string displayName)
    {
        if (snapshot == null || applied) return;

        SnapshotId = snapshot.snapshotId;
        SourcePlayerId = snapshot.sourcePlayerId;
        RunId = snapshot.runId;
        Sin = snapshot.sin;
        SourceWave = snapshot.sourceWave;

        actor = GetComponent<MonsterActor>();
        if (actor != null)
        {
            originalDisplayName = actor.displayName;
            if (!string.IsNullOrEmpty(displayName))
                actor.displayName = TextCatalog.Get("elite.name_prefix") + displayName;
        }

        var abilities = GetComponentsInChildren<EnemyAbility>(true);

        // 1) 剥离当前 Run 卡层：捕获现状后清空（含池化复用残留的解锁槽位与效果 Tag）
        foreach (var ability in abilities)
        {
            if (ability == null) continue;
            if (ability.upgrades != null)
            {
                foreach (var slot in ability.upgrades)
                {
                    if (slot == null) continue;
                    capturedSlots.Add(new CapturedSlot { slot = slot, wasUnlocked = slot.unlocked });
                    slot.unlocked = false;
                }
            }
            capturedTags.Add(new CapturedTags { ability = ability, tags = new List<string>(ability.appliedEffectTags) });
            ability.appliedEffectTags.Clear();
        }

        // 2) 应用快照 BD（stack 当前无叠层语义，按 cardId 去重后各应用一次）
        if (snapshot.bdData != null)
        {
            var cm = CardManager.Instance;
            foreach (var entry in snapshot.bdData)
            {
                if (entry == null || string.IsNullOrEmpty(entry.cardId)) continue;
                if (cardIds.Contains(entry.cardId)) continue; // 同卡去重
                var card = cm != null ? cm.FindCard(entry.cardId) : null;
                if (card == null)
                {
                    Debug.LogWarning($"[EliteBuildCarrier] 快照卡 '{entry.cardId}' 不在当前 CardLibrary，静默跳过（F11）。");
                    continue;
                }
                cardIds.Add(entry.cardId);
                foreach (var ability in abilities)
                {
                    if (ability == null || !CardManager.DoesCardTargetAbility(card, ability)) continue;
                    UnlockSlot(ability, card.effectId);
                    ability.AddAppliedEffectTags(card.grantedEffectTags);
                }
            }
        }

        applied = true;
    }

    /// <summary>快照是否包含指定卡（全局解锁判定的精英替代路径，如 LustBodyState 的 LU-TG01）。</summary>
    public bool HasCard(string effectId)
    {
        return applied && !string.IsNullOrEmpty(effectId) && cardIds.Contains(effectId);
    }

    /// <summary>
    /// 从快照卡解析能力参数（EnemyAbility.GetCardParameter 的精英替代路径）：
    /// 只查快照内的卡，不读 CardManager 全局已解锁卡。
    /// </summary>
    public bool TryGetCardParameter(EnemyAbility ability, string key, out float value)
    {
        value = 0f;
        if (!applied || ability == null || string.IsNullOrEmpty(key)) return false;
        var cm = CardManager.Instance;
        if (cm == null) return false;
        foreach (var id in cardIds)
        {
            var card = cm.FindCard(id);
            if (card == null || !CardManager.DoesCardTargetAbility(card, ability) || card.abilityParameters == null) continue;
            foreach (var p in card.abilityParameters)
            {
                if (p != null && string.Equals(p.key, key, System.StringComparison.OrdinalIgnoreCase))
                {
                    value = p.value;
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>同 CardManager.UnlockOnAbility 逻辑，额外记录新增槽位以便还原。</summary>
    void UnlockSlot(EnemyAbility ability, string effectId)
    {
        if (ability.upgrades == null) ability.upgrades = new List<EnemyAbility.UpgradeSlot>();
        foreach (var slot in ability.upgrades)
        {
            if (slot != null && !string.IsNullOrEmpty(slot.effectId)
                && slot.effectId.Equals(effectId, System.StringComparison.OrdinalIgnoreCase))
            {
                slot.unlocked = true;
                return;
            }
        }
        var added = new EnemyAbility.UpgradeSlot { effectId = effectId, unlocked = true };
        ability.upgrades.Add(added);
        addedSlots.Add(new AddedSlot { ability = ability, slot = added });
    }

    void OnDisable()
    {
        Restore();
    }

    /// <summary>还原捕获的卡层状态并自毁（回池 / 销毁路径均安全，幂等）。</summary>
    void Restore()
    {
        if (!applied) return;
        applied = false;

        foreach (var c in capturedSlots)
            if (c.slot != null) c.slot.unlocked = c.wasUnlocked;
        foreach (var a in addedSlots)
            if (a.ability != null && a.ability.upgrades != null) a.ability.upgrades.Remove(a.slot);
        foreach (var t in capturedTags)
        {
            if (t.ability == null) continue;
            t.ability.appliedEffectTags.Clear();
            if (t.tags != null) t.ability.appliedEffectTags.AddRange(t.tags);
        }
        if (actor != null) actor.displayName = originalDisplayName;

        capturedSlots.Clear();
        capturedTags.Clear();
        addedSlots.Clear();
        cardIds.Clear();

        Destroy(this); // 载体一次性：回池即移除，不影响该实例后续作为普通怪复用
    }
}
