using System;

/// <summary>
/// 随机流工厂（种子确定性统一入口）。
///
/// 原则：本局一切"可复现随机"（地图/卡牌/刷怪/AI 决策）都从 RunSession.WorldSeed 派生，
/// 经本工厂创建独立 System.Random 流，域之间用质数混合隔离（同域同 salt 同序列，跨域无关联）。
///
/// 使用：SeedSystem.CreateFlow(DomainCard, waveIndex) —— 每波/每会话固定 salt，同种子整局可复现。
/// 例外：worldSeed 本身的生成允许全局随机（种子必须随机，见 RunSession.BeginNewRun）。
/// 遗留：AI 行为（MonsterBTNodes/BTComposite）仍用全局 UnityEngine.Random，
/// 接入时改经本工厂（战斗侧）。
/// </summary>
public static class SeedSystem
{
    // 域常量：新增可复现系统时在此登记，避免公式散落/复制错误
    public const int DomainCard = 1;      // 卡牌（初始三张/刷新/重抽）
    public const int DomainWave = 2;      // 波次刷怪（编队抽取/群系散射/取点）
    public const int DomainAI = 3;        // 怪物 AI/技能随机（每怪子流：salt=刷怪序号）

    /// <summary>当前运行种子（无会话时为 0：直接 Play 场景的可复现基线）。</summary>
    public static int RunSeed => RunSession.Instance != null ? (int)RunSession.Instance.WorldSeed : 0;

    /// <summary>
    /// 创建独立随机流：seed = RunSeed ^ (domain × 1000003) ^ (salt × 786433)。
    /// 质数混合保证：同域不同 salt 序列独立；不同域即使 salt 相同也互不关联。
    /// </summary>
    /// <param name="domain">随机域（DomainXxx 常量）。</param>
    /// <param name="salt">域内区分种子（如波次号、房间号）；同一调用点传固定值。</param>
    public static System.Random CreateFlow(int domain, int salt = 0)
    {
        int seed = RunSeed ^ (domain * 1000003) ^ (salt * 786433);
        return new System.Random(seed);
    }

    /// <summary>流内 [0,1) 均匀随机（替代 UnityEngine.Random.value）。</summary>
    public static float NextFloat(this System.Random rng)
    {
        return (float)rng.NextDouble();
    }

    /// <summary>流内 [min,max) 均匀随机（替代 UnityEngine.Random.Range(float,float)）。</summary>
    public static float NextFloat(this System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    /// <summary>流内 [min,max) 均匀随机整数（替代 UnityEngine.Random.Range(int,int)）。</summary>
    public static int NextInt(this System.Random rng, int min, int maxExclusive)
    {
        return rng.Next(min, maxExclusive);
    }

    /// <summary>流内单位球面均匀方向（替代 UnityEngine.Random.onUnitSphere）。</summary>
    public static UnityEngine.Vector3 NextUnitSphere(this System.Random rng)
    {
        float u = (float)rng.NextDouble();
        float v = (float)rng.NextDouble();
        float theta = 2f * MathF.PI * u;
        float phi = MathF.Acos(2f * v - 1f);
        return new UnityEngine.Vector3(MathF.Sin(phi) * MathF.Cos(theta), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(theta));
    }

    /// <summary>流内单位圆盘均匀点（替代 UnityEngine.Random.insideUnitCircle）。</summary>
    public static UnityEngine.Vector2 NextInsideUnitCircle(this System.Random rng)
    {
        float ang = 2f * MathF.PI * (float)rng.NextDouble();
        float r = MathF.Sqrt((float)rng.NextDouble());
        return new UnityEngine.Vector2(MathF.Cos(ang) * r, MathF.Sin(ang) * r);
    }
}
