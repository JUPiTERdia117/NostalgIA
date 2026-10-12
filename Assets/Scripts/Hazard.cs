using UnityEngine;

/// <summary>
/// Obstáculo que inflige daño o elimina al jugador al contacto.
/// Soporta tanto triggers como colisiones físicas con tiempo de invulnerabilidad.
/// </summary>
public class Hazard : MonoBehaviour
{
    [Header("Configuración de Daño")]
    [Tooltip("Cantidad de daño infringido al jugador.")]
    [SerializeField] private int damage = 1;
    [Tooltip("Si es verdadero, elimina inmediatamente al jugador forzando respawn.")]
    [SerializeField] private bool isInstantKill = false;
    [Tooltip("Fuerza del empuje recibido al colisionar.")]
    [SerializeField] private float knockbackForce = 6f;
    [Tooltip("Tiempo mínimo entre impactos sucesivos.")]
    [SerializeField] private float hitCooldown = 0.8f;

    private float lastHitTime = -10f;

    void OnTriggerEnter(Collider other)
    {
        ProcessHit(other.gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        ProcessHit(collision.gameObject);
    }

    private void ProcessHit(GameObject target)
    {
        if (Time.time - lastHitTime < hitCooldown) return;

        PlayerController player = target.GetComponentInParent<PlayerController>();
        if (player == null && target.CompareTag("Player"))
        {
            player = target.GetComponent<PlayerController>();
        }

        if (player != null && !player.dead)
        {
            lastHitTime = Time.time;

            if (GameAudio.Instance != null)
            {
                GameAudio.Instance.PlayHurt();
            }

            if (isInstantKill)
            {
                player.Die();
            }
            else
            {
                player.TakeDamage(damage);

                // Aplicar knockback si tiene CharacterController
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null)
                {
                    Vector3 away = (player.transform.position - transform.position).normalized;
                    away.y = 0.4f;
                    cc.Move(away * knockbackForce * Time.deltaTime * 20f);
                }
            }
        }
    }
}
