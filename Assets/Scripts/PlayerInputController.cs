using UnityEngine;

public class PlayerInputController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public LayerMask groundLayer = -1;

    private Rigidbody rb;
    private PlayerCombat combat;
    private PlayerHealth health;
    private Camera mainCamera;

    [HideInInspector] public Vector3 moveDirection;
    [HideInInspector] public Vector3 mouseWorldTarget;
    [HideInInspector] public bool skillQPressed;
    [HideInInspector] public bool skillWPressed;
    [HideInInspector] public bool skillEPressed;
    [HideInInspector] public bool skillRPressed;
    [HideInInspector] public bool interactPressed;

    private bool isControllingEnemy = false;
    private Enemy controlledEnemy;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        combat = GetComponent<PlayerCombat>();
        health = GetComponent<PlayerHealth>();
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (health == null) return;
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.GameOver) return;
        if (health.isFlyingToPossess) return;
        HandleSkillInput();
        HandleInteractInput();
        if (isControllingEnemy)
        {
            HandleEnemyMovement();
            UpdateEnemyMouseAim();
        }
        else if (!health.isPossessing) HandleMouseMovement();
    }

    void FixedUpdate()
    {
        if (health != null && health.isFlyingToPossess) return;
        if (isControllingEnemy)
        {
            if (controlledEnemy != null && controlledEnemy.rb != null)
            {
                if (moveDirection != Vector3.zero)
                {
                    float stepDist = controlledEnemy.moveSpeed * Time.fixedDeltaTime;
                    Vector3 targetPos = ApplySpherecast(controlledEnemy.rb.position, moveDirection, stepDist, 0.75f, 0.4f);
                    targetPos.y = controlledEnemy.rb.position.y;
                    controlledEnemy.rb.MovePosition(targetPos);
                }
                // Update animator speed for possessed enemy
                var anim = controlledEnemy.GetComponent<Animator>();
                if (anim != null) anim.SetFloat("Speed", moveDirection.magnitude * controlledEnemy.moveSpeed);
            }
        }
        else if (rb != null && moveDirection != Vector3.zero && !health.isPossessing)
        {
            float stepDist = moveSpeed * Time.fixedDeltaTime;
            // 玩家 capsule center y=0.9(在 PlayerInputController 自己的 root 上,所以用 0.9 而不是 0.75)
            Vector3 targetPos = ApplySpherecast(rb.position, moveDirection, stepDist, 0.9f, 0.4f);
            targetPos.y = rb.position.y;
            rb.MovePosition(targetPos);
        }
    }

    /// <summary>
    /// 手动 spherecast 预检测 — 替代 Kinematic Rigidbody + MovePosition 缺失的 CCD sweep。
    /// 避免玩家穿墙/穿柱/穿掩体。
    /// </summary>
    Vector3 ApplySpherecast(Vector3 origin, Vector3 dir, float stepDist, float castHeightOffset, float castRadius)
    {
        // 不和 Layer 8(Enemy)、Layer 9(Player) 自身检测,只关心环境
        int obstacleMask = ~((1 << 8) | (1 << 9));
        Vector3 castOrigin = origin + Vector3.up * castHeightOffset;
        if (Physics.SphereCast(castOrigin, castRadius, dir, out RaycastHit hit, stepDist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            stepDist = Mathf.Max(0f, hit.distance - 0.05f);
        }
        return origin + dir * stepDist;
    }

    // WASD movement (shared by soul and possessed states)
    Vector3 GetWASDDirection()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(h, 0, v);
        if (dir.magnitude > 1f) dir.Normalize();
        return dir;
    }

    void ApplyWASDMovement(Transform targetTransform)
    {
        Vector3 dir = GetWASDDirection();
        if (dir.magnitude > 0.1f)
        {
            moveDirection = dir;
            targetTransform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
        else
        {
            moveDirection = Vector3.zero;
        }
    }

    // Soul state: WASD movement
    void HandleMouseMovement()
    {
        ApplyWASDMovement(transform);
    }

    // Possessed: WASD drives the enemy
    void HandleEnemyMovement()
    {
        if (controlledEnemy == null) return;
        Vector3 dir = GetWASDDirection();
        if (dir.magnitude > 0.1f)
        {
            moveDirection = dir;
            controlledEnemy.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
        else
        {
            moveDirection = Vector3.zero;
        }
    }

    // Possessed: when not moving, enemy faces mouse cursor for skill direction
    void UpdateEnemyMouseAim()
    {
        if (controlledEnemy == null) return;
        if (moveDirection != Vector3.zero) return;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        Vector3 targetPoint;
        if (Physics.Raycast(ray, out hit, 100f, groundLayer))
        {
            targetPoint = hit.point;
        }
        else
        {
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            float dist;
            if (plane.Raycast(ray, out dist))
                targetPoint = ray.GetPoint(dist);
            else
                return;
        }
        Vector3 dir = targetPoint - controlledEnemy.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            controlledEnemy.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }

    void HandleSkillInput()
    {
        skillQPressed = Input.GetKeyDown(KeyCode.Q);
        skillWPressed = Input.GetKeyDown(KeyCode.W);
        skillEPressed = Input.GetKeyDown(KeyCode.E);
        skillRPressed = Input.GetKeyDown(KeyCode.Space);

        if (isControllingEnemy && controlledEnemy != null)
        {
            if (Input.GetMouseButtonDown(0)) controlledEnemy.PlayerTriggerBasicAttack();
            if (Input.GetMouseButtonDown(1)) controlledEnemy.PlayerTriggerSkill();
            if (skillRPressed && health != null) health.Unpossess();
        }
        else
        {
            if (combat != null)
            {
                // Left-click: trigger basic attack
                if (Input.GetMouseButtonDown(0)) combat.PlayerTriggerBasicAttack();
                // Right-click: trigger first skill (index 0 = SoulBullet)
                if (Input.GetMouseButtonDown(1)) combat.PlayerTriggerSkill(0);
                // E: also basic attack
                if (skillEPressed) combat.PlayerTriggerBasicAttack();
                // Q: trigger second skill (index 1 = GhostDash, if available)
                if (skillQPressed && combat.skillAbilities.Count > 1) combat.PlayerTriggerSkill(1);
            }
            if (skillRPressed) TryPossessDownedEnemy();
        }
    }

    void TryPossessDownedEnemy()
    {
        if (health == null) return;

        // Check possession cooldown
        if (health.possessCooldownTimer > 0f)
        {
            Debug.Log("[Possess] Cooldown: " + health.possessCooldownTimer.ToString("F1") + "s remaining");
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f))
        {
            var enemy = hit.collider.GetComponent<Enemy>();
            if (enemy != null && enemy.isDowned)
            {
                health.FlyAndPossess(enemy);
            }
            else if (enemy != null && !enemy.isDowned)
            {
                Debug.Log("Enemy is not downed yet, keep attacking!");
            }
        }
    }

    void HandleInteractInput()
    {
        interactPressed = Input.GetKeyDown(KeyCode.F);
        if (interactPressed)
        {
            if (health != null && health.isPossessing) health.Unpossess();
        }
    }

    public void OnPossessionStarted(Enemy enemy)
    {
        isControllingEnemy = true;
        controlledEnemy = enemy;
        moveDirection = Vector3.zero;
    }

    public void OnPossessionEnded()
    {
        isControllingEnemy = false;
        controlledEnemy = null;
        moveDirection = Vector3.zero;
    }
}
