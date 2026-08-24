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
