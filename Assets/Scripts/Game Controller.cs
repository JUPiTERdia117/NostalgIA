using UnityEngine;
using TMPro;

public class GameController : MonoBehaviour
{
    private int starsCollected = 0;

    private Vector3 spawnPoint = Vector3.zero;

    [SerializeField] TextMeshProUGUI scoreText;


    public void CollectStar()
    {
        starsCollected++;
        scoreText.text = "Stars Collected: " + starsCollected.ToString() + "/3";
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
            player.transform.position = spawnPoint;
            PlayerController playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.dead = false;
                playerController.health = 3;
            }
        }
    }

}
