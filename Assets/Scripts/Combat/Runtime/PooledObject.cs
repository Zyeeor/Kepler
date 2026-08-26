using UnityEngine;

/// <summary>
/// Marks a GameObject as owned by <see cref="VfxPool"/>. Stores the source prefab and original local scale
/// so reuse never compounds transforms or loses the pool mapping.
/// </summary>
public class PooledObject : MonoBehaviour
{
    public GameObject SourcePrefab;
    public Vector3 OriginalLocalScale = Vector3.one;

    private ParticleSystem[] particleSystems;
    private bool particleSystemsCached;

    /// <summary>Returns the cached hierarchy particle systems, initializing the cache on first access.</summary>
    public ParticleSystem[] ParticleSystems
    {
        get
        {
            EnsureParticleSystemsCached();
            return particleSystems;
        }
    }

    /// <summary>Refreshes the cache for a runtime hierarchy that explicitly added particle systems.</summary>
    public void RefreshParticleSystems()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        particleSystemsCached = true;
    }

    private void EnsureParticleSystemsCached()
    {
        if (particleSystemsCached) return;
        RefreshParticleSystems();
    }
}
