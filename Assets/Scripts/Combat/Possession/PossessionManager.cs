using System.Collections;
using UnityEngine;

/// <summary>
/// Scene-level possession orchestrator. Possession switches the shared PlayerController
/// from SoulActor to MonsterActor, while PossessionBehavior resolves right-click targets.
/// </summary>
public class PossessionManager : MonoBehaviour
{
    public static PossessionManager Instance { get; private set; }

    [Header("Possession")]
    public float possessFlySpeedMultiplier = 5f;
    public float possessYOffset = 0.5f;
    public float possessCooldown = 3f;
    public float minPossessTime = 1f;
    public float possessionDecayPercent = 0.05f;
    public float decayInterval = 1f;

    [Header("Bullet Time")]
    [Range(0.05f, 1f)] public float bulletTimeScale = 0.2f;
    [Min(0.01f)] public float bulletTimeDuration = 2f;

    public enum SwitchState { Idle, Flying, Possessing, Releasing }
    public SwitchState State { get; private set; }
    public MonsterActor CurrentBody { get; private set; }
    public float CooldownRemaining { get; private set; }

    public event System.Action<MonsterActor> OnPossessionStarted;
    public event System.Action OnPossessionEnded;
    public event System.Action<MonsterActor> OnBodyDiedWhilePossessing;

    private Coroutine flyRoutine;
    private Coroutine bulletTimeRoutine;
    private float possessStartTime;
    private float possessionDecayTimer;
    private SoulActor soul;
    private PossessionBehavior behavior;
    private MonsterActor reservedBody;
    private bool handlingGameOver;
    private bool ownsBulletTime;
    private float bulletTimeRestoreScale = 1f;
    private int lastPossessionInputFrame = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        State = SwitchState.Idle;
        soul = FindObjectOfType<SoulActor>();
        behavior = GetComponent<PossessionBehavior>();
        if (behavior == null) behavior = gameObject.AddComponent<PossessionBehavior>();
        behavior.Initialize(this);
    }

    void OnDisable()
    {
        if (State != SwitchState.Flying) return;

        if (flyRoutine != null)
        {
            StopCoroutine(flyRoutine);
            flyRoutine = null;
        }
        if (reservedBody != null) reservedBody.CancelPossessionReservation();
        reservedBody = null;
        if (soul == null) soul = FindObjectOfType<SoulActor>();
        if (soul != null)
        {
            soul.SetPossessionFlight(false);
            soul.SetSuppressed(false);
        }
        State = SwitchState.Idle;
        Debug.LogWarning("[Possession] Flight aborted because PossessionManager was disabled; soul control restored.");
    }

    void Update()
    {
        if (handlingGameOver && (GameManager.Instance == null || GameManager.Instance.currentState != GameManager.GameState.GameOver))
            handlingGameOver = false;

        if (CooldownRemaining > 0f) CooldownRemaining -= Time.deltaTime;

        if (State != SwitchState.Possessing || CurrentBody == null) return;
        if (CurrentBody.suppressPossessionDrain || MonsterActor.IsDamageImmune(CurrentBody)) return;

        possessionDecayTimer += Time.deltaTime;
        if (possessionDecayTimer < decayInterval) return;

        possessionDecayTimer -= decayInterval;
        float decayAmount = CurrentBody.maxHealth * possessionDecayPercent;
        CurrentBody.currentHealth -= decayAmount;
        if (CurrentBody.currentHealth > 0f) return;

        CurrentBody.currentHealth = 0f;
        Debug.Log("[Possession] Current possessed body expired from decay.");
        NotifyBodyDied();
    }

    private void ProcessPossessionInput()
    {
        if (PlayerController.IsGameplayInputBlocked) return;
        if (!Input.GetMouseButtonDown(1)) return;

        PlayerController controller = PlayerController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[PossessionInput] Ignored manager right-click: PlayerController is missing.");
            return;
        }

        TryRequestPossessFromInput(controller.GetMouseRay(), "PossessionManager");
    }

    public bool TryRequestPossessFromInput(Ray aimRay, string source)
    {
        if (lastPossessionInputFrame == Time.frameCount) return false;
        lastPossessionInputFrame = Time.frameCount;
        Debug.Log("[PossessionInput] Right-click received by " + source + ".");
        return TryRequestPossess(aimRay);
    }

    public bool TryRequestPossess(Ray aimRay)
    {
        if (behavior == null)
        {
            behavior = GetComponent<PossessionBehavior>();
            if (behavior == null) behavior = gameObject.AddComponent<PossessionBehavior>();
            behavior.Initialize(this);
            Debug.LogWarning("[Possession] Recreated missing PossessionBehavior.");
        }

        return behavior.TryBegin(aimRay);
    }

    public void RequestPossess(Ray aimRay)
    {
        TryRequestPossess(aimRay);
    }

    public bool CanStartPossession(out string reason)
    {
        if (handlingGameOver || (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.GameOver))
        {
            reason = "game is over";
            return false;
        }

        if (State == SwitchState.Flying || State == SwitchState.Releasing)
        {
            reason = "possession state is busy: " + State;
            return false;
        }

        if (State == SwitchState.Idle && CooldownRemaining > 0f)
        {
            reason = "possession cooldown remaining=" + CooldownRemaining.ToString("F2");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool ValidatePossessionTarget(MonsterActor target, out string reason)
    {
        if (target == null)
        {
            reason = "target has no MonsterActor";
            return false;
        }

        if (!target.CanBePossessed)
        {
            reason = "target is not in its downed possession window";
            return false;
        }

        if (target.isPossessed)
        {
            reason = "target is already possessed";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool BeginPossessionFlight(MonsterActor target)
    {
        if (!CanStartPossession(out string stateReason))
        {
            Debug.Log("[Possession] Flight rejected: " + stateReason);
            return false;
        }

        if (!ValidatePossessionTarget(target, out string targetReason))
        {
            Debug.Log("[Possession] Flight rejected: " + targetReason);
            return false;
        }

        if (soul == null) soul = FindObjectOfType<SoulActor>();
        if (soul == null)
        {
            Debug.LogWarning("[Possession] Flight rejected: SoulActor is missing.");
            return false;
        }

        if (!target.TryReserveForPossession())
        {
            Debug.Log("[Possession] Flight rejected: target reservation failed.");
            return false;
        }
        reservedBody = target;

        if (State == SwitchState.Possessing)
        {
            StopBulletTime();
            DetachCurrentBodyForSwitch();
        }

        State = SwitchState.Flying;
        soul.SetPossessionFlight(true);
        flyRoutine = StartCoroutine(FlyAndCommitRoutine(target));
        Debug.Log($"[Possession] Flight started: target='{target.displayName}', speedMultiplier={possessFlySpeedMultiplier:F1}");
        return true;
    }

    public void RequestRelease(bool force)
    {
        if (State != SwitchState.Possessing) return;
        if (!force && Time.time - possessStartTime < minPossessTime)
        {
            Debug.Log("[Possession] Release rejected: min possession time remaining=" + (minPossessTime - (Time.time - possessStartTime)).ToString("F2"));
            return;
        }

        CommitRelease(recycleBody: true, startCooldown: true);
    }

    /// <summary>
    /// Debug/test helper: instantly possess a living body, skipping downed-window validation and flight.
    /// </summary>
    public bool DebugForcePossess(MonsterActor target)
    {
        if (target == null)
        {
            Debug.LogWarning("[Possession] DebugForcePossess rejected: target is null.");
            return false;
        }

        if (handlingGameOver || (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.GameOver))
        {
            Debug.Log("[Possession] DebugForcePossess rejected: game is over.");
            return false;
        }

        if (soul == null) soul = FindObjectOfType<SoulActor>();
        if (soul == null)
        {
            Debug.LogWarning("[Possession] DebugForcePossess rejected: SoulActor is missing.");
            return false;
        }

        if (State == SwitchState.Flying)
        {
            if (flyRoutine != null)
            {
                StopCoroutine(flyRoutine);
                flyRoutine = null;
            }
            if (reservedBody != null) reservedBody.CancelPossessionReservation();
            reservedBody = null;
            soul.SetPossessionFlight(false);
            State = SwitchState.Idle;
        }

        if (State == SwitchState.Possessing)
        {
            if (CurrentBody == target) return true;
            StopBulletTime();
            DetachCurrentBodyForSwitch();
        }

        CooldownRemaining = 0f;
        CommitPossession(target);
        Debug.Log($"[Possession] DebugForcePossess committed: target='{target.displayName}'");
        return true;
    }

    public void TriggerBulletTime()
    {
        if (State != SwitchState.Possessing || CurrentBody == null) return;

        if (bulletTimeRoutine != null || ownsBulletTime) StopBulletTime();
        bulletTimeRoutine = StartCoroutine(BulletTimeRoutine());
    }

    private IEnumerator BulletTimeRoutine()
    {
        bulletTimeRestoreScale = Time.timeScale;
        ownsBulletTime = true;
        if (GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.BulletTime);
        Time.timeScale = bulletTimeScale;
        Debug.Log($"[Possession] Bullet time started: scale={bulletTimeScale:F2}, duration={bulletTimeDuration:F2}s");
        yield return new WaitForSecondsRealtime(bulletTimeDuration);

        if (ownsBulletTime && State == SwitchState.Possessing && !handlingGameOver)
        {
            ownsBulletTime = false;
            if (GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.Possessed);
            else Time.timeScale = bulletTimeRestoreScale;
        }

        bulletTimeRoutine = null;
        Debug.Log("[Possession] Bullet time ended.");
    }

    private IEnumerator FlyAndCommitRoutine(MonsterActor target)
    {
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.currentHealth = PlayerHealth.Instance.soulMaxHealth;
            PlayerHealth.Instance.UpdateHealthUI();
        }

        float flySpeed = (PlayerHealth.Instance != null ? PlayerHealth.Instance.SoulMoveSpeedForFly : 5f) * possessFlySpeedMultiplier;
        while (target != null && target.CanCompleteReservedPossession)
        {
            Vector3 targetPosition = target.transform.position;
            targetPosition.y = soul.transform.position.y;
            if (Vector3.Distance(soul.transform.position, targetPosition) <= 0.3f) break;

            Vector3 direction = targetPosition - soul.transform.position;
            soul.transform.position = Vector3.MoveTowards(soul.transform.position, targetPosition, flySpeed * Time.unscaledDeltaTime);
            if (direction.sqrMagnitude > 0.0001f) soul.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            yield return null;
        }

        flyRoutine = null;
        if (target == null || !target.CanCompleteReservedPossession)
        {
            if (reservedBody != null) reservedBody.CancelPossessionReservation();
            reservedBody = null;
            Debug.Log("[Possession] Flight cancelled: target was no longer a valid reserved corpse.");
            CancelFlightToSoul();
            yield break;
        }

        CommitPossession(target);
    }

    private void CommitPossession(MonsterActor target)
    {
        if (target == null) return;

        reservedBody = null;
        CurrentBody = target;
        State = SwitchState.Possessing;
        possessionDecayTimer = 0f;
        possessStartTime = Time.time;

        if (soul != null)
        {
            soul.SetPossessionFlight(false);
            soul.SetSuppressed(true);
            if (target is Enemy enemy && enemy.soulAnchorPoint != null)
                soul.AttachToPossessionAnchor(enemy.soulAnchorPoint);
            if (soul.Combat != null) soul.Combat.AddLooseTags(this, new[] { "State.Possession.Active", "State.Soul.Suppressed" });
        }
        else if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.SetSoulActive(false);
        }

        if (target.Combat != null) target.Combat.AddLooseTags(this, new[] { "State.Possession.Active", "State.Possession.Controlled" });
        target.OnPossessed();
        target.SetController(PlayerController.Instance);
        SetCameraTarget(target.transform);

        if (PlayerPassiveManager.Instance != null && target is Enemy)
            PlayerPassiveManager.Instance.OnEnemyPossessed(target as Enemy);

        if (PossessionHUD.Instance != null) PossessionHUD.Instance.Show(target);
        if (GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.Possessed);

        Debug.Log("[Possession] Possessed " + target.displayName);
        OnPossessionStarted?.Invoke(target);
    }

    private void DetachCurrentBodyForSwitch()
    {
        MonsterActor oldBody = CurrentBody;
        CurrentBody = null;
        if (oldBody != null)
        {
            if (oldBody.Combat != null) oldBody.Combat.RemoveLooseTags(this);
            oldBody.SetController(NullController.Instance);
            oldBody.OnUnpossessed();
            oldBody.BeginDisappearing();
        }

        if (soul != null)
        {
            soul.DetachFromPossessionAnchor();
            soul.SetSuppressed(false);
            if (oldBody != null) soul.PlaceInFreeSoulForm(oldBody.transform.position);
            if (soul.Combat != null) soul.Combat.RemoveLooseTags(this);
        }

        if (PossessionHUD.Instance != null) PossessionHUD.Instance.Hide();
        SetCameraTarget(soul != null ? soul.transform : null);
        if (GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.Soul);
    }

    private void CommitRelease(bool recycleBody, bool startCooldown)
    {
        MonsterActor oldBody = CurrentBody;
        CurrentBody = null;
        State = SwitchState.Releasing;
        StopBulletTime();

        if (soul != null && soul.Combat != null) soul.Combat.RemoveLooseTags(this);
        if (oldBody != null && oldBody.Combat != null) oldBody.Combat.RemoveLooseTags(this);

        if (oldBody != null)
        {
            oldBody.SetController(NullController.Instance);
            oldBody.OnUnpossessed();
            if (recycleBody) oldBody.BeginDisappearing();
        }

        if (soul != null)
        {
            soul.DetachFromPossessionAnchor();
            soul.PlaceInFreeSoulForm(oldBody != null ? oldBody.transform.position : soul.transform.position);
            soul.SetPossessionFlight(false);
            soul.SetSuppressed(false);
        }
        else if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.SetSoulActive(true);
        }

        SetCameraTarget(soul != null ? soul.transform : null);
        if (PossessionHUD.Instance != null) PossessionHUD.Instance.Hide();
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.maxHealth = PlayerHealth.Instance.soulMaxHealth;
            PlayerHealth.Instance.UpdateHealthUI();
        }
        if (!handlingGameOver && GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.Soul);

        State = SwitchState.Idle;
        if (startCooldown) CooldownRemaining = possessCooldown;
        Debug.Log("[Possession] Returned to soul form.");
        OnPossessionEnded?.Invoke();
    }

    private void CancelFlightToSoul()
    {
        if (reservedBody != null) reservedBody.CancelPossessionReservation();
        reservedBody = null;
        State = SwitchState.Idle;
        if (soul != null)
        {
            soul.SetPossessionFlight(false);
            soul.SetSuppressed(false);
        }
        SetCameraTarget(soul != null ? soul.transform : null);
        if (!handlingGameOver && GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.Soul);
    }

    private void StopBulletTime()
    {
        if (bulletTimeRoutine != null)
        {
            StopCoroutine(bulletTimeRoutine);
            bulletTimeRoutine = null;
        }

        if (ownsBulletTime)
        {
            if (GameManager.Instance != null && GameManager.Instance.currentState != GameManager.GameState.GameOver)
            {
                GameManager.Instance.SwitchState(State == SwitchState.Possessing
                    ? GameManager.GameState.Possessed
                    : GameManager.GameState.Soul);
            }
            else if (Mathf.Approximately(Time.timeScale, bulletTimeScale))
            {
                Time.timeScale = bulletTimeRestoreScale;
            }
        }
        ownsBulletTime = false;
    }

    private static void SetCameraTarget(Transform target)
    {
        if (CameraDirector.Instance != null) CameraDirector.Instance.Target = target;
    }

    public void NotifyBodyDied()
    {
        if (State != SwitchState.Possessing || CurrentBody == null) return;

        MonsterActor dead = CurrentBody;
        Debug.Log("[Possession] Possessed body died.");
        CommitRelease(recycleBody: true, startCooldown: true);
        OnBodyDiedWhilePossessing?.Invoke(dead);
    }

    public void OnGameOver()
    {
        handlingGameOver = true;
        if (flyRoutine != null)
        {
            StopCoroutine(flyRoutine);
            flyRoutine = null;
        }
        StopBulletTime();
        if (State == SwitchState.Possessing) CommitRelease(recycleBody: false, startCooldown: false);
        else if (State == SwitchState.Flying) CancelFlightToSoul();
        State = SwitchState.Idle;
    }
}
