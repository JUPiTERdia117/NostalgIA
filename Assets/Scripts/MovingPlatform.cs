using UnityEngine;

/// <summary>
/// Plataforma móvil con trayectoria sinusoidal y soporte para transportar al CharacterController.
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    [Header("Puntos de Trayectoria")]
    [SerializeField] private Vector3 offset = new Vector3(0, 0, 10f);
    [SerializeField] private float speed = 2.5f;
    [SerializeField] private float waitTimeAtEnds = 0.5f;

    private Vector3 startPos;
    private Vector3 targetPos;
    private Vector3 lastPos;
    private float progress = 0f;
    private bool movingForward = true;
    private float waitTimer = 0f;

    private CharacterController passenger;

    void Awake()
    {
        // Añadir trigger de detección de pasajero si no existe
        BoxCollider[] cols = GetComponents<BoxCollider>();
        bool hasTrigger = false;
        foreach (var c in cols)
        {
            if (c.isTrigger) { hasTrigger = true; break; }
        }

        if (!hasTrigger)
        {
            BoxCollider solid = GetComponent<BoxCollider>();
            BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            if (solid != null)
            {
                trigger.size = new Vector3(solid.size.x * 0.95f, 2.0f, solid.size.z * 0.95f);
                trigger.center = new Vector3(solid.center.x, solid.center.y + solid.size.y * 0.5f + 1.0f, solid.center.z);
            }
            else
            {
                trigger.size = new Vector3(6f, 2f, 6f);
                trigger.center = new Vector3(0, 1f, 0);
            }
        }
    }

    void Start()
    {
        startPos = transform.position;
        targetPos = startPos + offset;
        lastPos = transform.position;
    }

    void Update()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            lastPos = transform.position;
            return;
        }

        float journeyLength = Vector3.Distance(startPos, targetPos);
        if (journeyLength < 0.001f) return;

        float step = (speed / journeyLength) * Time.deltaTime;
        if (movingForward)
        {
            progress += step;
            if (progress >= 1f)
            {
                progress = 1f;
                movingForward = false;
                waitTimer = waitTimeAtEnds;
            }
        }
        else
        {
            progress -= step;
            if (progress <= 0f)
            {
                progress = 0f;
                movingForward = true;
                waitTimer = waitTimeAtEnds;
            }
        }

        // Suavizado sinusoidal
        float smoothT = Mathf.SmoothStep(0f, 1f, progress);
        Vector3 newPos = Vector3.Lerp(startPos, targetPos, smoothT);
        Vector3 delta = newPos - transform.position;
        transform.position = newPos;

        // Transportar al jugador sobre la plataforma si está apoyado
        if (passenger != null && passenger.enabled)
        {
            passenger.Move(delta);
        }

        lastPos = newPos;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
        {
            passenger = other.GetComponent<CharacterController>();
            if (passenger == null) passenger = other.GetComponentInParent<CharacterController>();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (passenger != null && (other.gameObject == passenger.gameObject || other.transform.IsChildOf(passenger.transform)))
        {
            passenger = null;
        }
    }
}
