using UnityEngine;

/// <summary>
/// Disparador que actualiza la guía dinámica del tutorial en la interfaz
/// cuando el jugador ingresa a una nueva zona.
/// </summary>
public class TutorialZonePrompt : MonoBehaviour
{
    [Header("Guía del Tutorial")]
    [SerializeField] private string phaseBadge = "FASE 1 / 4 · FUNDAMENTOS";
    [SerializeField] private string zoneTitle = "MOVIMIENTO Y EXPLORACIÓN";
    [TextArea(2, 4)]
    [SerializeField] private string description = "Avanza por el pasillo inicial y acostúmbrate a los controles del traje.";
    [SerializeField] private string controls = "<color=#FFE600><b>[ W A S D ]</b></color> Moverse   <color=#FFE600><b>[ ESPACIO ]</b></color> Saltar   <color=#FFE600><b>[ SHIFT ]</b></color> Correr";
    [SerializeField] private string task = ">> Encuentra la 1ra Estrella dorada.";

    [Header("Configuración")]
    [SerializeField] private bool triggerOnce = false;

    private bool hasTriggered = false;
    private GameController gameController;

    private void Awake()
    {
        gameController = FindAnyObjectByType<GameController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered) return;

        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
        {
            hasTriggered = true;
            if (gameController == null)
            {
                gameController = FindAnyObjectByType<GameController>();
            }

            if (gameController != null)
            {
                gameController.SetTutorialGuide(phaseBadge, zoneTitle, description, controls, task);
            }
        }
    }
}
