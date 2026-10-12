using UnityEngine;
using TMPro;

/// <summary>
/// Gestiona la meta de victoria del Nivel 1.
/// Al entrar el jugador, activa la pantalla de victoria y muestra las estrellas obtenidas.
/// </summary>
public class VictoryTrigger : MonoBehaviour
{
    [Header("UI de Victoria")]
    [Tooltip("Panel o Canvas que se activará al alcanzar la victoria.")]
    [SerializeField] private GameObject victoryPanel;
    [Tooltip("Texto opcional para mostrar el resumen de estrellas.")]
    [SerializeField] private TextMeshProUGUI victoryScoreText;

    [Header("Efectos")]
    [SerializeField] private GameObject celebrationEffects;
    [SerializeField] private Light victoryLight;

    private bool hasWon = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasWon) return;

        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
        {
            TriggerVictory();
        }
    }

    private void TriggerVictory()
    {
        hasWon = true;

        GameController gameController = FindAnyObjectByType<GameController>();
        int stars = gameController != null ? gameController.StarsCollected : 0;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        if (victoryScoreText != null)
        {
            victoryScoreText.text = $"Estrellas Conseguidas: {stars}/3";
        }

        if (celebrationEffects != null)
        {
            celebrationEffects.SetActive(true);
        }

        if (victoryLight != null)
        {
            victoryLight.intensity *= 2f;
        }

        // Desbloquear cursor si es necesario
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
