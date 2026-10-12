using UnityEngine;

public class StarController : MonoBehaviour
{
    private Vector3 initialPosition;

    void Awake()
    {
        gameController = FindAnyObjectByType<GameController>();
        initialPosition = transform.position;
    }

    void Update()
    {
        transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
        transform.position = initialPosition + new Vector3(0f, Mathf.Sin(Time.time * 2.5f) * 0.18f, 0f);
    }

    void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Player") || col.transform.root.CompareTag("Player"))
        {
            if (gameController == null) gameController = FindAnyObjectByType<GameController>();
            if (gameController != null) gameController.CollectStar();
            Destroy(gameObject);
        }
    }
}
