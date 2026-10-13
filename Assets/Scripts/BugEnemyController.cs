using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemigo básico tipo bicho que persigue al jugador en el suelo
/// y realiza un ataque cuerpo a cuerpo cuando está a corta distancia.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class BugEnemyController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float stoppingDistance = 1.5f;
    [SerializeField] private float turnSpeed = 10f;

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float windupTime = 0.25f;

    private PlayerController player;
    private NavMeshAgent navMeshAgent;
    private EnemyHealth health;
    private bool isAttacking;
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
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;

        if (player == null)
        {
            FindPlayer();
            return;
        }

        if (player.dead) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

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
            player = playerObj.GetComponent<PlayerController>();
        }
    }

    private void FollowPlayer(float distanceToPlayer)
    {
        Vector3 targetPos = player.transform.position;

        if (navMeshAgent != null && navMeshAgent.isOnNavMesh && navMeshAgent.isActiveAndEnabled)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(targetPos);
        }
        else
        {
            // Fallback en caso de que no haya NavMesh horneado debajo del enemigo
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
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh && navMeshAgent.isActiveAndEnabled)
        {
            navMeshAgent.isStopped = true;
        }

        // Rotar hacia el jugador
        Vector3 dir = (player.transform.position - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        // Animación de anticipación (encoger ligeramente)
        float elapsed = 0f;
        while (elapsed < windupTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / windupTime;
            transform.localScale = Vector3.Lerp(originalScale, new Vector3(originalScale.x * 1.2f, originalScale.y * 0.7f, originalScale.z * 1.2f), t);
            yield return null;
        }

        // Ataque (estiramiento y golpe hacia adelante)
        transform.localScale = new Vector3(originalScale.x * 0.9f, originalScale.y * 1.3f, originalScale.z * 1.3f);

        // Verificar si el jugador sigue dentro del rango de ataque
        float distance = Vector3.Distance(transform.position, player.transform.position);
        if (distance <= attackRange * 1.3f && player != null && !player.dead)
        {
            player.TakeDamage(attackDamage);
            Debug.Log($"[BugEnemyController] ¡El bicho golpeó al jugador cuerpo a cuerpo haciendo {attackDamage} de daño!");
        }

        yield return new WaitForSeconds(0.2f);

        // Restaurar escala original
        transform.localScale = originalScale;

        lastAttackTime = Time.time;
        isAttacking = false;
    }

    private void OnCollisionStay(Collision collision)
    {
        // Daño por contacto si se acerca al jugador y no está en cooldown
        if (health != null && health.IsDead) return;
        if (collision.gameObject.CompareTag("Player") && Time.time - lastAttackTime >= attackCooldown)
        {
            PlayerController target = collision.gameObject.GetComponent<PlayerController>();
            if (target != null && !target.dead)
            {
                target.TakeDamage(attackDamage);
                lastAttackTime = Time.time;
            }
        }
    }

    [ContextMenu("Test Attack Player")]
    public void TestAttackPlayer()
    {
        if (player != null)
        {
            player.TakeDamage(attackDamage);
        }
    }
}
