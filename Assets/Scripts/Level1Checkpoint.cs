using UnityEngine;

/// <summary>Checkpoint que guarda la posición segura al tocar su baliza.</summary>
public sealed class Level1Checkpoint : MonoBehaviour
{
    private bool activated;

    private void OnTriggerEnter(Collider other)
    {
        if (activated || !other.CompareTag("Player")) return;
        activated = true;
        Level1WorldFlow.Instance?.SetCheckpoint(transform.position);
    }
}
