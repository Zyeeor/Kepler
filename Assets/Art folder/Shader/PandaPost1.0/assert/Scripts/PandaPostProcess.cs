using System.Collections.Generic;
using PandaTitle;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
public sealed class PandaPostProcess : MonoBehaviour
{
    private static readonly List<PandaPostProcess> ActiveEffects = new List<PandaPostProcess>();

    public Material PostProcessMat;

    [DisplayName("像素剔除程度")]
    [Range(0f, 1f)]
    public float StepFactor = 0.5f;

    [DisplayName("总透明度")]
    [Range(0f, 1f)]
    public float MainAlpha = 1f;

    [DisplayName("径向模糊强度")]
    [Range(0f, 1f)]
    public float BlurFactor;

    [DisplayName("UV拉伸强度")]
    [Range(0f, 4f)]
    public float LineUVScale;

    [FormerlySerializedAs("RedBlueFactor")]
    [DisplayName("色散强度")]
    [Range(0f, 1.5f)]
    public float Chromatic;

    [FormerlySerializedAs("ShakeFrequency")]
    [DisplayName("振频")]
    [Range(0f, 1f)]
    public float Frequency;

    [FormerlySerializedAs("ShakeAmplitude")]
    [DisplayName("振幅")]
    [Range(0f, 1f)]
    public float Amplitude;

    [DisplayName("黑边框宽度")]
    [Range(1f, 3f)]
    public float VignettePower = 1.5f;

    [DisplayName("黑边框强度")]
    [Range(0f, 3f)]
    public float VignetteScale = 1.5f;

    public static bool TryGetActive(out PandaPostProcess effect)
    {
        for (int i = ActiveEffects.Count - 1; i >= 0; i--)
        {
            PandaPostProcess candidate = ActiveEffects[i];
            if (candidate == null)
            {
                ActiveEffects.RemoveAt(i);
                continue;
            }

            Material material = candidate.PostProcessMat;
            if (candidate.isActiveAndEnabled &&
                material != null &&
                material.shader != null &&
                material.shader.isSupported)
            {
                effect = candidate;
                return true;
            }
        }

        effect = null;
        return false;
    }

    private void OnEnable()
    {
        if (!ActiveEffects.Contains(this))
        {
            ActiveEffects.Add(this);
        }

        ApplyMaterialProperties();
    }

    private void OnDisable()
    {
        ActiveEffects.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveEffects.Remove(this);
    }

    private void OnValidate()
    {
        ApplyMaterialProperties();
    }

    private void Update()
    {
        ApplyMaterialProperties();
    }

    private void ApplyMaterialProperties()
    {
        if (PostProcessMat == null)
        {
            return;
        }

        PostProcessMat.SetFloat("_StepFactorK", StepFactor);
        PostProcessMat.SetFloat("_BlurFactorK", BlurFactor);
        PostProcessMat.SetFloat("_LineUVScaleK", LineUVScale);
        PostProcessMat.SetFloat("_MainAlphaK", MainAlpha);
        PostProcessMat.SetFloat("_zhenpinK", Frequency);
        PostProcessMat.SetFloat("_zhenfuK", Amplitude);
        PostProcessMat.SetFloat("_RedBlueFactorK", Chromatic);
        PostProcessMat.SetFloat("_VignettePowerK", VignettePower);
        PostProcessMat.SetFloat("_VignetteScaleK", VignetteScale);
    }
}
