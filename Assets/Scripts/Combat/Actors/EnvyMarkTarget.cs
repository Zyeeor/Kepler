using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Target-side Envy Mark: stores a portion of effective HP damage for Thunderstorm cashout.
/// Lives on the marked Enemy, never as a caster energy bar.
/// </summary>
[DisallowMultipleComponent]
public class EnvyMarkTarget : MonoBehaviour
{
    public const string MarkStateTag = "State.Combat.EnvyMark";
    public const string MarkEffectTag = "Effect.Combat.EnvyMark";

    private static readonly List<EnvyMarkTarget> ActiveMarks = new List<EnvyMarkTarget>();

    public Enemy Host { get; private set; }
    public Enemy Source { get; private set; }
    public float StoredDamage { get; private set; }
    public float StorageCap { get; private set; } = 100f;
    public float WriteRatio { get; private set; } = 0.2f;
    public bool IsActive => _active;
    public bool InGrace => _graceRemaining > 0f;

    private bool _active;
    private float _graceRemaining;
    private float _defaultGrace;
    private GameplayEffectDefinition _markEffect;

    public static IReadOnlyList<EnvyMarkTarget> AllActive => ActiveMarks;

    public static void ClearMarksFromSource(Enemy source)
    {
        for (int i = ActiveMarks.Count - 1; i >= 0; i--)
        {
            EnvyMarkTarget mark = ActiveMarks[i];
            if (mark == null || mark.Source != source) continue;
            mark.Clear();
        }
    }

    public static EnvyMarkTarget EnsureOn(Enemy target)
    {
        if (target == null) return null;
        EnvyMarkTarget mark = target.GetComponent<EnvyMarkTarget>();
        if (mark == null) mark = target.gameObject.AddComponent<EnvyMarkTarget>();
        mark.Host = target;
        return mark;
    }

    public static void NotifyDamageTaken(Enemy target, float amount)
    {
        if (target == null || amount <= 0f) return;
        EnvyMarkTarget mark = target.GetComponent<EnvyMarkTarget>();
        if (mark == null || !mark._active) return;
        mark.AddStoredDamage(amount * mark.WriteRatio);
    }

    private void Awake()
    {
        Host = GetComponent<Enemy>();
    }

    private void OnDisable()
    {
        Unregister();
    }

    private void OnDestroy()
    {
        Unregister();
    }

    private void Update()
    {
        if (!_active || _graceRemaining <= 0f) return;
        _graceRemaining -= Time.deltaTime;
        if (_graceRemaining <= 0f)
            Clear();
    }

    public void ApplyOrRefresh(
        Enemy source,
        float storageCap,
        float writeRatio,
        float graceDuration,
        GameplayEffectDefinition markEffect)
    {
        Source = source;
        StorageCap = Mathf.Max(1f, storageCap);
        WriteRatio = Mathf.Clamp01(writeRatio);
        _defaultGrace = Mathf.Max(0f, graceDuration);
        _markEffect = markEffect;
        _graceRemaining = 0f;
        _active = true;
        Register();

        if (Host != null && Host.Combat != null && _markEffect != null)
            Host.Combat.ApplyEffect(_markEffect, source != null ? source.Combat : Host.Combat, null, out _);
    }

    public void BeginGrace()
    {
        if (!_active) return;
        if (_defaultGrace <= 0f)
        {
            Clear();
            return;
        }

        _graceRemaining = _defaultGrace;
    }

    public void CancelGraceKeepMark()
    {
        _graceRemaining = 0f;
    }

    public float ConsumeStoredDamage()
    {
        float value = StoredDamage;
        StoredDamage = 0f;
        Clear();
        return value;
    }

    public void Clear()
    {
        StoredDamage = 0f;
        _graceRemaining = 0f;
        _active = false;
        if (Host != null && Host.Combat != null && _markEffect != null)
            Host.Combat.RemoveEffect(_markEffect);
        Unregister();
    }

    private void AddStoredDamage(float amount)
    {
        if (!_active || amount <= 0f) return;
        StoredDamage = Mathf.Min(StorageCap, StoredDamage + amount);
        _graceRemaining = 0f;
    }

    private void Register()
    {
        if (!ActiveMarks.Contains(this))
            ActiveMarks.Add(this);
    }

    private void Unregister()
    {
        ActiveMarks.Remove(this);
    }
}
