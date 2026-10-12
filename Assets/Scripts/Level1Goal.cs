using TMPro;
using UnityEngine;

/// <summary>Abre el final del nivel cuando el jugador reúne los tres prismas.</summary>
public class Level1Goal : MonoBehaviour
{
    [SerializeField, Min(0)] private int requiredPrisms = 3;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Level1Hud hud;

    private GameController gameController;
    private bool completed;

    void Awake()
    {
        gameController = FindAnyObjectByType<GameController>();
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (completed || !other.CompareTag("Player")) return;
        if (gameController == null) gameController = FindAnyObjectByType<GameController>();
        if (gameController == null) return;

        if (gameController.StarsCollected < requiredPrisms)
        {
            string message = $"PORTAL SELLADO   {gameController.StarsCollected} / {requiredPrisms} prismas";
            if (hud != null) hud.ShowMessage(message, 4f);
            else if (statusText != null) statusText.text = message;
            return;
        }

        completed = true;
        gameController.CompleteLevel();
        if (victoryPanel != null) victoryPanel.SetActive(true);
        Time.timeScale = 0f;
    }
}
