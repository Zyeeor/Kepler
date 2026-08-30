using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reuses spawned monster roots. MonsterActor owns its state reset before an instance is reused.
/// 生命周期（Kimi 评审整改 P2-4）：EnsureInstance 常驻（DDOL，与 AudioManager 同构）——
/// 池跨场景存活，重进对局零重新实例化；死亡 key 在重建实例时顺手清理（防字典单调增长）。
/// </summary>
public class MonsterPool : MonoBehaviour
{
    private static MonsterPool instance;

    private readonly Dictionary<GameObject, Queue<GameObject>> availableByPrefab = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly Dictionary<GameObject, GameObject> prefabByInstance = new Dictionary<GameObject, GameObject>();

    public static MonsterPool Instance => EnsureInstance();

    /// <summary>确保常驻池实例（BootStrapper 或首次刷怪调用）。</summary>
    public static MonsterPool EnsureInstance()
    {
        if (instance != null) return instance;

        instance = FindObjectOfType<MonsterPool>();
        if (instance == null)
        {
            GameObject poolRoot = new GameObject("MonsterPool");
            instance = poolRoot.AddComponent<MonsterPool>();
        }
        return instance;
    }

    void Start()
    {
        // DDOL 放 Start（与 AudioManager 同构）：Awake 期间（场景加载中）调用 DontDestroyOnLoad
        // 可能失效导致常驻池随场景卸载销毁；Start 时场景已加载完成，DDOL 可靠。
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Creates inactive instances ahead of the first spawn. This is intentionally a small
    /// pool warm-up API; MonsterPreloadService calls it once per prefab between card-choice
    /// frames so the expensive Instantiate/Awake cost is not concentrated in one frame.
    /// </summary>
    public void Preload(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;

        if (!availableByPrefab.TryGetValue(prefab, out Queue<GameObject> available))
        {
            available = new Queue<GameObject>();
            availableByPrefab.Add(prefab, available);
        }

        int validCount = 0;
        foreach (GameObject queued in available)
            if (queued != null) validCount++;

        while (validCount < count)
        {
            GameObject warmed = Instantiate(prefab);
            warmed.SetActive(false);
            warmed.transform.SetParent(transform, false);
            prefabByInstance[warmed] = prefab;
            GameManager.ApplyPerformanceOptimizations(warmed);
            available.Enqueue(warmed);
            validCount++;
        }
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (!availableByPrefab.TryGetValue(prefab, out Queue<GameObject> available))
        {
            available = new Queue<GameObject>();
            availableByPrefab.Add(prefab, available);
        }

        GameObject instanceToSpawn = null;
        while (available.Count > 0 && instanceToSpawn == null)
            instanceToSpawn = available.Dequeue();

        if (instanceToSpawn == null)
        {
            // Keep inactive until pose + state reset are done so OnEnable never sees a stale transform.
            // 顺手清理死亡 key（跨场景后旧实例已销毁）：防 prefabByInstance 单调增长
            CleanDeadKeys();
            instanceToSpawn = Instantiate(prefab);
            instanceToSpawn.SetActive(false);
            prefabByInstance[instanceToSpawn] = prefab;
            GameManager.ApplyPerformanceOptimizations(instanceToSpawn);
        }

        // Detach from pool root before reset / pose so world coordinates are authoritative.
        instanceToSpawn.transform.SetParent(null, false);

        MonsterActor monster = instanceToSpawn.GetComponentInChildren<MonsterActor>(true);
        if (monster != null) monster.ResetForSpawn();

        // Authoritative world pose AFTER reset (ResetForSpawn may touch local pose on nested actors).
        // The actor owns its configured vertical placement; the caller only supplies X/Z.
        if (monster != null)
            position.y = monster.aliveY;
        instanceToSpawn.transform.SetPositionAndRotation(position, rotation);
        instanceToSpawn.SetActive(true);

        // CardManager unlocks existing abilities when a card is selected, but pooled
        // monsters may be instantiated or reused after that point. Apply the current
        // run build after activation so ability OnEnable has already stamped its stable
        // ability tags; this also covers Boss mode's Boss and seven reserve corpses.
        if (CardManager.Instance != null)
            CardManager.Instance.ApplyAllUnlocksTo(instanceToSpawn);

        return instanceToSpawn;
    }

    public void Return(MonsterActor monster)
    {
        if (monster == null) return;

        // A composite Boss prefab can contain legacy MonsterActor components on its
        // visual source parts. Resolve the pooled root before checking Boss state so a
        // child fade can never deactivate an undefeated Boss root.
        GameObject instanceToReturn = FindPooledRoot(monster.transform);
        BossSevenfoldActor rootBoss = instanceToReturn != null
            ? instanceToReturn.GetComponent<BossSevenfoldActor>()
            : null;
        if (rootBoss != null && !rootBoss.IsDefeated)
        {
            Debug.LogWarning($"[MonsterPool] 拒绝回收尚未死亡的 Boss '{monster.name}'。", monster);
            return;
        }

        if (monster is BossSevenfoldActor boss && !boss.IsDefeated)
        {
            Debug.LogWarning($"[MonsterPool] 拒绝回收尚未死亡的 Boss '{monster.name}'。", monster);
            return;
        }

        // 防附身回收（主界面幽灵 bug 根因①）：被附身怪的子物体锚点下挂着灵魂，
        // 直接回池会把灵魂连带带入 DDOL 场景，之后 Detach 时灵魂成为 DDOL 根跨场景存活。
        // 被附身怪不得回池：附身结束走正常死亡→Fade 流程再回收。
        if (monster.isPossessed)
        {
            Debug.LogWarning($"[MonsterPool] 拒绝回收被附身怪 '{monster.name}'（灵魂仍挂在其锚点下，回池会污染 DDOL 场景）。", monster);
            return;
        }

        if (!prefabByInstance.TryGetValue(instanceToReturn, out GameObject prefab))
        {
            Destroy(instanceToReturn);
            return;
        }

        if (!availableByPrefab.TryGetValue(prefab, out Queue<GameObject> available))
        {
            available = new Queue<GameObject>();
            availableByPrefab.Add(prefab, available);
        }

        monster.ResetForPool();
        // Pose is irrelevant while pooled; park under the pool root so inactive Update never runs at the old fight location.
        instanceToReturn.transform.SetParent(transform, false);
        instanceToReturn.SetActive(false);
        available.Enqueue(instanceToReturn);
    }

    /// <summary>
    /// 清空对象池：销毁所有池化的怪物实例并清空映射。
    /// 供 Restart / 结束对局调用，确保上一局回收的怪物被彻底销毁，不跨局复用。
    /// 活跃怪物（不在池中）由场景重载统一销毁。
    /// </summary>
    public void ClearAll()
    {
        foreach (var kv in availableByPrefab)
        {
            var queue = kv.Value;
            while (queue != null && queue.Count > 0)
            {
                var instance = queue.Dequeue();
                if (instance != null) Destroy(instance);
            }
        }
        availableByPrefab.Clear();
        prefabByInstance.Clear();
    }

    /// <summary>
    /// 反查实例对应的 prefab 资产（存档等场景：prefabId 应存真实资产名，而非实例名，
    /// 实例名可能是 "X(Clone)" 或场景重命名后的 "X(1)"）。
    /// </summary>
    public GameObject GetPrefabOf(GameObject instance)
    {
        if (instance == null) return null;
        prefabByInstance.TryGetValue(instance, out GameObject prefab);
        if (prefab == null)
        {
            // 容忍父级：怪根实例可能被包在其它结构下
            GameObject root = FindPooledRoot(instance.transform);
            if (root != null) prefabByInstance.TryGetValue(root, out prefab);
        }
        return prefab;
    }

    private GameObject FindPooledRoot(Transform transformToResolve)
    {
        Transform current = transformToResolve;
        while (current != null)
        {
            if (prefabByInstance.ContainsKey(current.gameObject)) return current.gameObject;
            current = current.parent;
        }
        return transformToResolve.root.gameObject;
    }

    /// <summary>清理 prefabByInstance 中已销毁的实例 key（Unity == 重载判 fake-null）。</summary>
    void CleanDeadKeys()
    {
        var dead = new List<GameObject>();
        foreach (var kv in prefabByInstance)
            if (kv.Key == null) dead.Add(kv.Key);
        for (int i = 0; i < dead.Count; i++)
            prefabByInstance.Remove(dead[i]);
        var deadPrefabs = new List<GameObject>();
        foreach (var kv in availableByPrefab)
            if (kv.Key == null) deadPrefabs.Add(kv.Key);
        for (int i = 0; i < deadPrefabs.Count; i++)
            availableByPrefab.Remove(deadPrefabs[i]);
    }
}
