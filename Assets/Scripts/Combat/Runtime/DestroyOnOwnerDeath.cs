using UnityEngine;

/// <summary>
/// Return this GameObject to <see cref="VfxPool"/> (or Destroy if unpooled) when the owner is gone or pooled away.
/// Attach to any VFX spawned by an ability.
/// </summary>
public class DestroyOnOwnerDeath : MonoBehaviour
{
    public GameObject owner;

    void Update()
    {
        // Pooled monsters SetActive(false) instead of Destroy — treat inactive owner as released too.
        if (owner == null || !owner.activeInHierarchy)
            VfxPool.ReleaseOrDestroy(gameObject);
    }
}
