using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central combat presentation router: post-process, screen shake, and hit-stop.
/// Abilities send <see cref="HitFeedbackParams"/> on hit; this manager owns runtime playback.
/// Post-process currently drives PandaPostProcessSwitcher; URP Volume support is reserved.
/// </summary>
public class CombatEffectManager : MonoBehaviour
{
    public static CombatEffectManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Optional. Auto-found in scene if empty.")]
    [SerializeField] private PandaPostProcessSwitcher postProcessSwitcher;
    // Reserved for future URP Global Volume driver (option 3).
    // [SerializeField] private Volume globalVolume;

    [Header("Defaults (used when ability params are 0)")]
    [SerializeField] private float defaultShakeForce = 1f;
    [SerializeField] private float defaultHitStopDuration = 0.07f;
    [SerializeField] private float defaultPostProcessDuration = 0.15f;

    private Coroutine _hitStopRoutine;
    private Coroutine _postProcessRoutine;
    private readonly List<AnimatorSpeedSample> _animatorSamples = new List<AnimatorSpeedSample>(16);
    private int _savedPostIndex = -1;
    private bool _hasPostSnapshot;
    private PostProcessSnapshot _postSnapshot;

    private struct AnimatorSpeedSample
    {
        public Animator Animator;
        public float Speed;
    }

    private struct PostProcessSnapshot
    {
        public PandaPostProcess Effect;
        public float StepFactor;
        public float MainAlpha;
        public float BlurFactor;
        public float LineUVScale;
        public float Chromatic;
        public float Frequency;
        public float Amplitude;
        public float VignettePower;
        public float VignetteScale;
    }

    void Awake()
    {
        Instance = this;
        if (postProcessSwitcher == null)
            postProcessSwitcher = FindFirstObjectByType<PandaPostProcessSwitcher>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        RestoreAnimators();
        RestorePostProcess();
    }

    /// <summary>
    /// Play hit feedback. Safe to call when Instance is null (falls back to CameraDirector).
    /// </summary>
    public static void PlayHitFeedback(HitFeedbackParams parameters, Transform attacker = null, Transform victim = null)
    {
        if (parameters == null || !parameters.HasAnyEnabled)
            return;

        if (Instance != null)
        {
            Instance.Play(parameters, attacker, victim);
            return;
        }

        // Scene has no manager yet — keep shake / legacy timeScale hit-stop working.
        if (CameraDirector.Instance == null)
            return;

        if (parameters.shakeOnHit)
        {
            float force = parameters.shakeForce > 0f ? parameters.shakeForce : 1f;
            if (parameters.shakeDuration > 0f)
                CameraDirector.Instance.Shake(force, parameters.shakeDuration);
            else
                CameraDirector.Instance.Shake(force);
        }

        if (parameters.hitStopOnHit)
        {
            float duration = parameters.hitStopDuration > 0f ? parameters.hitStopDuration : 0.07f;
            CameraDirector.Instance.HitStop(duration, parameters.hitStopScale);
        }
    }

    public void Play(HitFeedbackParams parameters, Transform attacker = null, Transform victim = null)
    {
        if (parameters == null || !parameters.HasAnyEnabled)
            return;

        if (parameters.shakeOnHit)
            PlayShake(parameters);

        if (parameters.hitStopOnHit)
            PlayHitStop(parameters, attacker, victim);

        if (parameters.postProcessOnHit)
            PlayPostProcess(parameters);
    }

    private void PlayShake(HitFeedbackParams parameters)
    {
        if (CameraDirector.Instance == null)
            return;

        float force = parameters.shakeForce > 0f ? parameters.shakeForce : defaultShakeForce;
        if (parameters.shakeDuration > 0f)
            CameraDirector.Instance.Shake(force, parameters.shakeDuration);
        else
            CameraDirector.Instance.Shake(force);
    }

    private void PlayHitStop(HitFeedbackParams parameters, Transform attacker, Transform victim)
    {
        float duration = parameters.hitStopDuration > 0f ? parameters.hitStopDuration : defaultHitStopDuration;
        if (duration <= 0f)
            return;

        if (_hitStopRoutine != null)
        {
            StopCoroutine(_hitStopRoutine);
            RestoreAnimators();
            _hitStopRoutine = null;
        }

        _hitStopRoutine = StartCoroutine(HitStopRoutine(duration, parameters.hitStopScale, parameters.useGlobalTimeScale, attacker, victim));
    }

    private IEnumerator HitStopRoutine(float duration, float scale, bool useGlobalTimeScale, Transform attacker, Transform victim)
    {
        CaptureAnimators(attacker, victim);
        for (int i = 0; i < _animatorSamples.Count; i++)
        {
            Animator anim = _animatorSamples[i].Animator;
            if (anim != null)
                anim.speed = scale;
        }

        if (useGlobalTimeScale && CameraDirector.Instance != null)
            CameraDirector.Instance.HitStop(duration, scale);

        yield return new WaitForSecondsRealtime(duration);

        RestoreAnimators();
        _hitStopRoutine = null;
    }

    private void CaptureAnimators(Transform attacker, Transform victim)
    {
        _animatorSamples.Clear();
        AddAnimatorsFrom(attacker);
        AddAnimatorsFrom(victim);

        if (_animatorSamples.Count > 0)
            return;

        // Fallback: slow every active animator so the freeze is still visible.
        Animator[] all = FindObjectsByType<Animator>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            TryAddAnimator(all[i]);
    }

    private void AddAnimatorsFrom(Transform root)
    {
        if (root == null)
            return;

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
            TryAddAnimator(animators[i]);
    }

    private void TryAddAnimator(Animator animator)
    {
        if (animator == null || !animator.isActiveAndEnabled)
            return;

        for (int i = 0; i < _animatorSamples.Count; i++)
        {
            if (_animatorSamples[i].Animator == animator)
                return;
        }

        _animatorSamples.Add(new AnimatorSpeedSample
        {
            Animator = animator,
            Speed = animator.speed
        });
    }

    private void RestoreAnimators()
    {
        for (int i = 0; i < _animatorSamples.Count; i++)
        {
            Animator anim = _animatorSamples[i].Animator;
            if (anim != null)
                anim.speed = _animatorSamples[i].Speed;
        }

        _animatorSamples.Clear();
    }

    private void PlayPostProcess(HitFeedbackParams parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.postProcessEffectName))
            return;

        if (postProcessSwitcher == null)
            postProcessSwitcher = FindFirstObjectByType<PandaPostProcessSwitcher>();

        if (postProcessSwitcher == null)
            return;

        float duration = parameters.postProcessDuration > 0f
            ? parameters.postProcessDuration
            : defaultPostProcessDuration;
        if (duration <= 0f)
            return;

        if (_postProcessRoutine != null)
        {
            StopCoroutine(_postProcessRoutine);
            RestorePostProcess();
            _postProcessRoutine = null;
        }

        _postProcessRoutine = StartCoroutine(PostProcessRoutine(parameters, duration));
    }

    private IEnumerator PostProcessRoutine(HitFeedbackParams parameters, float duration)
    {
        if (!TryActivatePostEffect(parameters.postProcessEffectName, out int index, out PandaPostProcess effect))
        {
            _postProcessRoutine = null;
            yield break;
        }

        _savedPostIndex = postProcessSwitcher.ActiveEffectIndex;
        _hasPostSnapshot = false;

        if (parameters.overridePostProcessParams && effect != null)
        {
            _postSnapshot = Snapshot(effect);
            _hasPostSnapshot = true;
            ApplyOverrides(effect, parameters);
        }

        // Ensure selection is the requested effect (may equal previous).
        postProcessSwitcher.SetActiveEffect(index);

        yield return new WaitForSecondsRealtime(duration);

        RestorePostProcess();
        _postProcessRoutine = null;
    }

    private bool TryActivatePostEffect(string effectName, out int index, out PandaPostProcess effect)
    {
        index = -1;
        effect = null;
        if (postProcessSwitcher == null)
            return false;

        postProcessSwitcher.RefreshEffects();
        IReadOnlyList<PandaPostProcess> effects = postProcessSwitcher.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            PandaPostProcess candidate = effects[i];
            if (candidate == null)
                continue;

            if (NamesMatch(candidate, effectName))
            {
                index = i;
                effect = candidate;
                return true;
            }
        }

        Debug.LogWarning($"[CombatEffectManager] Post-process effect '{effectName}' not found on PandaPostProcessSwitcher.", this);
        return false;
    }

    private static bool NamesMatch(PandaPostProcess effect, string effectName)
    {
        if (string.Equals(effect.name, effectName, System.StringComparison.OrdinalIgnoreCase))
            return true;

        if (effect.PostProcessMat != null &&
            string.Equals(effect.PostProcessMat.name, effectName, System.StringComparison.OrdinalIgnoreCase))
            return true;

        // Multiple PandaPostProcess on one GO share the same GameObject name — match "GO (n)" style via material first.
        return false;
    }

    private static PostProcessSnapshot Snapshot(PandaPostProcess effect)
    {
        return new PostProcessSnapshot
        {
            Effect = effect,
            StepFactor = effect.StepFactor,
            MainAlpha = effect.MainAlpha,
            BlurFactor = effect.BlurFactor,
            LineUVScale = effect.LineUVScale,
            Chromatic = effect.Chromatic,
            Frequency = effect.Frequency,
            Amplitude = effect.Amplitude,
            VignettePower = effect.VignettePower,
            VignetteScale = effect.VignetteScale
        };
    }

    private static void ApplyOverrides(PandaPostProcess effect, HitFeedbackParams parameters)
    {
        effect.StepFactor = parameters.stepFactor;
        effect.MainAlpha = parameters.mainAlpha;
        effect.BlurFactor = parameters.blurFactor;
        effect.LineUVScale = parameters.lineUVScale;
        effect.Chromatic = parameters.chromatic;
        effect.Frequency = parameters.frequency;
        effect.Amplitude = parameters.amplitude;
        effect.VignettePower = parameters.vignettePower;
        effect.VignetteScale = parameters.vignetteScale;
    }

    private void RestorePostProcess()
    {
        if (_hasPostSnapshot && _postSnapshot.Effect != null)
        {
            PandaPostProcess effect = _postSnapshot.Effect;
            effect.StepFactor = _postSnapshot.StepFactor;
            effect.MainAlpha = _postSnapshot.MainAlpha;
            effect.BlurFactor = _postSnapshot.BlurFactor;
            effect.LineUVScale = _postSnapshot.LineUVScale;
            effect.Chromatic = _postSnapshot.Chromatic;
            effect.Frequency = _postSnapshot.Frequency;
            effect.Amplitude = _postSnapshot.Amplitude;
            effect.VignettePower = _postSnapshot.VignettePower;
            effect.VignetteScale = _postSnapshot.VignetteScale;
        }

        _hasPostSnapshot = false;

        if (postProcessSwitcher != null && _savedPostIndex >= -1)
            postProcessSwitcher.SetActiveEffect(_savedPostIndex);

        _savedPostIndex = -1;
    }
}
