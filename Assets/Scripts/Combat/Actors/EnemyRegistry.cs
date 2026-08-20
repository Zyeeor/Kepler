using System.Collections.Generic;

/// <summary>
/// 敌方单位注册表（Kimi 评审整改 P1-2）：替代技能/索敌路径的 FindObjectsOfType /
/// FindGameObjectsWithTag 全场景扫描（每帧调用时性能随怪数量线性劣化）。
///
/// 注册时机：Enemy.OnEnable / OnDisable（池化怪 SetActive(false) 回池自动注销，
/// Spawn 激活自动注册——语义与"活跃敌方"一致）。
///
/// 读取方规则：枚举 All 时**只读过滤**（isDowned/isPossessed/距离等），不得在循环内注册/注销。
/// </summary>
public static class EnemyRegistry
{
    static readonly List<Enemy> enemies = new List<Enemy>(32);

    /// <summary>当前活跃敌方列表（只读语义；快照遍历前可用 Count 判断空）。</summary>
    public static IReadOnlyList<Enemy> All => enemies;

    public static int Count => enemies.Count;

    internal static void Register(Enemy e)
    {
        if (e == null || enemies.Contains(e)) return;
        enemies.Add(e);
    }

    internal static void Unregister(Enemy e)
    {
        enemies.Remove(e);
    }

    /// <summary>清空注册表（场景切换兜底；Enemy.OnDisable 覆盖常规路径，此方法供 GameManager 重置调用）。</summary>
    public static void Clear()
    {
        enemies.Clear();
    }
}
