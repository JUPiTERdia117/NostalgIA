using UnityEngine;

/// <summary>
/// Controla la estrella coleccionable en 3D: animación de rotación, flotación,
/// sonido armónico y notificación al GameController y HUD.
/// </summary>
public class CollectibleStar : MonoBehaviour
{
    [Header("Animación")]
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float bobbingAmplitude = 0.2f;
    [SerializeField] private float bobbingFrequency = 2.5f;

    [Header("Efectos")]
    [SerializeField] private ParticleSystem collectParticles;

    private Vector3 initialPosition;
    private GameController gameController;
    private bool isCollected = false;

    void Awake()
    {
        initialPosition = transform.position;
        gameController = FindAnyObjectByType<GameController>();
    }

    void Update()
    {
        if (isCollected) return;

        // Rotación continua
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // Flotación senoidal
        float bob = Mathf.Sin(Time.time * bobbingFrequency) * bobbingAmplitude;
        transform.position = initialPosition + Vector3.up * bob;
    }

    void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        if (other.CompareTag("Player"))
        {
            CollectStar();
        }
    }

    private void CollectStar()
    {
        isCollected = true;

        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.PlayStar();
        }

        if (gameController != null)
        {
            gameController.CollectStar();
        }

        if (LevelUIController.Instance != null)
        {
            LevelUIController.Instance.OnStarCollected();
        }

        if (collectParticles != null)
        {
            collectParticles.transform.parent = null;
            collectParticles.Play();
            Destroy(collectParticles.gameObject, 1.5f);
        }

        Destroy(gameObject);
    }
}
