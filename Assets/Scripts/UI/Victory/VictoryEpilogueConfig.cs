using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Victory Epilogue 的唯一运行时配置真源。正式流程、Debug Preview 和 Editor Tuner 均读取同一对象。
/// 资产可放在 Resources/Victory/VictoryEpilogueConfig；缺失时 Controller 使用同一套代码创建运行时默认配置。
/// 音频资源本体仍由 AudioManager 的 SfxBank / StageBgmMap 管理，本类只保存语义 SfxId。
/// </summary>
[CreateAssetMenu(menuName = "Kepler/Victory/Victory Epilogue Config", fileName = "VictoryEpilogueConfig")]
public class VictoryEpilogueConfig : ScriptableObject
{
    [Header("Content")]
    [Tooltip("手动布局 Prefab。打开它即可拖动位置、调字号、换字体和改颜色；为空时使用代码生成兜底。")]
    public VictoryEpilogueView presentationPrefab;
    [Tooltip("可选：补齐项目默认 SDF 缺少的中文字形（例如“弑”）。为空时保留 Prefab 当前字体。")]
    public TMP_FontAsset victoryFallbackFont;
    [TextArea(2, 4)] public string firstMessage = "你已踏过七罪。\n王座正在等待它的新主人。";
    public string namePrompt = "留下你的名字";
    [Tooltip("无统计或规则未命中时的固定兜底称号。不要改成“罪者”，默认保持完整的“弑罪者”。")]
    public string finalTitle = "弑罪者";
    [TextArea(1, 2)] public string finalCoronationLine = "于七罪之上，加冕为王。";
    [Tooltip("启用后，正式通关会根据本局已采集的 Per-Sin / 构筑数据选择称号；Preview 无本局统计时从通用词池随机。")]
    public bool useDynamicFinalTitle = true;
    [Min(1)] public int godBuildMinCards = 3;
    [Tooltip("七宗罪称号池；每个池为空时使用代码内置默认词。列表中的词会随机抽取。")]
    public List<VictoryTitlePool> titlePools = new List<VictoryTitlePool>();
    [Tooltip("没有可用七宗罪倾向时的通用随机称号。")]
    public List<string> neutralTitlePool = new List<string>();
    [Min(1)] public int maxNameLength = 16;

    [Serializable]
    public class VictoryTitlePool
    {
        public SinType sin;
        public List<string> tendencyTitles = new List<string>();
        public List<string> godBuildTitles = new List<string>();
    }

    [Header("Opening Timing")]
    [Min(0f)] public float fadeToBlackDuration = 0.8f;
    [Min(0f)] public float firstBlackHoldDuration = 1f;
    [Min(0f)] public float firstTextFadeInDuration = 0.8f;
    [Min(0f)] public float firstTextHoldBeforeInputDuration = 0.35f;
    [Min(0f)] public float inputFieldFadeInDuration = 0.3f;

    [Header("Coronation Timing")]
    [Min(0f)] public float firstStageFadeOutDuration = 0.5f;
    [Min(0f)] public float secondBlackHoldDuration = 1.5f;
    [Min(0f)] public float finalTitleRevealDelay;
    [Min(0f)] public float finalNameRevealDelay;
    [Min(0f)] public float finalCoronationRevealDelay;
    [Min(0f)] public float finalStageFadeInDuration = 1f;
    [Min(0f)] public float finalStageHoldDuration = 3.5f;

    [Header("Ending Timing")]
    [Min(0f)] public float finalStageFadeOutDuration = 1f;
    [Min(0f)] public float finalBlackHoldDuration = 0.8f;

    [Header("Debug")]
    public bool enableVictoryEpilogueDebugPreview = true;
    public string debugEpiloguePlayerName = "SONG";
    [Tooltip("完整预览主键。默认使用 Ctrl + Shift + V，避免占用现有 F 键调试工具。")]
    public KeyCode debugFullPreviewInput = KeyCode.V;
    [Tooltip("最终字幕预览主键。默认使用 Ctrl + Shift + C，避免占用现有 F 键调试工具。")]
    public KeyCode debugFinalPreviewInput = KeyCode.C;
    public bool debugRequireControl = true;
    public bool debugRequireShift = true;

    [Header("First Stage Layout")]
    [Min(1f)] public float firstMessageFontSize = 42f;
    public Vector2 firstMessagePosition = new Vector2(0f, 170f);
    public float firstMessageLineSpacing;
    [Min(1f)] public float namePromptFontSize = 30f;
    public Vector2 namePromptPosition = new Vector2(0f, 40f);
    public Vector2 inputFieldSize = new Vector2(560f, 74f);
    public Vector2 inputFieldPosition = new Vector2(0f, -65f);
    [Min(1f)] public float inputTextFontSize = 32f;

    [Header("Final Stage Layout")]
    [Min(1f)] public float finalTitleFontSize = 34f;
    public Vector2 finalTitlePosition = new Vector2(0f, 180f);
    [Min(1f)] public float playerNameFontSize = 76f;
    public Vector2 playerNamePosition = new Vector2(0f, 35f);
    [Min(1f)] public float coronationLineFontSize = 28f;
    public Vector2 coronationLinePosition = new Vector2(0f, -100f);
    public float finalGroupVerticalSpacing = 0f;

    [Header("Audio Events")]
    public SfxId enterBlackAudio = SfxId.VictoryEpilogueEnter;
    public SfxId firstTextRevealAudio = SfxId.VictoryEpilogueFirstTextReveal;
    public SfxId nameInputRevealAudio = SfxId.VictoryEpilogueNameInputReveal;
    public SfxId nameConfirmAudio = SfxId.VictoryEpilogueNameConfirm;
    public SfxId finalRevealAudio = SfxId.VictoryEpilogueFinalReveal;
    public SfxId finalTitleRevealAudio = SfxId.VictoryEpilogueFinalTitleReveal;
    public SfxId finalNameRevealAudio = SfxId.VictoryEpilogueFinalNameReveal;
    public SfxId finalCoronationRevealAudio = SfxId.VictoryEpilogueFinalCoronationReveal;
    public SfxId exitBlackAudio = SfxId.VictoryEpilogueExitBlack;

    [SerializeField] int configVersion;

    void OnEnable()
    {
        MigrateAudioDefaults();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        MigrateAudioDefaults();
    }
#endif

    void MigrateAudioDefaults()
    {
        if (configVersion >= 1) return;
        // v0 assets were created before the three separate final reveal IDs existed.
        // Preserve an explicit custom cue, but fill legacy None / old Enter mapping with the semantic Victory IDs.
        if (enterBlackAudio == SfxId.None || enterBlackAudio == SfxId.SoulEnter)
            enterBlackAudio = SfxId.VictoryEpilogueEnter;
        if (finalTitleRevealAudio == SfxId.None)
            finalTitleRevealAudio = SfxId.VictoryEpilogueFinalTitleReveal;
        if (finalNameRevealAudio == SfxId.None)
            finalNameRevealAudio = SfxId.VictoryEpilogueFinalNameReveal;
        if (finalCoronationRevealAudio == SfxId.None)
            finalCoronationRevealAudio = SfxId.VictoryEpilogueFinalCoronationReveal;
        configVersion = 1;
    }

    public static VictoryEpilogueConfig CreateRuntimeDefaults()
    {
        var config = CreateInstance<VictoryEpilogueConfig>();
        config.name = "VictoryEpilogueConfig_RuntimeDefaults";
        return config;
    }
}
