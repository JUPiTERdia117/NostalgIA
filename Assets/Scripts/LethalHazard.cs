using UnityEngine;

/// <summary>El contacto con una trampa o la caída devuelve al jugador al último punto seguro.</summary>
public class LethalHazard : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null) player.KillPlayer();
    }
}
