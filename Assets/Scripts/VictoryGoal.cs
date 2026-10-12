using UnityEngine;

/// <summary>
/// Meta final del nivel ubicada en el Altar de la Victoria.
/// Al entrar el jugador, reproduce la fanfarria de victoria y activa la pantalla de victoria con sus estadísticas.
/// </summary>
public class VictoryGoal : MonoBehaviour
{
    [SerializeField] private ParticleSystem victoryParticles;
    private bool hasWon = false;

    void OnTriggerEnter(Collider other)
    {
        if (hasWon) return;

        if (other.CompareTag("Player"))
        {
            hasWon = true;

            if (victoryParticles != null)
            {
                victoryParticles.Play();
            }

            if (GameAudio.Instance != null)
            {
                GameAudio.Instance.PlayVictory();
            }

            LevelUIController ui = FindAnyObjectByType<LevelUIController>();
            if (ui != null)
            {
                ui.ShowVictoryScreen();
            }

            Level1Manager manager = FindAnyObjectByType<Level1Manager>();
            if (manager != null)
            {
                manager.OnVictory();
            }
        }
    }
}
