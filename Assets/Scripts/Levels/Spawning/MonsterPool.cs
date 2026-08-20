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

    /// <summary>Playable ground plane Y. CapsuleCollider bottoms snap here on spawn.</summary>
    public const float GroundY = 0f;

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
        }

        // Detach from pool root before reset / pose so world coordinates are authoritative.
        instanceToSpawn.transform.SetParent(null, false);

        MonsterActor monster = instanceToSpawn.GetComponentInChildren<MonsterActor>(true);
        if (monster != null) monster.ResetForSpawn();

        // Authoritative world pose AFTER reset (ResetForSpawn may touch local pose on nested actors).
        instanceToSpawn.transform.SetPositionAndRotation(position, rotation);
        instanceToSpawn.SetActive(true);

        // After activation (and any OnEnable local tweaks), snap CapsuleCollider bottom to ground Y.
        SnapCapsuleBottomToGround(instanceToSpawn, GroundY);
        return instanceToSpawn;
    }

    /// <summary>
    /// Adjust root Y so the primary CapsuleCollider's world-space bottom sits on <paramref name="groundY"/>.
    /// Top-down planar combat: only Y is corrected.
    /// </summary>
    public static void SnapCapsuleBottomToGround(GameObject root, float groundY = GroundY)
    {
        if (root == null) return;

        CapsuleCollider capsule = null;
        MonsterActor monster = root.GetComponentInChildren<MonsterActor>(true);
        if (monster != null)
            capsule = monster.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = root.GetComponentInChildren<CapsuleCollider>(true);
        if (capsule == null) return;

        Transform t = capsule.transform;
        Vector3 lossy = t.lossyScale;
        float heightScale;
        float radiusScale;
        switch (capsule.direction)
        {
            case 0: // X
                heightScale = Mathf.Abs(lossy.x);
                radiusScale = Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
                break;
            case 2: // Z
                heightScale = Mathf.Abs(lossy.z);
                radiusScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));
                break;
            default: // Y
                heightScale = Mathf.Abs(lossy.y);
                radiusScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
                break;
        }

        float scaledHeight = capsule.height * heightScale;
        float scaledRadius = capsule.radius * radiusScale;
        float halfExtent = Mathf.Max(scaledHeight * 0.5f, scaledRadius);

        Vector3 worldCenter = t.TransformPoint(capsule.center);
        Vector3 axis = capsule.direction == 0 ? t.right : (capsule.direction == 1 ? t.up : t.forward);
        if (axis.sqrMagnitude < 0.0001f) axis = Vector3.up;
        axis.Normalize();

        Vector3 tipA = worldCenter + axis * halfExtent;
        Vector3 tipB = worldCenter - axis * halfExtent;
        float bottomY = Mathf.Min(tipA.y, tipB.y);

        float deltaY = groundY - bottomY;
        if (Mathf.Abs(deltaY) < 0.0001f) return;
        root.transform.position += new Vector3(0f, deltaY, 0f);
    }

    public void Return(MonsterActor monster)
    {
        if (monster == null) return;

        // 防附身回收（主界面幽灵 bug 根因①）：被附身怪的子物体锚点下挂着灵魂，
        // 直接回池会把灵魂连带带入 DDOL 场景，之后 Detach 时灵魂成为 DDOL 根跨场景存活。
        // 被附身怪不得回池：附身结束走正常死亡→Fade 流程再回收。
        if (monster.isPossessed)
        {
            Debug.LogWarning($"[MonsterPool] 拒绝回收被附身怪 '{monster.name}'（灵魂仍挂在其锚点下，回池会污染 DDOL 场景）。", monster);
            return;
        }

        GameObject instanceToReturn = FindPooledRoot(monster.transform);
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
