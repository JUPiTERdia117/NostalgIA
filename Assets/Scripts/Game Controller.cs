using UnityEngine;
using TMPro;
using System;

public class GameController : MonoBehaviour
{
    private int starsCollected = 0;

    private Vector3 spawnPoint = Vector3.zero;

    [SerializeField] TextMeshProUGUI scoreText;
    public int StarsCollected => starsCollected;
    public bool HasWon { get; private set; }
    public event Action CheckpointReached;

    void Start()
    {
        UpdateScoreText();
    }


    public void CollectStar()
    {
        starsCollected++;
        UpdateScoreText();
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = $"PRISMAS   {starsCollected} / 3";
    }

    public void CompleteLevel()
    {
        HasWon = true;
    }

    public void UpdateSpawnPoint(Vector3 newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
        CheckpointReached?.Invoke();

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
