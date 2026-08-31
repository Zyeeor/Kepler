using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 游戏内鼠标光标运行时应用器。启动时从 Resources/GameCursorSettings 加载配置，
/// 通过 Cursor.SetCursor 显式设置光标，保证 built（Standalone）版本可靠显示。
/// 支持 cursorScale 缩放：缩放后走软件光标（硬件光标不支持任意尺寸）。
/// </summary>
public static class GameCursorManager
{
    const string SettingsPath = "GameCursorSettings";
    const int WinSmCx = 13; // SM_CXCURSOR（GetSystemMetrics 索引值；典型返回值 32×DPI 缩放）
    const int WinSmCy = 14; // SM_CYCURSOR

    static Texture2D cachedScaledTexture;

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);

    /// <summary>系统光标基准尺寸（物理像素，已含系统 DPI 缩放）。非 Windows 平台回退 32x32。</summary>
    static Vector2Int GetSystemCursorSize()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        int w = GetSystemMetrics(WinSmCx);
        int h = GetSystemMetrics(WinSmCy);
        if (w > 0 && h > 0) return new Vector2Int(w, h);
#endif
        return new Vector2Int(32, 32);
    }

    /// <summary>应用配置的光标（纹理 + 热点 + 缩放 + 可见性）。找不到配置则回退为系统可见光标。</summary>
    public static void Apply()
    {
        if (Application.isEditor) return;

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
        CursorMode mode = CursorMode.Auto;

        // 光标统一对齐系统光标尺寸：与 Player Settings 默认光标（编辑器内表现）同源，
        // 不随打包分辨率 / 显示器分辨率变化；cursorScale 仅作为相对系统尺寸的微调乘数。
        Vector2Int sysCursor = GetSystemCursorSize();
        float fitScale = Mathf.Min((float)sysCursor.x / texture.width, (float)sysCursor.y / texture.height);
        float scale = Mathf.Max(0.01f, fitScale * Mathf.Max(0.01f, settings.cursorScale));
        int targetW = Mathf.Max(1, Mathf.RoundToInt(texture.width * scale));
        int targetH = Mathf.Max(1, Mathf.RoundToInt(texture.height * scale));
        if (targetW != texture.width || targetH != texture.height)
        {
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

    /// <summary>把源纹理分级缩小到指定尺寸，避免无 mipmap 的大幅一次性缩放产生锯齿。</summary>
    static Texture2D ScaleTexture(Texture2D source, int width, int height)
    {
        Texture sourceTexture = source;
        RenderTexture current = null;
        int currentWidth = source.width;
        int currentHeight = source.height;

        while (currentWidth > width || currentHeight > height)
        {
            int nextWidth = Mathf.Max(width, Mathf.CeilToInt(currentWidth * 0.5f));
            int nextHeight = Mathf.Max(height, Mathf.CeilToInt(currentHeight * 0.5f));
            RenderTexture next = RenderTexture.GetTemporary(nextWidth, nextHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            next.filterMode = FilterMode.Bilinear;
            Graphics.Blit(sourceTexture, next);

            if (current != null) RenderTexture.ReleaseTemporary(current);
            current = next;
            sourceTexture = current;
            currentWidth = nextWidth;
            currentHeight = nextHeight;
        }

        if (current == null)
        {
            current = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            current.filterMode = FilterMode.Bilinear;
            Graphics.Blit(sourceTexture, current);
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = current;
        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.filterMode = FilterMode.Bilinear;
        result.wrapMode = TextureWrapMode.Clamp;
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        RenderTexture.active = previous;
        if (current != null) RenderTexture.ReleaseTemporary(current);
        return result;
    }
}
