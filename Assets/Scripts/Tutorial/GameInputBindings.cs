using UnityEngine;

/// <summary>
/// 键位唯一真源（旧 Input Manager 下集中管理硬编码键位）。
/// 新增键位/改键一律在此处修改，PlayerController 与 UI（PossessionHUD/TutorialUI）读同一份映射，
/// 避免"代码改键、UI 文案忘改"的不一致。
/// 未来若迁移 InputSystem，仅需替换本类内部实现。
/// </summary>
public static class GameInputBindings
{
    // 键盘位
    public static readonly KeyCode Mobility = KeyCode.Space;   // 位移（灵魂闪避 / 怪位移）
    public static readonly KeyCode Skill3 = KeyCode.E;         // 子弹时间
    public static readonly KeyCode Release = KeyCode.F;        // 脱离附身
    // 鼠标位
    public const int MouseBasic = 0;    // 左键：普攻
    public const int MouseSkill2 = 1;   // 右键：附身怪技能
    public const int MouseSkill1 = 2;   // 中键：附身发起 / 换身


    /// <summary>按 CommandButtons 位返回中文键位显示名（UI 动态显示用）。</summary>
    public static string GlyphOf(CommandButtons button)
    {
        switch (button)
        {
            case CommandButtons.Basic: return "左键";
            case CommandButtons.Skill1: return "中键";
            case CommandButtons.Skill2: return "右键";
            case CommandButtons.Mobility: return "空格";
            case CommandButtons.Skill3: return Skill3.ToString();
            case CommandButtons.Release: return Release.ToString();
            default: return button.ToString();
        }
    }

    /// <summary>是否按下了某命令对应键（PlayerController.Tick 内部使用）。</summary>
    public static bool GetDown(CommandButtons button)
    {
        switch (button)
        {
            case CommandButtons.Basic: return Input.GetMouseButtonDown(MouseBasic);
            case CommandButtons.Skill1: return Input.GetMouseButtonDown(MouseSkill1);
            case CommandButtons.Skill2: return Input.GetMouseButtonDown(MouseSkill2);
            case CommandButtons.Mobility: return Input.GetKeyDown(Mobility);
            case CommandButtons.Skill3: return Input.GetKeyDown(Skill3);
            case CommandButtons.Release: return Input.GetKeyDown(Release);
            default: return false;
        }
    }

}
