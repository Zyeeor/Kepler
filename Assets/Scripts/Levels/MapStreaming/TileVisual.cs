using UnityEngine;

/// <summary>
/// Tile 视觉 + 玩法语义组件：挂在「每个 Tile 一个 prefab」的标准 prefab 根节点上，
/// 是这一格的唯一配置载体——玩法属性随 prefab 走，逻辑与视觉永不脱节。
///
/// 制作规范：手动在本组件上配置玩法语义即可（无需额外工具）。标准化约定（OnValidate 自检提示）：
///   - XZ 轴对齐（无子物体旋转）：1×1 网格、底面 y=0、XZ 中心在原点
///   - 碰撞体可选（伤害区用 Trigger）
/// 生成器与手摆布局从本组件读取玩法语义。
/// </summary>
public class TileVisual : MonoBehaviour
{
    [Header("玩法语义")]
    [Tooltip("地形类别：普通地面/可触发地块/装饰地块。")]
    public TerrainKind terrainKind = TerrainKind.Normal;
    [Tooltip("该格是否可行走（普通地面恒 true；可触发与装饰地块按需配置——装饰物不一定阻挡）。")]
    public bool isWalkable = true;
    [Tooltip("玩法标记（MarkerLayer）：刷怪点/出入口/事件触发区等。SpawnPoint 会被 MonsterSpawner 识别为刷怪点。")]
    public TileMarkerType markerType = TileMarkerType.None;

    [Header("显示")]
    [Tooltip("配置显示名（调试/手摆工具用）。")]
    public string displayName;

    /// <summary>
    /// 规范化自检（编辑期）：Tile prefab 应为 1×1 轴对齐、底面 y=0、XZ 中心在原点。
    /// 仅提示不改数据（避免编辑器自动改伤到手调内容），需手动按规范调整。
    /// </summary>
    void OnValidate()
    {
        if (Application.isPlaying) return;
        var renderer = GetComponentInChildren<Renderer>(true);
        if (renderer == null) return;
        if (renderer.transform.localRotation != Quaternion.identity ||
            renderer.transform.localScale.x < 0.95f || renderer.transform.localScale.x > 1.05f)
        {
            Debug.LogWarning($"[TileVisual] '{name}'：子 Renderer 存在旋转/非 1 缩放——Tile prefab 要求 1×1 轴对齐（美术 fbx 常带旋转面，请手动规范化为 1×1 轴对齐面片）。", this);
            return;
        }
        var b = renderer.bounds;
        // 根在原点、子物体仅平移（1×1）时，bounds 应 XZ ≈ 1、minY ≈ 0、center XZ ≈ 0
        if (b.size.x < 0.9f || b.size.x > 1.1f || b.size.z < 0.9f || b.size.z > 1.1f ||
            Mathf.Abs(b.min.y) > 0.01f || Mathf.Abs(b.center.x) > 0.01f || Mathf.Abs(b.center.z) > 0.01f)
        {
            Debug.LogWarning($"[TileVisual] '{name}'：包围盒 XZ 应≈1×1、底面 y≈0、XZ 中心≈0，实际 size={b.size.ToString("F2")} minY={b.min.y.ToString("F2")} center={b.center.ToString("F2")}。请手动规范化为 1×1 轴对齐面片。", this);
        }
    }
}
