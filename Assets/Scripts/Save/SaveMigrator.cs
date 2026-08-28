using System.Collections.Generic;

/// <summary>
/// 存档版本迁移链（Kimi 评审整改 P2-9）：SaveData 结构演进时按版本注册迁移函数，
/// LoadFromDisk 在版本不符时逐版本链式迁移（v1→v2→v3…），替代"版本不符直接废档"。
///
/// 使用：SchemaVersion +1 时在静态构造函数中注册新迁移函数：
///   static SaveMigrator()
///   {
///       migrations[1] = MigrateV1ToV2;   // 1 → 2
///   }
///   static SaveData MigrateV1ToV2(SaveData d)
///   {
///       // 例如：d.newField = 默认值 / 从旧字段换算
///       return d;
///   }
/// 缺失迁移函数（无法迁移）→ TryMigrate 返回 false，调用方走"作废新局"兜底。
/// </summary>
public static class SaveMigrator
{
    /// <summary>迁移函数表：key = 源版本（迁移到 key+1）。</summary>
    static readonly Dictionary<int, MigrationFunc> migrations = new Dictionary<int, MigrationFunc>();

    public delegate SaveData MigrationFunc(SaveData data);

    static SaveMigrator()
    {
        // v1 → v2：新增 runId（精英 BD 快照 upsert 键）。旧档无 runId，无需换算——
        // 读档恢复时 RunSession.ResumeFromSave 对缺失 runId 自动补生成。
        migrations[1] = MigrateV1ToV2;
        migrations[2] = MigrateV2ToV3;
        migrations[3] = MigrateV3ToV4;
        migrations[4] = MigrateV4ToV5;
        migrations[5] = MigrateV5ToV6;
        migrations[6] = MigrateV6ToV7;
        migrations[7] = MigrateV7ToV8;
    }

    static SaveData MigrateV1ToV2(SaveData d)
    {
        return d;
    }

    static SaveData MigrateV2ToV3(SaveData d)
    {
        if (d.possessionImprints == null) d.possessionImprints = new List<PossessionImprintState>();
        return d;
    }

    static SaveData MigrateV3ToV4(SaveData d)
    {
        // New LustHealProgress is a run-local fractional remainder; old saves start at zero.
        return d;
    }

    static SaveData MigrateV4ToV5(SaveData d)
    {
        d.narrative = null; // 旧档无叙事状态，恢复按新局初始化
        return d;
    }

    static SaveData MigrateV5ToV6(SaveData d)
    {
        // 旧档没有连续刷怪调度游标：恢复时由当前战斗时钟推导下一次调度，
        // 已经过去的配置精英时间点不会重复投放。
        if (d.continuousEliteSpawned == null)
            d.continuousEliteSpawned = new List<bool>();
        return d;
    }

    static SaveData MigrateV6ToV7(SaveData d)
    {
        // v6 没有流程阶段字段：pendingChoice 是唯一可靠的阶段线索。
        d.runPhase = d.pendingChoice ? RunPhase.Choice : RunPhase.Waves;
        return d;
    }

    static SaveData MigrateV7ToV8(SaveData d)
    {
        // v7 only stored the currently possessed body and downed corpses. New saves
        // additionally carry the complete in-scene monster snapshot; old bodies remain
        // available to WaveManager as the backward-compatible fallback.
        if (d.monsterSnapshots == null)
            d.monsterSnapshots = new List<SaveData.MonsterSnapshotSave>();
        return d;
    }

    /// <summary>逐版本迁移：fromVersion → SchemaVersion。任一环节缺函数/失败即返回 false。</summary>
    public static bool TryMigrate(SaveData data, int fromVersion, out SaveData migrated)
    {
        migrated = data;
        // 防御：未来版本档（fromVersion >= SchemaVersion，如回滚构建）或非法版本不可迁移
        if (fromVersion < 1 || fromVersion >= SaveCoordinator.SchemaVersion) return false;
        for (int v = fromVersion; v < SaveCoordinator.SchemaVersion; v++)
        {
            MigrationFunc fn;
            if (!migrations.TryGetValue(v, out fn)) return false;   // 缺少该级迁移函数：不可迁移
            migrated = fn(migrated);
            if (migrated == null) return false;
            migrated.schemaVersion = v + 1;
        }
        return true;
    }
}
