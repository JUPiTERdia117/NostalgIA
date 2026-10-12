using UnityEngine;

/// <summary>Portal final que termina el capítulo.</summary>
public sealed class Level1Goal : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Level1WorldFlow.Instance?.Win();
    }
}
