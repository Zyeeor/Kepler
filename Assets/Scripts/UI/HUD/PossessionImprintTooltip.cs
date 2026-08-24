using UnityEngine;
using UnityEngine.UI;

public sealed class PossessionImprintTooltip : MonoBehaviour
{
    public GameObject panel;
    public Text titleText;
    public Text effectText;
    public void Show(SinType sin, int stacks)
    {
        if (panel != null) panel.SetActive(true);
        if (titleText != null) titleText.text = GetTitle(sin) + " · " + stacks + "层";
        if (effectText != null) effectText.text = GetEffect(sin, stacks);
    }
    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    public static string GetTitle(SinType sin)
    {
        switch (sin)
        {
            case SinType.Pride: return "傲慢罪印";
            case SinType.Wrath: return "愤怒罪印";
            case SinType.Gluttony: return "暴食罪印";
            case SinType.Greed: return "贪婪罪印";
            case SinType.Envy: return "嫉妒罪印";
            case SinType.Lust: return "色欲罪印";
            case SinType.Sloth: return "怠惰罪印";
            default: return "罪印";
        }
    }

    public static string GetEffect(SinType sin, int stacks)
    {
        switch (sin)
        {
            case SinType.Pride:
                return "技能冷却缩减 " + ((1f - PossessionImprintMath.PrideCooldownMultiplier(stacks)) * 100f).ToString("0.0") + "%";
            case SinType.Wrath:
                return "攻击伤害提升 " + ((PossessionImprintMath.WrathDamageMultiplier(stacks) - 1f) * 100f).ToString("0") + "%";
            case SinType.Gluttony:
                return "附身体生命提升 " + ((PossessionImprintMath.GluttonyHealthMultiplier(stacks) - 1f) * 100f).ToString("0")
                    + "%\n视觉体型提升 " + ((PossessionImprintMath.GluttonyScaleMultiplier(stacks) - 1f) * 100f).ToString("0")
                    + "%\n投掷物与技能命中范围同步放大";
            case SinType.Greed:
                float progress = PossessionImprintManager.Instance != null ? PossessionImprintManager.Instance.GreedBonusProgress : 0f;
                return "每次夺舍额外叠层进度 " + Mathf.Min(stacks * 5, 100) + "%\n当前进度 " + (progress * 100f).ToString("0") + "%";
            case SinType.Envy:
                return "子弹时间延长 " + PossessionImprintMath.EnvyBulletTimeBonus(stacks).ToString("0.00") + "秒";
            case SinType.Lust:
                return "攻击伤害转化吸血 " + (PossessionImprintMath.LustLifestealMultiplier(stacks) * 100f).ToString("0") + "%";
            case SinType.Sloth:
                return "附身与技能生命消耗降低 " + ((1f - PossessionImprintMath.SlothDrainMultiplier(stacks)) * 100f).ToString("0.0") + "%";
            default:
                return string.Empty;
        }
    }
}
