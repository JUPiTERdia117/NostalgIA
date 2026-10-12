using UnityEngine;

/// <summary>
/// Plataforma móvil para plataformas 3D.
/// Se desplaza suavemente entre waypoints o sobre un desplazamiento configurado
/// y transporta al jugador mientras permanece sobre ella.
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    [Header("Ruta de Movimiento")]
    [Tooltip("Desplazamiento relativo desde la posición inicial si no se especifican waypoints")]
    [SerializeField] private Vector3 moveOffset = new Vector3(0f, 0f, 10f);
    [Tooltip("Velocidad de traslación")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 3.0f;
    [Tooltip("Tiempo de espera en cada extremo")]
    [SerializeField, Min(0f)] private float waitTime = 1.0f;
    [Tooltip("Suavizar el arranque y frenada con curva S")]
    [SerializeField] private bool smoothEasing = true;

    [Header("Rotación Opcional")]
    [Tooltip("Velocidad angular mientras se mueve (grados por segundo)")]
    [SerializeField] private Vector3 rotationSpeed = Vector3.zero;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float journeyLength;
    private float currentProgress = 0f;
    private bool movingToTarget = true;
    private float waitTimer = 0f;
    private Vector3 lastPosition;

    void Start()
    {
        startPosition = transform.position;
        targetPosition = startPosition + moveOffset;
        journeyLength = Vector3.Distance(startPosition, targetPosition);
        if (journeyLength < 0.001f) journeyLength = 0.001f;
        lastPosition = transform.position;
    }

    void Update()
    {
        // Rotación continua si está configurada
        if (rotationSpeed != Vector3.zero)
        {
            transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
        }

        // Si está en pausa de espera
        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            lastPosition = transform.position;
            return;
        }

        float step = (moveSpeed * Time.deltaTime) / journeyLength;
        currentProgress += movingToTarget ? step : -step;

        if (currentProgress >= 1f)
        {
            currentProgress = 1f;
            movingToTarget = false;
            waitTimer = waitTime;
        }
        else if (currentProgress <= 0f)
        {
            currentProgress = 0f;
            movingToTarget = true;
            waitTimer = waitTime;
        }

        float t = smoothEasing ? Mathf.SmoothStep(0f, 1f, currentProgress) : currentProgress;
        Vector3 newPos = Vector3.Lerp(startPosition, targetPosition, t);

        Vector3 deltaMovement = newPos - transform.position;
        transform.position = newPos;

        lastPosition = transform.position;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.SetParent(transform, true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.transform.parent == transform)
        {
            other.transform.SetParent(null, true);
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 pStart = Application.isPlaying ? startPosition : transform.position;
        Vector3 pEnd = pStart + moveOffset;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pStart, pEnd);
        Gizmos.DrawWireCube(pStart, transform.localScale);
        Gizmos.DrawWireCube(pEnd, transform.localScale);
    }
}
