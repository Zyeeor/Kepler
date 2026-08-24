using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音频调试面板（F11 切换）：强制触发音效 / 缺失名单 / 音量滑块 / BGM 状态 / Voice 测试 / Loop 测试。
/// 对齐 Narrative Baseline §10 音频相关项；遵循项目 Debug 惯例：
/// GameManager.IsFormalFlow 屏蔽 + Inspector enableDebug 开关 + 随 GameManager 常驻（EnsureOnGameManager）。
/// </summary>
public class AudioDebugPanel : MonoBehaviour
{
    [Tooltip("面板总开关（Inspector 可配）。")]
    public bool enableDebug = true;
    [Tooltip("是否显示面板（F11 切换）。")]
    public bool showPanel = false;
    [Tooltip("切换快捷键。")]
    public KeyCode toggleKey = KeyCode.F11;

    string sfxIdInput = "WaveStart";
    string voiceIdInput = "";
    int voiceChannel = 0; // 0=Mythic 1=System
    Vector2 missingScroll;
    bool loopTesting;
    CombatAudioManager.SfxLoopHandle loopHandle;

    /// <summary>挂载到 GameManager（随常驻对象 DDOL；已挂则复用）。</summary>
    public static AudioDebugPanel EnsureOnGameManager()
    {
        var gm = GameManager.Instance;
        if (gm == null) return null;
        var existing = gm.GetComponent<AudioDebugPanel>();
        return existing != null ? existing : gm.gameObject.AddComponent<AudioDebugPanel>();
    }

    void Update()
    {
        if (!enableDebug || GameManager.IsFormalFlow) return;
        if (Input.GetKeyDown(toggleKey))
            showPanel = !showPanel;
    }

    void OnGUI()
    {
        if (!showPanel || !enableDebug || !Application.isPlaying || GameManager.IsFormalFlow) return;

        float w = 420f, h = 520f;
        float x = Screen.width - w - 12f, y = 12f;
        GUI.Box(new Rect(x, y, w, h), "音频调试面板（F11）");

        const float lineH = 22f;
        const float pad = 8f;
        float ty = y + lineH + 6f;

        // ── 1. 强制触发音效 ──
        GUI.Label(new Rect(x + pad, ty, 380f, lineH), "强制触发（SfxId 名，大小写不敏感）：");
        ty += lineH;
        sfxIdInput = GUI.TextField(new Rect(x + pad, ty, 250f, lineH), sfxIdInput);
        if (GUI.Button(new Rect(x + 262f, ty, 140f, lineH), "Play"))
        {
            if (System.Enum.TryParse(sfxIdInput.Trim(), true, out SfxId id) && id != SfxId.None)
                AudioManager.Instance?.Play(id);
        }
        ty += lineH + 6f;

        // 常用快捷按钮
        var common = new[] { SfxId.WaveStart, SfxId.WaveClear, SfxId.PossessionStart, SfxId.SoulEnter, SfxId.CardOpen, SfxId.BulletTimeStart, SfxId.EliteSpawn, SfxId.BodyHit };
        float bx = x + pad;
        for (int i = 0; i < common.Length; i++)
        {
            if (GUI.Button(new Rect(bx, ty, 92f, lineH), common[i].ToString()))
                AudioManager.Instance?.Play(common[i]);
            bx += 98f;
            if (bx > x + w - 100f) { bx = x + pad; ty += lineH + 2f; }
        }
        ty += lineH + 8f;

        // ── 2. 缺失名单 ──
        var missingIds = AudioManager.GetMissingSfxIds();
        var missingNames = AudioManager.GetMissingSfxNames();
        GUI.Label(new Rect(x + pad, ty, 380f, lineH), $"缺失（bank 无条目/未注册名）：ids={missingIds.Count}, names={missingNames.Count}");
        ty += lineH;
        var missingList = new List<string>();
        foreach (var kv in missingIds) missingList.Add($"id: {kv.Key} ×{kv.Value}");
        foreach (var kv in missingNames) missingList.Add($"name: {kv.Key} ×{kv.Value}");
        if (missingList.Count > 0)
        {
            string all = string.Join("\n", missingList);
            missingScroll = GUI.BeginScrollView(new Rect(x + pad, ty, 390f, 70f), missingScroll,
                new Rect(0f, 0f, 370f, missingList.Count * 16f));
            GUI.Label(new Rect(0f, 0f, 370f, missingList.Count * 16f), all);
            GUI.EndScrollView();
            ty += 76f;
        }
        else
        {
            GUI.Label(new Rect(x + pad, ty, 380f, lineH), "（无）");
            ty += lineH;
        }
        ty += 6f;

        // ── 3. 音量滑块 ──
        var asm = AudioSettingsManager.Instance;
        if (asm != null)
        {
            GUI.Label(new Rect(x + pad, ty, 380f, lineH), "音量（四路独立）：");
            ty += lineH;
            float bgm = asm.GetMusicVolume();
            float sfx = asm.GetSFXVolume();
            float voice = asm.GetVoiceVolume();
            float ui = asm.GetUIVolume();
            float nb = GUI.HorizontalSlider(new Rect(x + pad, ty, 300f, lineH), bgm, 0f, 1f);
            GUI.Label(new Rect(x + 316f, ty, 90f, lineH), $"BGM {bgm:F2}");
            if (Mathf.Abs(nb - bgm) > 0.001f) asm.SetMusicVolume(nb);
            ty += lineH;
            float ns = GUI.HorizontalSlider(new Rect(x + pad, ty, 300f, lineH), sfx, 0f, 1f);
            GUI.Label(new Rect(x + 316f, ty, 90f, lineH), $"SFX {sfx:F2}");
            if (Mathf.Abs(ns - sfx) > 0.001f) asm.SetSFXVolume(ns);
            ty += lineH;
            float nv = GUI.HorizontalSlider(new Rect(x + pad, ty, 300f, lineH), voice, 0f, 1f);
            GUI.Label(new Rect(x + 316f, ty, 90f, lineH), $"Voice {voice:F2}");
            if (Mathf.Abs(nv - voice) > 0.001f) asm.SetVoiceVolume(nv);
            ty += lineH;
            float nu = GUI.HorizontalSlider(new Rect(x + pad, ty, 300f, lineH), ui, 0f, 1f);
            GUI.Label(new Rect(x + 316f, ty, 90f, lineH), $"UI {ui:F2}");
            if (Mathf.Abs(nu - ui) > 0.001f) asm.SetUIVolume(nu);
            ty += lineH + 4f;
        }

        // ── 4. BGM 状态 ──
        var am = AudioManager.Instance;
        if (am != null)
        {
            string cur = am.bgmSource != null && am.bgmSource.isPlaying && am.bgmSource.clip != null ? am.bgmSource.clip.name
                : am.bgmSource2 != null && am.bgmSource2.isPlaying && am.bgmSource2.clip != null ? am.bgmSource2.clip.name : "（静默）";
            GUI.Label(new Rect(x + pad, ty, 380f, lineH), $"BGM 当前：{cur}");
            ty += lineH + 4f;
        }

        // ── 5. Voice 测试 ──
        GUI.Label(new Rect(x + pad, ty, 380f, lineH), "Voice 测试（audioId + 通道；观察 BGM 压低恢复）：");
        ty += lineH;
        voiceIdInput = GUI.TextField(new Rect(x + pad, ty, 220f, lineH), voiceIdInput);
        voiceChannel = GUI.Toggle(new Rect(x + 232f, ty, 80f, lineH), voiceChannel == 0, "Mythic") ? 0 : voiceChannel;
        voiceChannel = GUI.Toggle(new Rect(x + 312f, ty, 80f, lineH), voiceChannel == 1, "System") ? 1 : voiceChannel;
        if (GUI.Button(new Rect(x + pad, ty + lineH + 2f, 380f, lineH), "PlayVoice"))
            AudioManager.Instance?.PlayVoice(voiceIdInput, (VoiceChannel)voiceChannel);
        ty += lineH * 2 + 8f;

        // ── 6. Loop 测试 ──
        if (!loopTesting)
        {
            if (GUI.Button(new Rect(x + pad, ty, 380f, lineH), "StartSfxLoop(MovementLoop) 测试"))
            {
                var soul = FindFirstObjectByType<SoulActor>();
                loopHandle = AudioManager.Instance != null
                    ? AudioManager.Instance.StartSfxLoop(SfxId.MovementLoop, soul != null ? soul.transform : null)
                    : default;
                loopTesting = loopHandle.IsValid;
            }
        }
        else
        {
            if (GUI.Button(new Rect(x + pad, ty, 380f, lineH), "StopSfxLoop（停止循环测试）"))
            {
                if (AudioManager.Instance != null) AudioManager.Instance.StopSfxLoop(loopHandle);
                loopTesting = false;
            }
        }
    }
}
