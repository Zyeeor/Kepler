using UnityEngine;

/// <summary>
/// 场景级单例基类（Kimi 评审整改 P3）：固化"场景级单例"生命周期模式——消灭三种并存写法：
///   (a) DDOL 常驻（GameManager/AudioManager/RunSession：不需要本基类）；
///   (b) 场景级 + OnDestroy 清 Instance（本基类固化）；
///   (c) 场景级 + 无清理（fake-null 残留：旧 PossessionManager 模式，已迁移）。
///
/// 语义：
///   - Awake：已有实例（含场景重载后 DDOL 残留）时销毁新对象——防重复注册；
///   - OnDestroy：Instance == this 时清空——防 fake-null 残留；
///   - 子类 Awake/OnDestroy 必须调用 base（override 时）。
/// 迁移注意：子类删除自己的 `public static X Instance` 字段（继承基类泛型静态字段，
/// 经子类名访问自动解析到 SceneSingleton&lt;T&gt;.Instance）。
/// </summary>
public abstract class SceneSingleton<T> : MonoBehaviour where T : SceneSingleton<T>
{
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = (T)this;
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
