using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Player;
using NostalgIA.Presentation.Audio;
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

    [Header("Audio")]
    [Tooltip("Sonido cuando el email dispara (Universfield - Arcade Shot).")]
    [SerializeField] private AudioClip shootClip;
    [SerializeField, Range(0f, 1f)] private float shootVolume = 0.85f;

    private PlayerHealth player;
    private EnemyHealth health;
    private AudioSource audioSource;
    private float nextFireTime;
    private bool isCharging;
    private Vector3 originalScale;
    private Vector3 spawnPosition;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        originalScale = transform.localScale;
        spawnPosition = transform.position;
        SetupAudioSource();
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
        audioSource.spatialBlend = 0.25f;
        audioSource.minDistance = 4f;
        audioSource.maxDistance = 35f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.outputAudioMixerGroup = GameAudio.Sfx;
    }

    private void Start()
    {
        if (player == null) FindPlayer();
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

        bool playedLocally = false;
        if (shootClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootClip, shootVolume);
            playedLocally = true;
        }
        EventBus<EnemyAttacked>.Raise(new EnemyAttacked(EnemyKind.SpamMail, origin, playedLocally));

        Debug.Log($"[FlyingEnemyController] ¡Calavera flotante disparó un proyectil hacia el jugador!");
    }
}
