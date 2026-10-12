using UnityEngine;
using TMPro;

public class GameController : MonoBehaviour
{
    private int starsCollected = 0;
    public int StarsCollected => starsCollected;

    private Vector3 spawnPoint = Vector3.zero;

    [SerializeField] TextMeshProUGUI scoreText;
    [SerializeField] GameObject victoryPanel;

    public bool isVictory { get; private set; }

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            spawnPoint = player.transform.position;
        }
        UpdateScoreDisplay();
    }

    public void CollectStar()
    {
        starsCollected++;
        UpdateScoreDisplay();
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = "Stars Collected: " + starsCollected.ToString() + "/3";
        }
    }

    public void UpdateSpawnPoint(Vector3 newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
    }

    [ContextMenu("Respawn Player")]
    public void RespawnPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = spawnPoint;

            if (cc != null) cc.enabled = true;

            PlayerController playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.dead = false;
                playerController.health = 3;
            }
        }
    }

    [ContextMenu("Win Game")]
    public void WinGame()
    {
        if (isVictory) return;
        isVictory = true;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
