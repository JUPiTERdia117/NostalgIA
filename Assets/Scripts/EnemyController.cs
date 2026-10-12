using UnityEngine;

/// <summary>
/// Controlador de IA para enemigos básicos en plataformas 3D.
/// Patrulla o flota en reposo, detecta al jugador, lo persigue, ataca cuerpo a cuerpo
/// y puede ser derrotado cuando el jugador le dispara objetos con la aspiradora.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyController : MonoBehaviour
{
    public enum EnemyState { Idle, Patrol, Chase, Attack, Hurt, Dead }

    [Header("Configuración de Estado y Detección")]
    [Tooltip("Estado actual del enemigo")]
    [SerializeField] private EnemyState currentState = EnemyState.Idle;
    [Tooltip("Radio en el que el enemigo detecta al jugador")]
    [SerializeField, Min(1f)] private float detectionRadius = 14f;
    [Tooltip("Rango de ataque cuerpo a cuerpo")]
    [SerializeField, Min(0.5f)] private float attackRange = 2.2f;
    [Tooltip("Distancia máxima de alejamiento de su punto de origen antes de regresar")]
    [SerializeField, Min(5f)] private float maxLeashDistance = 25f;

    [Header("Movimiento")]
    [Tooltip("Velocidad al perseguir al jugador")]
    [SerializeField, Min(0.5f)] private float chaseSpeed = 4.2f;
    [Tooltip("Velocidad de patrullaje")]
    [SerializeField, Min(0.5f)] private float patrolSpeed = 2f;
    [Tooltip("Velocidad de giro hacia el objetivo")]
    [SerializeField, Min(1f)] private float turnSpeed = 7f;
    [Tooltip("Flotación procedural (amplitud vertical)")]
    [SerializeField] private float hoverAmplitude = 0.35f;
    [Tooltip("Frecuencia de flotación")]
    [SerializeField] private float hoverFrequency = 2.5f;

    [Header("Combate y Daño")]
    [Tooltip("Vida máxima del enemigo")]
    [SerializeField] private int maxHealth = 2;
    [Tooltip("Daño infligido al jugador por golpe")]
    [SerializeField] private int attackDamage = 1;
    [Tooltip("Tiempo de preparación antes de asestar el golpe")]
    [SerializeField] private float attackWindup = 0.45f;
    [Tooltip("Tiempo de enfriamiento entre ataques")]
    [SerializeField] private float attackCooldown = 1.5f;
    [Tooltip("Fuerza de retroceso al ser impactado por un proyectil")]
    [SerializeField] private float knockbackResistance = 8f;

    [Header("Efectos Visuales")]
    [Tooltip("Renderer del núcleo/ojo para cambiar de color según estado")]
    [SerializeField] private Renderer eyeCoreRenderer;
    [Tooltip("Color de reposo / patrulla")]
    [SerializeField] private Color idleColor = new Color(0.2f, 0.8f, 1f); // Cyan
    [Tooltip("Color de alerta / persecución")]
    [SerializeField] private Color chaseColor = new Color(1f, 0.5f, 0f); // Naranja
    [Tooltip("Color de ataque")]
    [SerializeField] private Color attackColor = new Color(1f, 0.1f, 0.1f); // Rojo
    [Tooltip("Efecto de partículas o prefab a instanciar al morir (opcional)")]
    [SerializeField] private GameObject defeatFxPrefab;
    [Tooltip("Estrella o recompensa que suelta al ser derrotado (opcional)")]
    [SerializeField] private GameObject dropItemPrefab;

    // Variables internas
    private Transform playerTransform;
    private PlayerController playerController;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private int currentHealth;
    private float nextAttackTime;
    private float stateTimer;
    private bool isAttacking;
    private Rigidbody rb;
    private Collider enemyCollider;
    private Material eyeCoreMaterialInstance;
    private Vector3 moveDirection;

    void Awake()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();
        enemyCollider = GetComponent<Collider>();

        if (eyeCoreRenderer != null)
        {
            eyeCoreMaterialInstance = eyeCoreRenderer.material;
            SetCoreColor(idleColor);
        }
    }

    void Start()
    {
        FindPlayer();
    }

    void Update()
    {
        if (currentState == EnemyState.Dead) return;

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        // Si el jugador está muerto, volver a patrullar/reposo
        if (playerController != null && playerController.dead)
        {
            if (currentState != EnemyState.Idle && currentState != EnemyState.Patrol)
            {
                ChangeState(EnemyState.Patrol);
            }
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float distanceToSpawn = Vector3.Distance(transform.position, spawnPosition);

        // Flotación visual suave
        ApplyHoverMotion();

        switch (currentState)
        {
            case EnemyState.Idle:
            case EnemyState.Patrol:
                UpdatePatrolState(distanceToPlayer);
                break;

            case EnemyState.Chase:
                UpdateChaseState(distanceToPlayer, distanceToSpawn);
                break;

            case EnemyState.Attack:
                UpdateAttackState(distanceToPlayer);
                break;

            case EnemyState.Hurt:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    ChangeState(distanceToPlayer <= attackRange ? EnemyState.Attack : EnemyState.Chase);
                }
                break;
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    private void ApplyHoverMotion()
    {
        if (hoverAmplitude <= 0f) return;
        float hoverOffset = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude * Time.deltaTime;
        transform.position += Vector3.up * hoverOffset;
    }

    private void UpdatePatrolState(float distanceToPlayer)
    {
        // Si detecta al jugador y está vivo, empezar persecución
        if (distanceToPlayer <= detectionRadius && (playerController == null || !playerController.dead))
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        // Movimiento suave de patrullaje alrededor del punto de spawn
        float wanderRadius = 3f;
        Vector3 targetPatrol = spawnPosition + new Vector3(Mathf.Sin(Time.time * 0.5f) * wanderRadius, 0f, Mathf.Cos(Time.time * 0.5f) * wanderRadius);
        Vector3 dir = (targetPatrol - transform.position);
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * 0.5f * Time.deltaTime);
            MoveTowards(transform.position + dir.normalized * (patrolSpeed * Time.deltaTime));
        }
    }

    private void UpdateChaseState(float distanceToPlayer, float distanceToSpawn)
    {
        // Si el jugador se alejó demasiado o el enemigo salió del rango permitido
        if (distanceToPlayer > detectionRadius * 1.35f || distanceToSpawn > maxLeashDistance)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        // Si está en rango de ataque y se ha cumplido el cooldown
        if (distanceToPlayer <= attackRange && Time.time >= nextAttackTime)
        {
            ChangeState(EnemyState.Attack);
            return;
        }

        // Mirar y avanzar hacia el jugador
        Vector3 targetDirection = (playerTransform.position - transform.position);
        targetDirection.y = 0f;

        if (targetDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            MoveTowards(transform.position + targetDirection.normalized * (chaseSpeed * Time.deltaTime));
        }
    }

    private void UpdateAttackState(float distanceToPlayer)
    {
        // Mirar fijamente al jugador durante el ataque
        Vector3 dir = (playerTransform.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), turnSpeed * 1.5f * Time.deltaTime);
        }

        stateTimer += Time.deltaTime;

        if (!isAttacking && stateTimer >= attackWindup)
        {
            // Ejecutar golpe
            isAttacking = true;
            ExecuteAttack(distanceToPlayer);
        }

        if (stateTimer >= attackWindup + 0.35f)
        {
            // Fin de la animación de ataque
            isAttacking = false;
            nextAttackTime = Time.time + attackCooldown;
            ChangeState(EnemyState.Chase);
        }
    }

    private void ExecuteAttack(float currentDistance)
    {
        // Pequeño impulso de embestida
        if (rb != null && !rb.isKinematic)
        {
            Vector3 lunge = transform.forward * 4f;
            rb.AddForce(lunge, ForceMode.Impulse);
        }

        // Si el jugador sigue en rango, infligir daño
        if (currentDistance <= attackRange + 0.6f && playerTransform != null)
        {
            playerTransform.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void MoveTowards(Vector3 newPos)
    {
        if (rb != null && !rb.isKinematic)
        {
            rb.MovePosition(newPos);
        }
        else
        {
            transform.position = newPos;
        }
    }

    private void ChangeState(EnemyState newState)
    {
        currentState = newState;
        stateTimer = 0f;

        switch (newState)
        {
            case EnemyState.Idle:
            case EnemyState.Patrol:
                SetCoreColor(idleColor);
                break;
            case EnemyState.Chase:
                SetCoreColor(chaseColor);
                break;
            case EnemyState.Attack:
                SetCoreColor(attackColor);
                break;
            case EnemyState.Hurt:
                SetCoreColor(Color.white);
                break;
            case EnemyState.Dead:
                SetCoreColor(Color.gray);
                break;
        }
    }

    private void SetCoreColor(Color color)
    {
        if (eyeCoreMaterialInstance != null)
        {
            eyeCoreMaterialInstance.SetColor("_BaseColor", color);
            eyeCoreMaterialInstance.SetColor("_EmissionColor", color * 1.8f);
        }
    }

    /// <summary>
    /// Llamado cuando un objeto golpea al enemigo (por ejemplo, proyectiles lanzados con la aspiradora).
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (currentState == EnemyState.Dead) return;

        // Detectar si el objeto colisionado es de la capa Poolable (Capa 7) o tiene Rigidbody con velocidad
        bool isPoolable = collision.gameObject.layer == LayerMask.NameToLayer("Poolable");
        Rigidbody colRb = collision.rigidbody;
        bool hasSpeed = colRb != null && colRb.linearVelocity.magnitude > 2.5f;

        if (isPoolable || hasSpeed)
        {
            Vector3 hitDirection = (transform.position - collision.transform.position).normalized;
            TakeDamage(1, hitDirection);
        }
        else if (collision.gameObject.CompareTag("Player") && currentState == EnemyState.Chase)
        {
            // Daño por contacto si persigue al jugador
            if (Time.time >= nextAttackTime)
            {
                collision.gameObject.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
                nextAttackTime = Time.time + attackCooldown;
            }
        }
    }

    public void TakeDamage(int damageAmount, Vector3 hitDirection)
    {
        if (currentState == EnemyState.Dead) return;

        currentHealth -= damageAmount;
        ChangeState(EnemyState.Hurt);
        stateTimer = 0.25f;

        // Aplicar retroceso físico
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce((hitDirection + Vector3.up * 0.35f).normalized * knockbackResistance, ForceMode.Impulse);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        currentState = EnemyState.Dead;
        SetCoreColor(Color.black);

        // Desactivar collider para que no estorbe
        if (enemyCollider != null) enemyCollider.enabled = false;

        // Instanciar efectos de derrota si existen
        if (defeatFxPrefab != null)
        {
            Instantiate(defeatFxPrefab, transform.position, Quaternion.identity);
        }

        // Dropear item o recompensa
        if (dropItemPrefab != null)
        {
            Instantiate(dropItemPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }

        // Caída dramática con física antes de desaparecer
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);
        }

        Destroy(gameObject, 2.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
