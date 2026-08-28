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
        if (titleText != null) titleText.text = TextCatalog.Get("imprint.stack_suffix", GetTitle(sin), stacks);
        if (effectText != null) effectText.text = GetEffect(sin, stacks);
    }
    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    public static string GetTitle(SinType sin)
    {
        // 统一文本目录：七罪罪印名（imprint.title.*），文本只动资产不动代码
        switch (sin)
        {
            case SinType.Pride: return TextCatalog.Get("imprint.title.pride");
            case SinType.Wrath: return TextCatalog.Get("imprint.title.wrath");
            case SinType.Gluttony: return TextCatalog.Get("imprint.title.gluttony");
            case SinType.Greed: return TextCatalog.Get("imprint.title.greed");
            case SinType.Envy: return TextCatalog.Get("imprint.title.envy");
            case SinType.Lust: return TextCatalog.Get("imprint.title.lust");
            case SinType.Sloth: return TextCatalog.Get("imprint.title.sloth");
            default: return TextCatalog.Get("imprint.title.default");
        }
    }

    public static string GetEffect(SinType sin, int stacks)
    {
        // 统一文本目录：效果描述模板（imprint.effect.*，{0}/{1} 占位符），文本只动资产不动代码
        switch (sin)
        {
            case SinType.Pride:
                return TextCatalog.Get("imprint.effect.pride",
                    ((1f - PossessionImprintMath.PrideCooldownMultiplier(stacks)) * 100f).ToString("0.0"));
            case SinType.Wrath:
                return TextCatalog.Get("imprint.effect.wrath",
                    ((PossessionImprintMath.WrathDamageMultiplier(stacks) - 1f) * 100f).ToString("0"));
            case SinType.Gluttony:
                return TextCatalog.Get("imprint.effect.gluttony",
                    ((PossessionImprintMath.GluttonyHealthMultiplier(stacks) - 1f) * 100f).ToString("0"),
                    ((PossessionImprintMath.GluttonyScaleMultiplier(stacks) - 1f) * 100f).ToString("0"));
            case SinType.Greed:
                float progress = PossessionImprintMath.GreedProgressPerPossession(stacks);
                float fractionalProgress = progress - Mathf.Floor(progress);
                return TextCatalog.Get("imprint.effect.greed",
                    (progress * 100f).ToString("0"), (fractionalProgress * 100f).ToString("0"));
            case SinType.Envy:
                return TextCatalog.Get("imprint.effect.envy",
                    PossessionImprintMath.EnvyBulletTimeBonus(stacks).ToString("0.00"));
            case SinType.Lust:
                return TextCatalog.Get("imprint.effect.lust",
                    (PossessionImprintMath.LustLifestealMultiplier(stacks) * 100f).ToString("0"));
            case SinType.Sloth:
                return TextCatalog.Get("imprint.effect.sloth",
                    ((1f - PossessionImprintMath.SlothDrainMultiplier(stacks)) * 100f).ToString("0.0"));
            default:
                return string.Empty;
        }
    }
}
