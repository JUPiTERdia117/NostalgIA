using UnityEngine;

/// <summary>
/// Punto de reaparición / Checkpoint.
/// Al ser activado por el jugador, actualiza el punto de reaparición en GameController
/// y muestra retroalimentación visual/sonora.
/// </summary>
public class RespawnController : MonoBehaviour
{
    [SerializeField] private Color activatedColor = new Color(0.2f, 1f, 0.4f, 1f);
    [SerializeField] private string checkpointName = "¡Punto de control activado!";

    public bool IsActivated => activated;

    private GameController gameController;
    private bool activated = false;
    private Renderer checkpointRenderer;
    private Light checkpointLight;

    private void Awake()
    {
        gameController = FindAnyObjectByType<GameController>();
        checkpointRenderer = GetComponentInChildren<Renderer>();
        checkpointLight = GetComponentInChildren<Light>();
    }

    public void ActivateVisuals()
    {
        activated = true;
        if (checkpointLight == null) checkpointLight = GetComponentInChildren<Light>();
        if (checkpointRenderer == null) checkpointRenderer = GetComponentInChildren<Renderer>();

        if (checkpointLight != null)
        {
            checkpointLight.color = activatedColor;
            checkpointLight.intensity = 3.6f;
        }

        if (checkpointRenderer != null)
        {
            checkpointRenderer.material.color = activatedColor;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (activated) return;

        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
        {
            ActivateVisuals();

            if (gameController == null)
            {
                gameController = FindAnyObjectByType<GameController>();
            }

            if (gameController != null)
            {
                // Usar posición del checkpoint con ligera elevación para evitar quedar atrapado en el piso
                gameController.UpdateSpawnPoint(transform.position + Vector3.up * 0.2f);
                gameController.NotifyCheckpoint(checkpointName);
            }
        }
    }
}
