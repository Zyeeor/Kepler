using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI 音效通道控制器（挂 AudioManager 下）：
///   独立单源（不被战斗池抢占）+ 通用按钮点击音（EventSystem 指针判定）+ 弹窗静默例外（Push/Pop 计数）。
/// 通用点击音走 SfxBank 的 UiClick 条目（未配置 = 静默；无音时跳过 Raycast 保持原性能特征）。
/// 静默例外由各弹窗 PushClickMute/PopClickMute 自管（本控制器不感知具体 UI）。
/// </summary>
public class UiAudioController : AudioChannelController
{
    [System.NonSerialized] public AudioSource uiSource;    // 运行时自动创建（非序列化，非配置项）

    /// <summary>UI 点击音静默计数器（弹窗 Push/Pop 自管）。</summary>
    int _uiClickMuteCount;
    /// <summary>点击判定用 Raycast 结果缓冲（复用，避免每帧分配）。</summary>
    readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();

    protected override void EnsureSources()
    {
        if (uiSource == null)
        {
            var go = new GameObject("UI");
            go.transform.SetParent(transform);
            uiSource = go.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
        }
    }

    void Update()
    {
        // 通用 UI 点击音：仅当点击命中"可交互 Selectable"（按钮/滑块/开关等，含其子级文本）
        // 才发声——点在背景/面板等不可交互 UI 上不响。
        // 早退条件含"bank 条目无音"（无音时跳过 Raycast，保持原性能特征）。
        if (!HasUiClickSound || _uiClickMuteCount > 0) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (!TryHitInteractable()) return;
        if (Owner == null) return;
        Owner.Play(SfxId.UiClick);
    }

    /// <summary>UI 点击音是否存在（SfxBank UiClick 条目有 clip）。</summary>
    bool HasUiClickSound =>
        Owner != null && Owner.sfxBank != null && Owner.sfxBank.TryGet(SfxId.UiClick, out var e) && e.clip != null;

    /// <summary>指针点击位置是否命中可交互的 Selectable（Button/Slider/Toggle 等，含其子级文本）。</summary>
    bool TryHitInteractable()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        var ped = new PointerEventData(es) { position = Input.mousePosition };
        _uiRaycastResults.Clear();
        es.RaycastAll(ped, _uiRaycastResults);
        foreach (var r in _uiRaycastResults)
        {
            if (r.gameObject == null) continue;
            var sel = r.gameObject.GetComponentInParent<Selectable>();
            if (sel != null && sel.isActiveAndEnabled && sel.interactable)
                return true;
        }
        return false;
    }

    // ── 播放与静默例外 ──

    /// <summary>播放 UI 音效（独立一路）。</summary>
    public void PlayClip(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || uiSource == null)
        {
            Debug.Log($"[CardSfx] PlayClip FAIL: clip={(clip == null ? "NULL" : clip.name)}, uiSource={(uiSource == null ? "NULL" : "ok")}");
            return;
        }
        Debug.Log($"[CardSfx] PlayClip({clip.name}) volumeScale={volumeScale}, uiSource.volume={Perceptual(Owner.UiVolume):F3}");
        uiSource.volume = MixerActive ? 1f : Perceptual(Owner.UiVolume);
        uiSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>进入"UI 点击音静默"（如选卡弹窗打开期间，由专属音接管）。计数器叠加。</summary>
    public void PushClickMute()
    {
        _uiClickMuteCount++;
    }

    /// <summary>退出"UI 点击音静默"（与 PushClickMute 成对调用）。</summary>
    public void PopClickMute()
    {
        _uiClickMuteCount = Mathf.Max(0, _uiClickMuteCount - 1);
    }

    // ── 统一接口 ──

    public override void RefreshVolume()
    {
        if (uiSource != null)
            uiSource.volume = MixerActive ? 1f : Perceptual(Owner.UiVolume);
    }

    public override void PauseAll()
    {
        if (uiSource != null) uiSource.Pause();
    }

    public override void ResumeAll()
    {
        if (uiSource != null) uiSource.UnPause();
    }

    public override void StopAll()
    {
        if (uiSource != null) uiSource.Stop();
    }
}
