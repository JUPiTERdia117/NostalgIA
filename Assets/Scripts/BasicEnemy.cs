using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo dron/centinela volador que detecta y persigue al jugador, ataca cuerpo a cuerpo,
/// y es vulnerable a objetos lanzados con la aspiradora o impactos físicos.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BasicEnemy : MonoBehaviour
{
    [Header("Estadísticas")]
    [SerializeField] private int maxHealth = 2;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float moveSpeed = 3.6f;
    [SerializeField] private float detectionRadius = 16f;
    [SerializeField] private float attackDistance = 1.9f;
    [SerializeField] private float attackCooldown = 1.4f;

    [Header("Efectos Visuales")]
    [SerializeField] private Renderer mainRenderer;
    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private GameObject defeatPrefab;

    private int currentHealth;
    private Transform playerTransform;
    private PlayerController playerController;
    private float lastAttackTime = -10f;
    private bool isDead = false;
    private Vector3 initialPosition;
    private Material enemyMaterial;
    private Color originalMaterialColor;

    void Awake()
    {
        currentHealth = maxHealth;
        initialPosition = transform.position;

        if (mainRenderer == null)
            mainRenderer = GetComponentInChildren<Renderer>();

        if (mainRenderer != null)
        {
            enemyMaterial = mainRenderer.material;
            if (enemyMaterial.HasProperty("_BaseColor"))
                originalMaterialColor = enemyMaterial.GetColor("_BaseColor");
            else if (enemyMaterial.HasProperty("_Color"))
                originalMaterialColor = enemyMaterial.color;
        }
    }

    void Start()
    {
        FindPlayer();
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

    void Update()
    {
        if (isDead) return;

        if (playerTransform == null)
        {
            FindPlayer();
            HoverIdle();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= detectionRadius && (playerController == null || !playerController.dead))
        {
            // Perseguir al jugador
            Vector3 targetPosition = playerTransform.position + Vector3.up * 1.0f;
            Vector3 direction = (targetPosition - transform.position).normalized;

            // Rotar hacia el jugador
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 6f);

            if (distanceToPlayer > attackDistance)
            {
                // Movimiento hacia el jugador
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            }
            else
            {
                // Rango de ataque alcanzado
                TryAttack();
            }
        }
        else
        {
            HoverIdle();
        }
    }

    private void HoverIdle()
    {
        // Animación suave de flotación
        float hoverY = Mathf.Sin(Time.time * 2f) * 0.25f;
        transform.position = new Vector3(initialPosition.x, initialPosition.y + hoverY, initialPosition.z);
        transform.Rotate(Vector3.up, 20f * Time.deltaTime, Space.World);
    }

    private void TryAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;
        lastAttackTime = Time.time;

        if (playerController != null && !playerController.dead)
        {
            if (GameAudio.Instance != null)
                GameAudio.Instance.PlayHurt();

            playerController.TakeDamage(attackDamage);

            // Retroceso leve tras atacar
            transform.position -= transform.forward * 0.8f;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        CheckIncomingDamage(collision.gameObject, collision.relativeVelocity.magnitude);
    }

    void OnTriggerEnter(Collider other)
    {
        CheckIncomingDamage(other.gameObject, 5f);
    }

    private void CheckIncomingDamage(GameObject incomingObj, float speed)
    {
        if (isDead) return;

        // Comprobar si el objeto lanzado pertenece a la capa Poolable o tiene velocidad suficiente
        bool isPoolable = incomingObj.layer == LayerMask.NameToLayer("Poolable");
        Rigidbody rb = incomingObj.GetComponent<Rigidbody>();
        bool hasHighVelocity = rb != null && (rb.linearVelocity.magnitude > 2.5f || speed > 3f);

        if (isPoolable || hasHighVelocity)
        {
            TakeDamage(1);

            // Empujar al enemigo en dirección contraria al impacto
            if (rb != null)
            {
                Vector3 pushDir = rb.linearVelocity.normalized;
                transform.position += pushDir * 0.5f;
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;

        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayEnemyHit();

        StartCoroutine(FlashRoutine());

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator FlashRoutine()
    {
        if (enemyMaterial != null)
        {
            if (enemyMaterial.HasProperty("_BaseColor"))
                enemyMaterial.SetColor("_BaseColor", hitFlashColor);
            else if (enemyMaterial.HasProperty("_Color"))
                enemyMaterial.color = hitFlashColor;

            yield return new WaitForSeconds(0.12f);

            if (enemyMaterial.HasProperty("_BaseColor"))
                enemyMaterial.SetColor("_BaseColor", originalMaterialColor);
            else if (enemyMaterial.HasProperty("_Color"))
                enemyMaterial.color = originalMaterialColor;
        }
    }

    private void Die()
    {
        isDead = true;

        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayEnemyDefeat();

        // Efecto visual de desintegración
        SpawnDefeatParticles();

        Destroy(gameObject);
    }

    private void SpawnDefeatParticles()
    {
        // Crear efecto de explosión procedural con partículas
        GameObject burst = new GameObject("EnemyExplosion");
        burst.transform.position = transform.position;

        ParticleSystem ps = burst.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.6f;
        main.startSpeed = 6f;
        main.startSize = 0.25f;
        main.startColor = new Color(1f, 0.4f, 0.1f);
        main.duration = 0.3f;
        main.loop = false;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        Destroy(burst, 1.2f);
    }
}
