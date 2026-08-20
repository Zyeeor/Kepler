using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 主菜单灵魂展示模式（正规入口）。
/// 效果：打完一局返回主菜单时，玩家灵魂（主角）常驻到主菜单场景，可在背景后移动。
/// 触发：UIManager.bringSoulToMainMenu 开关开启后，回主菜单时调用 EnterShowcase()。
/// 结束：MainMenuController 开始/继续游戏时调用 ExitShowcase()，展示灵魂随主菜单卸载销毁。
/// 标记作用：GameManager.OnSceneLoaded 的 DDOL 清理逻辑据此区分"展示"与"bug 残留"。
/// </summary>
public class SoulMenuShowcase : MonoBehaviour
{
    /// <summary>
    /// 主菜单灵魂展示全局开关（跨场景桥接）。
    /// 由 UIManager.Awake 同步其 bringSoulToMainMenu 字段（UIManager 仅存在于对局场景）。
    /// 关闭时：主菜单不创建原生展示灵魂，对局结束也不带回灵魂。
    /// </summary>
    public static bool GlobalEnabled = true;

    [Tooltip("是否在进入主菜单后把灵魂摆到 showcasePosition（关闭则保持对局结束时的原位）。")]
    public bool snapToShowcasePosition = false;
    [Tooltip("主菜单中的展示位置（世界坐标，仅 snapToShowcasePosition 开启时生效）。")]
    public Vector3 showcasePosition = new Vector3(0f, 1f, 8f);

    PlayerHealth health;

    /// <summary>把玩家灵魂带入主菜单展示模式（DDOL + 禁自然衰减）。附身/飞行中调用会被拒绝。</summary>
    public static bool EnterShowcase()
    {
        if (!GlobalEnabled) return false;
        var pc = PlayerController.Instance;
        if (pc == null)
        {
            Debug.LogWarning("[SoulMenuShowcase] 未找到玩家对象，无法进入展示模式。");
            return false;
        }
        if (pc.GetComponent<SoulMenuShowcase>() != null) return true; // 已处于展示模式

        // 附身中不展示：锚点怪将随对局场景卸载，灵魂挂在上面会悬空（半途展示状态不可靠）
        var pm = PossessionManager.Instance;
        if (pm != null && pm.State != PossessionManager.SwitchState.Idle)
        {
            Debug.LogWarning($"[SoulMenuShowcase] 附身状态 {pm.State} 中，跳过主菜单展示。");
            return false;
        }

        // 脱离附身锚点（正常情况下此时已脱离；兜底确保不挂在对局场景对象下）
        var soul = pc.GetComponent<SoulActor>();
        if (soul != null) soul.DetachFromPossessionAnchor();

        var showcase = pc.gameObject.AddComponent<SoulMenuShowcase>();
        showcase.Configure();
        DontDestroyOnLoad(pc.gameObject);
        Debug.Log("[SoulMenuShowcase] 玩家灵魂进入主菜单展示模式。");
        return true;
    }

    /// <summary>
    /// 在主菜单场景内创建原生展示灵魂（游戏启动直进主菜单时使用，无需 DDOL）。
    /// 已有灵魂（对局带回的 DDOL 展示）时不再创建，保证主菜单只有唯一展示灵魂。
    /// </summary>
    public static bool SpawnNativeShowcase(GameObject playerPrefab, Vector3 position)
    {
        if (!GlobalEnabled)
        {
            Debug.Log("[SoulMenuShowcase] 全局开关关闭，跳过主菜单原生展示。");
            return false;
        }
        if (playerPrefab == null)
        {
            Debug.LogWarning("[SoulMenuShowcase] 未配置展示灵魂 prefab，跳过主菜单原生展示。");
            return false;
        }
        // 已有灵魂（含对局带回的 DDOL 展示）→ 不重复创建，保持唯一
        if (PlayerController.Instance != null) return false;

        var go = Instantiate(playerPrefab);
        // 防御：prefab 引用指错（如指向子物体/空壳）时实例化对象缺 SoulActor，静默产生"空壳灵魂"
        if (go.GetComponent<SoulActor>() == null)
        {
            Debug.LogError("[SoulMenuShowcase] soulShowcasePrefab 引用无效：实例化对象上没有 SoulActor。请把 MainMenuController.soulShowcasePrefab 指向 Player.prefab 根对象。");
            Destroy(go);
            return false;
        }
        go.name = "Player";
        Vector3 pos = position;
        pos.y = 1f; // 灵魂悬浮高度固定 1，出生位 Y 对齐
        go.transform.position = pos;
        go.transform.rotation = Quaternion.identity;

        var showcase = go.GetComponent<SoulMenuShowcase>();
        if (showcase == null) showcase = go.AddComponent<SoulMenuShowcase>();
        showcase.Configure();
        Debug.Log("[SoulMenuShowcase] 主菜单原生展示灵魂已创建（随主菜单场景存活，开始游戏时随场景卸载销毁）。");
        return true;
    }

    /// <summary>退出展示模式：展示灵魂移回当前活动场景（主菜单），随场景卸载自然销毁。</summary>
    public static void ExitShowcase()
    {
        var pc = PlayerController.Instance;
        if (pc == null) return;
        if (pc.GetComponent<SoulMenuShowcase>() == null) return;

        // 移回活动场景再销毁：若直接 Destroy（DDOL 对象延迟到帧末执行），
        // 新对局场景对象 Awake 会先于销毁执行，静态 Instance 竞争导致双 Player/被动管理器误删。
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && pc.gameObject.scene.name != active.name)
            SceneManager.MoveGameObjectToScene(pc.gameObject, active);
        Destroy(pc.gameObject);
        Debug.Log("[SoulMenuShowcase] 退出展示模式，展示灵魂随主菜单卸载销毁。");
    }

    public void Configure()
    {
        health = GetComponent<PlayerHealth>();
        // 禁用自然衰减：否则主菜单每秒扣血→死亡循环刷日志
        if (health != null) health.enabled = false;
        // 移动保留（PlayerController 输入层在主菜单无屏蔽来源 → 天然可动，保留原效果手感）
    }

    void Start()
    {
        // 本组件随 DDOL 提升后、场景切换完成后执行（此时已进入主菜单）
        if (snapToShowcasePosition)
        {
            Vector3 pos = showcasePosition;
            pos.y = 1f; // 灵魂悬浮高度由 SoulActor 固定为 1，展示位 Y 对齐该高度
            transform.position = pos;
        }
    }
}
