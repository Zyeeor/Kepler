using UnityEngine;

/// <summary>First Clear 八步序列配置（SO）：textKey/时长/黑屏/声明选项。占位 key 可后补文案。</summary>
[CreateAssetMenu(menuName = "Kepler/Narrative/First Clear Config", fileName = "FirstClearConfig")]
public class FirstClearConfig : ScriptableObject
{
    [Header("八步文本（经 TextCatalog 按 Display 解析）")]
    public string step1MythicClosureKey = "nar.firstclear.s1.throne";
    public string step2WhoAmIKey = "nar.firstclear.s2.whoami";

    [Header("三个 Self-Declaration 选项（第一人称；选择不改变胜负/Build/评分/结局）")]
    public string[] declarationKeys = { "nar.firstclear.decl.1", "nar.firstclear.decl.2", "nar.firstclear.decl.3" };
    public string[] declarationIds = { "decl_throne", "decl_carrier", "decl_witness" };

    [Header("Functional Summary 句式")]
    public string summaryTemplateKey = "nar.firstclear.s4.summary";
    public string summarySinTextKeyPrefix = "nar.sin.name.";

    [Header("System Confirmation / 最终句")]
    public string step6ConfirmationKey = "nar.firstclear.s6.confirm";
    public string step8DistillationKey = "nar.firstclear.s8.distilled";

    [Header("Model / Version 展示标题 key")]
    public string step5ModelTitleKey = "nar.firstclear.s5.model";

    [Header("节奏")]
    [Min(0.2f)] public float stepMinReadSeconds = 2.0f;
    [Min(0f)] public float blackHoldSeconds = 1.2f;
    [Min(0f)] public float finalHoldSeconds = 2.5f;
}
