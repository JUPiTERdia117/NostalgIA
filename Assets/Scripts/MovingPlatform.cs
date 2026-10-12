using UnityEngine;

/// <summary>
/// Mueve una plataforma flotante entre dos puntos con aceleración sinusoidal suave.
/// Permite transportar al jugador sin tirones.
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    [Tooltip("Punto de desplazamiento local relativo al inicio.")]
    [SerializeField] private Vector3 moveOffset = new Vector3(0f, 0f, 10f);
    [Tooltip("Velocidad o frecuencia de oscilación.")]
    [SerializeField] private float speed = 1.2f;

    private Vector3 startPos;
    private Vector3 endPos;

    void Start()
    {
        startPos = transform.position;
        endPos = startPos + moveOffset;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        transform.position = Vector3.Lerp(startPos, endPos, t);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.SetParent(transform, true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.SetParent(null, true);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? startPos : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + moveOffset);
        Gizmos.DrawWireSphere(origin + moveOffset, 0.4f);
    }
}
