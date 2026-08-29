#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Victory Epilogue 集中调参入口：Tools/Possession/Victory Epilogue Tuner。
/// Editor Window 集中编辑 VictoryEpilogueConfig，并代理编辑现有 StageBgmMap/SfxBank 的 Victory 槽位；不复制第二套运行时数据。
/// </summary>
public sealed class VictoryEpilogueTunerWindow : EditorWindow
{
    VictoryEpilogueConfig _config;
    SerializedObject _serializedConfig;
    StageBgmMap _stageBgmMapAsset;
    SfxBank _sfxBankAsset;
    Vector2 _scroll;

    static readonly SfxId[] VictorySfxIds =
    {
        SfxId.VictoryEpilogueEnter,
        SfxId.VictoryEpilogueFirstTextReveal,
        SfxId.VictoryEpilogueNameInputReveal,
        SfxId.VictoryEpilogueNameConfirm,
        SfxId.VictoryEpilogueFinalReveal,
        SfxId.VictoryEpilogueFinalTitleReveal,
        SfxId.VictoryEpilogueFinalNameReveal,
        SfxId.VictoryEpilogueFinalCoronationReveal,
        SfxId.VictoryEpilogueExitBlack,
    };

    [MenuItem("Tools/Possession/Victory Epilogue Tuner")]
    public static void Open()
    {
        var window = GetWindow<VictoryEpilogueTunerWindow>("Victory Epilogue Tuner / 胜利结尾调试");
        window.minSize = new Vector2(430f, 620f);
        window.RefreshConfig();
    }

    void OnEnable()
    {
        RefreshConfig();
    }

    void RefreshConfig()
    {
        _config = Selection.activeObject as VictoryEpilogueConfig;
        if (_config == null)
            _config = Resources.Load<VictoryEpilogueConfig>("Victory/VictoryEpilogueConfig");
        _serializedConfig = _config != null ? new SerializedObject(_config) : null;
        _stageBgmMapAsset = Resources.Load<StageBgmMap>("Audio/StageBgmMap");
        _sfxBankAsset = Resources.Load<SfxBank>("Audio/SfxBank");
        if (AudioManager.Instance != null)
        {
            if (_stageBgmMapAsset == null) _stageBgmMapAsset = AudioManager.Instance.stageBgmMap;
            if (_sfxBankAsset == null) _sfxBankAsset = AudioManager.Instance.sfxBank;
        }
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("VICTORY EPILOGUE TUNER / 胜利结尾调试面板", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Formal Victory / 正式胜利、Full Preview / 完整预览、Final Preview / 最终字幕预览共用同一份 Config。音频 Clip 不复制，直接编辑现有 StageBgmMap / SfxBank。", MessageType.Info);

        if (_config == null)
        {
            if (GUILayout.Button("Create VictoryEpilogueConfig / 创建配置资产"))
            {
                CreateConfigAsset();
                RefreshConfig();
            }
            EditorGUILayout.HelpBox("当前未找到 Resources/Victory/VictoryEpilogueConfig。运行时会使用默认值；创建资产后可持久化调参。", MessageType.Warning);
            DrawPreviewButtons();
            return;
        }

        if (_serializedConfig == null || _serializedConfig.targetObject == null)
            _serializedConfig = new SerializedObject(_config);
        _serializedConfig.Update();

        DrawPreviewButtons();
        DrawPresentationPrefabSection();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawProperties("Content / 文案", "firstMessage", "namePrompt", "finalTitle", "finalCoronationLine", "maxNameLength");
        DrawProperties("Opening Timing / 开场时序", "fadeToBlackDuration", "firstBlackHoldDuration", "firstTextFadeInDuration", "firstTextHoldBeforeInputDuration", "inputFieldFadeInDuration");
        DrawProperties("Coronation Timing / 加冕时序", "firstStageFadeOutDuration", "secondBlackHoldDuration", "finalTitleRevealDelay", "finalNameRevealDelay", "finalCoronationRevealDelay", "finalStageFadeInDuration", "finalStageHoldDuration");
        DrawProperties("Ending Timing / 结束时序", "finalStageFadeOutDuration", "finalBlackHoldDuration");
        DrawProperties("First Stage Layout / 第一幕布局", "firstMessageFontSize", "firstMessagePosition", "firstMessageLineSpacing", "namePromptFontSize", "namePromptPosition", "inputFieldSize", "inputFieldPosition", "inputTextFontSize");
        DrawProperties("Final Stage Layout / 最终幕布局", "finalTitleFontSize", "finalTitlePosition", "playerNameFontSize", "playerNamePosition", "coronationLineFontSize", "coronationLinePosition", "finalGroupVerticalSpacing");
        DrawProperties("Debug / 调试", "enableVictoryEpilogueDebugPreview", "debugEpiloguePlayerName", "debugFullPreviewInput", "debugFinalPreviewInput", "debugRequireControl", "debugRequireShift");
        DrawProperties("Audio Event IDs / 音频事件ID", "enterBlackAudio", "firstTextRevealAudio", "nameInputRevealAudio", "nameConfirmAudio", "finalRevealAudio", "finalTitleRevealAudio", "finalNameRevealAudio", "finalCoronationRevealAudio", "exitBlackAudio");
        DrawAudioConfigSection();
        EditorGUILayout.EndScrollView();

        if (_serializedConfig.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
        }
    }

    void DrawPresentationPrefabSection()
    {
        EditorGUILayout.LabelField("Presentation Prefab / 手动布局Prefab", EditorStyles.boldLabel);
        var prefabProp = _serializedConfig.FindProperty("presentationPrefab");
        EditorGUILayout.PropertyField(prefabProp, new GUIContent("Victory UI Prefab / 胜利UI预制体"));
        var prefab = prefabProp != null ? prefabProp.objectReferenceValue as VictoryEpilogueView : null;
        if (prefab != null)
        {
            EditorGUILayout.HelpBox("运行时优先使用这个 Prefab。双击或点击定位后，可手动调整 RectTransform、TMP 字体、字号、颜色和层级。", MessageType.None);
            if (GUILayout.Button("Ping Prefab / 定位并选中 Prefab"))
            {
                Selection.activeObject = prefab.gameObject;
                EditorGUIUtility.PingObject(prefab.gameObject);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("尚未绑定手动布局 Prefab。运行时将继续使用代码生成兜底。", MessageType.Warning);
        }
    }

    void DrawPreviewButtons()
    {
        EditorGUILayout.LabelField("Preview Controls / 预览控制", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Play Full Preview / 完整预览"))
                VictoryEpilogueController.EnsureInstance(_config).PlayFullPreview();
            if (GUILayout.Button("Play Final Preview / 最终预览"))
                VictoryEpilogueController.EnsureInstance(_config).PlayFinalPreview();
            if (GUILayout.Button("Stop / Reset / 停止重置"))
                VictoryEpilogueController.Instance?.StopResetPreview();
            GUI.enabled = true;
        }
        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("进入 Play Mode 后可点击预览。快捷键：Ctrl + Shift + V / 完整预览；Ctrl + Shift + C / 最终字幕。", MessageType.None);
    }

    void DrawProperties(string title, params string[] names)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        for (int i = 0; i < names.Length; i++)
        {
            var property = _serializedConfig.FindProperty(names[i]);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(LabelFor(names[i])), true);
        }
        EditorGUILayout.Space(4f);
    }

    static string LabelFor(string name)
    {
        switch (name)
        {
            case "presentationPrefab": return "Presentation Prefab / 手动布局预制体";
            case "firstMessage": return "First Message / 第一幕主文案";
            case "namePrompt": return "Name Prompt / 姓名提示";
            case "finalTitle": return "Final Title / 最终称号";
            case "finalCoronationLine": return "Final Coronation Line / 加冕句";
            case "maxNameLength": return "Max Name Length / 名字最大长度";
            case "fadeToBlackDuration": return "Fade To Black / 淡入黑幕";
            case "firstBlackHoldDuration": return "First Black Hold / 第一段黑幕停留";
            case "firstTextFadeInDuration": return "First Text Fade In / 第一幕字幕淡入";
            case "firstTextHoldBeforeInputDuration": return "Text Hold Before Input / 输入框前停留";
            case "inputFieldFadeInDuration": return "Input Field Fade In / 输入框淡入";
            case "firstStageFadeOutDuration": return "First Stage Fade Out / 第一幕淡出";
            case "secondBlackHoldDuration": return "Second Black Hold / 第二段黑幕停留";
            case "finalTitleRevealDelay": return "Final Title Delay / 称号延迟";
            case "finalNameRevealDelay": return "Final Name Delay / 玩家名延迟";
            case "finalCoronationRevealDelay": return "Coronation Delay / 加冕句延迟";
            case "finalStageFadeInDuration": return "Final Fade In / 最终幕淡入";
            case "finalStageHoldDuration": return "Final Hold / 最终幕停留";
            case "finalStageFadeOutDuration": return "Final Fade Out / 最终幕淡出";
            case "finalBlackHoldDuration": return "Final Black Hold / 最终黑幕停留";
            case "firstMessageFontSize": return "First Message Font / 第一幕字号";
            case "firstMessagePosition": return "First Message Position / 第一幕位置";
            case "firstMessageLineSpacing": return "First Message Line Spacing / 第一幕行距";
            case "namePromptFontSize": return "Name Prompt Font / 姓名提示字号";
            case "namePromptPosition": return "Name Prompt Position / 姓名提示位置";
            case "inputFieldSize": return "Input Field Size / 输入框尺寸";
            case "inputFieldPosition": return "Input Field Position / 输入框位置";
            case "inputTextFontSize": return "Input Text Font / 输入文字字号";
            case "finalTitleFontSize": return "Final Title Font / 最终称号字号";
            case "finalTitlePosition": return "Final Title Position / 最终称号位置";
            case "playerNameFontSize": return "Player Name Font / 玩家名字号";
            case "playerNamePosition": return "Player Name Position / 玩家名位置";
            case "coronationLineFontSize": return "Coronation Font / 加冕句字号";
            case "coronationLinePosition": return "Coronation Position / 加冕句位置";
            case "finalGroupVerticalSpacing": return "Final Vertical Spacing / 最终幕间距";
            case "enableVictoryEpilogueDebugPreview": return "Enable Preview / 启用预览";
            case "debugEpiloguePlayerName": return "Debug Player Name / 调试玩家名";
            case "debugFullPreviewInput": return "Full Preview Key / 完整预览主键";
            case "debugFinalPreviewInput": return "Final Preview Key / 最终预览主键";
            case "debugRequireControl": return "Require Ctrl / 需要Ctrl";
            case "debugRequireShift": return "Require Shift / 需要Shift";
            case "enterBlackAudio": return "Enter Black / 进入黑幕音频";
            case "firstTextRevealAudio": return "First Text Reveal / 第一幕字幕音频";
            case "nameInputRevealAudio": return "Name Input Reveal / 输入框出现音频";
            case "nameConfirmAudio": return "Name Confirm / 姓名确认音频";
            case "finalRevealAudio": return "Final Reveal / 最终幕音频";
            case "finalTitleRevealAudio": return "Title Reveal / 称号出现音频";
            case "finalNameRevealAudio": return "Name Reveal / 玩家名出现音频";
            case "finalCoronationRevealAudio": return "Coronation Reveal / 加冕句出现音频";
            case "exitBlackAudio": return "Exit Black / 最终黑幕音频";
            default: return name;
        }
    }

    void DrawAudioConfigSection()
    {
        EditorGUILayout.LabelField("Audio Config Center / 音频配置中心", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("这里的 Clip 槽位可以直接拖入音频文件。对象字段只用于选择/定位 StageBgmMap 和 SfxBank，不是第二套音频数据库。", MessageType.Info);

        StageBgmMap previousBgmMap = _stageBgmMapAsset;
        SfxBank previousSfxBank = _sfxBankAsset;
        _stageBgmMapAsset = (StageBgmMap)EditorGUILayout.ObjectField(
            "Stage BGM Map / 阶段BGM配置", _stageBgmMapAsset, typeof(StageBgmMap), false);
        _sfxBankAsset = (SfxBank)EditorGUILayout.ObjectField(
            "SFX Bank / 音效配置", _sfxBankAsset, typeof(SfxBank), false);
        if (AudioManager.Instance != null)
        {
            if (_stageBgmMapAsset != previousBgmMap) AudioManager.Instance.stageBgmMap = _stageBgmMapAsset;
            if (_sfxBankAsset != previousSfxBank) AudioManager.Instance.sfxBank = _sfxBankAsset;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (_stageBgmMapAsset != null && GUILayout.Button("Ping StageBgmMap / 定位BGM配置"))
            {
                Selection.activeObject = _stageBgmMapAsset;
                EditorGUIUtility.PingObject(_stageBgmMapAsset);
            }
            if (_sfxBankAsset != null && GUILayout.Button("Ping SfxBank / 定位音效配置"))
            {
                Selection.activeObject = _sfxBankAsset;
                EditorGUIUtility.PingObject(_sfxBankAsset);
            }
        }

        DrawVictoryBgmSlots();
        DrawVictorySfxSlots();
    }

    void DrawVictoryBgmSlots()
    {
        EditorGUILayout.LabelField("Victory BGM / 胜利结尾音乐", EditorStyles.boldLabel);
        if (_stageBgmMapAsset == null)
        {
            EditorGUILayout.HelpBox("先把 StageBgmMap.asset 拖到上面的配置槽位。", MessageType.None);
            return;
        }

        if (_stageBgmMapAsset.victoryEpilogueBase == null)
            _stageBgmMapAsset.victoryEpilogueBase = new StageBgmMap.Slot { action = BgmAction.Play };
        if (_stageBgmMapAsset.victoryEpilogueExit == null)
            _stageBgmMapAsset.victoryEpilogueExit = new StageBgmMap.Slot { action = BgmAction.Play };

        var so = new SerializedObject(_stageBgmMapAsset);
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("victoryEpilogueBase"), new GUIContent("Base BGM / 进入结尾音乐"), true);
        EditorGUILayout.PropertyField(so.FindProperty("victoryEpilogueExit"), new GUIContent("Exit BGM / 最终黑幕音乐"), true);
        if (so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_stageBgmMapAsset);
            AssetDatabase.SaveAssets();
        }
    }

    void DrawVictorySfxSlots()
    {
        EditorGUILayout.LabelField("Victory SFX / 胜利结尾音效", EditorStyles.boldLabel);
        if (_sfxBankAsset == null)
        {
            EditorGUILayout.HelpBox("先把 SfxBank.asset 拖到上面的配置槽位。", MessageType.None);
            return;
        }

        var so = new SerializedObject(_sfxBankAsset);
        so.Update();
        var entries = so.FindProperty("entries");
        for (int i = 0; i < VictorySfxIds.Length; i++)
        {
            SfxId id = VictorySfxIds[i];
            SerializedProperty entry = FindOrCreateSfxEntry(entries, id);
            var clip = entry.FindPropertyRelative("clip");
            EditorGUILayout.PropertyField(clip, new GUIContent(SfxLabel(id)));
        }
        if (so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_sfxBankAsset);
            AssetDatabase.SaveAssets();
        }
    }

    static SerializedProperty FindOrCreateSfxEntry(SerializedProperty entries, SfxId id)
    {
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("id").intValue == (int)id)
                return entry;
        }

        entries.arraySize++;
        var created = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        created.FindPropertyRelative("id").intValue = (int)id;
        created.FindPropertyRelative("volumeScale").floatValue = 1f;
        created.FindPropertyRelative("pitch").floatValue = 1f;
        created.FindPropertyRelative("channel").enumValueIndex = 0;
        created.FindPropertyRelative("prefer3D").boolValue = false;
        return created;
    }

    static string SfxLabel(SfxId id)
    {
        switch (id)
        {
            case SfxId.VictoryEpilogueEnter: return "Enter / 进入黑幕";
            case SfxId.VictoryEpilogueFirstTextReveal: return "First Text / 第一幕字幕";
            case SfxId.VictoryEpilogueNameInputReveal: return "Name Input / 输入框出现";
            case SfxId.VictoryEpilogueNameConfirm: return "Name Confirm / 姓名确认";
            case SfxId.VictoryEpilogueFinalReveal: return "Final Reveal / 最终字幕";
            case SfxId.VictoryEpilogueFinalTitleReveal: return "Final Title / 最终称号";
            case SfxId.VictoryEpilogueFinalNameReveal: return "Final Name / 玩家名字";
            case SfxId.VictoryEpilogueFinalCoronationReveal: return "Coronation / 加冕句";
            case SfxId.VictoryEpilogueExitBlack: return "Exit Black / 最终黑幕";
            default: return id.ToString();
        }
    }

    void CreateConfigAsset()
    {
        const string resourcesFolder = "Assets/Resources";
        const string victoryFolder = "Assets/Resources/Victory";
        if (!AssetDatabase.IsValidFolder(resourcesFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(victoryFolder))
            AssetDatabase.CreateFolder(resourcesFolder, "Victory");

        string path = AssetDatabase.GenerateUniqueAssetPath(victoryFolder + "/VictoryEpilogueConfig.asset");
        var asset = VictoryEpilogueConfig.CreateRuntimeDefaults();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        _config = asset;
    }
}
#endif
