using UnityEngine;

/// <summary>
/// 地形类型：每个 Tile 归属一类，生成期确定并快照进 TileData.kind（生成策略/占比按此调配）。
/// 不在 prefab 上手动配置——由 TileSemantics.ResolveKind 按 prefab 语义自动推导：
///   挂 TerrainEffectTile → Trigger；带 solid Collider → Decoration；其余 → Normal。
/// ChunkDef 各池（normalTiles/triggerTiles/decorationTiles）即分类载体，池归属与推导结果应一致。
/// </summary>
public enum TerrainKind
{
    /// <summary>普通地面：可行走。无视觉 prefab 时由 ChunkVisualizer 默认地面板兜底显示。</summary>
    Normal = 0,
    /// <summary>可触发地块：机关/触发区（岩浆/地刺/传送/事件等）。阻挡/伤害由组件语义决定。</summary>
    Trigger = 1,
    /// <summary>装饰地块：柱子/雕塑等视觉装饰。阻挡由 prefab 自带 solid Collider 物理决定（可贴边绕行），模型可超出单格。</summary>
    Decoration = 2,
}

/// <summary>
/// 地块语义推导（原 TileVisual 组件的静态职责，组件已删除）：
/// prefab 的玩法语义由其自带的组件/碰撞体直接表达，无需任何标记组件：
///   - 挂 TerrainEffectTile → Trigger（机关地块）
///   - 带 solid Collider → Decoration（装饰物，物理阻挡精确贴边）
///   - 其余 → Normal（普通地面）
/// 可走性同理：逻辑通行 = 无 solid Collider；物理阻挡由模型 Collider 精确决定。
/// </summary>
public static class TileSemantics
{
    /// <summary>推导地块生成分类（生成策略/占比仍按 TerrainKind 调配）。</summary>
    public static TerrainKind ResolveKind(GameObject prefab)
    {
        if (prefab == null) return TerrainKind.Normal;
        if (prefab.GetComponent<TerrainEffectTile>() != null) return TerrainKind.Trigger;
        if (HasSolidCollider(prefab)) return TerrainKind.Decoration;
        return TerrainKind.Normal;
    }

    /// <summary>
    /// 是否带 solid 碰撞体（非 Trigger）。物理阻挡的唯一来源：
    /// 玩家/怪物移动用 Physics.SphereCast，碰撞体精确挡路（可贴边绕行，不再整格封锁）。
    /// 刷怪/开放边等逻辑通行性也以「无 solid 碰撞体」为判据（避免刷进柱子）。
    /// </summary>
    public static bool HasSolidCollider(GameObject prefab)
    {
        if (prefab == null) return false;
        var colliders = prefab.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && !colliders[i].isTrigger) return true;
        }
        return false;
    }
}
