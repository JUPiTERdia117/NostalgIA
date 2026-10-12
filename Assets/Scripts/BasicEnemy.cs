using UnityEngine;

/// <summary>
/// Enemigo básico que detecta al jugador, lo persigue y lo ataca en rango cuerpo a cuerpo.
/// Es vulnerable a impactos de proyectiles/objetos lanzados con la aspiradora.
/// </summary>
public class BasicEnemy : MonoBehaviour
{
    [Header("Detección y Movimiento")]
    [Tooltip("Distancia a la que el enemigo detecta y empieza a seguir al jugador.")]
    [SerializeField] private float detectionRange = 15f;
    [Tooltip("Velocidad de persecución.")]
    [SerializeField] private float moveSpeed = 3.2f;
    [Tooltip("Velocidad de giro.")]
    [SerializeField] private float rotationSpeed = 6f;
    [Tooltip("Distancia mínima para frenar antes de encimar al jugador.")]
    [SerializeField] private float stoppingDistance = 1.6f;

    [Header("Ataque")]
    [Tooltip("Distancia a la que puede atacar.")]
    [SerializeField] private float attackRange = 2.0f;
    [Tooltip("Daño por ataque.")]
    [SerializeField] private int attackDamage = 1;
    [Tooltip("Tiempo de enfriamiento entre ataques.")]
    [SerializeField] private float attackCooldown = 1.3f;
    [Tooltip("Fuerza con la que empuja al jugador al golpear.")]
    [SerializeField] private float attackKnockback = 6f;

    [Header("Vida y Vulnerabilidad")]
    [Tooltip("Puntos de salud del enemigo.")]
    [SerializeField] private int maxHealth = 2;
    [Tooltip("Velocidad mínima de impacto de un objeto para causarle daño.")]
    [SerializeField] private float impactThreshold = 3.5f;

    [Header("Efectos Visuales")]
    [SerializeField] private Transform visualModel;
    [SerializeField] private float hoverHeight = 0.5f;
    [SerializeField] private float hoverBobFrequency = 3f;
    [SerializeField] private float hoverBobAmplitude = 0.15f;

    private int currentHealth;
    private float lastAttackTime = -10f;
    private Transform playerTransform;
    private PlayerController playerController;
    private Rigidbody rb;
    private Vector3 initialPosition;
    private bool isChasing = false;
    private float bobTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.freezeRotation = true;
        }
        currentHealth = maxHealth;
        initialPosition = transform.position;
    }

    void Start()
    {
        FindPlayer();
    }

    void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        if (playerController != null && playerController.dead)
        {
            isChasing = false;
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= detectionRange)
        {
            isChasing = true;
        }
        else if (distanceToPlayer > detectionRange * 1.3f)
        {
            isChasing = false;
        }

        if (isChasing)
        {
            LookAtTarget(playerTransform.position);

            if (distanceToPlayer > stoppingDistance)
            {
                MoveTowards(playerTransform.position);
            }

            if (distanceToPlayer <= attackRange && Time.time - lastAttackTime >= attackCooldown)
            {
                AttackPlayer();
            }
        }

        // Efecto de flotación / paso
        if (visualModel != null)
        {
            bobTimer += Time.deltaTime * hoverBobFrequency;
            float bobOffset = Mathf.Sin(bobTimer) * hoverBobAmplitude;
            visualModel.localPosition = new Vector3(0, hoverHeight + bobOffset, 0);
        }
    }

    private void LookAtTarget(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position);
        direction.y = 0;
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }

    private void MoveTowards(Vector3 targetPos)
    {
        Vector3 moveDir = (targetPos - transform.position).normalized;
        moveDir.y = 0;

        Vector3 nextPos = transform.position + moveDir * moveSpeed * Time.deltaTime;

        // Mantener altura sobre el suelo con raycast
        if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 4f))
        {
            nextPos.y = hit.point.y;
        }

        if (rb != null)
        {
            rb.MovePosition(nextPos);
        }
        else
        {
            transform.position = nextPos;
        }
    }

    private void AttackPlayer()
    {
        lastAttackTime = Time.time;

        if (playerController != null && !playerController.dead)
        {
            playerController.TakeDamage(attackDamage);

            // Empuje
            CharacterController cc = playerController.GetComponent<CharacterController>();
            if (cc != null && !playerController.dead)
            {
                Vector3 push = (playerTransform.position - transform.position).normalized;
                push.y = 0.4f;
                cc.Move(push.normalized * attackKnockback * Time.deltaTime);
            }
        }
    }

    public void TakeHit(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // Desactivación o destrucción con feedback
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Detectar si fue impactado por un objeto arrojado con la aspiradora o con suficiente masa/velocidad
        Rigidbody otherRb = collision.rigidbody;
        bool isPoolable = collision.gameObject.layer == LayerMask.NameToLayer("Poolable") ||
                          collision.gameObject.GetComponentInParent<PoolableController>() != null;

        if (isPoolable || (otherRb != null && collision.relativeVelocity.magnitude >= impactThreshold))
        {
            TakeHit(1);

            // Pequeño retroceso al enemigo
            if (rb != null)
            {
                Vector3 knockDir = -collision.contacts[0].normal;
                knockDir.y = 0.3f;
                rb.AddForce(knockDir.normalized * 5f, ForceMode.Impulse);
            }
        }
    }
}
