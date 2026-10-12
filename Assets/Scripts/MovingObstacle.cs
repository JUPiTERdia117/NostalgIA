using UnityEngine;

/// <summary>
/// Mueve una plataforma u obstáculo entre su posición original y un desplazamiento configurado.
/// Permite crear plataformas móviles sincronizadas y trampas oscilantes.
/// </summary>
public class MovingObstacle : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Desplazamiento relativo hacia el cual se mueve.")]
    [SerializeField] private Vector3 moveOffset = new Vector3(0f, 0f, 6f);
    [Tooltip("Velocidad de desplazamiento.")]
    [SerializeField] private float speed = 2.2f;
    [Tooltip("Tiempo de pausa en los extremos.")]
    [SerializeField] private float pauseDuration = 0.4f;
    [Tooltip("Si es verdadero, utiliza suavizado en los extremos del trayecto.")]
    [SerializeField] private bool easeMovement = true;

    private Vector3 startPoint;
    private Vector3 endPoint;
    private float progress = 0f;
    private bool movingForward = true;
    private float waitTimer = 0f;
    private Rigidbody rb;
    private Vector3 lastPosition;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    void Start()
    {
        startPoint = transform.position;
        endPoint = startPoint + moveOffset;
        lastPosition = transform.position;
    }

    void FixedUpdate()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            lastPosition = transform.position;
            return;
        }

        float journeyLength = Vector3.Distance(startPoint, endPoint);
        if (journeyLength <= 0.001f) return;

        float step = (speed / journeyLength) * Time.fixedDeltaTime;

        if (movingForward)
        {
            progress += step;
            if (progress >= 1f)
            {
                progress = 1f;
                movingForward = false;
                waitTimer = pauseDuration;
            }
        }
        else
        {
            progress -= step;
            if (progress <= 0f)
            {
                progress = 0f;
                movingForward = true;
                waitTimer = pauseDuration;
            }
        }

        float evaluatedT = easeMovement ? Mathf.SmoothStep(0f, 1f, progress) : progress;
        Vector3 targetPos = Vector3.Lerp(startPoint, endPoint, evaluatedT);

        Vector3 deltaMovement = targetPos - transform.position;

        if (rb != null)
        {
            rb.MovePosition(targetPos);
        }
        else
        {
            transform.position = targetPos;
        }

        lastPosition = targetPos;
    }

    private void OnTriggerStay(Collider other)
    {
        // Acompañar al CharacterController del jugador sobre la plataforma móvil
        if (other.CompareTag("Player"))
        {
            CharacterController cc = other.GetComponent<CharacterController>();
            if (cc != null && waitTimer <= 0f)
            {
                Vector3 delta = transform.position - lastPosition;
                cc.Move(delta);
            }
        }
    }
}
