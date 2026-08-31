using UnityEngine;

/// <summary>
/// 游戏内鼠标光标运行时应用器。启动时从 Resources/GameCursorSettings 加载配置，
/// 通过 Cursor.SetCursor 显式设置光标，保证 built（Standalone）版本可靠显示。
/// 支持 cursorScale 缩放：缩放后走软件光标（硬件光标不支持任意尺寸）。
/// </summary>
public static class GameCursorManager
{
    const string SettingsPath = "GameCursorSettings";

    static Texture2D cachedScaledTexture;

    /// <summary>应用配置的光标（纹理 + 热点 + 缩放 + 可见性）。找不到配置则回退为系统可见光标。</summary>
    public static void Apply()
    {
        GameCursorSettings settings = Resources.Load<GameCursorSettings>(SettingsPath);
        if (settings == null)
        {
            Debug.LogWarning("[GameCursor] 未找到 Resources/GameCursorSettings.asset，回退到系统可见光标（Player Settings 默认光标）。");
            Cursor.visible = true;
            return;
        }

        Cursor.visible = settings.visible;
        if (settings.cursorTexture == null)
            return;

        Texture2D texture = settings.cursorTexture;
        Vector2 hotspot = settings.hotspot;
        // 硬件光标（Auto）有尺寸上限（约 64x64，视平台而定），大图必须走软件光标，
        // 否则 Cursor.SetCursor 静默失败、光标不替换。
        CursorMode mode = texture.width > 64 || texture.height > 64
            ? CursorMode.ForceSoftware
            : CursorMode.Auto;

        float scale = Mathf.Max(0.01f, settings.cursorScale);
        if (Mathf.Abs(scale - 1f) > 0.001f)
        {
            int targetW = Mathf.Max(1, Mathf.RoundToInt(texture.width * scale));
            int targetH = Mathf.Max(1, Mathf.RoundToInt(texture.height * scale));
            if (cachedScaledTexture == null
                || cachedScaledTexture.width != targetW
                || cachedScaledTexture.height != targetH)
            {
                if (cachedScaledTexture != null) Object.Destroy(cachedScaledTexture);
                cachedScaledTexture = ScaleTexture(texture, targetW, targetH);
            }
            texture = cachedScaledTexture;
            hotspot *= scale;
            mode = CursorMode.ForceSoftware; // 硬件光标不支持任意尺寸，缩放后走软件光标
        }

        Cursor.SetCursor(texture, hotspot, mode);
        Debug.Log($"[GameCursor] 已应用游戏光标：{texture.name}，hotspot={hotspot}，scale={scale}，mode={mode}。");
    }

    /// <summary>把源纹理缩放到指定尺寸（经 RenderTexture 双线性重采样，不依赖源纹理 Read/Write）。</summary>
    static Texture2D ScaleTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }
}
