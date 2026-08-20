using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Body-bound Lust combat state: single main Anchor, per-target Link registry,
/// LU-TG01 body buffs, and possession / fatal clears.
/// </summary>
[DisallowMultipleComponent]
public class LustBodyState : MonoBehaviour
{
    public const string LinkStateTag = "State.Combat.Lust.Linked";
    public const string IsolationStateTag = "State.Combat.Lust.PullDamageIsolation";
    public const string PulledSourceTag = "State.Combat.Lust.PulledSource";

    public enum BodyWeightClass
    {
        Light = 0,
        Medium = 1,
        Heavy = 2
    }

    [Header("Defaults (TUNABLE)")]
    public float defaultAnchorLifetime = 8f;
    public float defaultLinkDuration = 6f;
    public BodyWeightClass bodyWeight = BodyWeightClass.Medium;

    [Header("LU-TG01")]
    public float tg01MoveSpeedMult = 1.15f;
    public float tg01CooldownMult = 0.70f;

    [Header("Effects")]
    public GameplayEffectDefinition linkEffect;
    public GameObject anchorPrefab;
    public GameObject anchorSpawnVfx;
    public GameObject anchorSwapVfx;

    public LustAnchorMarker ActiveAnchor { get; private set; }
    public bool HasValidAnchor => ActiveAnchor != null;

    private Enemy _owner;
    private bool _wasPossessed;
    private readonly Dictionary<int, LinkRecord> _links = new Dictionary<int, LinkRecord>();
    private float _baseMoveSpeed = -1f;
    private float _baseAttackSpeed = -1f;
    private bool _tg01Applied;

    private class LinkRecord
    {
        public Enemy target;
        public float expiresAt;
    }

    private void Awake()
    {
        _owner = GetComponent<Enemy>();
        _wasPossessed = _owner != null && _owner.isPossessed;
        CaptureBaseStats();
        ClearBodyBoundState();
    }

    private void LateUpdate()
    {
        PruneExpiredLinks();
        RefreshTg01();

        if (_owner == null) return;
        if (_wasPossessed == _owner.isPossessed) return;
        _wasPossessed = _owner.isPossessed;
        ClearBodyBoundState();
    }

    private void OnDisable()
    {
        ClearBodyBoundState();
        RevertTg01();
    }

    public float GetCooldownMultiplier()
    {
        return IsTg01Unlocked() ? tg01CooldownMult : 1f;
    }

    public float GetPullDistanceMultiplier(MonsterActor target)
    {
        BodyWeightClass weight = ResolveWeight(target);
        switch (weight)
        {
            case BodyWeightClass.Light: return 1f;
            case BodyWeightClass.Heavy: return 0.30f;
            default: return 0.60f;
        }
    }

    public static BodyWeightClass ResolveWeight(MonsterActor target)
    {
        if (target == null) return BodyWeightClass.Medium;
        LustBodyState lust = target.GetComponent<LustBodyState>();
        if (lust != null) return lust.bodyWeight;
        // Possessed player body defaults Light; AI monsters default Medium (TUNABLE).
        return target.isPossessed ? BodyWeightClass.Light : BodyWeightClass.Medium;
    }

    public LustAnchorMarker PlaceOrReplaceAnchor(Vector3 worldPosition, float lifetime = -1f)
    {
        float life = lifetime > 0f ? lifetime : defaultAnchorLifetime;
        DestroyActiveAnchor();

        GameObject go;
        if (anchorPrefab != null)
            go = Object.Instantiate(anchorPrefab, worldPosition, Quaternion.identity);
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "PF_MON_LUST_ANCHOR";
            go.transform.position = worldPosition;
            go.transform.localScale = Vector3.one * 0.55f;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null && rend.material != null)
            {
                rend.material.color = new Color(1f, 0.35f, 0.75f, 0.85f);
            }
        }

        LustAnchorMarker marker = go.GetComponent<LustAnchorMarker>();
        if (marker == null) marker = go.AddComponent<LustAnchorMarker>();
        marker.Configure(this, life);
        ActiveAnchor = marker;

        if (anchorSpawnVfx != null)
            Object.Instantiate(anchorSpawnVfx, worldPosition, Quaternion.identity);

        return marker;
    }

    public void DestroyActiveAnchor()
    {
        if (ActiveAnchor == null) return;
        LustAnchorMarker marker = ActiveAnchor;
        ActiveAnchor = null;
        if (marker != null) Object.Destroy(marker.gameObject);
    }

    public void NotifyAnchorExpired(LustAnchorMarker marker)
    {
        if (marker == null) return;
        if (ActiveAnchor == marker) ActiveAnchor = null;
        Object.Destroy(marker.gameObject);
    }

    public void WriteOrRefreshLink(Enemy target, float duration = -1f)
    {
        if (target == null || target.isDowned) return;
        float life = duration > 0f ? duration : defaultLinkDuration;
        int id = target.GetInstanceID();
        if (_links.TryGetValue(id, out LinkRecord existing) && existing != null)
        {
            existing.target = target;
            existing.expiresAt = Time.time + life;
        }
        else
        {
            _links[id] = new LinkRecord
            {
                target = target,
                expiresAt = Time.time + life
            };
        }

        if (linkEffect != null && target.Combat != null)
            target.Combat.ApplyEffect(linkEffect, _owner != null ? _owner.Combat : target.Combat, null, out _);
    }

    public bool HasValidLink(Enemy target)
    {
        if (target == null) return false;
        if (!_links.TryGetValue(target.GetInstanceID(), out LinkRecord record) || record == null) return false;
        if (record.target == null || record.target.isDowned || Time.time > record.expiresAt)
        {
            ClearLink(target);
            return false;
        }
        return true;
    }

    public List<Enemy> GetValidLinkedTargets()
    {
        PruneExpiredLinks();
        List<Enemy> result = new List<Enemy>();
        foreach (var pair in _links)
        {
            LinkRecord record = pair.Value;
            if (record == null || record.target == null || record.target.isDowned) continue;
            if (Time.time > record.expiresAt) continue;
            result.Add(record.target);
        }
        return result;
    }

    public void ConsumeLinks(IEnumerable<Enemy> targets)
    {
        if (targets == null) return;
        foreach (Enemy target in targets)
            ClearLink(target);
    }

    public void ClearLink(Enemy target)
    {
        if (target == null) return;
        int id = target.GetInstanceID();
        if (_links.ContainsKey(id)) _links.Remove(id);
        if (linkEffect != null && target.Combat != null)
            target.Combat.RemoveEffect(linkEffect);
    }

    public void ClearAllLinks()
    {
        List<Enemy> snapshot = new List<Enemy>();
        foreach (var pair in _links)
            if (pair.Value != null && pair.Value.target != null)
                snapshot.Add(pair.Value.target);
        _links.Clear();
        for (int i = 0; i < snapshot.Count; i++)
        {
            Enemy target = snapshot[i];
            if (target != null && linkEffect != null && target.Combat != null)
                target.Combat.RemoveEffect(linkEffect);
        }
    }

    public void ClearBodyBoundState()
    {
        DestroyActiveAnchor();
        ClearAllLinks();
        LustPullDamageGate.Clear();
    }

    private void PruneExpiredLinks()
    {
        if (_links.Count == 0) return;
        List<int> expired = null;
        foreach (var pair in _links)
        {
            LinkRecord record = pair.Value;
            if (record == null || record.target == null || record.target.isDowned || Time.time > record.expiresAt)
            {
                if (expired == null) expired = new List<int>();
                expired.Add(pair.Key);
                if (record != null && record.target != null && linkEffect != null && record.target.Combat != null)
                    record.target.Combat.RemoveEffect(linkEffect);
            }
        }

        if (expired == null) return;
        for (int i = 0; i < expired.Count; i++)
            _links.Remove(expired[i]);
    }

    private void CaptureBaseStats()
    {
        if (_owner == null) return;
        if (_baseMoveSpeed < 0f) _baseMoveSpeed = _owner.moveSpeed > 0f ? _owner.moveSpeed : 6f;
        if (_baseAttackSpeed < 0f) _baseAttackSpeed = _owner.attackSpeed > 0f ? _owner.attackSpeed : 1f;
    }

    private bool IsTg01Unlocked()
    {
        // 精英怪：只认自身历史 BD 快照中的 LU-TG01，不看当前 Run 全局解锁（Canonical §23）
        var carrier = EliteBuildCarrier.Get(this);
        if (carrier != null) return carrier.HasCard("LU-TG01");
        return CardManager.Instance != null && CardManager.Instance.IsEffectUnlocked("LU-TG01");
    }

    private void RefreshTg01()
    {
        if (_owner == null) return;
        CaptureBaseStats();
        bool want = IsTg01Unlocked();
        if (want == _tg01Applied) return;
        if (want)
        {
            _owner.moveSpeed = _baseMoveSpeed * tg01MoveSpeedMult;
            // EffectiveCooldown = cooldown / attackSpeed; attackSpeed = 1/0.7 ≈ CD×0.70.
            _owner.attackSpeed = _baseAttackSpeed / Mathf.Max(0.01f, tg01CooldownMult);
            _tg01Applied = true;
        }
        else
        {
            RevertTg01();
        }
    }

    private void RevertTg01()
    {
        if (_owner == null || !_tg01Applied) return;
        _owner.moveSpeed = _baseMoveSpeed;
        _owner.attackSpeed = _baseAttackSpeed;
        _tg01Applied = false;
    }
}
