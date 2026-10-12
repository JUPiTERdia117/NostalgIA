using UnityEngine;

/// <summary>
/// Mueve una plataforma entre waypoints con suavidad.
/// Transporta con precisión al jugador si utiliza CharacterController.
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    [Header("Puntos de Movimiento")]
    [Tooltip("Desplazamiento relativo desde la posición inicial hacia el destino.")]
    [SerializeField] private Vector3 movementOffset = new Vector3(0, 0, 10f);
    [Tooltip("Velocidad de traslación en unidades por segundo.")]
    [SerializeField] private float speed = 3.0f;
    [Tooltip("Tiempo de pausa en cada extremo antes de regresar.")]
    [SerializeField] private float pauseDuration = 0.8f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 previousPosition;
    private float journeyProgress = 0f;
    private bool movingToTarget = true;
    private float pauseTimer = 0f;

    private CharacterController playerControllerOnPlatform;

    void Start()
    {
        startPosition = transform.position;
        targetPosition = startPosition + movementOffset;
        previousPosition = transform.position;
    }

    void Update()
    {
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            previousPosition = transform.position;
            return;
        }

        float totalDistance = Vector3.Distance(startPosition, targetPosition);
        if (totalDistance < 0.01f) return;

        float step = (speed / totalDistance) * Time.deltaTime;

        if (movingToTarget)
        {
            journeyProgress += step;
            if (journeyProgress >= 1f)
            {
                journeyProgress = 1f;
                movingToTarget = false;
                pauseTimer = pauseDuration;
            }
        }
        else
        {
            journeyProgress -= step;
            if (journeyProgress <= 0f)
            {
                journeyProgress = 0f;
                movingToTarget = true;
                pauseTimer = pauseDuration;
            }
        }

        // Interpolación suave
        float smoothT = Mathf.SmoothStep(0f, 1f, journeyProgress);
        Vector3 newPos = Vector3.Lerp(startPosition, targetPosition, smoothT);
        
        Vector3 delta = newPos - transform.position;
        transform.position = newPos;

        // Transportar al jugador sobre la plataforma
        if (playerControllerOnPlatform != null)
        {
            playerControllerOnPlatform.Move(delta);
        }

        previousPosition = transform.position;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            CharacterController cc = other.GetComponent<CharacterController>();
            if (cc != null)
            {
                playerControllerOnPlatform = cc;
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (playerControllerOnPlatform != null && other.gameObject == playerControllerOnPlatform.gameObject)
            {
                playerControllerOnPlatform = null;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 start = Application.isPlaying ? startPosition : transform.position;
        Vector3 target = start + movementOffset;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(start, target);
        Gizmos.DrawWireSphere(target, 0.4f);
    }
}
