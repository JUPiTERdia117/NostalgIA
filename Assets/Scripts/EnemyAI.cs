using UnityEngine;

/// <summary>
/// Enemigo básico que patrulla, detecta al jugador, lo persigue y lo ataca.
/// Puede ser derrotado si el jugador le lanza un objeto con la aspiradora o cae al vacío.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    public enum EnemyState { Idle, Patrol, Chase, Attack }

    [Header("Detección y Movimiento")]
    [Tooltip("Distancia máxima en unidades para detectar al jugador.")]
    [SerializeField, Min(1f)] private float detectionRange = 16f;
    [Tooltip("Distancia a la que inicia el ataque cuerpo a cuerpo.")]
    [SerializeField, Min(0.5f)] private float attackRange = 2.2f;
    [Tooltip("Velocidad de movimiento al perseguir al jugador.")]
    [SerializeField, Min(0.5f)] private float chaseSpeed = 4.2f;
    [Tooltip("Velocidad de giro para encarar al objetivo.")]
    [SerializeField, Min(1f)] private float turnSpeed = 6f;
    [Tooltip("Altura de flotación sobre el suelo.")]
    [SerializeField] private float hoverHeight = 1.4f;

    [Header("Combate")]
    [Tooltip("Daño causado al jugador en cada ataque.")]
    [SerializeField, Min(1)] private int attackDamage = 1;
    [Tooltip("Tiempo de espera en segundos entre ataques.")]
    [SerializeField, Min(0.2f)] private float attackCooldown = 1.8f;
    [Tooltip("Puntos de salud del enemigo antes de ser derrotado.")]
    [SerializeField, Min(1)] private int maxHealth = 2;

    [Header("Efectos Visuales")]
    [Tooltip("Renderer del visor o cuerpo para cambiar de color al alertarse.")]
    [SerializeField] private Renderer eyeRenderer;
    [SerializeField] private Color idleEyeColor = new Color(0.2f, 0.8f, 1.0f);
    [SerializeField] private Color alertEyeColor = new Color(1.0f, 0.1f, 0.1f);

    private int currentHealth;
    private EnemyState currentState = EnemyState.Idle;
    private Transform playerTarget;
    private PlayerController playerController;
    private float lastAttackTime = -999f;
    private Vector3 initialPosition;
    private float bobTimer = 0f;

    private void Start()
    {
        currentHealth = maxHealth;
        initialPosition = transform.position;
        FindPlayer();
        UpdateEyeColor(idleEyeColor);
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    private void Update()
    {
        if (playerTarget == null || playerController == null)
        {
            FindPlayer();
            if (playerTarget == null) return;
        }

        // Si el jugador está muerto, vuelve a patrullar/reposo
        if (playerController.dead)
        {
            currentState = EnemyState.Idle;
            UpdateEyeColor(idleEyeColor);
            HoverIdle();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        switch (currentState)
        {
            case EnemyState.Idle:
            case EnemyState.Patrol:
                HoverIdle();
                if (distanceToPlayer <= detectionRange)
                {
                    currentState = EnemyState.Chase;
                    UpdateEyeColor(alertEyeColor);
                }
                break;

            case EnemyState.Chase:
                if (distanceToPlayer > detectionRange * 1.3f)
                {
                    currentState = EnemyState.Idle;
                    UpdateEyeColor(idleEyeColor);
                }
                else if (distanceToPlayer <= attackRange)
                {
                    currentState = EnemyState.Attack;
                }
                else
                {
                    ChasePlayer();
                }
                break;

            case EnemyState.Attack:
                if (distanceToPlayer > attackRange * 1.2f)
                {
                    currentState = EnemyState.Chase;
                }
                else
                {
                    FaceTarget(playerTarget.position);
                    PerformAttack();
                }
                break;
        }
    }

    private void HoverIdle()
    {
        bobTimer += Time.deltaTime * 2f;
        float bobOffset = Mathf.Sin(bobTimer) * 0.25f;
        transform.position = new Vector3(initialPosition.x, initialPosition.y + bobOffset, initialPosition.z);
    }

    private void ChasePlayer()
    {
        Vector3 targetPos = playerTarget.position;
        targetPos.y += hoverHeight;

        FaceTarget(targetPos);

        bobTimer += Time.deltaTime * 3.5f;
        float bobOffset = Mathf.Sin(bobTimer) * 0.15f;
        Vector3 desiredPos = Vector3.MoveTowards(transform.position, targetPos, chaseSpeed * Time.deltaTime);
        desiredPos.y += bobOffset * Time.deltaTime;

        transform.position = desiredPos;
    }

    private void FaceTarget(Vector3 target)
    {
        Vector3 lookDir = (target - transform.position).normalized;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }
    }

    private void PerformAttack()
    {
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;

            // Embestida rápida hacia el jugador
            if (playerController != null && !playerController.dead)
            {
                playerController.TakeDamage(attackDamage);

                // Pequeño retroceso tras golpear
                Vector3 knockDir = (transform.position - playerTarget.position).normalized;
                transform.position += knockDir * 0.6f;
            }
        }
    }

    private void UpdateEyeColor(Color color)
    {
        if (eyeRenderer != null && eyeRenderer.material != null)
        {
            eyeRenderer.material.color = color;
            if (eyeRenderer.material.HasProperty("_EmissionColor"))
            {
                eyeRenderer.material.SetColor("_EmissionColor", color * 2f);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Defeat();
        }
        else
        {
            // Pequeño parpadeo / retroceso
            transform.position += -transform.forward * 0.8f;
        }
    }

    private void Defeat()
    {
        // Efecto de destrucción
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Detectar si fue golpeado por un proyectil lanzado con la aspiradora
        if (collision.gameObject.GetComponent<PoolableController>() != null ||
            collision.gameObject.layer == LayerMask.NameToLayer("Poolable"))
        {
            TakeDamage(2); // Se destruye de un impacto con el proyectil
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<PoolableController>() != null ||
            other.gameObject.layer == LayerMask.NameToLayer("Poolable"))
        {
            TakeDamage(2);
        }
    }
}
