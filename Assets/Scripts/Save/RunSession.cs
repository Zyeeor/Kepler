using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对局会话（Run）：一局完整对局的生命周期状态，常驻（DontDestroyOnLoad）。
///
/// 三层架构的"会话级"：跨场景持有对局进度（地图种子/已选卡/波次/灵魂状态），
/// 场景对象（MapStreamingSystem/WaveManager/CardManager）Awake 时向本会话查询并重建，
/// 不直接持有跨场景状态；持久化统一经 SaveCoordinator（纯 IO 层）。
///
/// 流转：
///   - 主菜单[新游戏] → BeginNewRun：随机种子 + 清状态 + 清旧存档
///   - 主菜单[继续]   → LoadFromSave：读档填充内存态（随后场景对象据此重建）
///   - 对局中波间存档  → SaveProgress：更新内存态 + 落盘
///   - 返回主菜单     → 会话保留（内存态=最近波间，再进入零读盘恢复）
///   - 重开/胜利/失败 → EndRun：清内存态 + 清存档
/// </summary>
public class RunSession : MonoBehaviour
{
    public static RunSession Instance { get; private set; }

    /// <summary>是否有进行中的对局（BeginNewRun/LoadFromSave 后 true，EndRun 后 false）。</summary>
    public bool HasActiveRun { get; private set; }

    /// <summary>地图种子：对局期间锁定，恢复时注入 MapStreamingSystem（地图确定性重建）。</summary>
    public uint WorldSeed { get; private set; }

    /// <summary>已完成波次索引（-1 = 尚未完成任何波），恢复从下一波开始。</summary>
    public int CompletedWaveIndex { get; private set; } = -1;

    /// <summary>选卡未完成标记：为 true 时恢复需先补弹选卡（在选卡界面退出后继续，不跳过本波选卡）。</summary>
    public bool PendingChoice { get; private set; }

    /// <summary>选卡界面退出时的候选卡 effectId 快照（恢复补弹时直接还原，保证与退出时一致）。</summary>
    public readonly List<string> ChoicePicks = new List<string>();

    /// <summary>本局已解锁卡牌效果（选卡会话结算后由场景对象同步进来）。</summary>
    public readonly List<string> UnlockedEffects = new List<string>();

    /// <summary>灵魂位置（最近一次波间存档点的玩家位置 = 下一波起点）。</summary>
    public Vector3 SoulPosition { get; private set; }

    /// <summary>灵魂 HP（存档点采样）。</summary>
    public float SoulHealth { get; private set; }

    /// <summary>灵魂时间（存档点采样）。</summary>
    public float SoulTime { get; private set; }

    /// <summary>玩家当前附身的怪（存档点采样；null = 灵魂态）。</summary>
    public SaveData.MonsterBodySave PossessedBody { get; private set; }

    /// <summary>场上可附身尸体（存档点采样，downed 且窗口内）。</summary>
    public readonly List<SaveData.MonsterBodySave> Corpses = new List<SaveData.MonsterBodySave>();

    /// <summary>
    /// 确保会话实例存在（主菜单/对局场景均可调用）。
    /// 自动创建常驻对象，无需在场景中挂载。
    /// </summary>
    public static RunSession EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[RunSession]");
        DontDestroyOnLoad(go);
        return go.AddComponent<RunSession>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>开始新对局：随机地图种子（或编辑器配置的固定种子）、清空进度、清除旧存档。</summary>
    public void BeginNewRun()
    {
        // 固定种子调试能力：GameManager 配置 useFixedSeed 时复用固定种子（便于复现同一张地图）
        var gm = GameManager.Instance;
        WorldSeed = (gm != null && gm.useFixedSeed) ? gm.fixedSeed
                                                    : (uint)UnityEngine.Random.Range(1, int.MaxValue);
        CompletedWaveIndex = -1;
        PendingChoice = false;
        ChoicePicks.Clear();
        UnlockedEffects.Clear();
        SoulPosition = Vector3.zero;
        SoulHealth = 0f;
        SoulTime = 0f;
        PossessedBody = null;
        Corpses.Clear();
        HasActiveRun = true;
        SaveCoordinator.DeleteSave();
        Debug.Log($"[RunSession] 新对局开始：worldSeed={WorldSeed}");
    }

    /// <summary>
    /// 从存档恢复对局（主菜单"继续"）。成功返回 true；无有效存档返回 false（不开启会话）。
    /// </summary>
    public bool LoadFromSave()
    {
        SaveCoordinator.RequestResume();
        var data = SaveCoordinator.ResumeData;
        if (data == null)
        {
            Debug.LogWarning("[RunSession] 无有效存档，无法继续。");
            HasActiveRun = false;
            return false;
        }
        WorldSeed = data.worldSeed;
        CompletedWaveIndex = data.completedWaveIndex;
        PendingChoice = data.pendingChoice;
        ChoicePicks.Clear();
        if (data.choicePicks != null) ChoicePicks.AddRange(data.choicePicks);
        UnlockedEffects.Clear();
        if (data.unlockedEffects != null) UnlockedEffects.AddRange(data.unlockedEffects);
        SoulPosition = data.soulPosition;
        SoulHealth = data.soulHealth;
        SoulTime = data.soulTime;
        PossessedBody = data.possessedBody;
        Corpses.Clear();
        if (data.corpses != null) Corpses.AddRange(data.corpses);
        HasActiveRun = true;
        Debug.Log($"[RunSession] 读档恢复对局：已完成波 {CompletedWaveIndex + 1}，worldSeed={WorldSeed}，解锁卡 {UnlockedEffects.Count} 张。");
        return true;
    }

    /// <summary>
    /// 波间存档点：采样当前玩家状态 → 更新会话内存态 → 落盘。
    /// 由 WaveManager 在"波清场后"与"选完卡后"两个时间点调用。
    /// </summary>
    /// <param name="completedWaveIndex">刚完成的波次索引（恢复从下一波开始）。</param>
    /// <param name="pendingChoice">选卡是否未完成（true = 选卡界面退出，恢复时需补弹选卡）。</param>
    public void SaveProgress(int completedWaveIndex, bool pendingChoice = false)
    {
        // 采样场景运行时状态（波间时刻：SoulActor/PlayerHealth/GameManager 均在场景中）
        var soul = FindObjectOfType<SoulActor>();
        SoulPosition = soul != null ? soul.transform.position : Vector3.zero;
        SoulHealth = PlayerHealth.Instance != null ? PlayerHealth.Instance.currentHealth : 0f;
        SoulTime = GameManager.Instance != null ? GameManager.Instance.soulTime : 0f;
        CompletedWaveIndex = completedWaveIndex;
        PendingChoice = pendingChoice;
        SampleBodies();
        SampleChoicePicks(); // 选卡界面退出时快照当前候选（pendingChoice=true 才有意义）

        SaveCoordinator.SaveSnapshot(completedWaveIndex, WorldSeed, UnlockedEffects,
            SoulPosition, SoulHealth, SoulTime, PossessedBody, Corpses, pendingChoice, ChoicePicks);
        Debug.Log($"[RunSession] 波 {completedWaveIndex} 存档完成：位置={SoulPosition} HP={SoulHealth} 时间={SoulTime} 附身={(PossessedBody != null ? PossessedBody.prefabId : "无")} 尸体={Corpses.Count}");
    }

    /// <summary>采样附身怪与可附身尸体（波间时刻：玩家身体 + 场上待附身尸体）。</summary>
    void SampleBodies()
    {
        PossessedBody = null;
        Corpses.Clear();

        // 玩家当前附身怪
        var poss = PossessionManager.Instance;
        if (poss != null && poss.CurrentBody != null && poss.CurrentBody.isPossessed)
        {
            var body = poss.CurrentBody;
            PossessedBody = new SaveData.MonsterBodySave
            {
                prefabId = ResolvePrefabId(body.gameObject),
                position = body.transform.position,
                health = body.currentHealth,
            };
        }

        // 场上可附身尸体（downed 且窗口内、未附身未保留）
        var all = FindObjectsOfType<MonsterActor>(true);
        foreach (var m in all)
        {
            if (m == null || !m.CanBePossessed) continue;
            Corpses.Add(new SaveData.MonsterBodySave
            {
                prefabId = ResolvePrefabId(m.gameObject),
                position = m.transform.position,
                health = 0f,
            });
        }
    }

    /// <summary>采样选卡候选（选卡界面退出时调用；pendingChoice=true 时把当前候选卡快照进存档）。</summary>
    void SampleChoicePicks()
    {
        ChoicePicks.Clear();
        var cm = CardManager.Instance;
        if (cm == null || cm.currentPicks == null) return;
        foreach (var c in cm.currentPicks)
            if (c != null && !string.IsNullOrEmpty(c.effectId)) ChoicePicks.Add(c.effectId);
    }

    /// <summary>
    /// 解析 prefabId：优先取 MonsterPool 反查的真实 prefab 资产名（恢复时与波表 prefab.name 匹配）；
    /// 非池实例（如场景静态怪）回退去 "(Clone)" 的实例名。
    /// </summary>
    static string ResolvePrefabId(GameObject instance)
    {
        if (instance == null) return null;
        var prefab = MonsterPool.Instance != null ? MonsterPool.Instance.GetPrefabOf(instance) : null;
        if (prefab != null) return prefab.name;
        string n = instance.name;
        return n != null ? n.Replace("(Clone)", "") : null;
    }

    /// <summary>
    /// 结束对局（重开/胜利/失败）：清内存态 + 清存档，回到无会话状态。
    /// </summary>
    public void EndRun()
    {
        HasActiveRun = false;
        CompletedWaveIndex = -1;
        UnlockedEffects.Clear();
        SaveCoordinator.DeleteSave();
        Debug.Log("[RunSession] 对局结束，进度已清除。");
    }
}
