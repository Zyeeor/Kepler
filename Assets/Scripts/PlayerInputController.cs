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
            if (controlledEnemy != null && controlledEnemy.rb != null && moveDirection != Vector3.zero)
            {
                Vector3 targetPos = controlledEnemy.rb.position + moveDirection * controlledEnemy.moveSpeed * Time.fixedDeltaTime;
                targetPos.y = controlledEnemy.rb.position.y;
                controlledEnemy.rb.MovePosition(targetPos);
            }
        }
        else if (rb != null && moveDirection != Vector3.zero && !health.isPossessing)
        {
            Vector3 targetPos = rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime;
            targetPos.y = rb.position.y;
            rb.MovePosition(targetPos);
        }
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
            else if (combat != null) combat.OnInteract();
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
