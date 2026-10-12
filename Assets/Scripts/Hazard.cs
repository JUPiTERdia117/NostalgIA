using UnityEngine;

/// <summary>
/// Controla obstáculos mortales y peligros que dañan o eliminan al jugador.
/// Puede ser un plano de muerte (abismo), pinchos, sierras giratorias o lásers.
/// </summary>
public class Hazard : MonoBehaviour
{
    [Tooltip("Si está activo, elimina al jugador inmediatamente. Si no, resta puntos de vida.")]
    [SerializeField] private bool instantKill = false;
    [Tooltip("Cantidad de daño aplicada al jugador si no es muerte instantánea.")]
    [SerializeField, Min(1)] private int damageAmount = 1;
    [Tooltip("Tiempo en segundos de invulnerabilidad entre impactos para no drenar toda la vida instantáneamente.")]
    [SerializeField, Min(0.1f)] private float damageCooldown = 1.0f;
    [Tooltip("Fuerza de empuje aplicada al jugador al hacer contacto.")]
    [SerializeField] private float knockbackForce = 5f;

    private float lastDamageTime = -999f;

    private void OnTriggerEnter(Collider other)
    {
        HandlePlayerCollision(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        HandlePlayerCollision(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandlePlayerCollision(collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandlePlayerCollision(collision.gameObject);
    }

    private void HandlePlayerCollision(GameObject target)
    {
        if (Time.time - lastDamageTime < damageCooldown && !instantKill) return;

        PlayerController player = target.GetComponentInParent<PlayerController>();
        if (player == null && target.CompareTag("Player"))
        {
            player = target.GetComponent<PlayerController>();
        }

        if (player != null && !player.dead)
        {
            lastDamageTime = Time.time;

            if (instantKill)
            {
                player.Die();
            }
            else
            {
                player.TakeDamage(damageAmount);

                // Empuje lejos del obstáculo si el jugador sigue con vida
                if (!player.dead && knockbackForce > 0f)
                {
                    CharacterController cc = player.GetComponent<CharacterController>();
                    if (cc != null)
                    {
                        Vector3 pushDir = (player.transform.position - transform.position).normalized;
                        pushDir.y = 0.5f;
                        cc.Move(pushDir.normalized * knockbackForce * Time.deltaTime);
                    }
                }
            }
        }
    }
}
