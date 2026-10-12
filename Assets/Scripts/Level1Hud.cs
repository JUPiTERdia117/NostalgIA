using TMPro;
using UnityEngine;

/// <summary>Actualiza vitalidad y avisos de objetivo del nivel.</summary>
public class Level1Hud : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI statusText;

    private PlayerController player;
    private GameController gameController;
    private float statusExpiresAt;
    private string currentMessage;

    void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        gameController = FindAnyObjectByType<GameController>();
        if (gameController != null) gameController.CheckpointReached += OnCheckpointReached;
        ShowMessage("OBJETIVO   Reúne los 3 prismas y alcanza el portal.", 8f);
        RefreshHealth();
    }

    void Update()
    {
        RefreshHealth();
        if (statusText != null && Time.unscaledTime > statusExpiresAt && currentMessage != null)
        {
            currentMessage = null;
            statusText.text = string.Empty;
        }
    }

    public void ShowMessage(string message, float duration = 3.5f)
    {
        currentMessage = message;
        statusExpiresAt = Time.unscaledTime + duration;
        if (statusText != null) statusText.text = message;
    }

    private void RefreshHealth()
    {
        if (healthText == null || player == null) return;
        healthText.text = $"VITALIDAD   {player.health} / 3";
    }

    private void OnCheckpointReached()
    {
        ShowMessage("PUNTO DE REAPARICIÓN ACTIVADO", 4f);
    }

    void OnDestroy()
    {
        if (gameController != null) gameController.CheckpointReached -= OnCheckpointReached;
    }
}
