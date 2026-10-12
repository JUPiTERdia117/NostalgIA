using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo básico cuerpo a cuerpo: detecta al jugador, lo persigue y lo ataca al estar a corta distancia.
/// Puede recibir daño de objetos lanzados con la aspiradora.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class BasicEnemy : MonoBehaviour
{
    [Header("Estadísticas")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int currentHealth;

    [Header("Detección y Movimiento")]
    [Tooltip("Distancia a la que el enemigo detecta y empieza a seguir al jugador.")]
    [SerializeField] private float detectionRange = 18f;
    [Tooltip("Velocidad de movimiento al perseguir al jugador.")]
    [SerializeField] private float moveSpeed = 3.5f;
    [Tooltip("Velocidad de giro hacia el objetivo.")]
    [SerializeField] private float rotationSpeed = 8f;
    [Tooltip("Fuerza de gravedad aplicada al enemigo.")]
    [SerializeField] private float gravity = 15f;

    [Header("Ataque Cuerpo a Cuerpo")]
    [Tooltip("Distancia mínima para ejecutar el golpe cuerpo a cuerpo.")]
    [SerializeField] private float attackRange = 1.8f;
    [Tooltip("Tiempo de recarga entre ataques en segundos.")]
    [SerializeField] private float attackCooldown = 1.2f;
    [Tooltip("Daño infligido al jugador por cada ataque.")]
    [SerializeField] private int attackDamage = 1;

    [Header("Daño por Impactos")]
    [Tooltip("Velocidad mínima que debe llevar un objeto lanzado para hacerle daño al enemigo.")]
    [SerializeField] private float minImpactVelocity = 3f;

    [Header("Feedback Visual")]
    [SerializeField] private Renderer enemyRenderer;
    [SerializeField] private Color damageFlashColor = Color.red;

    private CharacterController controller;
    private PlayerController player;
    private Transform playerTransform;
    private float nextAttackTime = 0f;
    private float verticalVelocity = 0f;
    private Color originalColor = Color.white;
    private bool isFlashing = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        currentHealth = maxHealth;

        if (enemyRenderer == null)
            enemyRenderer = GetComponentInChildren<Renderer>();

        if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            originalColor = enemyRenderer.material.color;
        }
        else if (enemyRenderer != null && enemyRenderer.material.HasProperty("_BaseColor"))
        {
            originalColor = enemyRenderer.material.GetColor("_BaseColor");
        }
    }

    void Start()
    {
        FindPlayer();
    }

    void Update()
    {
        if (player == null || playerTransform == null)
        {
            FindPlayer();
            return;
        }

        if (player.dead) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Aplica gravedad continua
        if (controller.isGrounded)
        {
            verticalVelocity = -1f;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        // Si el jugador está dentro del rango de detección
        if (distanceToPlayer <= detectionRange)
        {
            LookAtPlayer();

            if (distanceToPlayer > attackRange)
            {
                // Perseguir al jugador
                Vector3 moveDir = (playerTransform.position - transform.position);
                moveDir.y = 0;
                moveDir.Normalize();

                Vector3 motion = moveDir * moveSpeed;
                motion.y = verticalVelocity;
                controller.Move(motion * Time.deltaTime);
            }
            else
            {
                // En rango de ataque
                Vector3 motion = Vector3.up * verticalVelocity;
                controller.Move(motion * Time.deltaTime);

                if (Time.time >= nextAttackTime)
                {
                    PerformMeleeAttack();
                }
            }
        }
        else
        {
            // Fuera de rango: solo gravedad
            controller.Move(new Vector3(0, verticalVelocity, 0) * Time.deltaTime);
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<PlayerController>();
            playerTransform = playerObj.transform;
        }
    }

    private void LookAtPlayer()
    {
        Vector3 targetPos = playerTransform.position;
        targetPos.y = transform.position.y;
        Vector3 direction = (targetPos - transform.position).normalized;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void PerformMeleeAttack()
    {
        nextAttackTime = Time.time + attackCooldown;

        // Infligir daño al jugador
        if (player != null && !player.dead)
        {
            player.TakeDamage(attackDamage);
            StartCoroutine(AttackVisualFeedback());
        }
    }

    private IEnumerator AttackVisualFeedback()
    {
        // Pequeño impulso visual / retroceso
        Vector3 originalScale = transform.localScale;
        transform.localScale = originalScale * 1.15f;
        yield return new WaitForSeconds(0.12f);
        transform.localScale = originalScale;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        if (!isFlashing && gameObject.activeInHierarchy)
        {
            StartCoroutine(FlashDamage());
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator FlashDamage()
    {
        isFlashing = true;
        if (enemyRenderer != null)
        {
            SetColor(damageFlashColor);
            yield return new WaitForSeconds(0.15f);
            SetColor(originalColor);
        }
        isFlashing = false;
    }

    private void SetColor(Color col)
    {
        if (enemyRenderer == null) return;
        if (enemyRenderer.material.HasProperty("_Color"))
            enemyRenderer.material.color = col;
        else if (enemyRenderer.material.HasProperty("_BaseColor"))
            enemyRenderer.material.SetColor("_BaseColor", col);
    }

    private void Die()
    {
        // Efecto simple de derrota: desactiva o destruye
        Destroy(gameObject);
    }

    // Detectar impactos de proyectiles / objetos físicos lanzados con la aspiradora
    void OnCollisionEnter(Collision collision)
    {
        CheckImpactDamage(collision.gameObject, collision.relativeVelocity.magnitude);
    }

    void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
        {
            CheckImpactDamage(other.gameObject, rb.linearVelocity.magnitude);
        }
    }

    private void CheckImpactDamage(GameObject sourceObject, float velocityMagnitude)
    {
        // Si es un objeto de la capa Poolable o tiene Rigidbody y suficiente velocidad
        PoolableController poolable = sourceObject.GetComponentInParent<PoolableController>();
        Rigidbody rb = sourceObject.GetComponentInParent<Rigidbody>();

        bool isPoolable = poolable != null || sourceObject.layer == LayerMask.NameToLayer("Poolable");
        bool hasSpeed = velocityMagnitude >= minImpactVelocity;

        if (isPoolable || hasSpeed)
        {
            int damage = 1;
            if (velocityMagnitude > 15f) damage = 2;
            TakeDamage(damage);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
