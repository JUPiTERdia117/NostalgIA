using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo básico que patrulla, detecta al jugador, lo persigue y ataca al acercarse.
/// Puede ser derrotado lanzándole objetos con la aspiradora o arrojándolo al vacío.
/// </summary>
public class EnemyController : MonoBehaviour
{
    [Header("Detección y Movimiento")]
    [Tooltip("Distancia a la que el enemigo detecta al jugador para empezar a perseguirlo.")]
    [SerializeField] private float detectionRange = 14f;
    [Tooltip("Distancia para asestar un golpe/ataque al jugador.")]
    [SerializeField] private float attackRange = 2.2f;
    [Tooltip("Velocidad de persecución.")]
    [SerializeField] private float chaseSpeed = 4.2f;
    [Tooltip("Velocidad de rotación para encarar al objetivo.")]
    [SerializeField] private float turnSpeed = 6f;
    [Tooltip("Radio de patrullaje desde su punto de origen.")]
    [SerializeField] private float patrolRadius = 4f;

    [Header("Combate")]
    [Tooltip("Daño que inflige por ataque.")]
    [SerializeField] private int attackDamage = 1;
    [Tooltip("Tiempo de enfriamiento entre ataques.")]
    [SerializeField] private float attackCooldown = 1.5f;
    [Tooltip("Vida del enemigo antes de ser destruido.")]
    [SerializeField] private int maxHealth = 1;

    [Header("Efectos")]
    [SerializeField] private Transform visualModel;
    [SerializeField] private Renderer glowRenderer;
    [SerializeField] private Color normalGlowColor = new Color(0.8f, 0.2f, 0.9f);
    [SerializeField] private Color alertGlowColor = new Color(1f, 0.1f, 0.1f);
    [SerializeField] private GameObject defeatVfxPrefab;

    private int currentHealth;
    private Transform playerTransform;
    private PlayerController playerController;
    private Vector3 spawnPosition;
    private Vector3 patrolTarget;
    private float nextAttackTime;
    private float patrolTimer;
    private bool isAttacking;
    private bool isDefeated;
    private Rigidbody rb;
    private Material glowMatInstance;

    void Awake()
    {
        currentHealth = maxHealth;
        spawnPosition = transform.position;
        patrolTarget = spawnPosition;
        rb = GetComponent<Rigidbody>();

        if (visualModel == null && transform.childCount > 0)
        {
            visualModel = transform.GetChild(0);
        }

        if (glowRenderer != null)
        {
            glowMatInstance = glowRenderer.material;
            SetGlowColor(normalGlowColor);
        }
    }

    void Start()
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
        if (isDefeated) return;

        // Comprobación periódica del jugador por si reapareció
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                playerController = playerObj.GetComponent<PlayerController>();
            }
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= detectionRange)
        {
            // Estado de persecución y ataque
            SetGlowColor(alertGlowColor);
            FaceTarget(playerTransform.position);

            if (distanceToPlayer <= attackRange)
            {
                if (Time.time >= nextAttackTime && !isAttacking)
                {
                    StartCoroutine(ExecuteAttack());
                }
            }
            else if (!isAttacking)
            {
                MoveTowards(playerTransform.position, chaseSpeed);
            }
        }
        else
        {
            // Estado de patrullaje relajado
            SetGlowColor(normalGlowColor);
            UpdatePatrol();
        }

        // Animación suave de flotación/levitación si tiene modelo visual
        if (visualModel != null)
        {
            float bobbing = Mathf.Sin(Time.time * 3f + spawnPosition.x) * 0.12f;
            visualModel.localPosition = new Vector3(0f, bobbing, 0f);
        }

        // Si cae al vacío por debajo del nivel
        if (transform.position.y < -30f)
        {
            Die();
        }
    }

    private void MoveTowards(Vector3 target, float speed)
    {
        Vector3 direction = (target - transform.position);
        direction.y = 0f; // Mantener nivel horizontal
        if (direction.sqrMagnitude > 0.05f)
        {
            Vector3 movement = direction.normalized * (speed * Time.deltaTime);
            if (rb != null && !rb.isKinematic)
            {
                rb.MovePosition(rb.position + movement);
            }
            else
            {
                transform.position += movement;
            }
        }
    }

    private void FaceTarget(Vector3 target)
    {
        Vector3 lookDir = target - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }

    private void UpdatePatrol()
    {
        patrolTimer += Time.deltaTime;
        if (patrolTimer >= 4f || Vector3.Distance(transform.position, patrolTarget) < 0.5f)
        {
            patrolTimer = 0f;
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
            patrolTarget = spawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
        }

        FaceTarget(patrolTarget);
        MoveTowards(patrolTarget, chaseSpeed * 0.5f);
    }

    private IEnumerator ExecuteAttack()
    {
        isAttacking = true;
        nextAttackTime = Time.time + attackCooldown;

        // Retroceso preparatorio antes del golpe
        Vector3 backwardDir = -transform.forward;
        float elapsed = 0f;
        while (elapsed < 0.25f)
        {
            elapsed += Time.deltaTime;
            transform.position += backwardDir * (1.5f * Time.deltaTime);
            yield return null;
        }

        // Lunge / Embestida hacia el jugador
        elapsed = 0f;
        Vector3 lungeDir = transform.forward;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            transform.position += lungeDir * (12f * Time.deltaTime);
            yield return null;
        }

        // Aplicar daño si el jugador sigue en rango
        if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) <= attackRange + 0.8f)
        {
            if (playerController != null)
            {
                playerController.TakeDamage(attackDamage);
            }
            else
            {
                playerTransform.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
            }
        }

        yield return new WaitForSeconds(0.2f);
        isAttacking = false;
    }

    private void SetGlowColor(Color color)
    {
        if (glowMatInstance != null)
        {
            glowMatInstance.SetColor("_EmissionColor", color * 2.5f);
            glowMatInstance.SetColor("_BaseColor", color);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDefeated) return;

        // Si colisiona con un objeto con capa Poolable o proyectil disparado
        bool isPoolable = collision.gameObject.layer == LayerMask.NameToLayer("Poolable") ||
                          collision.gameObject.GetComponent<PoolableController>() != null;
        bool hasHighImpact = collision.relativeVelocity.magnitude > 3.5f;

        if (isPoolable || hasHighImpact)
        {
            TakeDamage(1);
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        StartCoroutine(HitFlash());

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator HitFlash()
    {
        SetGlowColor(Color.white * 3f);
        yield return new WaitForSeconds(0.12f);
        SetGlowColor(alertGlowColor);
    }

    private void Die()
    {
        if (isDefeated) return;
        isDefeated = true;

        if (defeatVfxPrefab != null)
        {
            Instantiate(defeatVfxPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            CreateProceduralDeathVfx(transform.position);
        }

        Destroy(gameObject);
    }

    private void CreateProceduralDeathVfx(Vector3 position)
    {
        GameObject fxObj = new GameObject("EnemyDefeatVfx");
        fxObj.transform.position = position;
        ParticleSystem ps = fxObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.4f, 0.8f), new Color(0.3f, 0.8f, 1f));
        main.startSize = 0.35f;
        main.startSpeed = 5f;
        main.startLifetime = 0.8f;
        main.duration = 0.4f;
        main.loop = false;
        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;
        Destroy(fxObj, 1.2f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
