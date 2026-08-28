using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 卡牌选卡宝石。生命周期：掉落（可选）→ 待拾取 → 吸附（飘向玩家 + 缩小消失）→ 打开选卡会话。
///
/// 玩家控制的灵魂/躯体进入拾取半径后，先播放吸附动画，动画结束才由 CardManager 打开一次选卡会话。
/// 宝石本身只保存本次 Offer 的参数，不直接依赖 CoreChoiceUI，避免不同触发方绕过统一拾取入口。
///
/// 互斥：同一时刻只允许一颗宝石进入拾取流程（飞行中或选卡中）。
/// 由 CardManager 的单例闸门统一裁决，见 CardManager.IsCardOfferGemBusy / OccupyCardOfferGem。
/// </summary>
[DisallowMultipleComponent]
public sealed class CardChoiceGemPickup : MonoBehaviour
{
    CardManager owner;
    Action onChoiceCompleted;
    float pickupRadius = 1.25f;
    Vector3 originalScale = Vector3.one;

    public bool IsCollected { get; private set; }
    public bool IsChoiceCompleted { get; private set; }
    public bool DoublePick { get; private set; }
    public bool KeepPicks { get; private set; }
    public int WaveIndex { get; private set; }
    public CardOfferGemSource Source { get; private set; }

    /// <summary>是否正在播放"从掉落点散落到周围"的掉落动画（此时不可拾取）。</summary>
    public bool IsDropping { get; private set; }

    /// <summary>是否正在播放"飘向玩家 + 缩小消失"的吸附动画（此时尚未打开选卡界面）。</summary>
    public bool IsFlying { get; private set; }

    internal void Initialize(CardManager manager, bool doublePick, bool keepPicks, int waveIndex,
        CardOfferGemSource source, float radius, Action completed)
    {
        owner = manager;
        DoublePick = doublePick;
        KeepPicks = keepPicks;
        WaveIndex = waveIndex;
        Source = source;
        pickupRadius = Mathf.Max(0.25f, radius);
        onChoiceCompleted = completed;
        IsCollected = false;
        IsChoiceCompleted = false;
        IsDropping = false;
        IsFlying = false;
        originalScale = transform.localScale;
        if (originalScale == Vector3.zero) originalScale = Vector3.one;
        EnsurePickupCollider();
    }

    void Update()
    {
        if (owner == null || IsCollected || IsChoiceCompleted || IsFlying) return;
        // 掉落动画播完（落点确定）后才允许拾取，避免"还在空中就被吸走"。
        if (IsDropping) return;
        // 已有宝石在飞/在选卡 → 本颗原地等待，必须等上一颗选完卡释放闸门。
        if (owner.IsCardOfferGemBusy()) return;
        // 只挡"暂停"域（选卡弹窗/暂停菜单），不挡子弹时间(BulletTime)/顿帧(HitStop)：
        // 那些域下玩家仍可移动，宝石理应能拾取。
        if (TimeScaleManager.IsDomainActive(TimeDomain.Pause)) return;
        if (!owner.IsPlayerWithinPickupRadius(transform.position, pickupRadius)) return;

        owner.TryCollectCardOfferGem(this);
    }

    void EnsurePickupCollider()
    {
        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger == null) trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = pickupRadius;
    }

    /// <summary>
    /// 播放掉落动画：从掉落点（如精英怪死亡位置）弹射到周围散落点，抛物线 + 落地小反弹。
    /// 动画期间不可拾取，落地后回调通知（落定即可被拾取）。
    /// </summary>
    internal void StartDrop(Vector3 from, Vector3 to, float groundY, Action onDropFinished)
    {
        if (IsChoiceCompleted) return;
        IsDropping = true;

        // 掉落途中不参与碰撞检测，但可以看见。
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = false;

        transform.position = from;
        transform.localScale = originalScale;
        StartCoroutine(DropRoutine(from, to, groundY, onDropFinished));
    }

    IEnumerator DropRoutine(Vector3 from, Vector3 to, float groundY, Action onDropFinished)
    {
        float forwardSpeed = owner != null ? owner.cardOfferGemDropForwardSpeed : 3.5f;
        float upSpeed = owner != null ? owner.cardOfferGemDropUpSpeed : 4f;
        float gravity = owner != null ? owner.cardOfferGemDropGravity : 18f;

        Vector3 flat = to - from;
        flat.y = 0f;
        float distance = flat.magnitude;
        Vector3 flatDir = distance > 0.0001f ? flat / distance : Vector3.zero;

        // 保证至少有一小段滞空（原地掉落也有向上抛的观感），且飞行时间不吃 0 除。
        float duration = Mathf.Max(0.18f, distance / Mathf.Max(0.5f, forwardSpeed));

        Vector3 velocity = flatDir * forwardSpeed + Vector3.up * upSpeed;
        Vector3 position = from;
        int bounce = 0;

        while (true)
        {
            // 掉落动画属于世界表现，跟随游戏时间（暂停时冻结，缩放/顿帧时同步慢放）。
            float dt = Time.deltaTime;
            velocity.y -= gravity * dt;
            position += velocity * dt;

            if (position.y <= groundY)
            {
                position.y = groundY;
                if (bounce >= 1 || Mathf.Abs(velocity.y) < 0.75f) break;

                // 一次小反弹：水平衰减、垂直按恢复系数回弹。
                bounce++;
                velocity.x *= 0.45f;
                velocity.z *= 0.45f;
                velocity.y = Mathf.Abs(velocity.y) * 0.32f;
            }

            transform.position = position;
            yield return null;
            if (IsChoiceCompleted) yield break;
        }

        transform.position = new Vector3(position.x, groundY, position.z);
        transform.localScale = originalScale;
        IsDropping = false;
        EnsurePickupCollider();
        onDropFinished?.Invoke();
    }

    /// <summary>
    /// 开始吸附：先关碰撞，再播"飘向玩家 + 缩小"动画；动画结束由回调交给 CardManager 开弹窗。
    /// </summary>
    internal void StartAttract(Action onAttractFinished)
    {
        if (IsCollected || IsChoiceCompleted || IsFlying || IsDropping) return;
        IsFlying = true;
        IsCollected = true;

        // 关卡掉动画期间不再参与碰撞检测，但保持可见，否则动画看不到。
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = false;

        if (owner != null && owner.TryGetPlayerAnchorPosition(out Vector3 anchor))
            StartCoroutine(AttractRoutine(anchor, onAttractFinished));
        else
            FinishAttract(onAttractFinished);   // 拿不到玩家位置时直接收尾，不要卡住流程
    }

    IEnumerator AttractRoutine(Vector3 startAnchor, Action onAttractFinished)
    {
        Vector3 start = transform.position;
        float duration = owner != null ? owner.cardOfferGemAttractSeconds : 0.35f;
        float targetHeight = owner != null ? owner.cardOfferGemAttractHeight : 0.8f;
        float endScale = owner != null ? owner.cardOfferGemAttractEndScale : 0f;
        Vector3 anchor = startAnchor;

        // 用 unscaledDeltaTime：吸附动画不应被本局残留的 Pause 时间域拖慢/冻住。
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration > 0.0001f ? Mathf.Clamp01(elapsed / duration) : 1f;

            // 玩家可能边抢宝石边移动，实时跟新锚点。
            if (owner != null && owner.TryGetPlayerAnchorPosition(out Vector3 current)) anchor = current;

            Vector3 target = anchor + new Vector3(0f, targetHeight, 0f);
            // easeOutCubic：起步快、末尾贴身减速，观感比线性更"吸"。
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(start, target, eased);
            transform.localScale = originalScale * Mathf.Lerp(1f, endScale, eased);

            yield return null;
            if (IsChoiceCompleted) yield break;
        }

        FinishAttract(onAttractFinished);
    }

    void FinishAttract(Action onAttractFinished)
    {
        IsFlying = false;
        // 缩小到消失：即便外部把结束时缩放配成了非 0，也保证宝石不再留在场上。
        transform.localScale = Vector3.zero;
        onAttractFinished?.Invoke();
    }

    internal void MarkCollected()
    {
        if (IsChoiceCompleted) return;
        IsCollected = true;
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = false;
        gameObject.SetActive(false);
    }

    /// <summary>弹窗未能打开时回退：恢复宝石，让玩家可以再拾取一次。</summary>
    internal void CancelCollection()
    {
        if (IsChoiceCompleted) return;
        IsCollected = false;
        IsFlying = false;
        transform.localScale = originalScale != Vector3.zero ? originalScale : Vector3.one;
        gameObject.SetActive(true);
        EnsurePickupCollider();
    }

    internal void CompleteChoice()
    {
        if (IsChoiceCompleted) return;
        IsChoiceCompleted = true;
        Action callback = onChoiceCompleted;
        onChoiceCompleted = null;
        callback?.Invoke();
        Destroy(gameObject);
    }
}

public enum CardOfferGemSource
{
    Opening = 0,
    Wave = 1,
    Elite = 2,
    Debug = 3,
}
