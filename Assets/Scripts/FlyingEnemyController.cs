using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo volador inspirado en las calaveras flotantes de Doom.
/// Flota en el aire, mantiene distancia con el jugador y le dispara proyectiles.
/// </summary>
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

    private PlayerController player;
    private EnemyHealth health;
    private float nextFireTime;
    private bool isCharging;
    private Vector3 originalScale;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        originalScale = transform.localScale;
        FindPlayer();
    }

    private void Start()
    {
        if (player == null) FindPlayer();
        nextFireTime = Time.time + Random.Range(1f, fireRate);
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

        HandleFlightMovement();

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
            player = playerObj.GetComponent<PlayerController>();
        }
    }

    private void HandleFlightMovement()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 targetPos = playerPos;

        // Mantener altura sobre el suelo / jugador con efecto de flotación
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        targetPos.y = Mathf.Max(playerPos.y + hoverHeight + bobOffset, hoverHeight * 0.5f);

        // Vector hacia el jugador
        Vector3 vectorToPlayer = playerPos - transform.position;
        float currentDistance = vectorToPlayer.magnitude;

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

        // Animación de carga: vibración y aumento de tamaño
        float elapsed = 0f;
        while (elapsed < chargeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / chargeTime;
            transform.localScale = Vector3.Lerp(originalScale, originalScale * 1.25f, t) + Random.insideUnitSphere * 0.03f;
            yield return null;
        }

        transform.localScale = originalScale;

        // Disparar proyectil
        Shoot();

        nextFireTime = Time.time + fireRate;
        isCharging = false;
    }

    [ContextMenu("Test Shoot")]
    public void Shoot()
    {
        if (player == null) FindPlayer();

        Vector3 spawnPos = shootPoint != null ? shootPoint.position : transform.position + transform.forward * 0.8f;
        Vector3 targetPos = player != null ? (player.transform.position + Vector3.up * 1.0f) : (transform.position + transform.forward * 5f);
        Vector3 shootDir = (targetPos - spawnPos).normalized;

        GameObject projObj;
        if (projectilePrefab != null)
        {
            projObj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(shootDir));
        }
        else
        {
            // Si no se asignó prefab en el inspector, crear uno dinámico con primitivo de esfera
            projObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projObj.name = "FloatingSkull_Fireball";
            projObj.transform.position = spawnPos;
            projObj.transform.localScale = Vector3.one * 0.45f;
            Collider col = projObj.GetComponent<Collider>();
            col.isTrigger = true;
        }

        EnemyProjectile projectile = projObj.GetComponent<EnemyProjectile>();
        if (projectile == null)
        {
            projectile = projObj.AddComponent<EnemyProjectile>();
        }

        projectile.Initialize(shootDir, projectileDamage, projectileSpeed);
        Debug.Log($"[FlyingEnemyController] ¡Calavera flotante disparó un proyectil hacia el jugador!");
    }
}
