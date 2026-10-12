using UnityEngine;

/// <summary>
/// Controla obstáculos dañinos y mortales (pinchos, abismos, lava, sierras).
/// Puede causar daño incremental o muerte instantánea al jugador.
/// </summary>
public class Hazard : MonoBehaviour
{
    [Header("Configuración de Daño")]
    [Tooltip("Si es verdadero, elimina al jugador de inmediato activando el respawn.")]
    [SerializeField] private bool isFatal = true;
    [Tooltip("Daño infligido al jugador si no es muerte instantánea.")]
    [SerializeField, Min(1)] private int damage = 1;
    [Tooltip("Tiempo de inmunidad temporal entre impactos consecutivos.")]
    [SerializeField] private float damageCooldown = 1.0f;

    [Header("Efecto de Impulso / Rechazo")]
    [SerializeField] private float knockbackForce = 8f;

    [Header("Comportamiento Opcional: Trampa Periódica")]
    [SerializeField] private bool isPeriodic = false;
    [SerializeField] private float activeDuration = 2.0f;
    [SerializeField] private float inactiveDuration = 2.0f;
    [SerializeField] private Transform visualRoot;

    private float lastDamageTime = -10f;
    private float timer;
    private bool hazardActive = true;
    private Vector3 initialVisualPos;

    void Start()
    {
        if (visualRoot != null)
        {
            initialVisualPos = visualRoot.localPosition;
        }
    }

    void Update()
    {
        if (!isPeriodic) return;

        timer += Time.deltaTime;
        if (hazardActive && timer >= activeDuration)
        {
            hazardActive = false;
            timer = 0f;
            if (visualRoot != null) visualRoot.localPosition = initialVisualPos - Vector3.up * 1.5f;
        }
        else if (!hazardActive && timer >= inactiveDuration)
        {
            hazardActive = true;
            timer = 0f;
            if (visualRoot != null) visualRoot.localPosition = initialVisualPos;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyDamage(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryApplyDamage(collision.gameObject);
    }

    private void TryApplyDamage(GameObject target)
    {
        if (!hazardActive) return;
        if (Time.time - lastDamageTime < damageCooldown) return;

        PlayerController player = target.GetComponentInParent<PlayerController>();
        if (player == null && target.CompareTag("Player"))
        {
            player = target.GetComponent<PlayerController>();
        }

        if (player != null && !player.dead)
        {
            lastDamageTime = Time.time;

            if (isFatal)
            {
                player.Die();
            }
            else
            {
                player.TakeDamage(damage);
                
                // Aplicar knockback si tiene CharacterController
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null && !player.dead)
                {
                    Vector3 pushDir = (player.transform.position - transform.position).normalized;
                    pushDir.y = 0.5f;
                    cc.Move(pushDir.normalized * knockbackForce * Time.deltaTime);
                }
            }
        }
    }
}
