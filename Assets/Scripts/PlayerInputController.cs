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

        // Block all input during GameOver
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.GameOver) return;

        // Don't process while flying to possess
        if (health.isFlyingToPossess) return;

        // Process skill input always (even while possessing)
        HandleSkillInput();
        HandleInteractInput();

        // Movement only in soul form (not possessing)
        if (!health.isPossessing)
        {
            HandleMouseMovement();
        }
    }

    void FixedUpdate()
    {
        if (health != null && health.isFlyingToPossess) return;
        if (rb != null && moveDirection != Vector3.zero)
        {
            Vector3 targetPos = rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime;
            targetPos.y = rb.position.y;
            rb.MovePosition(targetPos);
        }
    }

    void HandleMouseMovement()
    {
        if (Input.GetMouseButton(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                mouseWorldTarget = hitPoint;
                Vector3 toTarget = hitPoint - transform.position;
                toTarget.y = 0;

                if (toTarget.magnitude > 0.1f)
                {
                    moveDirection = toTarget.normalized;
                    transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                }
                else moveDirection = Vector3.zero;
            }
        }
        else moveDirection = Vector3.zero;
    }

    void HandleSkillInput()
    {
        // Q = Basic Attack (Slash)
        skillQPressed = Input.GetKeyDown(KeyCode.Q);
        // W = Basic Attack (Slash)
        skillWPressed = Input.GetKeyDown(KeyCode.W);
        // E = Basic Attack (Slash)
        skillEPressed = Input.GetKeyDown(KeyCode.E);
        // R = Possess (click on downed enemy with mouse)
        skillRPressed = Input.GetKeyDown(KeyCode.R);

        if (combat != null)
        {
            if (skillQPressed) combat.SlashAttack();
            if (skillWPressed) combat.CastSkill(1);   // W = Soul Bullet (Projectile)
            if (skillEPressed) combat.SlashAttack();
        }

        if (skillRPressed)
        {
            // R key: Mouse-click on a downed enemy to possess
            // Works in both soul and possessed state
            TryPossessDownedEnemy();
        }
    }

    void TryPossessDownedEnemy()
    {
        if (health == null) return;

        // If currently possessing, unpossess first
        if (health.isPossessing)
        {
            // Look for a new downed enemy to possess
        }

        // Raycast from mouse position to find a downed enemy
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            var enemy = hit.collider.GetComponent<Enemy>();
            if (enemy != null && enemy.isDowned)
            {
                // Start flying to the downed enemy at 3x speed, then possess
                health.FlyAndPossess(enemy);
                Debug.Log("Flying to downed enemy: " + enemy.name);
            }
            else if (enemy != null && !enemy.isDowned)
            {
                Debug.Log("Enemy is not downed yet, keep attacking!");
            }
            else
            {
                Debug.Log("No downed enemy at mouse position");
            }
        }
        else
        {
            Debug.Log("No target at mouse position");
        }
    }

    void HandleInteractInput()
    {
        interactPressed = Input.GetKeyDown(KeyCode.F);
        if (interactPressed)
        {
            // F key: unpossess or leap
            if (health != null && health.isPossessing)
                health.Unpossess();
            else if (combat != null)
                combat.OnInteract();
        }
    }
}
