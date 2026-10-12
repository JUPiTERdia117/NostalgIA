using UnityEngine;

public class RespawnController : MonoBehaviour
{
    GameController gameController;
    private bool activated;


    void Awake()
    {
        gameController = FindAnyObjectByType<GameController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!activated && other.CompareTag("Player"))
        {
            if (gameController == null) gameController = FindAnyObjectByType<GameController>();
            if (gameController != null)
            {
                gameController.UpdateSpawnPoint(transform.position);
                activated = true;
            }
        }
    }


}
