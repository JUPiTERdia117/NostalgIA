using UnityEngine;
using System.Collections;

/// <summary>
/// Enemigo centinela flotante (Drone) que patrulla, detecta al jugador, lo persigue y ataca cuerpo a cuerpo mediante embestida.
/// Puede ser destruido lanzándole objetos con la aspiradora o mediante colisiones físicas.
/// </summary>
public class EnemyDroneController : MonoBehaviour
{
    [Header("Estadísticas")]
    [SerializeField] private int maxHealth = 2;
    [SerializeField] private int currentHealth;
    [SerializeField] private int attackDamage = 1;

    [Header("Movimiento y Persecución")]
    [SerializeField] private float detectionRadius = 15f;
    [SerializeField] private float chaseSpeed = 3.8f;
    [SerializeField] private float rotationSpeed = 6f;
    [SerializeField] private float hoverHeight = 1.4f;
    [SerializeField] private float bobbingAmplitude = 0.25f;
    [SerializeField] private float bobbingFrequency = 2.0f;

    [Header("Ataque")]
    [SerializeField] private float attackRange = 2.2f;
    [SerializeField] private float attackCooldown = 1.8f;
    [SerializeField] private float attackLungeSpeed = 8.0f;
    [SerializeField] private float lungeDuration = 0.35f;

    [Header("Efectos Visuales")]
    [SerializeField] private Renderer droneRenderer;
    [SerializeField] private Color alertColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private ParticleSystem defeatParticles;

    private Transform playerTransform;
    private Vector3 spawnPosition;
    private float nextAttackTime = 0f;
    private bool isLunging = false;
    private bool isDead = false;
    private Material droneMaterial;
    private Color originalEmissionColor;

    private enum DroneState { IdleHover, Chasing, Attacking }
    private DroneState currentState = DroneState.IdleHover;

    void Awake()
    {
        currentHealth = maxHealth;
        spawnPosition = transform.position;

        if (droneRenderer == null)
            droneRenderer = GetComponentInChildren<Renderer>();

        if (droneRenderer != null && droneRenderer.material != null)
        {
            droneMaterial = droneRenderer.material;
            if (droneMaterial.HasProperty("_EmissionColor"))
                originalEmissionColor = droneMaterial.GetColor("_EmissionColor");
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
        }
    }

    void Update()
    {
        if (isDead) return;

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        if (isLunging) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        switch (currentState)
        {
            case DroneState.IdleHover:
                HandleIdleState(distanceToPlayer);
                break;

            case DroneState.Chasing:
                HandleChaseState(distanceToPlayer);
                break;
        }
    }

    private void HandleIdleState(float distanceToPlayer)
    {
        // Movimiento senoidal de flotación en el punto de spawn
        float bob = Mathf.Sin(Time.time * bobbingFrequency) * bobbingAmplitude;
        Vector3 targetPos = spawnPosition + Vector3.up * bob;
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 2f);

        // Giro suave ambiental
        transform.Rotate(Vector3.up, 25f * Time.deltaTime, Space.World);

        if (distanceToPlayer <= detectionRadius)
        {
            currentState = DroneState.Chasing;
            SetAlertVisual(true);
        }
    }

    private void HandleChaseState(float distanceToPlayer)
    {
        if (distanceToPlayer > detectionRadius * 1.4f)
        {
            currentState = DroneState.IdleHover;
            SetAlertVisual(false);
            return;
        }

        // Orientación hacia el jugador
        Vector3 targetPos = playerTransform.position + Vector3.up * hoverHeight;
        Vector3 toPlayer = targetPos - transform.position;
        Vector3 direction = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : transform.forward;

        if (toPlayer.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationSpeed * Time.deltaTime);
        }

        // Movimiento hacia el jugador
        if (distanceToPlayer > attackRange)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, chaseSpeed * Time.deltaTime);
        }
        else
        {
            // En rango de ataque
            if (Time.time >= nextAttackTime)
            {
                StartCoroutine(PerformLungeAttack(direction));
            }
        }
    }

    private IEnumerator PerformLungeAttack(Vector3 attackDir)
    {
        isLunging = true;
        nextAttackTime = Time.time + attackCooldown;
        if (attackDir.sqrMagnitude < 0.001f)
            attackDir = transform.forward;
        else
            attackDir.Normalize();

        // Breve anticipación
        yield return new WaitForSeconds(0.15f);

        float elapsed = 0f;
        while (elapsed < lungeDuration)
        {
            if (isDead) yield break;
            transform.position += attackDir * (attackLungeSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Retroceso suave tras el ataque
        yield return new WaitForSeconds(0.2f);
        isLunging = false;
    }

    private void SetAlertVisual(bool alert)
    {
        if (droneMaterial != null && droneMaterial.HasProperty("_EmissionColor"))
        {
            droneMaterial.SetColor("_EmissionColor", alert ? alertColor * 2f : originalEmissionColor);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isDead) return;

        if (other.CompareTag("Player"))
        {
            // Atacar al jugador al impactar
            other.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
            if (SoundEffectsManager.Instance != null)
                SoundEffectsManager.Instance.PlayHurt();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isDead) return;

        // Verificar si un objeto arrojado impactó al dron
        bool hitByVacuumObject = collision.gameObject.GetComponent<PoolableController>() != null ||
                                collision.gameObject.layer == LayerMask.NameToLayer("Poolable");

        Rigidbody rb = collision.rigidbody;
        bool highVelocityImpact = rb != null && collision.relativeVelocity.magnitude > 3f;

        if (hitByVacuumObject || highVelocityImpact)
        {
            TakeDamage(1);
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        StartCoroutine(FlashDamageVisual());

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator FlashDamageVisual()
    {
        if (droneMaterial != null && droneMaterial.HasProperty("_BaseColor"))
        {
            Color origColor = droneMaterial.GetColor("_BaseColor");
            droneMaterial.SetColor("_BaseColor", Color.white);
            yield return new WaitForSeconds(0.1f);
            droneMaterial.SetColor("_BaseColor", origColor);
        }
    }

    private void Die()
    {
        isDead = true;

        if (SoundEffectsManager.Instance != null)
            SoundEffectsManager.Instance.PlayEnemyDefeat();

        if (defeatParticles != null)
        {
            defeatParticles.transform.parent = null;
            defeatParticles.Play();
            Destroy(defeatParticles.gameObject, 2f);
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
