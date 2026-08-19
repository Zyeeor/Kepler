using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 装饰物 Mesh 碰撞工具（方案：模型精确碰撞）。
///
/// 需求：装饰物物理阻挡由模型自带 solid Collider 决定（玩家可贴边绕行）。
/// 碰撞体采用 convex MeshCollider：
///   - 形状贴合模型轮廓（比 Capsule/Box 精确）；
///   - convex 不要求 mesh 可读（美术 fbx 默认 Read/Write 关闭也直接可用）；
///   - 挂载在三角面最多的 MeshFilter 同物体上（mesh 顶点在其 local 空间，transform 一致）。
///
/// 用法：菜单 Tools → Decorations → Add Mesh Colliders。
///   - 遍历 ChunkDef.decorationTiles：删除全部 CapsuleCollider，按主体 mesh 添加 convex MeshCollider；
///   - 已有 MeshCollider 的 prefab 跳过；Force 变体先删旧 MeshCollider 再重建。
///
/// 已知限制与形状选择：
///   - convex 凸包会把凹形状填满（如拱门的门洞被凸包封死），**有门洞/凹腔的模型**
///     （拱门/门）必须用 non-convex MeshCollider（需可读 mesh 副本——已在 arch1/Gate1
///     上通过 _col 子资产落地，本工具不重复处理）；
///   - convex 凸包 256 多边形上限，高面数模型（如 20 万面的雕像）用 partial hull，
///     轮廓可能略失细节——终极方案由美术提供简化碰撞网格。
/// </summary>
public static class DecorColliderTools
{
    [MenuItem("Tools/Decorations/Add Mesh Colliders")]
    static void AddMeshColliders()
    {
        Process(forceOverride: false);
    }

    [MenuItem("Tools/Decorations/Add Mesh Colliders (Force)")]
    static void AddMeshCollidersForce()
    {
        Process(forceOverride: true);
    }

    static void Process(bool forceOverride)
    {
        var def = AssetDatabase.LoadAssetAtPath<ChunkDef>("Assets/Settings/MapStreaming/ChunkDef.asset");
        if (def == null || def.decorationTiles == null)
        {
            Debug.LogWarning("[DecorColliderTools] ChunkDef.asset 或 decorationTiles 为空。");
            return;
        }

        int converted = 0, skipped = 0;
        foreach (var entry in def.decorationTiles)
        {
            if (entry == null) continue;
            string path = AssetDatabase.GetAssetPath(entry);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogWarning($"[DecorColliderTools] prefab 不存在: {path}");
                skipped++;
                continue;
            }
            try
            {
                var existing = root.GetComponentsInChildren<MeshCollider>(true);
                if (existing.Length > 0)
                {
                    if (forceOverride)
                    {
                        foreach (var mc in existing) Object.DestroyImmediate(mc);
                    }
                    else
                    {
                        Debug.Log($"[DecorColliderTools] 跳过（已有 MeshCollider）: {root.name}");
                        skipped++;
                        continue;
                    }
                }

                // 主体 mesh = 三角面最多的 MeshFilter
                MeshFilter best = null;
                float bestTri = -1f;
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    float tri = mf.sharedMesh.triangles.Length / 3f;
                    if (tri > bestTri) { bestTri = tri; best = mf; }
                }
                if (best == null)
                {
                    Debug.LogWarning($"[DecorColliderTools] 无 MeshFilter: {root.name}");
                    skipped++;
                    continue;
                }

                var mcNew = best.gameObject.AddComponent<MeshCollider>();
                mcNew.sharedMesh = best.sharedMesh;
                mcNew.convex = true;

                int removed = 0;
                foreach (var c in root.GetComponentsInChildren<CapsuleCollider>(true))
                {
                    Object.DestroyImmediate(c);
                    removed++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                converted++;
                Debug.Log($"[DecorColliderTools] {root.name} → MeshCollider(convex) @'{best.transform.name}' " +
                          $"tri={bestTri} 移除 Capsule x{removed}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[DecorColliderTools] 完成: 转换 {converted}，跳过 {skipped}");
    }
}
