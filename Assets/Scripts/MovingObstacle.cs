using UnityEngine;

/// <summary>
/// Controla plataformas móviles y obstáculos oscilantes o giratorios.
/// Permite movimiento lineal tipo ping-pong y rotación continua.
/// </summary>
public class MovingObstacle : MonoBehaviour
{
    public enum MovementType { PingPong, Rotate, PingPongAndRotate }

    [SerializeField] private MovementType movementType = MovementType.PingPong;

    [Header("Movimiento Lineal")]
    [Tooltip("Punto inicial en coordenadas del mundo. Si se deja en cero, se toma la posición inicial.")]
    [SerializeField] private Vector3 startOffset = Vector3.zero;
    [Tooltip("Desplazamiento total relativo a la posición inicial hasta donde viajará.")]
    [SerializeField] private Vector3 moveOffset = new Vector3(0, 0, 5);
    [Tooltip("Velocidad de traslación en unidades por segundo.")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 3f;
    [Tooltip("Tiempo de pausa en cada extremo antes de regresar.")]
    [SerializeField, Min(0f)] private float pauseDuration = 0.5f;

    [Header("Rotación Continua")]
    [Tooltip("Eje y velocidad angular en grados por segundo.")]
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0, 45, 0);

    private Vector3 originPosition;
    private Vector3 targetPosition;
    private float journeyLength;
    private float pauseTimer;
    private bool movingToTarget = true;
    private float t = 0f;

    private Transform playerPassenger = null;
    private Vector3 lastPlatformPos;

    private void Start()
    {
        originPosition = transform.position + startOffset;
        targetPosition = originPosition + moveOffset;
        journeyLength = Vector3.Distance(originPosition, targetPosition);
        if (journeyLength < 0.01f) journeyLength = 0.01f;
        lastPlatformPos = transform.position;
    }

    private void Update()
    {
        Vector3 previousPosition = transform.position;

        // 1. Manejo de Rotación
        if (movementType == MovementType.Rotate || movementType == MovementType.PingPongAndRotate)
        {
            transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
        }

        // 2. Manejo de Traslación
        if (movementType == MovementType.PingPong || movementType == MovementType.PingPongAndRotate)
        {
            if (pauseTimer > 0f)
            {
                pauseTimer -= Time.deltaTime;
            }
            else
            {
                float step = (moveSpeed / journeyLength) * Time.deltaTime;
                t += movingToTarget ? step : -step;

                if (t >= 1f)
                {
                    t = 1f;
                    movingToTarget = false;
                    pauseTimer = pauseDuration;
                }
                else if (t <= 0f)
                {
                    t = 0f;
                    movingToTarget = true;
                    pauseTimer = pauseDuration;
                }

                // Suavizado cinemático
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                transform.position = Vector3.Lerp(originPosition, targetPosition, smoothT);
            }
        }

        // 3. Arrastre de jugador para plataformas móviles
        if (playerPassenger != null)
        {
            Vector3 deltaMove = transform.position - previousPosition;
            CharacterController cc = playerPassenger.GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
            {
                cc.Move(deltaMove);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerPassenger = other.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (playerPassenger == other.transform)
            {
                playerPassenger = null;
            }
        }
    }
}
