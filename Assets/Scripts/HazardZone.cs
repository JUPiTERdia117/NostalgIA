using UnityEngine;

/// <summary>
/// Zona de peligro (abismo / caída al vacío). Si el jugador entra en contacto, reinicia en el último punto de respawn.
/// </summary>
public class HazardZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage(pc.health);
            }
        }
    }
}
