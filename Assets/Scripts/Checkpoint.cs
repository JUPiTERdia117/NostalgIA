using UnityEngine;

/// <summary>
/// Punto de control interactivo con totem ancestral.
/// Actualiza el punto de reaparición, cambia el color de su cristal de rojo a verde esmeralda
/// y reproduce un sonido de activación.
/// </summary>
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private Renderer crystalRenderer;
    [SerializeField] private Material activeMaterial;
    [SerializeField] private ParticleSystem activateParticles;

    private bool isActivated = false;
    private GameController gameController;

    void Awake()
    {
        gameController = FindAnyObjectByType<GameController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (isActivated) return;

        if (other.CompareTag("Player"))
        {
            isActivated = true;

            if (gameController != null)
            {
                // Aparecer ligeramente elevado y al frente del totem
                Vector3 spawnPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
                gameController.UpdateSpawnPoint(spawnPos);
            }

            if (crystalRenderer != null && activeMaterial != null)
            {
                crystalRenderer.material = activeMaterial;
            }

            if (activateParticles != null)
            {
                activateParticles.Play();
            }

            if (GameAudio.Instance != null)
            {
                GameAudio.Instance.PlayCheckpoint();
            }

            // Notificar a la UI
            LevelUIController ui = FindAnyObjectByType<LevelUIController>();
            if (ui != null)
            {
                ui.ShowToast("¡PUNTO DE CONTROL ALCANZADO!");
            }
        }
    }
}
