using UnityEngine;
using TMPro;

public class GameController : MonoBehaviour
{
    private int starsCollected = 0;
    private Vector3 spawnPoint = new Vector3(0, 1.2f, 0);

    [SerializeField] TextMeshProUGUI scoreText;

    public int StarsCollected => starsCollected;
    public Vector3 CurrentSpawnPoint => spawnPoint;
    public System.Action<int> onStarCollected;
    public System.Action onPlayerRespawned;

    void Start()
    {
        UpdateScoreDisplay();
    }

    public void CollectStar()
    {
        starsCollected++;
        UpdateScoreDisplay();
        onStarCollected?.Invoke(starsCollected);
    }

    public void UpdateSpawnPoint(Vector3 newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = "Stars Collected: " + starsCollected.ToString() + "/3";
        }
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
                playerController.ResetHealth(3);
            }
        }
        onPlayerRespawned?.Invoke();
    }
}
