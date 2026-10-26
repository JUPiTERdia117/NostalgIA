using NostalgIA.Gameplay.Player;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemigo hostil tipo WinRar / bicho.
/// Persigue al jugador y, al acercarse, realiza un salto/embestida (lunge) letal hacia adelante
/// que elimina al jugador de un solo golpe.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class BugEnemyController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.0f;
    [SerializeField] private float stoppingDistance = 1.2f;
    [SerializeField] private float turnSpeed = 12f;
    [Tooltip("Distancia de detección para empezar a perseguir al jugador.")]
    [SerializeField] private float detectionRange = 18f;

    [Header("Attack / Lunge Settings")]
    [Tooltip("Distancia a la que inicia la embestida letal.")]
    [SerializeField] private float attackRange = 6.5f;
    [Tooltip("Daño infligido. 99 para matar al jugador de un solo golpe.")]
    [SerializeField] private int attackDamage = 99;
    [Tooltip("Si es verdadero, siempre inflige daño letal al impactar al jugador.")]
    [SerializeField] private bool oneHitKill = true;
    [SerializeField] private float attackCooldown = 2.0f;
    [Tooltip("Tiempo de anticipación antes de lanzarse (se agacha y apunta).")]
    [SerializeField] private float windupTime = 0.35f;
    [Tooltip("Velocidad de traslación durante el salto/lanzamiento.")]
    [SerializeField] private float lungeSpeed = 16f;
    [Tooltip("Altura máxima del arco parabólico del salto.")]
    [SerializeField] private float lungeArcHeight = 1.6f;
    [Tooltip("Distancia adicional que rebasa la posición original del jugador si este esquiva.")]
    [SerializeField] private float lungeOvershoot = 1.5f;
    [Tooltip("Pausa de recuperación en el suelo tras aterrizar, dando oportunidad al jugador de contraatacar.")]
    [SerializeField] private float recoveryTime = 0.5f;

    private PlayerHealth player;
    private NavMeshAgent navMeshAgent;
    private EnemyHealth health;
    private bool isAttacking;
    private bool isLunging;
    private float lastAttackTime = -99f;
    private Vector3 originalScale;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        originalScale = transform.localScale;

        if (navMeshAgent != null)
        {
            navMeshAgent.speed = moveSpeed;
            navMeshAgent.stoppingDistance = stoppingDistance;
        }

        FindPlayer();
    }

    private void Start()
    {
        if (player == null)
        {
            FindPlayer();
        }

        if (navMeshAgent != null && !navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.enabled = false;
        }
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;

        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        if (player.IsDead)
        {
            if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = true;
            }
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        if (distanceToPlayer > detectionRange)
        {
            if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = true;
            }
            return;
        }

        if (!isAttacking && distanceToPlayer <= attackRange && Time.time - lastAttackTime >= attackCooldown)
        {
            StartCoroutine(AttackRoutine());
        }
        else if (!isAttacking)
        {
            FollowPlayer(distanceToPlayer);
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<PlayerHealth>();
        }
    }

    private void FollowPlayer(float distanceToPlayer)
    {
        Vector3 targetPos = player.transform.position;

        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(targetPos);
        }
        else
        {
            if (navMeshAgent != null && navMeshAgent.enabled && !navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.enabled = false;
            }

            Vector3 direction = (targetPos - transform.position);
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

                if (distanceToPlayer > stoppingDistance)
                {
                    transform.position += direction.normalized * (moveSpeed * Time.deltaTime);
                }
            }
        }
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;

        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
        }

        // 1. ANTICIPACIÓN (WINDUP): Orientar y agacharse antes del salto
        Vector3 playerPos = player != null ? player.transform.position : transform.position + transform.forward;
        Vector3 dirToPlayer = (playerPos - transform.position);
        dirToPlayer.y = 0f;
        if (dirToPlayer.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dirToPlayer.normalized);
        }

        float elapsed = 0f;
        while (elapsed < windupTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / windupTime;
            // Se agacha / comprime para tomar impulso
            transform.localScale = Vector3.Lerp(originalScale, new Vector3(originalScale.x * 1.35f, originalScale.y * 0.55f, originalScale.z * 1.35f), t);
            yield return null;
        }

        // 2. LANZAMIENTO / EMBESTIDA (LUNGE): Salto físico hacia el jugador
        isLunging = true;
        bool wasNavAgentEnabled = navMeshAgent != null && navMeshAgent.enabled;
        if (navMeshAgent != null)
        {
            navMeshAgent.enabled = false;
        }

        Vector3 startPos = transform.position;
        Vector3 targetPoint = player != null ? player.transform.position : startPos + transform.forward * attackRange;
        Vector3 lungeDir = (targetPoint - startPos);
        lungeDir.y = 0f;
        if (lungeDir.sqrMagnitude < 0.01f) lungeDir = transform.forward;
        lungeDir.Normalize();

        // Punto de aterrizaje calculado con rebase si el jugador esquiva
        Vector3 destination = targetPoint + lungeDir * lungeOvershoot;
        float totalDist = Vector3.Distance(startPos, destination);
        float leapDuration = Mathf.Max(0.25f, totalDist / Mathf.Max(1f, lungeSpeed));

        // Estiramiento dinámico durante el vuelo
        transform.localScale = new Vector3(originalScale.x * 0.85f, originalScale.y * 1.25f, originalScale.z * 1.4f);

        float leapTime = 0f;
        bool hitLethal = false;

        while (leapTime < leapDuration)
        {
            if (health != null && health.IsDead) break;

            leapTime += Time.deltaTime;
            float progress = Mathf.Clamp01(leapTime / leapDuration);

            // Trayectoria parabólica
            Vector3 currentHoriz = Vector3.Lerp(startPos, destination, progress);
            float currentY = Mathf.Lerp(startPos.y, destination.y, progress) + Mathf.Sin(progress * Mathf.PI) * lungeArcHeight;
            transform.position = new Vector3(currentHoriz.x, currentY, currentHoriz.z);

            // Detección de impacto letal en vuelo
            if (!hitLethal && player != null && !player.IsDead)
            {
                float hitDist = Vector3.Distance(transform.position, player.transform.position);
                if (hitDist <= 2.2f)
                {
                    hitLethal = true;
                    DealLethalDamage(player);
                }
            }

            yield return null;
        }

        isLunging = false;
        transform.localScale = originalScale;

        // 3. RECUPERACIÓN / ATERRIZAJE
        if (wasNavAgentEnabled && navMeshAgent != null)
        {
            navMeshAgent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                navMeshAgent.Warp(hit.position);
            }
        }

        // Breve ventana de vulnerabilidad tras caer al suelo
        yield return new WaitForSeconds(recoveryTime);

        lastAttackTime = Time.time;
        isAttacking = false;
    }

    private void DealLethalDamage(PlayerHealth target)
    {
        if (target == null || target.IsDead) return;
        int damage = oneHitKill ? 99 : attackDamage;
        target.TakeDamage(damage);
        Debug.Log($"[BugEnemyController] ¡WinRar se lanzó con éxito sobre el jugador e infligió {damage} de daño letal (1 golpe = muerte)!");
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleDirectDamage(collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandleDirectDamage(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleDirectDamage(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleDirectDamage(other.gameObject);
    }

    private void HandleDirectDamage(GameObject obj)
    {
        if (health != null && health.IsDead) return;
        if (!isLunging && Time.time - lastAttackTime < attackCooldown) return;

        PlayerHealth target = obj.GetComponentInParent<PlayerHealth>();
        if (target != null && !target.IsDead)
        {
            DealLethalDamage(target);
            lastAttackTime = Time.time;
        }
    }

    [ContextMenu("Test Attack Player")]
    public void TestAttackPlayer()
    {
        if (player != null)
        {
            DealLethalDamage(player);
        }
    }
}
