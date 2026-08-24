using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reuses VFX / projectile prefab instances.
/// Pose is always applied while inactive, then activated — OnEnable / first Update never see the previous world position.
/// </summary>
public class VfxPool : MonoBehaviour
{
    private static VfxPool instance;

    private readonly Dictionary<GameObject, Queue<GameObject>> availableByPrefab =
        new Dictionary<GameObject, Queue<GameObject>>();
    private readonly Dictionary<GameObject, GameObject> prefabByInstance =
        new Dictionary<GameObject, GameObject>();
    /// <summary>Invalidates pending delayed releases when an instance is re-rented or released early.</summary>
    private readonly Dictionary<GameObject, int> releaseEpochByInstance =
        new Dictionary<GameObject, int>();
    private readonly HashSet<GameObject> deferredReleases = new HashSet<GameObject>();

    public static VfxPool Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindObjectOfType<VfxPool>();
            if (instance != null) return instance;

            GameObject poolRoot = new GameObject("VfxPool");
            DontDestroyOnLoad(poolRoot);
            instance = poolRoot.AddComponent<VfxPool>();
            return instance;
        }
    }

    /// <summary>
    /// Rent an instance. Transform is set before SetActive(true). Particles are cleared; caller should Play.
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;

        if (!availableByPrefab.TryGetValue(prefab, out Queue<GameObject> available))
        {
            available = new Queue<GameObject>();
            availableByPrefab.Add(prefab, available);
        }

        GameObject rented = null;
        while (available.Count > 0 && rented == null)
            rented = available.Dequeue();

        if (rented == null)
        {
            // Instantiate inactive so Awake/OnEnable run only after pose is applied below.
            rented = Instantiate(prefab);
            rented.SetActive(false);
            prefabByInstance[rented] = prefab;

            PooledObject marker = rented.GetComponent<PooledObject>();
            if (marker == null) marker = rented.AddComponent<PooledObject>();
            marker.SourcePrefab = prefab;
            marker.OriginalLocalScale = rented.transform.localScale;
        }

        BumpReleaseEpoch(rented);
        PrepareForSpawn(rented);
        if (parent != null)
            rented.transform.SetParent(parent, false);
        else
            rented.transform.SetParent(null, false);

        rented.transform.SetPositionAndRotation(position, rotation);
        rented.SetActive(true);
        return rented;
    }

    /// <summary>Return to pool, or Destroy if the instance was never pooled.</summary>
    public void Release(GameObject instance)
    {
        if (instance == null) return;

        // Effect cleanup can run from a child OnDisable while its owner root is
        // still being deactivated. Unity rejects SetParent during that transition;
        // defer exactly one frame, then perform the normal pooled release.
        if (IsUnderInactiveParent(instance.transform))
        {
            DeferReleaseUntilNextFrame(instance);
            return;
        }

        ReleaseImmediately(instance);
    }

    private void ReleaseImmediately(GameObject instance)
    {
        if (instance == null) return;

        if (!prefabByInstance.TryGetValue(instance, out GameObject prefab))
        {
            Destroy(instance);
            return;
        }

        if (!instance.activeSelf && IsInAvailableQueue(prefab, instance))
            return;

        BumpReleaseEpoch(instance);
        PrepareForRelease(instance);
        instance.SetActive(false);
        instance.transform.SetParent(transform, false);

        if (!availableByPrefab.TryGetValue(prefab, out Queue<GameObject> available))
        {
            available = new Queue<GameObject>();
            availableByPrefab.Add(prefab, available);
        }

        available.Enqueue(instance);
    }

    private bool IsUnderInactiveParent(Transform child)
    {
        return child != null && child.parent != null && !child.parent.gameObject.activeInHierarchy;
    }

    private void DeferReleaseUntilNextFrame(GameObject instance)
    {
        if (!deferredReleases.Add(instance)) return;
        int epoch = BumpReleaseEpoch(instance);
        StartCoroutine(ReleaseAfterOwnerDeactivation(instance, epoch));
    }

    private IEnumerator ReleaseAfterOwnerDeactivation(GameObject instance, int epoch)
    {
        yield return null;
        deferredReleases.Remove(instance);
        if (instance == null || !IsCurrentEpoch(instance, epoch)) yield break;
        ReleaseImmediately(instance);
    }

    /// <summary>Delayed release. Cancelled automatically if the instance is re-spawned or released early.</summary>
    public void Release(GameObject instance, float delay)
    {
        if (instance == null) return;
        if (delay <= 0f)
        {
            Release(instance);
            return;
        }

        int epoch = BumpReleaseEpoch(instance);
        StartCoroutine(ReleaseAfterDelay(instance, delay, epoch));
    }

    /// <summary>Pool-aware destroy: pooled → Release, otherwise → Destroy.</summary>
    public static void ReleaseOrDestroy(GameObject instance)
    {
        if (instance == null) return;
        Instance.Release(instance);
    }

    /// <summary>Pool-aware delayed destroy.</summary>
    public static void ReleaseOrDestroy(GameObject instance, float delay)
    {
        if (instance == null) return;
        Instance.Release(instance, delay);
    }

    private IEnumerator ReleaseAfterDelay(GameObject instance, float delay, int epoch)
    {
        float remaining = delay;
        while (remaining > 0f)
        {
            if (instance == null) yield break;
            if (!IsCurrentEpoch(instance, epoch)) yield break;
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (instance == null) yield break;
        if (!IsCurrentEpoch(instance, epoch)) yield break;
        Release(instance);
    }

    private int BumpReleaseEpoch(GameObject instance)
    {
        int next = 1;
        if (releaseEpochByInstance.TryGetValue(instance, out int current))
            next = current + 1;
        releaseEpochByInstance[instance] = next;
        return next;
    }

    private bool IsCurrentEpoch(GameObject instance, int epoch)
    {
        return releaseEpochByInstance.TryGetValue(instance, out int current) && current == epoch;
    }

    private void PrepareForSpawn(GameObject instance)
    {
        PooledObject marker = instance.GetComponent<PooledObject>();
        if (marker != null)
            instance.transform.localScale = marker.OriginalLocalScale;

        DestroyOnOwnerDeath tracker = instance.GetComponent<DestroyOnOwnerDeath>();
        if (tracker != null)
            tracker.owner = null;

        StopAndClearParticles(instance);
    }

    private void PrepareForRelease(GameObject instance)
    {
        StopAndClearParticles(instance);

        DestroyOnOwnerDeath tracker = instance.GetComponent<DestroyOnOwnerDeath>();
        if (tracker != null)
            tracker.owner = null;
    }

    private static void StopAndClearParticles(GameObject instance)
    {
        if (instance == null) return;
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
        }
    }

    private bool IsInAvailableQueue(GameObject prefab, GameObject instance)
    {
        if (!availableByPrefab.TryGetValue(prefab, out Queue<GameObject> available))
            return false;
        foreach (GameObject queued in available)
        {
            if (queued == instance) return true;
        }
        return false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
