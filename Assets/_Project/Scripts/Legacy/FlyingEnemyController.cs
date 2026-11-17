using NostalgIA.Gameplay.Enemies;
using NostalgIA.Gameplay.Player;
using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo volador inspirado en las calaveras flotantes de Doom.
/// Flota en el aire, mantiene distancia con el jugador y le dispara proyectiles.
/// </summary>
/// <remarks>
/// Animaciones del Spam Mail (si el modelo tiene <see cref="EnemyAnimation"/>):
/// <b>Flying</b> en bucle siempre (capa base, mueve las alas) y <b>Shoot</b> al disparar en la capa
/// «Disparo», que solo afecta la lengüeta del sobre; por eso las alas no dejan de moverse.
/// Con animaciones, la vibración por escala al cargar el disparo se desactiva.
/// </remarks>
[RequireComponent(typeof(EnemyHealth))]
public class FlyingEnemyController : MonoBehaviour
{
    [Header("Hover & Flight")]
    [SerializeField] private float hoverHeight = 3.5f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobAmount = 0.4f;
    [SerializeField] private float flySpeed = 3.0f;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Combat Distances")]
    [Tooltip("Distancia a partir de la cual detecta y ataca al jugador.")]
    [SerializeField] private float detectionRange = 24f;
    [Tooltip("Distancia deseada que intentará mantener respecto al jugador.")]
    [SerializeField] private float preferredDistance = 8f;
    [Tooltip("Distancia mínima; si el jugador se acerca más, el enemigo retrocede.")]
    [SerializeField] private float minDistance = 4f;

    [Header("Shooting")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private float fireRate = 3.0f;
    [SerializeField] private float chargeTime = 0.5f;
    [SerializeField] private int projectileDamage = 1;
    [SerializeField] private float projectileSpeed = 12f;

    private PlayerHealth player;
    private EnemyHealth health;
    private float nextFireTime;
    private bool isCharging;
    private Vector3 originalScale;
    private Vector3 spawnPosition;
    private EnemyAnimation anim;

    /// <summary>Capa del Animator donde está el disparo (encima del vuelo).</summary>
    private const int ShootLayer = 1;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        originalScale = transform.localScale;
        spawnPosition = transform.position;
        anim = GetComponent<EnemyAnimation>();
        FindPlayer();
    }

    private void Start()
    {
        if (player == null) FindPlayer();
        if (anim != null) anim.Play("Flying");   // Vuela en todo momento.
        nextFireTime = Time.time + Random.Range(1.5f, fireRate);
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;

        if (player == null)
        {
            FindPlayer();
            return;
        }

        if (player.IsDead)
        {
            HandleIdleHover();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        if (distanceToPlayer > detectionRange)
        {
            HandleIdleHover();
            return;
        }

        HandleFlightMovement(distanceToPlayer);

        if (!isCharging && Time.time >= nextFireTime)
        {
            StartCoroutine(ChargeAndShootRoutine());
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

    private void HandleIdleHover()
    {
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        Vector3 targetPos = spawnPosition + Vector3.up * bobOffset;
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 2f);
    }

    private void HandleFlightMovement(float currentDistance)
    {
        Vector3 playerPos = player.transform.position;
        Vector3 targetPos = playerPos;

        // Mantener altura sobre el suelo / jugador con efecto de flotación
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        targetPos.y = Mathf.Max(playerPos.y + hoverHeight + bobOffset, hoverHeight * 0.5f);

        // Movimiento de acercamiento o retroceso
        Vector3 desiredPosition = transform.position;
        if (currentDistance > preferredDistance + 1f)
        {
            // Acercarse
            Vector3 approachDir = (playerPos - transform.position).normalized;
            desiredPosition += approachDir * (flySpeed * Time.deltaTime);
            desiredPosition.y = targetPos.y;
        }
        else if (currentDistance < minDistance)
        {
            // Retroceder
            Vector3 retreatDir = (transform.position - playerPos).normalized;
            desiredPosition += retreatDir * (flySpeed * Time.deltaTime);
            desiredPosition.y = targetPos.y;
        }
        else
        {
            // Flotar en su posición con el cabeceo suave
            desiredPosition.y = Mathf.Lerp(transform.position.y, targetPos.y, Time.deltaTime * 2f);
        }

        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * flySpeed);

        // Orientarse hacia el jugador
        Vector3 lookDir = (playerPos + Vector3.up * 1f) - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    private IEnumerator ChargeAndShootRoutine()
    {
        isCharging = true;
        bool animated = anim != null && anim.IsAnimated;

        // La lengüeta se abre mientras carga y el proyectil sale al terminar la carga.
        if (animated) anim.Play("Shoot", ShootLayer, true);

        // Animación de carga: vibración y aumento de tamaño (solo sin animaciones)
        float elapsed = 0f;
        while (elapsed < chargeTime)
        {
            if (health != null && health.IsDead) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / chargeTime;
            if (!animated) transform.localScale = Vector3.Lerp(originalScale, originalScale * 1.25f, t) + Random.insideUnitSphere * 0.03f;
            yield return null;
        }

        transform.localScale = originalScale;

        // Disparar proyectil si sigue en rango
        if (player != null && !player.IsDead && Vector3.Distance(transform.position, player.transform.position) <= detectionRange * 1.2f)
        {
            Shoot();
        }

        nextFireTime = Time.time + fireRate;
        isCharging = false;
    }

    [ContextMenu("Test Shoot")]
    public void Shoot()
    {
        Vector3 origin = shootPoint != null ? shootPoint.position : transform.position + transform.forward * 0.8f;
        Vector3 target = player != null ? player.transform.position + Vector3.up * 1f : origin + transform.forward * 10f;
        Vector3 direction = (target - origin).normalized;

        if (projectilePrefab != null)
        {
            GameObject projObj = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction));
            EnemyProjectile proj = projObj.GetComponent<EnemyProjectile>();
            if (proj != null)
            {
                proj.Initialize(direction, projectileDamage, projectileSpeed);
            }
        }
        else
        {
            // Proyectil de respaldo si no hay prefab asignado
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "EnemyFireball";
            sphere.transform.position = origin;
            sphere.transform.localScale = Vector3.one * 0.5f;

            var rend = sphere.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = new Color(1f, 0.3f, 0f);
            }

            SphereCollider col = sphere.GetComponent<SphereCollider>();
            if (col != null) col.isTrigger = true;

            EnemyProjectile proj = sphere.AddComponent<EnemyProjectile>();
            proj.Initialize(direction, projectileDamage, projectileSpeed);
        }

        Debug.Log($"[FlyingEnemyController] ¡Calavera flotante disparó un proyectil hacia el jugador!");
    }
}
