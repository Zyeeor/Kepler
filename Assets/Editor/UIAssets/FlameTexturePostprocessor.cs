#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 血条火苗贴图（HealthBar_Flame.png）的导入设置。
///
/// 该图是带 alpha 的透明背景 PNG，必须：
///   * 以 Sprite (2D and UI) 导入，否则不能赋给 Image
///   * alphaSource = FromInput，否则透明背景会被当成不透明
///   * pivot 设为底部中心 —— 火苗从底部燃起，缩放窜动时底部不动
///   * 关闭 mipmap（UI 不需要，且会让小尺寸下发虚）
///
/// 仅作用于这一条路径，其它贴图完全不触碰。
/// </summary>
public class FlameTexturePostprocessor : AssetPostprocessor
{
    const string FLAME_PATH = "Assets/Art folder/UI/HealthBar_Flame.png";

    void OnPreprocessTexture()
    {
        if (assetPath != FLAME_PATH) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.spritePixelsPerUnit = 100f;

        // 这些字段在 TextureImporterSettings 上，不在 TextureImporter 上
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spriteGenerateFallbackPhysicsShape = false;
        ti.SetTextureSettings(settings);

        var platform = ti.GetDefaultPlatformTextureSettings();
        platform.maxTextureSize = 256;
        platform.format = TextureImporterFormat.Automatic;
        platform.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.SetPlatformTextureSettings(platform);
    }
}
#endif
