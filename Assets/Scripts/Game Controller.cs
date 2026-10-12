using UnityEngine;
using TMPro;

public class GameController : MonoBehaviour
{
    private int starsCollected = 0;

    private Vector3 spawnPoint = Vector3.zero;

    [SerializeField] TextMeshProUGUI scoreText;

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            spawnPoint = player.transform.position;
        }
    }

    public void CollectStar()
    {
        starsCollected++;
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
            player.transform.position = spawnPoint + Vector3.up * 0.1f;
            if (cc != null) cc.enabled = true;

            PlayerController playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.dead = false;
                playerController.health = 3;
            }
        }
    }
}
