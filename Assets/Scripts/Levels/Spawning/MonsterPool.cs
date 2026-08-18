using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reuses spawned monster roots. MonsterActor owns its state reset before an instance is reused.
/// </summary>
public class MonsterPool : MonoBehaviour
{
    private static MonsterPool instance;

    private readonly Dictionary<GameObject, Queue<GameObject>> availableByPrefab = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly Dictionary<GameObject, GameObject> prefabByInstance = new Dictionary<GameObject, GameObject>();

    public static MonsterPool Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindObjectOfType<MonsterPool>();
            if (instance != null) return instance;

            GameObject poolRoot = new GameObject("MonsterPool");
            instance = poolRoot.AddComponent<MonsterPool>();
            return instance;
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
            instanceToSpawn = Instantiate(prefab, position, rotation);
            prefabByInstance[instanceToSpawn] = prefab;
        }
        else
        {
            instanceToSpawn.transform.SetPositionAndRotation(position, rotation);
            instanceToSpawn.SetActive(true);
        }

        MonsterActor monster = instanceToSpawn.GetComponentInChildren<MonsterActor>(true);
        if (monster != null) monster.ResetForSpawn();
        return instanceToSpawn;
    }

    public void Return(MonsterActor monster)
    {
        if (monster == null) return;

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
}
