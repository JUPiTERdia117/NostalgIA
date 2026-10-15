using UnityEngine;

/// <summary>
/// Meta final del nivel de tutorial.
/// Al pasar o colocarse sobre la puerta, se activa la victoria inmediatamente.
/// Cuenta con detección de trigger y comprobación continua de proximidad.
/// </summary>
public class TutorialDoor : MonoBehaviour
{
    [Header("Visual Feedback")]
    [SerializeField] private Light doorLight;
    [SerializeField] private Color victoryColor = new Color(0.2f, 1f, 0.4f, 1f);
    [SerializeField] private float triggerDistance = 4.0f;

    private GameController gameController;
    private PlayerController player;
    private bool hasTriggered = false;

    private void Awake()
    {
        gameController = FindAnyObjectByType<GameController>();
        FindPlayer();
    }

    private void Start()
    {
        if (player == null) FindPlayer();
    }

    private void Update()
    {
        if (hasTriggered) return;

        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        // Comprobación de proximidad en tiempo real por si PhysX pasa por alto el trigger
        Vector3 playerPos = player.transform.position;
        Vector3 doorPos = transform.position;

        float horizDist = Vector2.Distance(new Vector2(playerPos.x, playerPos.z), new Vector2(doorPos.x, doorPos.z));
        float vertDist = Mathf.Abs(playerPos.y - doorPos.y);

        // Si el jugador está dentro del arco de la puerta en la cima de la escalera
        if (horizDist <= triggerDistance && vertDist <= 4.0f)
        {
            TriggerVictory();
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<PlayerController>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
        {
            TriggerVictory();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (hasTriggered) return;
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
        {
            TriggerVictory();
        }
    }

    private void TriggerVictory()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        Debug.Log("[TutorialDoor] ¡El jugador cruzó la puerta de salida! ¡Victoria del tutorial!");

        if (doorLight != null)
        {
            doorLight.color = victoryColor;
            doorLight.intensity = 6.0f;
            doorLight.range = 15f;
        }

        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = victoryColor;
            rend.material.SetColor("_BaseColor", victoryColor);
            rend.material.SetColor("_EmissionColor", victoryColor * 2.5f);
            rend.material.EnableKeyword("_EMISSION");
        }

        if (gameController == null)
        {
            gameController = FindAnyObjectByType<GameController>();
        }

        if (gameController != null)
        {
            gameController.WinLevel();
        }
    }
}
