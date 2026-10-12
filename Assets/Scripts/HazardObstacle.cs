using UnityEngine;

/// <summary>
/// Controla obstáculos dañinos y mortales (pinchos, caídas al vacío, láseres, sierras).
/// Puede infligir daño parcial o muerte instantánea al contacto con el jugador.
/// </summary>
public class HazardObstacle : MonoBehaviour
{
    [Header("Configuración de Daño")]
    [Tooltip("Si está activo, mata instantáneamente al jugador provocando respawn directo.")]
    [SerializeField] private bool instantKill = false;
    [Tooltip("Cantidad de daño si no es muerte instantánea (la vida máxima del jugador es 3).")]
    [SerializeField] private int damageAmount = 1;
    [Tooltip("Tiempo de inmunidad temporal entre daños continuos.")]
    [SerializeField] private float damageCooldown = 1.2f;

    private float nextDamageTime = 0f;

    void OnTriggerEnter(Collider other)
    {
        ProcessHazardInteraction(other.gameObject);
    }

    void OnTriggerStay(Collider other)
    {
        ProcessHazardInteraction(other.gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        ProcessHazardInteraction(collision.gameObject);
    }

    private void ProcessHazardInteraction(GameObject target)
    {
        if (!target.CompareTag("Player")) return;

        if (Time.time < nextDamageTime) return;

        nextDamageTime = Time.time + damageCooldown;

        if (instantKill)
        {
            if (SoundEffectsManager.Instance != null)
                SoundEffectsManager.Instance.PlayHurt();

            target.SendMessage("Die", SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            if (SoundEffectsManager.Instance != null)
                SoundEffectsManager.Instance.PlayHurt();

            target.SendMessage("TakeDamage", damageAmount, SendMessageOptions.DontRequireReceiver);
        }
    }
}
