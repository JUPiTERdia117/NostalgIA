using UnityEngine;

/// <summary>
/// Estrella coleccionable para el tutorial y niveles.
/// Rota continuamente y se balancea; al ser recogida por el jugador,
/// notifica al GameController y se destruye.
/// </summary>
public class StarController : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobFrequency = 2f;

    private GameController gameController;
    private Vector3 startPosition;
    private bool collected = false;

    private void Awake()
    {
        gameController = FindAnyObjectByType<GameController>();
        startPosition = transform.position;
    }

    private void Update()
    {
        // Animación de rotación y flotación suave
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        float newY = startPosition.y + Mathf.Sin(Time.time * bobFrequency) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    private void OnTriggerEnter(Collider col)
    {
        if (collected) return;

        if (col.CompareTag("Player") || col.GetComponentInParent<PlayerController>() != null)
        {
            collected = true;
            if (gameController == null)
            {
                gameController = FindAnyObjectByType<GameController>();
            }

            if (gameController != null)
            {
                gameController.CollectStar(gameObject.name);
            }

            Destroy(gameObject);
        }
    }
}
