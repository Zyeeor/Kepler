using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Warms the monster and ability-visual catalog during the card-selection pause. The
/// catalog is assembled from the active wave tables and scene spawn bindings, so no new
/// Resources/Addressables contract is required. One asset is instantiated per frame to
/// keep the preload itself from introducing a loading spike.
/// </summary>
public sealed class MonsterPreloadService : MonoBehaviour
{
    private static MonsterPreloadService instance;

    private readonly HashSet<GameObject> monsterPrefabs = new HashSet<GameObject>();
    private readonly HashSet<GameObject> vfxPrefabs = new HashSet<GameObject>();
    private Coroutine preloadRoutine;

    public static MonsterPreloadService EnsureInstance()
    {
        if (instance != null) return instance;

        instance = FindObjectOfType<MonsterPreloadService>(true);
        if (instance != null) return instance;

        GameObject root = new GameObject("MonsterPreloadService");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<MonsterPreloadService>();
        return instance;
    }

    /// <summary>Starts an idempotent, frame-sliced preload pass.</summary>
    public void BeginPreload()
    {
        if (preloadRoutine != null) return;

        CollectCatalog();
        if (monsterPrefabs.Count == 0 && vfxPrefabs.Count == 0)
        {
            Debug.LogWarning("[MonsterPreloadService] 未找到可预加载的怪物或技能 VFX 引用。");
            return;
        }

        preloadRoutine = StartCoroutine(PreloadRoutine());
    }

    /// <summary>Stops a pending preload when card selection ends or its scene is unloaded.</summary>
    public void CancelPreload()
    {
        if (preloadRoutine == null) return;
        StopCoroutine(preloadRoutine);
        preloadRoutine = null;
    }

    private IEnumerator PreloadRoutine()
    {
        int monsterCount = 0;
        int vfxCount = 0;

        foreach (GameObject prefab in monsterPrefabs)
        {
            if (prefab == null) continue;
            MonsterPool.Instance.Preload(prefab, GameManager.MonsterPreloadInstancesPerPrefab);
            monsterCount++;
            yield return null;
        }

        foreach (GameObject prefab in vfxPrefabs)
        {
            if (prefab == null) continue;
            VfxPool.Instance.Preload(prefab, GameManager.MonsterPreloadInstancesPerPrefab);
            vfxCount++;
            yield return null;
        }

        preloadRoutine = null;
        Debug.Log($"[MonsterPreloadService] 分帧预加载完成：怪物 {monsterCount} 个，VFX/投射物 {vfxCount} 个。");
    }

    private void CollectCatalog()
    {
        monsterPrefabs.Clear();
        vfxPrefabs.Clear();

        RunSpawnDirector director = RunSpawnDirector.Instance;
        if (director != null)
        {
            CollectMonsterPrefab(director.bossPrefab);
            CollectMonsterPrefabs(director.normalPrefabs);
        }

        EnemySpawner[] spawners = FindObjectsOfType<EnemySpawner>(true);
        for (int i = 0; i < spawners.Length; i++)
            CollectMonsterEntries(spawners[i] != null ? spawners[i].enemyPrefabs : null);

        ENGPOSS001SceneInstaller[] installers = FindObjectsOfType<ENGPOSS001SceneInstaller>(true);
        for (int i = 0; i < installers.Length; i++)
            if (installers[i] != null) CollectMonsterPrefab(installers[i].bossPrefab);

        WaveManager[] waveManagers = FindObjectsOfType<WaveManager>(true);
        for (int i = 0; i < waveManagers.Length; i++)
            CollectWavePrefabs(waveManagers[i] != null ? waveManagers[i].waves : null);
    }

    private void CollectMonsterPrefabs(List<GameObject> prefabs)
    {
        if (prefabs == null) return;
        for (int i = 0; i < prefabs.Count; i++) CollectMonsterPrefab(prefabs[i]);
    }

    private void CollectMonsterEntries(List<EnemyPrefabEntry> entries)
    {
        if (entries == null) return;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null) CollectMonsterPrefab(entries[i].prefab);
    }

    private void CollectWavePrefabs(List<WaveConfig> waves)
    {
        if (waves == null) return;
        for (int w = 0; w < waves.Count; w++)
        {
            WaveConfig wave = waves[w];
            if (wave == null || wave.weightedTable == null) continue;
            for (int e = 0; e < wave.weightedTable.Count; e++)
            {
                WaveDefEntry weighted = wave.weightedTable[e];
                MonsterWaveDef definition = weighted != null ? weighted.def : null;
                if (definition == null || definition.monsters == null) continue;
                for (int m = 0; m < definition.monsters.Count; m++)
                {
                    MonsterEntry entry = definition.monsters[m];
                    if (entry != null) CollectMonsterPrefab(entry.prefab);
                }
            }
        }
    }

    private void CollectMonsterPrefab(GameObject prefab)
    {
        if (prefab == null || prefab.GetComponentInChildren<MonsterActor>(true) == null)
            return;
        if (!monsterPrefabs.Add(prefab)) return;

        MonoBehaviour[] components = prefab.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < components.Length; i++)
        {
            MonoBehaviour component = components[i];
            if (component == null) continue;
            CollectSerializedReferences(component, prefab.transform);
        }
    }

    private void CollectSerializedReferences(MonoBehaviour component, Transform monsterRoot)
    {
        FieldInfo[] fields = component.GetType().GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (field.IsStatic || field.IsNotSerialized) continue;

            object value;
            try
            {
                value = field.GetValue(component);
            }
            catch (Exception)
            {
                continue;
            }

            if (field.FieldType == typeof(GameObject))
            {
                CollectReferencedObject(value as GameObject, monsterRoot);
                continue;
            }

            if (typeof(GameplayEffectDefinition).IsAssignableFrom(field.FieldType))
                CollectEffect(value as GameplayEffectDefinition, monsterRoot);
        }
    }

    private void CollectEffect(GameplayEffectDefinition effect, Transform monsterRoot)
    {
        if (effect == null) return;
        CollectReferencedObject(effect.activeVfxPrefab, monsterRoot);
        CollectReferencedObject(effect.hitVfxPrefab, monsterRoot);
    }

    private void CollectReferencedObject(GameObject referenced, Transform monsterRoot)
    {
        if (referenced == null) return;
        Transform referencedTransform = referenced.transform;
        if (referencedTransform == monsterRoot || referencedTransform.IsChildOf(monsterRoot)) return;

        if (referenced.GetComponentInChildren<MonsterActor>(true) != null)
            CollectMonsterPrefab(referenced);
        else
            vfxPrefabs.Add(referenced);
    }
}
