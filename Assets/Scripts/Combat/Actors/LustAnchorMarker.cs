using UnityEngine;

/// <summary>Marks a Lust Anchor marker object. Not an Enemy / Corpse / summon; ignored by Pull.</summary>
[DisallowMultipleComponent]
public class LustAnchorMarker : MonoBehaviour
{
    public LustBodyState ownerState;
    public float lifetime = 8f;
    public GameObject spawnVfx;
    public GameObject telegraphVfx;

    private float _expiresAt = -1f;

    public void Configure(LustBodyState owner, float life)
    {
        ownerState = owner;
        lifetime = Mathf.Max(0.1f, life);
        _expiresAt = Time.time + lifetime;
    }

    public void RefreshLifetime(float life)
    {
        lifetime = Mathf.Max(0.1f, life);
        _expiresAt = Time.time + lifetime;
    }

    private void Update()
    {
        if (_expiresAt < 0f) return;
        if (Time.time < _expiresAt) return;
        if (ownerState != null) ownerState.NotifyAnchorExpired(this);
        else Destroy(gameObject);
    }
}
