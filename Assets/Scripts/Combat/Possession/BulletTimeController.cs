using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single configuration and runtime entry point for Bullet Time.
/// Player-origin output remains on unscaled time; non-player output follows Time.timeScale.
/// </summary>
[DisallowMultipleComponent]
public sealed class BulletTimeController : MonoBehaviour
{
    public static BulletTimeController Instance { get; private set; }

    [Header("Bullet Time")]
    [Tooltip("子弹时间总开关：关闭后整个游戏不应用子弹时间（不触发、不消耗充能）。")]
    public bool activate = true;
    [Min(0.01f)] public float duration = 2f;
    [Range(0.05f, 1f)] public float timeScale = 0.2f;

    [Header("Bullet Time Charges")]
    [Tooltip("每具 Body 默认拥有的 Bullet Time Charge 次数。成功附身新 Body 刷新；E 使用 -1；0 时同 Body 不可再使用；换 Body 后重新刷新。Boss Reserve Body 同样刷新。")]
    [Min(1)] public int bulletTimeChargesPerBody = 1;

    [Header("Damage Immunity Effect")]
    [Tooltip("Optional direct Effect asset. When empty, the Effect Tag is resolved from the Gameplay Tag Catalog/CardManager.")]
    public GameplayEffectDefinition damageImmunityEffect;
    public GameplayTagCatalog effectCatalog;
    [Tooltip("Fallback lookup tag for the temporary immunity Effect.")]
    public string damageImmunityEffectTag = "Effect.Defense.DamageImmune";
    [Min(0f)] public float damageImmunityDuration = 0.5f;

    [Header("Post Process")]
    [Tooltip("Panda post-process effect name or material name activated while Bullet Time is active.")]
    public string postProcessEffectName = "RadialBlur";
    [Tooltip("Optional. Auto-found in the current scene when empty.")]
    public PandaPostProcessSwitcher postProcessSwitcher;
    [Tooltip("Override the selected Panda effect's scalar values for the Bullet Time duration.")]
    public bool overridePostProcessParams;
    [Range(0f, 1f)] public float postStepFactor = 0.5f;
    [Range(0f, 1f)] public float postMainAlpha = 1f;
    [Range(0f, 1f)] public float postBlurFactor = 0.47f;
    [Range(0f, 4f)] public float postLineUvScale = 2.24f;
    [Range(0f, 1.5f)] public float postChromatic = 0.18f;
    [Range(0f, 1f)] public float postFrequency;
    [Range(0f, 1f)] public float postAmplitude;
    [Range(1f, 3f)] public float postVignettePower = 1f;
    [Range(0f, 3f)] public float postVignetteScale;

    private Coroutine bulletTimeRoutine;
    private bool isActive;
    private int savedPostProcessIndex = -2;
    private PandaPostProcess activePostProcess;
    private bool hasPostProcessSnapshot;
    private PostProcessSnapshot postProcessSnapshot;
    private GameplayEffectDefinition runtimeFallbackEffect;

    private struct PostProcessSnapshot
    {
        public float stepFactor;
        public float mainAlpha;
        public float blurFactor;
        public float lineUvScale;
        public float chromatic;
        public float frequency;
        public float amplitude;
        public float vignettePower;
        public float vignetteScale;
    }

    public bool IsActive => isActive;

    public static float ConfiguredTimeScale
    {
        get { return Instance != null ? Mathf.Clamp(Instance.timeScale, 0.05f, 1f) : 0.2f; }
    }

    /// <summary>子弹时间总开关：Instance 未创建时视为激活（默认开启）。</summary>
    public static bool ConfiguredActive
    {
        get { return Instance == null || Instance.activate; }
    }

    public static int ConfiguredChargesPerBody
    {
        get { return Instance != null ? Mathf.Max(1, Instance.bulletTimeChargesPerBody) : 1; }
    }

    public static BulletTimeController EnsureInstance()
    {
        if (Instance != null) return Instance;

        BulletTimeController existing = FindFirstObjectByType<BulletTimeController>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("BulletTimeController");
        DontDestroyOnLoad(go);
        return go.AddComponent<BulletTimeController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (bulletTimeRoutine != null)
            StopCoroutine(bulletTimeRoutine);

        RestorePostProcess();
        if (isActive)
            TimeScaleManager.Pop(TimeDomain.BulletTime);

        if (runtimeFallbackEffect != null)
            Destroy(runtimeFallbackEffect);

        if (Instance == this) Instance = null;
    }

    public void Trigger(MonsterActor currentBody)
    {
        if (currentBody == null) return;
        if (!activate) return; // 总开关关闭，不触发子弹时间

        if (isActive || bulletTimeRoutine != null)
            Stop(GameManager.GameState.Possessed);

        bulletTimeRoutine = StartCoroutine(BulletTimeRoutine(currentBody));
    }

    public void Stop(GameManager.GameState restoreState)
    {
        if (bulletTimeRoutine != null)
        {
            StopCoroutine(bulletTimeRoutine);
            bulletTimeRoutine = null;
        }

        bool wasActive = isActive;
        isActive = false;
        RestorePostProcess();

        if (!wasActive)
        {
            if (TimeScaleManager.IsDomainActive(TimeDomain.BulletTime))
                TimeScaleManager.Pop(TimeDomain.BulletTime);
            return;
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.currentState == GameManager.GameState.BulletTime &&
            restoreState != GameManager.GameState.GameOver)
        {
            GameManager.Instance.SwitchState(restoreState);
        }
        else
        {
            TimeScaleManager.Pop(TimeDomain.BulletTime);
        }
    }

    private IEnumerator BulletTimeRoutine(MonsterActor currentBody)
    {
        isActive = true;
        if (GameManager.Instance != null)
            GameManager.Instance.SwitchState(GameManager.GameState.BulletTime);
        else
            TimeScaleManager.Push(TimeDomain.BulletTime, ConfiguredTimeScale);

        ApplyDamageImmunity(currentBody);
        ActivatePostProcess();

        float effectiveDuration = duration;
        if (PossessionImprintManager.Instance != null)
            effectiveDuration = PossessionImprintManager.Instance.GetBulletTimeDuration(duration);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, effectiveDuration));

        bulletTimeRoutine = null;
        if (!isActive) yield break;

        isActive = false;
        RestorePostProcess();
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.BulletTime)
            GameManager.Instance.SwitchState(GameManager.GameState.Possessed);
        else
            TimeScaleManager.Pop(TimeDomain.BulletTime);
    }

    /// <summary>
    /// Applies temporary damage immunity for a configurable duration. Used by the post-possess
    /// protection (Pass v1 §7.5) — independent of Bullet Time: no slow motion, no BT charge cost.
    /// </summary>
    public void ApplyDamageImmunityForDuration(MonsterActor target, float duration)
    {
        if (target == null || target.Combat == null || duration <= 0f)
            return;
        if (target.Combat.Tags.HasTag("State.Defense.DamageImmune"))
            return;

        GameplayEffectDefinition definition = ResolveDamageImmunityEffect();
        if (definition == null) return;

        if (target.Combat.TryGetEffectStacks(definition, out _))
            return;

        if (!target.Combat.ApplyEffect(definition, null, null, out string reason, duration, -1))
        {
            Debug.LogWarning($"[BulletTime] Failed to apply damage immunity Effect: {reason}", this);
        }
    }

    private void ApplyDamageImmunity(MonsterActor currentBody)
    {
        ApplyDamageImmunityForDuration(currentBody, damageImmunityDuration);
    }

    private GameplayEffectDefinition ResolveDamageImmunityEffect()
    {
        if (damageImmunityEffect != null) return damageImmunityEffect;

        if (effectCatalog != null && effectCatalog.TryGetEffect(damageImmunityEffectTag, out GameplayEffectDefinition fromCatalog))
            return fromCatalog;

        CardManager manager = CardManager.Instance;
        if (manager == null) manager = FindFirstObjectByType<CardManager>();
        if (manager != null && manager.TryGetGameplayEffect(damageImmunityEffectTag, out GameplayEffectDefinition fromCardManager))
            return fromCardManager;

        if (runtimeFallbackEffect == null)
        {
            runtimeFallbackEffect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            runtimeFallbackEffect.effectName = "Bullet Time Damage Immunity";
            runtimeFallbackEffect.effectTag = damageImmunityEffectTag;
            runtimeFallbackEffect.grantedTags = new List<string> { "State.Defense.DamageImmune" };
        }

        return runtimeFallbackEffect;
    }

    private void ActivatePostProcess()
    {
        if (string.IsNullOrWhiteSpace(postProcessEffectName)) return;
        if (postProcessSwitcher == null)
            postProcessSwitcher = FindFirstObjectByType<PandaPostProcessSwitcher>();
        if (postProcessSwitcher == null) return;

        postProcessSwitcher.RefreshEffects();
        IReadOnlyList<PandaPostProcess> effects = postProcessSwitcher.Effects;
        int index = -1;
        for (int i = 0; i < effects.Count; i++)
        {
            PandaPostProcess candidate = effects[i];
            if (candidate == null) continue;
            if (string.Equals(candidate.name, postProcessEffectName, System.StringComparison.OrdinalIgnoreCase) ||
                (candidate.PostProcessMat != null &&
                 string.Equals(candidate.PostProcessMat.name, postProcessEffectName, System.StringComparison.OrdinalIgnoreCase)))
            {
                index = i;
                activePostProcess = candidate;
                break;
            }
        }

        if (index < 0)
        {
            Debug.LogWarning($"[BulletTime] Post-process effect '{postProcessEffectName}' was not found.", this);
            return;
        }

        savedPostProcessIndex = postProcessSwitcher.ActiveEffectIndex;
        if (overridePostProcessParams && activePostProcess != null)
        {
            postProcessSnapshot = new PostProcessSnapshot
            {
                stepFactor = activePostProcess.StepFactor,
                mainAlpha = activePostProcess.MainAlpha,
                blurFactor = activePostProcess.BlurFactor,
                lineUvScale = activePostProcess.LineUVScale,
                chromatic = activePostProcess.Chromatic,
                frequency = activePostProcess.Frequency,
                amplitude = activePostProcess.Amplitude,
                vignettePower = activePostProcess.VignettePower,
                vignetteScale = activePostProcess.VignetteScale
            };
            hasPostProcessSnapshot = true;
            activePostProcess.StepFactor = postStepFactor;
            activePostProcess.MainAlpha = postMainAlpha;
            activePostProcess.BlurFactor = postBlurFactor;
            activePostProcess.LineUVScale = postLineUvScale;
            activePostProcess.Chromatic = postChromatic;
            activePostProcess.Frequency = postFrequency;
            activePostProcess.Amplitude = postAmplitude;
            activePostProcess.VignettePower = postVignettePower;
            activePostProcess.VignetteScale = postVignetteScale;
        }

        postProcessSwitcher.SetActiveEffect(index);
    }

    private void RestorePostProcess()
    {
        if (hasPostProcessSnapshot && activePostProcess != null)
        {
            activePostProcess.StepFactor = postProcessSnapshot.stepFactor;
            activePostProcess.MainAlpha = postProcessSnapshot.mainAlpha;
            activePostProcess.BlurFactor = postProcessSnapshot.blurFactor;
            activePostProcess.LineUVScale = postProcessSnapshot.lineUvScale;
            activePostProcess.Chromatic = postProcessSnapshot.chromatic;
            activePostProcess.Frequency = postProcessSnapshot.frequency;
            activePostProcess.Amplitude = postProcessSnapshot.amplitude;
            activePostProcess.VignettePower = postProcessSnapshot.vignettePower;
            activePostProcess.VignetteScale = postProcessSnapshot.vignetteScale;
        }

        hasPostProcessSnapshot = false;
        activePostProcess = null;
        if (postProcessSwitcher != null && savedPostProcessIndex >= -1)
            postProcessSwitcher.SetActiveEffect(savedPostProcessIndex);
        savedPostProcessIndex = -2;
    }

    /// <summary>Marks pooled VFX so VfxPool can keep player-origin output on unscaled time.</summary>
    public static void MarkVfxOrigin(GameObject instance, bool playerOrigin)
    {
        if (instance == null) return;
        BulletTimeVfxPlayback marker = instance.GetComponent<BulletTimeVfxPlayback>();
        if (marker == null) marker = instance.AddComponent<BulletTimeVfxPlayback>();
        marker.SetPlayerOrigin(playerOrigin);
    }
}

/// <summary>Runtime origin metadata for pooled VFX; reset by VfxPool before each spawn.</summary>
[DisallowMultipleComponent]
public sealed class BulletTimeVfxPlayback : MonoBehaviour
{
    public bool IsPlayerOrigin { get; private set; }

    public void SetPlayerOrigin(bool playerOrigin)
    {
        IsPlayerOrigin = playerOrigin;
    }
}
