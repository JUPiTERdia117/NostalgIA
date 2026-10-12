using UnityEngine;

/// <summary>
/// Controla la recolección de las Estrellas Celestiales del nivel.
/// </summary>
public class StarController : MonoBehaviour
{
    private GameController gameController;
    private bool collected = false;

    void Awake()
    {
        gameController = FindAnyObjectByType<GameController>();
    }

    void OnTriggerEnter(Collider col)
    {
        if (collected) return;

        if (col.CompareTag("Player") || col.GetComponentInParent<PlayerController>() != null)
        {
            collected = true;

            if (gameController != null)
            {
                gameController.CollectStar();
            }

            if (GameAudio.Instance != null)
            {
                GameAudio.Instance.PlayCheckpoint();
            }

            LevelUIController ui = FindAnyObjectByType<LevelUIController>();
            if (ui != null && gameController != null)
            {
                ui.ShowToast($"¡ESTRELLA CELESTIAL RECOGIDA! ({gameController.StarsCollected}/3)");
            }

            Destroy(gameObject);
        }
    }
}
