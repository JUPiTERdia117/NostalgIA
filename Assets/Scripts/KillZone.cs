using UnityEngine;

/// <summary>
/// Zona de vacío mortal que se ubica debajo de las islas flotantes.
/// Elimina al jugador de inmediato y lo reaparece en su último punto de control.
/// </summary>
public class KillZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null && other.CompareTag("Player"))
        {
            player = other.GetComponent<PlayerController>();
        }

        if (player != null && !player.dead)
        {
            if (GameAudio.Instance != null)
            {
                GameAudio.Instance.PlayHurt();
            }
            player.Die();
        }
    }
}
