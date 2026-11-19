using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Enemies;
using NostalgIA.Gameplay.Player;
using NostalgIA.Presentation.Audio;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemigo hostil tipo WinRar / bicho.
/// Persigue al jugador y, al acercarse, realiza un salto/embestida (lunge) letal hacia adelante
/// que elimina al jugador de un solo golpe.
/// </summary>
/// <remarks>
/// Animaciones (si el modelo tiene <see cref="EnemyAnimation"/>):
/// <list type="bullet">
/// <item><b>Idle</b> mientras no detecta al jugador.</item>
/// <item>Al detectarlo se voltea hacia él y ruge (<b>Rawr</b>) una vez, quieto, para avisar que está activo.</item>
/// <item>Después lo persigue con <b>Bite</b> en bucle; al atacar, <b>Bite</b> vuelve a empezar.</item>
/// <item>Si el jugador se aleja mucho o muere, vuelve a <b>Idle</b> y rugirá otra vez al verlo.</item>
/// </list>
/// Con animaciones, el aplastado y estirado por escala del prototipo se desactiva.
/// </remarks>
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

    [Header("Alerta")]
    [Tooltip("Factor de la distancia de detección a partir del cual pierde al jugador y vuelve a reposo.")]
    [SerializeField, Min(1f)] private float loseRangeMultiplier = 1.5f;
    [Tooltip("Velocidad con la que se voltea hacia el jugador mientras ruge.")]
    [SerializeField] private float alertTurnSpeed = 8f;

    [Header("Audio")]
    [Tooltip("Sonido cuando el WinRar muerde (Anon - Mimi Chomp).")]
    [SerializeField] private AudioClip biteClip;
    [SerializeField, Range(0f, 1f)] private float biteVolume = 0.9f;

    private PlayerHealth player;
    private NavMeshAgent navMeshAgent;
    private EnemyHealth health;
    private AudioSource audioSource;
    private float lastBiteSoundTime = -99f;
    private bool isAttacking;
    private bool isLunging;
    private float lastAttackTime = -99f;
    private Vector3 originalScale;
    private EnemyAnimation anim;
    private bool isAlerted;   // Ya rugió y está persiguiendo.
    private bool isRoaring;   // Está en la animación de rugido.

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        anim = GetComponent<EnemyAnimation>();
        originalScale = transform.localScale;
        SetupAudioSource();

        if (navMeshAgent != null)
        {
            navMeshAgent.speed = moveSpeed;
            navMeshAgent.stoppingDistance = stoppingDistance;
        }

        FindPlayer();
    }

    private void SetupAudioSource()
    {
        if (!TryGetComponent(out audioSource))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0.2f;
        audioSource.minDistance = 4f;
        audioSource.maxDistance = 35f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.outputAudioMixerGroup = GameAudio.Sfx;
    }

    private void PlayBiteSound()
    {
        if (Time.time - lastBiteSoundTime < 0.25f) return;
        lastBiteSoundTime = Time.time;

        bool playedLocally = false;
        if (biteClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(biteClip, biteVolume);
            playedLocally = true;
        }
        EventBus<EnemyAttacked>.Raise(new EnemyAttacked(EnemyKind.Antivirus, transform.position, playedLocally));
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
        if (health != null && health.IsDead) return;   // La muerte la anima EnemyHealth.

        if (player == null)
        {
            FindPlayer();
            if (player == null)
            {
                Rest();
                return;
            }
        }

        if (player.IsDead)
        {
            isAlerted = false;
            Rest();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        // En reposo: espera hasta ver al jugador; al verlo, ruge una vez.
        if (!isAlerted)
        {
            if (isRoaring) return;
            if (distanceToPlayer <= detectionRange) StartCoroutine(AlertRoutine());
            else Rest();
            return;
        }

        // Alerta: si el jugador se aleja demasiado, lo pierde y vuelve a reposo.
        if (!isAttacking && distanceToPlayer > detectionRange * loseRangeMultiplier)
        {
            isAlerted = false;
            Rest();
            return;
        }

        if (!isAttacking && distanceToPlayer <= attackRange && Time.time - lastAttackTime >= attackCooldown)
        {
            StartCoroutine(AttackRoutine());
        }
        else if (!isAttacking)
        {
            FollowPlayer(distanceToPlayer);
            if (anim != null) anim.Play("Bite");   // Persigue mordiendo en bucle.
        }
    }

    /// <summary>Se detiene y reproduce la animación de reposo.</summary>
    private void Rest()
    {
        StopAgent();
        if (anim != null) anim.Play("Idle");
    }

    private void StopAgent()
    {
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
        }
    }

    /// <summary>
    /// Detecta al jugador: se voltea hacia él y ruge una vez antes de empezar a perseguirlo.
    /// Sin animaciones, el rugido es solo una pausa corta.
    /// </summary>
    private IEnumerator AlertRoutine()
    {
        isRoaring = true;
        StopAgent();
        if (anim != null) anim.Play("Rawr", 0, true);

        float duration = anim != null && anim.Length("Rawr") > 0f ? anim.Length("Rawr") : 0.4f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (health != null && health.IsDead) yield break;
            elapsed += Time.deltaTime;
            if (player != null) TurnTowards(player.transform.position, alertTurnSpeed);
            yield return null;
        }

        isRoaring = false;
        isAlerted = true;
    }

    /// <summary>Gira en el plano horizontal hacia un punto.</summary>
    private void TurnTowards(Vector3 point, float speed)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction.normalized), speed * Time.deltaTime);
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
        // El ataque también es una mordida: la animación vuelve a empezar al lanzarse.
        if (anim != null) anim.Play("Bite", 0, true);
        bool squash = anim == null || !anim.IsAnimated;   // Sin animaciones, se usa la deformación por escala.

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
            if (squash) transform.localScale = Vector3.Lerp(originalScale, new Vector3(originalScale.x * 1.35f, originalScale.y * 0.55f, originalScale.z * 1.35f), t);
            yield return null;
        }

        // 2. LANZAMIENTO / EMBESTIDA (LUNGE): Salto físico hacia el jugador
        isLunging = true;
        PlayBiteSound();
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
        if (squash) transform.localScale = new Vector3(originalScale.x * 0.85f, originalScale.y * 1.25f, originalScale.z * 1.4f);

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
        if (anim != null) anim.Play("Bite", 0, true);   // Al alcanzar al jugador, la mordida se ve completa.
        PlayBiteSound();
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
