using UnityEngine;

/// <summary>
/// Controla un tótem de punto de control (Checkpoint). Al tocarlo, actualiza la posición de reaparición del jugador,
/// ilumina el monumento con un cristal verde y reproduce sonido y efectos de partículas.
/// </summary>
public class EnhancedCheckpoint : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Renderer crystalRenderer;
    [SerializeField] private Material activeMaterial;
    [SerializeField] private Light checkpointLight;
    [SerializeField] private ParticleSystem activateParticles;
    [SerializeField] private Transform respawnPoint;

    private bool isActivated = false;
    private GameController gameController;

    void Awake()
    {
        gameController = FindAnyObjectByType<GameController>();
        if (respawnPoint == null)
            respawnPoint = transform;
    }

    void OnTriggerEnter(Collider other)
    {
        if (isActivated) return;

        if (other.CompareTag("Player"))
        {
            ActivateCheckpoint();
        }
    }

    private void ActivateCheckpoint()
    {
        isActivated = true;

        Vector3 targetRespawn = respawnPoint.position + Vector3.up * 0.5f;

        if (gameController != null)
        {
            gameController.UpdateSpawnPoint(targetRespawn);
        }

        if (crystalRenderer != null && activeMaterial != null)
        {
            crystalRenderer.material = activeMaterial;
        }

        if (checkpointLight != null)
        {
            checkpointLight.color = new Color(0.2f, 1f, 0.5f, 1f);
            checkpointLight.intensity *= 1.8f;
        }

        if (activateParticles != null)
        {
            activateParticles.Play();
        }

        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.PlayCheckpoint();
        }

        if (LevelUIController.Instance != null)
        {
            LevelUIController.Instance.ShowNotification("¡PUNTO DE CONTROL GUARDADO!");
        }
    }
}
