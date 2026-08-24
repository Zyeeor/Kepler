using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 运行时全局字体应用器 —— 场景加载后自动把 FontRegistry 字体应用到场景内全部 TMP 文本。
///
/// 与 FontRegistry.ApplyAllToActiveScene 的区别：
///   - 后者是设计期工具（Application.isPlaying 下禁用，防误改运行时状态）；
///   - 本组件是运行时执行者：常驻（DDOL），订阅场景加载事件，对新加载的场景统一套字体。
///
/// 挂载方式：无需场景挂载，首次访问 EnsureInstance() 自动创建（DontDestroyOnLoad）。
/// 若场景 TMP 已在 YAML 绑定字体，本应用器按"场景原字体 → 槽匹配 → 替换为注册表字体"覆盖，
/// 从而把全部场景文本收敛到 FontRegistry 一个资产控制。
/// </summary>
public class FontApplier : MonoBehaviour
{
    public static FontApplier Instance { get; private set; }

    public static FontApplier EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[FontApplier]");
        DontDestroyOnLoad(go);
        return go.AddComponent<FontApplier>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 只处理有根对象的正式场景（DontDestroyOnLoad 场景无根对象，跳过防重复）
        if (!scene.IsValid() || scene.rootCount == 0) return;
        var registry = FontRegistry.Instance;
        if (registry == null) return;
        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root != null) count += registry.ApplyToTree(root.transform);
        }
        Debug.Log($"[FontApplier] 场景 '{scene.name}' 字体统一应用完成（替换 {count} 个 TMP 文本）。");
    }
}
