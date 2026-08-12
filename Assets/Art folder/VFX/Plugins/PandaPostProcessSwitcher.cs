using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("VFX/Panda 后期材质切换器")]
public sealed class PandaPostProcessSwitcher : MonoBehaviour
{
    [SerializeField]
    private bool automaticallyFindEffects = true;

    [SerializeField]
    private List<PandaPostProcess> effects = new List<PandaPostProcess>();

    [SerializeField]
    private int activeEffectIndex = -1;

    private int lastAppliedIndex = int.MinValue;

    public IReadOnlyList<PandaPostProcess> Effects => effects;
    public int ActiveEffectIndex => activeEffectIndex;
    public bool AutomaticallyFindEffects => automaticallyFindEffects;

    private void Reset()
    {
        RefreshEffects();
        activeEffectIndex = FindLastEnabledEffectIndex();
        if (activeEffectIndex < 0 && effects.Count > 0)
        {
            activeEffectIndex = 0;
        }

        ApplyCurrentSelection();
    }

    private void OnEnable()
    {
        if (automaticallyFindEffects)
        {
            RefreshEffects();
        }

        ClampActiveIndex();
        ApplyCurrentSelection();
    }

    private void OnValidate()
    {
        if (automaticallyFindEffects)
        {
            RefreshEffects();
        }

        ClampActiveIndex();
        ApplyCurrentSelection();
    }

    private void Update()
    {
        if (lastAppliedIndex != activeEffectIndex || HasIncorrectEnabledState())
        {
            ApplyCurrentSelection();
        }
    }

    public void RefreshEffects()
    {
        PandaPostProcess selectedEffect = GetSelectedEffect();
        PandaPostProcess[] foundEffects = GetComponents<PandaPostProcess>();

        effects.Clear();
        effects.AddRange(foundEffects);

        if (selectedEffect != null)
        {
            activeEffectIndex = effects.IndexOf(selectedEffect);
        }

        ClampActiveIndex();
    }

    public void SetActiveEffect(int index)
    {
        activeEffectIndex = effects.Count == 0
            ? -1
            : Mathf.Clamp(index, -1, effects.Count - 1);
        ApplyCurrentSelection();
    }

    public void SelectNextEffect()
    {
        if (effects.Count == 0)
        {
            SetActiveEffect(-1);
            return;
        }

        SetActiveEffect(activeEffectIndex >= effects.Count - 1 ? 0 : activeEffectIndex + 1);
    }

    public void SelectPreviousEffect()
    {
        if (effects.Count == 0)
        {
            SetActiveEffect(-1);
            return;
        }

        SetActiveEffect(activeEffectIndex <= 0 ? effects.Count - 1 : activeEffectIndex - 1);
    }

    public void DisableAllEffects()
    {
        SetActiveEffect(-1);
    }

    public void UseCurrentlyEnabledEffect()
    {
        activeEffectIndex = FindLastEnabledEffectIndex();
        ApplyCurrentSelection();
    }

    public void ApplyCurrentSelection()
    {
        ClampActiveIndex();

        for (int i = 0; i < effects.Count; i++)
        {
            PandaPostProcess effect = effects[i];
            if (effect == null)
            {
                continue;
            }

            bool shouldEnable = i == activeEffectIndex;
            if (effect.enabled != shouldEnable)
            {
                effect.enabled = shouldEnable;
            }
        }

        lastAppliedIndex = activeEffectIndex;
    }

    private PandaPostProcess GetSelectedEffect()
    {
        return activeEffectIndex >= 0 && activeEffectIndex < effects.Count
            ? effects[activeEffectIndex]
            : null;
    }

    private int FindLastEnabledEffectIndex()
    {
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i] != null && effects[i].enabled)
            {
                return i;
            }
        }

        return -1;
    }

    private bool HasIncorrectEnabledState()
    {
        for (int i = 0; i < effects.Count; i++)
        {
            PandaPostProcess effect = effects[i];
            if (effect != null && effect.enabled != (i == activeEffectIndex))
            {
                return true;
            }
        }

        return false;
    }

    private void ClampActiveIndex()
    {
        activeEffectIndex = effects.Count == 0
            ? -1
            : Mathf.Clamp(activeEffectIndex, -1, effects.Count - 1);
    }
}
