using System;
using UnityEngine;

/// <summary>
/// Per-ability combat presentation settings fired on a successful hit.
/// Zero numeric values resolve to <see cref="CombatEffectManager"/> defaults at runtime.
/// </summary>
[Serializable]
public class HitFeedbackParams
{
    [Header("Post Process (Panda)")]
    [Tooltip("Play a post-process effect on hit. Leave effect name empty to skip.")]
    public bool postProcessOnHit = false;
    [Tooltip("Effect display name matching PandaPostProcessSwitcher (material name or component name).")]
    public string postProcessEffectName = "";
    [Tooltip("How long the post-process stays active (unscaled seconds). 0 = manager default.")]
    public float postProcessDuration = 0f;
    [Tooltip("When enabled, override PandaPostProcess scalar params for the duration.")]
    public bool overridePostProcessParams = false;
    [Range(0f, 1f)] public float stepFactor = 0.5f;
    [Range(0f, 1f)] public float mainAlpha = 1f;
    [Range(0f, 1f)] public float blurFactor = 0f;
    [Range(0f, 4f)] public float lineUVScale = 0f;
    [Range(0f, 1.5f)] public float chromatic = 0f;
    [Range(0f, 1f)] public float frequency = 0f;
    [Range(0f, 1f)] public float amplitude = 0f;
    [Range(1f, 3f)] public float vignettePower = 1.5f;
    [Range(0f, 3f)] public float vignetteScale = 1.5f;

    [Header("Screen Shake")]
    [Tooltip("Trigger camera shake on hit.")]
    public bool shakeOnHit = true;
    [Tooltip("Impulse force. 0 = manager default.")]
    public float shakeForce = 0f;
    [Tooltip("Impulse duration override (seconds). 0 = keep CameraDirector / Impulse default.")]
    public float shakeDuration = 0f;

    [Header("Hit Stop (顿帧)")]
    [Tooltip("Trigger hit-stop on hit.")]
    public bool hitStopOnHit = true;
    [Tooltip("Hit-stop duration in unscaled seconds. 0 = manager default.")]
    public float hitStopDuration = 0f;
    [Tooltip("Animator speed (and optional Time.timeScale) during hit-stop. 0 = full freeze.")]
    [Range(0f, 1f)]
    public float hitStopScale = 0f;
    [Tooltip("Also scale Time.timeScale (legacy). Default is Animator-only so gameplay logic keeps running.")]
    public bool useGlobalTimeScale = false;

    public bool HasAnyEnabled =>
        (postProcessOnHit && !string.IsNullOrWhiteSpace(postProcessEffectName)) ||
        shakeOnHit ||
        hitStopOnHit;
}
