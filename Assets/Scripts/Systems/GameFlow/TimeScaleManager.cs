using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局时间缩放单写点（Kimi K3 架构评审整改 P0-2）：
/// 此前 Time.timeScale 有 6 文件 27 处直接写点，互相覆盖已长出 4 个互感特判。
/// 本管理器收敛为「优先级栈 + 引用计数」：
///   - Push(domain, scale)：记录该域的时间请求；同域重复 Push 引用计数 +1 并覆盖 scale；
///   - Pop(domain)：计数 -1，归零才释放该域；未 Push 过的 Pop 无副作用（幂等）；
///   - 每次变更后，取当前请求中优先级最高的域生效；无请求 = 恢复 1。
/// 高优先级全覆盖低优先级（如 DebugCamera 冻结压倒 GameOver 冻结），同级后者覆盖前者。
/// 用法：
///   TimeScaleManager.Push(TimeDomain.Pause, 0f);     // 暂停
///   TimeScaleManager.Pop(TimeDomain.Pause);          // 恢复
///   TimeScaleManager.Push(TimeDomain.BulletTime, 0.2f);
///   TimeScaleManager.ResetAll();                     // 场景切换/重开清空全部请求
/// </summary>
public class TimeScaleManager : MonoBehaviour
{
    public static TimeScaleManager Instance { get; private set; }

    struct Entry
    {
        public int count;
        public float scale;
    }

    readonly Dictionary<TimeDomain, Entry> entries = new Dictionary<TimeDomain, Entry>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        DontDestroyOnLoad(gameObject);   // DDOL 放 Start（同 AudioManager：Awake 期间可能失效）
        Apply();
    }

    public static TimeScaleManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("TimeScaleManager");
        go.AddComponent<TimeScaleManager>();
        // 二次校验：AddComponent 触发 Awake 时若场景已预置实例（Awake 先跑），以既有实例为准
        if (Instance != null && Instance.gameObject != go)
        {
            Destroy(go);
            return Instance;
        }
        return go.GetComponent<TimeScaleManager>();
    }

    public static void Push(TimeDomain domain, float scale)
    {
        var m = EnsureInstance();
        Entry e;
        if (m.entries.TryGetValue(domain, out e))
        {
            e.count++;
            e.scale = scale;
        }
        else
        {
            e.count = 1;
            e.scale = scale;
        }
        m.entries[domain] = e;
        m.Apply();
    }

    public static void Pop(TimeDomain domain)
    {
        if (Instance == null) return;
        Entry e;
        if (!Instance.entries.TryGetValue(domain, out e)) return;   // 未 Push 过：幂等无副作用
        e.count--;
        if (e.count <= 0) Instance.entries.Remove(domain);
        else Instance.entries[domain] = e;
        Instance.Apply();
    }

    /// <summary>清空全部时间域请求（场景切换/重开前调用），恢复 timeScale=1。</summary>
    public static void ResetAll()
    {
        if (Instance == null)
        {
            Time.timeScale = 1f;
            return;
        }
        Instance.entries.Clear();
        Instance.Apply();
    }

    /// <summary>按最高优先级请求生效（单写点）。</summary>
    void Apply()
    {
        TimeDomain best = TimeDomain.None;
        int bestPrio = -1;
        foreach (var kv in entries)
        {
            if (kv.Value.count <= 0) continue;
            int p = (int)kv.Key;
            if (p > bestPrio)
            {
                bestPrio = p;
                best = kv.Key;
            }
        }
        Time.timeScale = best == TimeDomain.None ? 1f : entries[best].scale;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

/// <summary>
/// 时间域（优先级从低到高，枚举值即优先级）：
/// BulletTime（子弹时间） < HitStop（顿帧） < Pause（选卡/暂停） < GameOver < DebugCamera（调试冻结）。
/// </summary>
public enum TimeDomain
{
    None = 0,
    BulletTime = 10,
    HitStop = 20,
    Pause = 30,
    GameOver = 40,
    DebugCamera = 50,
}
