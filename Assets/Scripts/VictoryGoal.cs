using UnityEngine;

/// <summary>
/// Controla la meta y condición de victoria del nivel. Al ingresar el jugador,
/// activa fuegos artificiales, sonido triunfal y despliega la pantalla de victoria.
/// </summary>
public class VictoryGoal : MonoBehaviour
{
    [Header("Efectos")]
    [SerializeField] private ParticleSystem victoryFireworks;
    [SerializeField] private Light victoryLight;

    private bool hasWon = false;

    void OnTriggerEnter(Collider other)
    {
        if (hasWon) return;

        if (other.CompareTag("Player"))
        {
            TriggerVictory(other.gameObject);
        }
    }

    private void TriggerVictory(GameObject player)
    {
        hasWon = true;

        if (victoryFireworks != null)
        {
            victoryFireworks.Play();
        }

        if (victoryLight != null)
        {
            victoryLight.intensity *= 2.5f;
        }

        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.PlayVictory();
        }

        if (LevelUIController.Instance != null)
        {
            LevelUIController.Instance.ShowVictoryScreen();
        }
    }
}
