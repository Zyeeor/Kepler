using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

/// <summary>
/// 灰盒竞技场构建器 — Editor 工具。
/// 在 Inspector 中点击"Build Arena"生成整个场景的灰盒结构。
/// 参数由场景中的 ArenaConfig 组件提供。
/// </summary>
[CustomEditor(typeof(ArenaConfig))]
public class ArenaBuilder : Editor
{
    private static readonly string ArenaRootName = "GrayboxArena";
    private static readonly string WallsParentName = "Walls";
    private static readonly string PillarsParentName = "Pillars";
    private static readonly string CoversParentName = "Covers";
    private static readonly string SpawnsParentName = "SpawnPoints";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "点击 Build Arena 将：\n" +
            "  1. 删除旧的灰盒结构\n" +
            "  2. 按当前参数重新生成所有几何体\n" +
            "  3. 自动分配纯色材质\n" +
            "  4. 添加 BoxCollider\n" +
            "  5. 创建带 Gizmo 的出生点标记",
            MessageType.Info);

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Build Arena", GUILayout.Height(36)))
        {
            BuildArena((ArenaConfig)target);
        }
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
        if (GUILayout.Button("Cleanup Arena", GUILayout.Height(36)))
        {
            CleanupArena();
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "手动调整柱子/掩体位置后，先点「Capture Layout」回写位置到配置，\n" +
            "再 Build Arena 就不会覆盖你的手动调整了。\n" +
            "清空自定义位置后 Build 会恢复算法生成。",
            MessageType.Info);

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
        if (GUILayout.Button("Capture Current Layout", GUILayout.Height(32)))
        {
            CaptureSceneLayout((ArenaConfig)target);
        }
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
        if (GUILayout.Button("Clear Custom Layout", GUILayout.Height(32)))
        {
            ClearCustomLayout((ArenaConfig)target);
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
    }

    [MenuItem("Tools/Arena/Build Arena", false, 100)]
    private static void BuildArenaMenu()
    {
        ArenaConfig config = FindObjectOfType<ArenaConfig>();
        if (config == null)
        {
            GameObject go = new GameObject("ArenaConfig");
            Undo.RegisterCreatedObjectUndo(go, "Create ArenaConfig");
            config = go.AddComponent<ArenaConfig>();
        }
        BuildArena(config);
    }

    [MenuItem("Tools/Arena/Cleanup Arena", false, 101)]
    private static void CleanupArenaMenu()
    {
        CleanupArena();
    }

    [MenuItem("Tools/Arena/Reset EnemySpawner", false, 102)]
    private static void ResetEnemySpawnerMenu()
    {
        ResetEnemySpawner();
    }

    private static void BuildArena(ArenaConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[Arena] No ArenaConfig found in scene!");
            return;
        }

        // 记录 Undo
        Undo.SetCurrentGroupName("Build Graybox Arena");
        int undoGroup = Undo.GetCurrentGroup();

        // 1. 清理旧结构
        CleanupArenaInternal();

        // 2. 创建根节点
        Transform arenaRoot = EnsureChild(null, ArenaRootName).transform;

        // 3. 创建地面
        CreateFloor(arenaRoot, config);

        // 4. 创建墙壁
        Transform wallsParent = EnsureChild(arenaRoot, WallsParentName).transform;
        CreateWalls(wallsParent, config);

        // 5. 创建中央柱子
        Transform pillarsParent = EnsureChild(arenaRoot, PillarsParentName).transform;
        CreatePillars(pillarsParent, config);

        // 6. 创建掩体
        Transform coversParent = EnsureChild(arenaRoot, CoversParentName).transform;
        CreateCovers(coversParent, config);

        // 7. 创建出生点标记
        Transform spawnsParent = EnsureChild(arenaRoot, SpawnsParentName).transform;
        CreateSpawnMarkers(spawnsParent, config);

        // 8. 处理现有 Ground
        ProcessExistingGround();

        // 9. 放置/移动 3D Player
        PlacePlayer(config);

        // 10. 更新 EnemySpawner
        UpdateEnemySpawner(config);

        // 11. 调整 Main Camera
        UpdateCamera(config);

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("[Arena] Graybox arena built successfully!");
    }

    private static void CleanupArena()
    {
        Undo.SetCurrentGroupName("Cleanup Graybox Arena");
        CleanupArenaInternal();
        Debug.Log("[Arena] Arena cleaned up.");
    }

    private static void CleanupArenaInternal()
    {
        GameObject existing = GameObject.Find(ArenaRootName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        // 删除之前的 Player 实例（每次 Build 都会创建新的，防止重复）
        GameObject[] oldPlayers = GameObject.FindObjectsOfType<GameObject>();
        foreach (GameObject go in oldPlayers)
        {
            if (go.name == "Player" && PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.Connected)
            {
                Undo.DestroyObjectImmediate(go);
            }
        }

        // 删除之前生成的敌人实例（防止多次 Build 后敌人重叠 + 高度不对）
        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in allEnemies)
        {
            Undo.DestroyObjectImmediate(enemy);
        }

        // 删除 ArenaBuilder 创建的独立 SpawnPoint marker（如果有残留）
        GameObject oldSpawn = GameObject.Find("PlayerSpawnPoint");
        if (oldSpawn != null) Undo.DestroyObjectImmediate(oldSpawn);
        oldSpawn = GameObject.Find("EnemySpawnPoint");
        if (oldSpawn != null) Undo.DestroyObjectImmediate(oldSpawn);
    }

    private static GameObject EnsureChild(Transform parent, string name)
    {
        GameObject child;
        if (parent != null)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            child = new GameObject(name);
        }
        else
        {
            child = GameObject.Find(name);
            if (child != null) return child;
            child = new GameObject(name);
        }
        Undo.RegisterCreatedObjectUndo(child, "Create " + name);
        if (parent != null) child.transform.SetParent(parent);
        return child;
    }

    // ─── Ground ────────────────────────────────────────────
    private static void CreateFloor(Transform parent, ArenaConfig c)
    {
        // 用薄 Cube 代替 Plane，BoxCollider 更厚实，怪物不会穿透
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(floor, "Create Floor");
        floor.name = "Floor";
        floor.transform.SetParent(parent);

        float thickness = 0.2f;
        float halfT = thickness * 0.5f;
        floor.transform.localScale = new Vector3(c.roomWidth, thickness, c.roomDepth);
        floor.transform.localPosition = new Vector3(0, c.floorY - halfT, 0);

        SetMaterialColor(floor, c.floorColor);
        EnsureSolidCollider(floor);
    }

    // ─── Walls ─────────────────────────────────────────────
    private static void CreateWalls(Transform parent, ArenaConfig c)
    {
        float halfW = c.roomWidth * 0.5f;
        float halfD = c.roomDepth * 0.5f;

        // North (+Z)
        float ny = c.floorY + c.wallHeight * 0.5f;
        CreateWall(parent, "Wall_North",
            new Vector3(0, ny, halfD),
            new Vector3(c.roomWidth + c.wallThickness * 2, c.wallHeight, c.wallThickness),
            c.wallColor);

        // South (-Z) — 矮墙，避免遮挡玩家出生点
        float sy = c.floorY + c.southWallHeight * 0.5f;
        CreateWall(parent, "Wall_South",
            new Vector3(0, sy, -halfD),
            new Vector3(c.roomWidth + c.wallThickness * 2, c.southWallHeight, c.wallThickness),
            c.wallColor);

        // East (+X)
        float ey = c.floorY + c.wallHeight * 0.5f;
        CreateWall(parent, "Wall_East",
            new Vector3(halfW, ey, 0),
            new Vector3(c.wallThickness, c.wallHeight, c.roomDepth),
            c.wallColor);

        // West (-X)
        CreateWall(parent, "Wall_West",
            new Vector3(-halfW, ey, 0),
            new Vector3(c.wallThickness, c.wallHeight, c.roomDepth),
            c.wallColor);
    }

    private static GameObject CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(wall, "Create " + name);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.localPosition = pos;
        wall.transform.localScale = scale;

        // Cube 自带 BoxCollider，不需额外添加。确保不是 Trigger。
        BoxCollider bc = wall.GetComponent<BoxCollider>();
        if (bc != null)
        {
            bc.isTrigger = false;
            bc.center = Vector3.zero;
            bc.size = Vector3.one;
        }
        else
        {
            BoxCollider added = wall.AddComponent<BoxCollider>();
            added.isTrigger = false;
        }

        SetMaterialColor(wall, color);
        return wall;
    }

    // ─── Pillars ───────────────────────────────────────────
    private static void CreatePillars(Transform parent, ArenaConfig c)
    {
        // 如果有自定义位置，优先使用
        if (c.customPillarPositions != null && c.customPillarPositions.Length > 0)
        {
            float halfH = c.pillarHeight * 0.5f;
            string[] names = { "Pillar_Center_Left", "Pillar_Center_Right" };

            for (int i = 0; i < Mathf.Min(c.customPillarPositions.Length, c.pillarCount); i++)
            {
                Vector3 pos = c.customPillarPositions[i];
                // y 用 floorY + halfH（保持高度一致），只从自定义位置取 x/z
                Vector3 finalPos = new Vector3(pos.x, c.floorY + halfH, pos.z);
                string name = i < names.Length ? names[i] : $"Pillar_{i}";
                CreatePillarObject(parent, name, finalPos, c);
            }

            // 如果自定义位置不够 pillarCount，剩余用算法补
            if (c.customPillarPositions.Length < c.pillarCount)
            {
                CreatePillarsAlgorithmic(parent, c, startIndex: c.customPillarPositions.Length);
            }
            return;
        }

        CreatePillarsAlgorithmic(parent, c, 0);
    }

    private static void CreatePillarsAlgorithmic(Transform parent, ArenaConfig c, int startIndex)
    {
        float halfSpacing = c.pillarSpacing * 0.5f;
        float halfH = c.pillarHeight * 0.5f;
        string[] names = { "Pillar_Center_Left", "Pillar_Center_Right" };
        float[] xOffsets = { -halfSpacing, halfSpacing };

        for (int i = startIndex; i < c.pillarCount; i++)
        {
            string name = i < names.Length ? names[i] : $"Pillar_{i}";
            Vector3 pos = new Vector3(xOffsets[i], c.floorY + halfH, 0);
            CreatePillarObject(parent, name, pos, c);
        }
    }

    private static GameObject CreatePillarObject(Transform parent, string name, Vector3 localPos, ArenaConfig c)
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Undo.RegisterCreatedObjectUndo(pillar, "Create " + name);
        pillar.name = name;
        pillar.transform.SetParent(parent);
        pillar.transform.localPosition = localPos;
        float targetRadiusX = c.pillarWidth * 0.5f;
        float targetRadiusZ = c.pillarDepth * 0.5f;
        pillar.transform.localScale = new Vector3(
            targetRadiusX / 0.5f,
            c.pillarHeight / 2f,
            targetRadiusZ / 0.5f);
        SetMaterialColor(pillar, c.pillarColor);
        EnsureSolidCollider(pillar);
        return pillar;
    }

    // ─── Covers ────────────────────────────────────────────
    private static void CreateCovers(Transform parent, ArenaConfig c)
    {
        // 如果有自定义位置，优先使用
        if (c.customCoverPositions != null && c.customCoverPositions.Length > 0)
        {
            float halfH = c.coverHeight * 0.5f;
            int totalAlgorithmic = c.coversPerSide * 2;

            for (int i = 0; i < Mathf.Min(c.customCoverPositions.Length, totalAlgorithmic); i++)
            {
                Vector3 pos = c.customCoverPositions[i];
                Vector3 finalPos = new Vector3(pos.x, c.floorY + halfH, pos.z);
                CreateCover(parent, $"Cover_{i}", finalPos,
                    new Vector3(c.coverWidth, c.coverHeight, c.coverDepth), c.coverColor);
            }

            // 如果自定义位置不够，剩余用算法补
            if (c.customCoverPositions.Length < totalAlgorithmic)
            {
                CreateCoversAlgorithmic(parent, c, startIndex: c.customCoverPositions.Length);
            }
            return;
        }

        CreateCoversAlgorithmic(parent, c, 0);
    }

    private static void CreateCoversAlgorithmic(Transform parent, ArenaConfig c, int startIndex)
    {
        float halfW = c.roomWidth * 0.5f;
        float halfH = c.coverHeight * 0.5f;
        float xPos = halfW - c.coverDistanceFromWall;
        float totalSpan = c.coverSpacing * (c.coversPerSide - 1);
        float zStart = -totalSpan * 0.5f;

        // 跳过前 startIndex 个（已被自定义位置覆盖）
        int skipPairs = startIndex / 2;
        for (int i = skipPairs; i < c.coversPerSide; i++)
        {
            float z = zStart + i * c.coverSpacing;

            CreateCover(parent, $"Cover_Left_{i + 1}",
                new Vector3(-xPos, c.floorY + halfH, z),
                new Vector3(c.coverWidth, c.coverHeight, c.coverDepth),
                c.coverColor);

            CreateCover(parent, $"Cover_Right_{i + 1}",
                new Vector3(xPos, c.floorY + halfH, z),
                new Vector3(c.coverWidth, c.coverHeight, c.coverDepth),
                c.coverColor);
        }
    }

    private static GameObject CreateCover(Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(cover, "Create " + name);
        cover.name = name;
        cover.transform.SetParent(parent);
        cover.transform.localPosition = pos;
        cover.transform.localScale = scale;

        SetMaterialColor(cover, color);
        EnsureSolidCollider(cover);
        return cover;
    }

    // ─── Spawn Markers ─────────────────────────────────────
    private static void CreateSpawnMarkers(Transform parent, ArenaConfig c)
    {
        // Player spawn — 绿色标记
        GameObject playerMark = new GameObject("PlayerSpawnPoint");
        Undo.RegisterCreatedObjectUndo(playerMark, "Create PlayerSpawnPoint");
        playerMark.transform.SetParent(parent);
        playerMark.transform.position = c.playerSpawnPos;
        playerMark.AddComponent<PlayerSpawnGizmo>();

        // Enemy spawn zone — 红色标记
        GameObject enemyMark = new GameObject("EnemySpawnZone");
        Undo.RegisterCreatedObjectUndo(enemyMark, "Create EnemySpawnZone");
        enemyMark.transform.SetParent(parent);
        enemyMark.transform.position = c.enemySpawnerPos;
        EnemySpawnGizmo esg = enemyMark.AddComponent<EnemySpawnGizmo>();
        esg.spawnRadius = c.enemySpawnRadius;
    }

    // ─── Player Placement ─────────────────────────────────
    private const string PlayerPrefabGuid = "9099eb884cfa35b438e6b9e4fd5f7cb5";

    private static void PlacePlayer(ArenaConfig c)
    {
        // 先找场景中 tag=Player 的 3D 对象（排除 UI RectTransform 的假 Player）
        GameObject existing3dPlayer = null;
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
        {
            Transform t = go.transform;
            if (t is RectTransform) continue;
            if (go.scene.IsValid())
            {
                existing3dPlayer = go;
                break;
            }
        }

        if (existing3dPlayer != null)
        {
            Undo.RecordObject(existing3dPlayer.transform, "Move Player to Spawn");
            existing3dPlayer.transform.position = c.playerSpawnPos;
            Debug.Log($"[Arena] Existing Player moved to spawn point: {c.playerSpawnPos}");
            return;
        }

        string prefabPath = AssetDatabase.GUIDToAssetPath(PlayerPrefabGuid);
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError("[Arena] Player prefab not found. Please drag Player.prefab into the scene manually at the green spawn marker.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError("[Arena] Failed to load Player prefab. Please place it manually.");
            return;
        }

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.transform.position = c.playerSpawnPos;
        Undo.RegisterCreatedObjectUndo(player, "Place Player Prefab");

        // 修复 Player 碰撞器：原 Prefab 上的 SphereCollider 是 Trigger, 子物体的 MeshCollider 是非凸的
        // 两者都不会对静态墙柱产生物理碰撞。添加一个实心 CapsuleCollider。
        FixPlayerColliders(player);

        Debug.Log($"[Arena] Player prefab instantiated at spawn point: {c.playerSpawnPos}");
    }

    private static void FixPlayerColliders(GameObject player)
    {
        // 1. 确保 Triggger SphereCollider 保持为 Trigger（不影响墙壁碰撞，用于游戏检测）
        SphereCollider triggerSphere = player.GetComponent<SphereCollider>();
        if (triggerSphere != null)
        {
            Undo.RecordObject(triggerSphere, "Ensure Trigger Sphere");
            triggerSphere.isTrigger = true;  // 确认为 trigger，不影响物理碰撞
            triggerSphere.enabled = true;
        }

        // 2. 递归禁用所有非凸的 MeshCollider（不能用于 Rigidbody 动态碰撞）
        MeshCollider[] allMeshColliders = player.GetComponentsInChildren<MeshCollider>(true);
        foreach (MeshCollider mc in allMeshColliders)
        {
            if (!mc.convex)
            {
                Undo.RecordObject(mc, "Disable Non-Convex MeshCollider");
                mc.enabled = false;
            }
        }

        // 3. 添加实心 CapsuleCollider
        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            capsule = Undo.AddComponent<CapsuleCollider>(player);
        }
        capsule.isTrigger = false;
        capsule.center = new Vector3(0, 0.9f, 0);  // 脚到腰部
        capsule.radius = 0.4f;
        capsule.height = 1.8f;
        capsule.direction = 1;  // Y 轴

        // 4. 设为 Kinematic — MovePosition 只在 kinematic 模式下正确检测静态碰撞器
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Undo.RecordObject(rb, "Set Player Rigidbody Kinematic");
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        Debug.Log("[Arena] Player colliders fixed: kinematic=true, CapsuleCollider added, non-convex meshes disabled.");
    }

    // ─── Existing Objects ──────────────────────────────────
    private static void ProcessExistingGround()
    {
        // 旧 Ground 是一个超大 Cube (位置 y=-0.82, scale.y=20)，顶面在 y=9.18
        // 这会导致相机斜视角下看到"半高"的地板。直接整个删掉，让新的 Floor Plane 接管。
        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            Undo.DestroyObjectImmediate(ground);
            Debug.Log("[Arena] Old Ground cube removed (was at y=9.18 top face).");
        }

        // 同时检查其他可能干扰的巨型对象（如 Landscape、Terrain）
        string[] otherNames = { "Landscape", "Terrain", "Plane" };
        foreach (string name in otherNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                Undo.DestroyObjectImmediate(obj);
                Debug.Log($"[Arena] Removed leftover: {name}");
            }
        }
    }

    private static void UpdateEnemySpawner(ArenaConfig c)
    {
        // EnemySpawner 是独立组件，挂载在 GameManager GameObject 上
        EnemySpawner spawner = Object.FindObjectOfType<EnemySpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("[Arena] EnemySpawner not found. Please add EnemySpawner component to GameManager.");
            return;
        }

        Undo.RecordObject(spawner, "Update EnemySpawner");
        Undo.RecordObject(spawner.transform, "Move EnemySpawner");

        spawner.spawnRadius = c.enemySpawnRadius;

        // 关键:不再把 spawner 抬到 y=1.5 — 敌人 prefab 自带的 CapsuleCollider 中心 y=0.75,
        // spawner y=0 时敌人 root y=0 → 胶囊底部 y=0 正好坐在地面顶面 y=0.1 上方。
        // 之前 y=1.5 导致敌人浮在地面上方 1.5 米(胶囊中心 y=2.25)。
        spawner.transform.position = new Vector3(
            c.enemySpawnerPos.x,
            c.enemySpawnerPos.y,
            c.enemySpawnerPos.z);

        // 同步更新所有敌人配置中的移动速度和攻击范围
        Debug.Log($"[Arena] EnemySpawner updated: spawnRadius={c.enemySpawnRadius}, position={spawner.transform.position}");
    }

    private static void ResetEnemySpawner()
    {
        EnemySpawner spawner = Object.FindObjectOfType<EnemySpawner>();
        if (spawner != null)
        {
            Undo.RecordObject(spawner, "Reset EnemySpawner");
            spawner.spawnRadius = 5f;
            Debug.Log("[Arena] EnemySpawner reset to default radius=5.");
        }
    }

    // ─── Camera ─────────────────────────────────────────
    private static void UpdateCamera(ArenaConfig c)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[Arena] No Main Camera found (tag MainCamera).");
            return;
        }

        Undo.RecordObject(mainCam, "Update Camera");
        Undo.RecordObject(mainCam.transform, "Update Camera Transform");

        // 1. 移除旧的 CameraFollow（帧率相关 Lerp、跟随 Y、无边界）
        CameraFollow oldFollow = mainCam.GetComponent<CameraFollow>();
        if (oldFollow != null)
        {
            Undo.DestroyObjectImmediate(oldFollow);
        }

        // 2. 设置投影
        if (c.cameraOrthographic)
        {
            mainCam.orthographic = true;
            mainCam.orthographicSize = c.cameraOrthoSize;
        }
        else
        {
            mainCam.orthographic = false;
            mainCam.fieldOfView = c.cameraFieldOfView;
        }

        // 3. 创建 / 查找 CameraTarget（独立对象，不作为 Player 或 Camera 的子物体）
        GameObject targetGO = GameObject.Find("CameraTarget");
        if (targetGO == null)
        {
            targetGO = new GameObject("CameraTarget");
            Undo.RegisterCreatedObjectUndo(targetGO, "Create CameraTarget");
        }
        CameraTarget camTarget = targetGO.GetComponent<CameraTarget>();
        if (camTarget == null) camTarget = Undo.AddComponent<CameraTarget>(targetGO);
        camTarget.enableLookAhead = false; // 第一版关闭前瞻
        // 初始放到玩家出生点的地面位置
        targetGO.transform.position = new Vector3(c.playerSpawnPos.x, 0f, c.playerSpawnPos.z);

        // 4. 加 / 配置 BattleCameraRig
        BattleCameraRig rig = mainCam.GetComponent<BattleCameraRig>();
        if (rig == null) rig = Undo.AddComponent<BattleCameraRig>(mainCam.gameObject);
        Undo.RecordObject(rig, "Configure BattleCameraRig");

        rig.target = targetGO.transform;
        rig.projection = c.cameraOrthographic
            ? BattleCameraRig.ProjectionMode.Orthographic
            : BattleCameraRig.ProjectionMode.Perspective;
        rig.fieldOfView = c.cameraFieldOfView;
        rig.orthographicSize = c.cameraOrthoSize;
        rig.cameraPitch = c.cameraPitch;
        rig.cameraYaw = c.cameraYaw;
        rig.cameraDistance = c.cameraDistance;
        rig.cameraHeight = c.cameraHeight;
        rig.playerScreenOffset = c.playerScreenOffset;
        rig.followSmoothTime = c.followSmoothTime;
        rig.deadZoneRadius = 0.5f;

        // 5. 房间边界（Confiner）— 用 ArenaConfig 的房间尺寸
        rig.useConfiner = true;
        rig.roomCenter = new Vector2(0f, 0f);
        rig.roomSize = new Vector2(c.roomWidth, c.roomDepth);
        rig.confinerPadding = c.confinerPadding;

        // 6. 编辑器下先摆一个合理的初始相机位姿（LookAt 聚焦点，与运行时一致）
        Vector3 focus = new Vector3(0f, 0f, 0f);
        Vector3 backDir = Quaternion.Euler(0f, c.cameraYaw, 0f) * new Vector3(0f, 0f, -1f);
        Vector3 camPos = focus + backDir * c.cameraDistance + Vector3.up * c.cameraHeight;
        mainCam.transform.position = camPos;
        mainCam.transform.LookAt(focus + Vector3.up * 0.5f);

        Debug.Log($"[Arena] Camera rig configured: projection={(c.cameraOrthographic ? "Ortho" : "Persp")}, " +
                  $"pitch={c.cameraPitch}, fov={c.cameraFieldOfView}, dist={c.cameraDistance}, height={c.cameraHeight}, " +
                  $"room={c.roomWidth}x{c.roomDepth}");
    }

    // ─── Material Helpers ──────────────────────────────────
    private static void SetMaterialColor(GameObject go, Color color)
    {
        Renderer rend = go.GetComponent<Renderer>();
        if (rend == null) return;

        // 创建独立材质以避免共享覆盖
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat == null) mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        rend.sharedMaterial = mat;

        // 标记为新创建的资产便于清理
        Undo.RegisterCreatedObjectUndo(mat, "Create Material");
    }

    private static void EnsureSolidCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = false;
            col.enabled = true;
        }
        // 没有碰撞器就加一个
        else
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = false;
            bc.center = Vector3.zero;
            bc.size = Vector3.one;
        }
    }

    // ─── Capture Scene Layout ───────────────────────────────

    private static void CaptureSceneLayout(ArenaConfig config)
    {
        GameObject arena = GameObject.Find(ArenaRootName);
        if (arena == null)
        {
            Debug.LogWarning("[Arena] GrayboxArena not found. Build the arena first.");
            return;
        }

        Undo.RecordObject(config, "Capture Layout Positions");

        // Capture pillars
        Transform pillarsParent = arena.transform.Find(PillarsParentName);
        if (pillarsParent != null && pillarsParent.childCount > 0)
        {
            config.customPillarPositions = new Vector3[pillarsParent.childCount];
            for (int i = 0; i < pillarsParent.childCount; i++)
            {
                config.customPillarPositions[i] = pillarsParent.GetChild(i).localPosition;
            }
            Debug.Log($"[Arena] Captured {config.customPillarPositions.Length} pillar positions.");
        }
        else
        {
            config.customPillarPositions = new Vector3[0];
        }

        // Capture covers
        Transform coversParent = arena.transform.Find(CoversParentName);
        if (coversParent != null && coversParent.childCount > 0)
        {
            config.customCoverPositions = new Vector3[coversParent.childCount];
            for (int i = 0; i < coversParent.childCount; i++)
            {
                config.customCoverPositions[i] = coversParent.GetChild(i).localPosition;
            }
            Debug.Log($"[Arena] Captured {config.customCoverPositions.Length} cover positions.");
        }
        else
        {
            config.customCoverPositions = new Vector3[0];
        }

        EditorUtility.SetDirty(config);
        Debug.Log("[Arena] Layout captured! Next Build Arena will use these exact positions.");
    }

    private static void ClearCustomLayout(ArenaConfig config)
    {
        Undo.RecordObject(config, "Clear Custom Layout");
        config.customPillarPositions = new Vector3[0];
        config.customCoverPositions = new Vector3[0];
        EditorUtility.SetDirty(config);
        Debug.Log("[Arena] Custom layout cleared. Next Build Arena will use algorithmic placement.");
    }
}

/// <summary>
/// 在 Scene 视图中绘制玩家出生点的 Gizmo。
/// </summary>
public class PlayerSpawnGizmo : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 pos = transform.position;
        Gizmos.DrawSphere(pos, 0.25f);
        Gizmos.DrawWireSphere(pos, 0.5f);
        Gizmos.DrawLine(pos, pos + Vector3.up * 2f);
        Gizmos.DrawWireCube(pos + Vector3.up * 0.5f, new Vector3(0.8f, 1.6f, 0.8f));
    }
}

/// <summary>
/// 在 Scene 视图中绘制敌人生成区的 Gizmo。
/// </summary>
public class EnemySpawnGizmo : MonoBehaviour
{
    public float spawnRadius = 5f;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 pos = transform.position;
        Gizmos.DrawSphere(pos, 0.2f);
        Gizmos.DrawWireSphere(pos, spawnRadius);
        // 画十字线
        float halfR = spawnRadius * 0.5f;
        Gizmos.DrawLine(pos + Vector3.left * halfR, pos + Vector3.right * halfR);
        Gizmos.DrawLine(pos + Vector3.forward * halfR, pos + Vector3.back * halfR);
    }
}
