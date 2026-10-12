using UnityEngine;

/// <summary>
/// Coordinador principal del Nivel 1.
/// Gestiona la inicialización de la escena, Audio procedural y eventos de nivel.
/// </summary>
public class Level1Manager : MonoBehaviour
{
    [Header("Configuración de Inicio")]
    [SerializeField] private Transform initialSpawnPoint;

    private GameController gameController;

    void Awake()
    {
        // Asegurar que GameAudio exista en la escena
        if (FindAnyObjectByType<GameAudio>() == null)
        {
            GameObject audioObj = new GameObject("GameAudio");
            audioObj.AddComponent<GameAudio>();
        }

        gameController = FindAnyObjectByType<GameController>();
    }

    void Start()
    {
        if (initialSpawnPoint != null && gameController != null)
        {
            gameController.UpdateSpawnPoint(initialSpawnPoint.position);
        }
    }

    public void OnVictory()
    {
        Debug.Log("¡Victoria! Nivel 1 completado con éxito.");
    }
}
