using UnityEngine;

public class RespawnController : MonoBehaviour
{
    GameController gameController;



    void Awake()
    {
        gameController = FindAnyObjectByType<GameController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            gameController.UpdateSpawnPoint(transform.position);
            Destroy(gameObject);
        }
    }


}
