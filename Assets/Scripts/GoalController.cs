using UnityEngine;

/// <summary>
/// Controla la meta y portal de victoria al final del nivel.
/// Al entrar el jugador en el área, notifica al GameController y desata la secuencia de victoria.
/// </summary>
public class GoalController : MonoBehaviour
{
    [SerializeField] private GameObject victoryFX;
    [SerializeField] private Light victoryLight;
    [SerializeField] private float rotationSpeedOnWin = 180f;

    private bool isReached = false;
    private GameController gameController;

    private void Awake()
    {
        gameController = FindAnyObjectByType<GameController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isReached) return;

        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
        {
            isReached = true;

            if (gameController == null)
            {
                gameController = FindAnyObjectByType<GameController>();
            }

            if (gameController != null)
            {
                gameController.WinGame();
            }

            if (victoryFX != null)
            {
                victoryFX.SetActive(true);
            }

            if (victoryLight != null)
            {
                victoryLight.intensity *= 2.5f;
            }
        }
    }

    private void Update()
    {
        if (isReached)
        {
            transform.Rotate(Vector3.up, rotationSpeedOnWin * Time.deltaTime, Space.World);
        }
    }
}
