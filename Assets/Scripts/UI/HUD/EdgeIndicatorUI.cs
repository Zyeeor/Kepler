using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 屏幕边缘方向提示：
/// 视野外战斗怪（数据源 MonsterSpawner.OffscreenCombatMonsters，已含"战斗怪 ≤ 10 + 持续视野外"过滤）
/// 每只生成一个屏幕边缘指示器：箭头 Icon 指向目标方向 + 距离文本（米）。
///
/// 实现方式：UI 元素运行时动态创建（自建 ScreenSpaceOverlay Canvas + 指示器对象池），
/// 无需场景预摆，挂到任意场景物体即生效；与 AbilityCooldownUI / PossessionHUD 同属 UGUI + TMP 体系。
/// 箭头默认使用程序化三角图元（纯色），正式美术资产到位后在 Inspector 指定 arrowSprite 即可替换。
///
/// TODO(美术): arrowSprite 正式箭头贴图替换程序化三角图元；可加脉冲动画。
/// TODO(Phase 4 后续): 同方向多只怪的聚合合并显示（当前每只一个指示器，数据源上限 10 只，量小可接受）。
/// </summary>
public class EdgeIndicatorUI : MonoBehaviour
{
    [Header("样式")]
    [Tooltip("可选：正式箭头贴图；为空时使用程序化三角图元（TODO 美术替换）。")]
    public Sprite arrowSprite;
    [Tooltip("指示器离屏幕边缘的像素距离。")]
    [Min(0f)] public float edgePadding = 48f;
    [Tooltip("箭头图标边长（像素）。")]
    [Min(8f)] public float indicatorSize = 36f;
    [Tooltip("箭头颜色（威胁红，与战斗提示色系一致）。")]
    public Color arrowColor = new Color(1f, 0.35f, 0.25f, 0.95f);
    [Tooltip("是否显示距离文本（米）。")]
    public bool showDistance = true;

    /// <summary>指示器池上限：数据源上限 10 只，留 2 余量。</summary>
    const int MaxIndicators = 12;

    /// <summary>单个指示器：根节点 + 旋转箭头 + 距离文本。</summary>
    class Indicator
    {
        public RectTransform root;
        public RectTransform arrow;
        public TMP_Text distanceText;
    }

    readonly List<Indicator> pool = new List<Indicator>(MaxIndicators);
    Canvas canvas;
    RectTransform canvasRect;
    Camera mainCamera;
    Transform player;
    Sprite fallbackSprite;

    void Awake()
    {
        BuildCanvas();
        for (int i = 0; i < MaxIndicators; i++)
            pool.Add(BuildIndicator(i));
    }

    void Update(){
        var spawner = MonsterSpawner.Instance;
        var monsters = spawner != null ? spawner.OffscreenCombatMonsters : null;
        if (monsters == null || monsters.Count == 0)
        {
            HideAll();
            return;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null)
        {
            HideAll();
            return;
        }
        if (player == null && PlayerController.Instance != null)
            player = PlayerController.Instance.transform;
        Vector3 origin = player != null ? player.position : mainCamera.transform.position;

        Rect rect = canvasRect.rect;
        Vector2 center = rect.center;
        float halfW = rect.width * 0.5f - edgePadding;
        float halfH = rect.height * 0.5f - edgePadding;

        int used = 0;
        for (int i = 0; i < monsters.Count && used < pool.Count; i++)
        {
            var m = monsters[i];
            if (m == null) continue;
            var ind = pool[used++];

            // 方向：世界 → 视口偏移（相机背后的点视口坐标镜像，翻回真实方向；玩家转向实时更新，）
            Vector3 vp = mainCamera.WorldToViewportPoint(m.transform.position);
            var dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (vp.z < 0f) dir = -dir;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.up;

            // 视口方向（归一化分数）→ Canvas 局部方向（像素），与内缩边缘矩形求交得指示器位置
            var canvasDir = new Vector2(dir.x * rect.width, dir.y * rect.height).normalized;
            float tx = Mathf.Abs(canvasDir.x) > 1e-5f ? halfW / Mathf.Abs(canvasDir.x) : float.MaxValue;
            float ty = Mathf.Abs(canvasDir.y) > 1e-5f ? halfH / Mathf.Abs(canvasDir.y) : float.MaxValue;
            ind.root.anchoredPosition = center + canvasDir * Mathf.Min(tx, ty);

            // 箭头图元默认朝上，绕 Z 轴旋转指向目标方向
            float angle = Mathf.Atan2(canvasDir.y, canvasDir.x) * Mathf.Rad2Deg - 90f;
            ind.arrow.localEulerAngles = new Vector3(0f, 0f, angle);

            if (ind.distanceText != null)
            {
                ind.distanceText.enabled = showDistance;
                if (showDistance)
                    ind.distanceText.text = $"{Vector3.Distance(origin, m.transform.position):0}m";
            }

            ind.root.gameObject.SetActive(true);
        }
        for (int i = used; i < pool.Count; i++)
            if (pool[i].root.gameObject.activeSelf) pool[i].root.gameObject.SetActive(false);
    }

    void HideAll(){
        for (int i = 0; i < pool.Count; i++)
            if (pool[i].root.gameObject.activeSelf) pool[i].root.gameObject.SetActive(false);
    }

    // ── 运行时 UI 构建 ──

    /// <summary>自建 ScreenSpaceOverlay Canvas（HUD 层），无需场景预摆。</summary>
    void BuildCanvas(){
        var go = new GameObject("EdgeIndicatorCanvas", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // HUD 层：高于游戏世界，低于弹窗/菜单
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasRect = (RectTransform)go.transform;
    }

    /// <summary>构建单个指示器：根节点 + 箭头 Image + 距离 TMP_Text，初始隐藏入池。</summary>
    Indicator BuildIndicator(int index)
    {
        var rootGo = new GameObject($"EdgeIndicator_{index}", typeof(RectTransform));
        var root = (RectTransform)rootGo.transform;
        root.SetParent(canvasRect, false);
        root.sizeDelta = new Vector2(indicatorSize, indicatorSize);

        var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        var arrow = (RectTransform)arrowGo.transform;
        arrow.SetParent(root, false);
        arrow.anchorMin = Vector2.zero;
        arrow.anchorMax = Vector2.one;
        arrow.offsetMin = arrow.offsetMax = Vector2.zero;
        var img = arrowGo.GetComponent<Image>();
        img.sprite = arrowSprite != null ? arrowSprite : GetFallbackSprite();
        img.color = arrowColor;
        img.raycastTarget = false;

        var textGo = new GameObject("Distance", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRt = (RectTransform)textGo.transform;
        textRt.SetParent(root, false);
        textRt.anchorMin = new Vector2(0.5f, 0f);
        textRt.anchorMax = new Vector2(0.5f, 0f);
        textRt.pivot = new Vector2(0.5f, 1f);
        textRt.anchoredPosition = new Vector2(0f, -2f);
        textRt.sizeDelta = new Vector2(80f, 20f);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 14f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        rootGo.SetActive(false);
        return new Indicator { root = root, arrow = arrow, distanceText = tmp };
    }

    /// <summary>
    /// 程序化三角图元（顶点朝上的等腰三角形，纯白色，颜色由 Image.color 控制）。
    /// TODO(美术): 正式箭头贴图到位后经 arrowSprite 字段替换。
    /// </summary>
    Sprite GetFallbackSprite(){
        if (fallbackSprite != null) return fallbackSprite;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // 半宽随高度线性收窄：底部满宽 → 顶部收为顶点（Texture2D y=0 在底部）
            float halfWidth = Mathf.Lerp(size * 0.5f, 0f, (float)y / (size - 1));
            bool inside = Mathf.Abs(x - (size - 1) * 0.5f) <= halfWidth;
            tex.SetPixel(x, y, inside ? Color.white : clear);
        }
        tex.Apply();
        fallbackSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return fallbackSprite;
    }
}
