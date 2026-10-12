using UnityEngine;

/// <summary>Trampa de contacto. Las hojas móviles también llevan este componente en su volumen de impacto.</summary>
public sealed class Level1Hazard : MonoBehaviour
{
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField] private bool lethal;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Level1WorldFlow.Instance?.DamagePlayer(damage, lethal);
    }
}
