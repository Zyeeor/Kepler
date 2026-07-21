using UnityEngine;

/// <summary>
/// 灰盒测试场景的集中参数配置。
/// 挂在场景中任意 GameObject 上，所有参数可在 Inspector 中调整。
/// ArenaBuilder 读取此配置来生成场景几何体。
/// </summary>
public class ArenaConfig : MonoBehaviour
{
    [Header("Room Dimensions")]
    [Tooltip("房间总长度（X轴）")]
    public float roomWidth = 64f;
    [Tooltip("房间总深度（Z轴）")]
    public float roomDepth = 48f;
    [Tooltip("墙壁高度（北/东/西三面）")]
    public float wallHeight = 6f;
    [Tooltip("南墙高度 — 降低以避免遮挡玩家出生点视线")]
    public float southWallHeight = 1.5f;
    [Tooltip("墙壁厚度")]
    public float wallThickness = 1f;
    [Tooltip("地面 Y 坐标")]
    public float floorY = 0f;

    [Header("Central Pillars")]
    [Tooltip("中央大柱子数量")]
    public int pillarCount = 2;
    [Tooltip("柱子宽度（X轴）")]
    public float pillarWidth = 3f;
    [Tooltip("柱子深度（Z轴）")]
    public float pillarDepth = 3f;
    [Tooltip("柱子高度")]
    public float pillarHeight = 6f;
    [Tooltip("两个柱子之间的间距（中心到中心）")]
    public float pillarSpacing = 14f;
    [Tooltip("自定义柱子位置（非空时覆盖算法生成）。用 Capture Layout 按钮填充。")]
    public Vector3[] customPillarPositions;

    [Header("Side Covers")]
    [Tooltip("单侧掩体数量（左右各N个）")]
    public int coversPerSide = 2;
    [Tooltip("掩体宽度（X轴）")]
    public float coverWidth = 1f;
    [Tooltip("掩体深度（Z轴）")]
    public float coverDepth = 4f;
    [Tooltip("掩体高度")]
    public float coverHeight = 4f;
    [Tooltip("掩体到墙壁的距离")]
    public float coverDistanceFromWall = 8f;
    [Tooltip("相邻掩体在Z轴上的间距")]
    public float coverSpacing = 12f;
    [Tooltip("自定义掩体位置（非空时覆盖算法生成）。用 Capture Layout 按钮填充。")]
    public Vector3[] customCoverPositions;

    [Header("Spawn Points")]
    [Tooltip("玩家出生位置（南侧中央）。y=0.05 让 Player 的 CapsuleCollider（中心 y=0.9）底部正好坐在地面顶面 y=0.1 上方。")]
    public Vector3 playerSpawnPos = new Vector3(0, 0.05f, -20f);
    [Tooltip("敌人生成器位置（北侧）。y=1 是因为 enemy prefab 用的是 Unity 默认 Capsule mesh（原点在几何中心，垂直方向占 y:[-1,+1]），抬高 1m 让 mesh 底部正好坐在地面上 — 不要再手动加 y。")]
    public Vector3 enemySpawnerPos = new Vector3(0, 1f, 18f);
    [Tooltip("敌人生成半径。掩体在 x=±24，柱子在 x=±7，所以 8 不会覆盖到静态障碍（Spawner 自带避障）。")]
    public float enemySpawnRadius = 8f;

    [Header("Camera — Hades Style (Perspective)")]
    [Tooltip("投影模式：true=正交 Orthographic，false=透视 Perspective（推荐）。")]
    public bool cameraOrthographic = false;
    [Tooltip("透视视野角度。25–35 减弱近大远小。仅透视生效。")]
    public float cameraFieldOfView = 30f;
    [Tooltip("正交视野半高。仅正交生效。")]
    public float cameraOrthoSize = 11f;
    [Tooltip("相机俯角（绕 X 轴）。Hades 风格 45°–60°。")]
    public float cameraPitch = 55f;
    [Tooltip("相机绕 Y 轴朝向（0 或 45，按房间统一）。")]
    public float cameraYaw = 0f;
    [Tooltip("相机相对目标的水平距离（固定）。")]
    public float cameraDistance = 12f;
    [Tooltip("相机高度（自动计算 = distance × tan(pitch)）。改 Pitch 或 Distance 来调整。")]
    public float cameraHeight = 17f;
    [Tooltip("玩家屏幕归一化位置。(0.5,0.5)=正中；y=0.35 让玩家偏下方（类 Hades）。")]
    public Vector2 playerScreenOffset = new Vector2(0.5f, 0.35f);
    [Tooltip("跟随平滑时间（秒）。0.1–0.25。")]
    public float followSmoothTime = 0.15f;
    [Tooltip("边界内缩边距（世界单位）。")]
    public float confinerPadding = 0f;

    [Header("Material Colors")]
    public Color wallColor = new Color(0.25f, 0.25f, 0.3f, 1f);
    public Color pillarColor = new Color(0.35f, 0.3f, 0.35f, 1f);
    public Color coverColor = new Color(0.4f, 0.4f, 0.45f, 1f);
    public Color floorColor = new Color(0.55f, 0.55f, 0.58f, 1f);

    [Header("Gizmo Settings")]
    [Tooltip("是否在 Scene 视图中绘制出生点 Gizmo")]
    public bool showSpawnGizmos = true;
    [Tooltip("玩家出生点 Gizmo 颜色")]
    public Color playerGizmoColor = Color.green;
    [Tooltip("敌人生成区 Gizmo 颜色")]
    public Color enemyGizmoColor = Color.red;
    [Tooltip("出生点 Gizmo 大小")]
    public float gizmoSize = 2f;

    private void OnDrawGizmos()
    {
        if (!showSpawnGizmos) return;

        // 玩家出生点
        Gizmos.color = playerGizmoColor;
        Gizmos.DrawSphere(playerSpawnPos, gizmoSize * 0.3f);
        Gizmos.DrawWireSphere(playerSpawnPos, gizmoSize * 0.6f);
        Gizmos.DrawLine(playerSpawnPos, playerSpawnPos + Vector3.up * gizmoSize * 2f);

        // 敌人生成区域
        Gizmos.color = enemyGizmoColor;
        Gizmos.DrawWireSphere(enemySpawnerPos, enemySpawnRadius);
        Gizmos.DrawSphere(enemySpawnerPos, gizmoSize * 0.2f);

        // 房间地板轮廓
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        float halfW = roomWidth * 0.5f;
        float halfD = roomDepth * 0.5f;
        Vector3 p0 = new Vector3(-halfW, floorY, -halfD);
        Vector3 p1 = new Vector3( halfW, floorY, -halfD);
        Vector3 p2 = new Vector3( halfW, floorY,  halfD);
        Vector3 p3 = new Vector3(-halfW, floorY,  halfD);
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p0);
    }
}
