/// <summary>
/// 地形类型：每个 Tile 归属一类，生成期确定并快照进 TileData.kind。
/// 由 TileVisual.terrainKind 承载（随 prefab 走），生成器从组件读取。
/// </summary>
public enum TerrainKind
{
    /// <summary>普通地面：可行走。无视觉 prefab 时由 ChunkVisualizer 默认地面板兜底显示。</summary>
    Normal = 0,
    /// <summary>可触发地块：机关/触发区（岩浆/地刺/传送/事件等）。可走性由 TileVisual.isWalkable 配置。</summary>
    Trigger = 1,
    /// <summary>装饰地块：柱子/雕塑等视觉装饰。可走性由 TileVisual.isWalkable 配置（不一定阻挡），模型可超出单格。</summary>
    Decoration = 2,
}

/// <summary>
/// Tile 玩法标记层（MarkerLayer）类型：刷怪点 / 出入口 / 事件触发区等。
/// 由 TileVisual.markerType 承载（随 prefab 走），MonsterSpawner 识别 SpawnPoint 作刷怪点。
/// </summary>
public enum TileMarkerType
{
    None = 0,
    SpawnPoint = 1,
    Entrance = 2,
    EventTrigger = 3,
    Teleport = 4,
}
