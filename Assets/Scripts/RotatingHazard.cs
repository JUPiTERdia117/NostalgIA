using UnityEngine;

/// <summary>
/// Controla sierras giratorias u obstáculos rotatorios con desplazamiento opcional de vaivén a lo largo de un riel.
/// </summary>
[RequireComponent(typeof(HazardObstacle))]
public class RotatingHazard : MonoBehaviour
{
    [Header("Rotación de la Sierra")]
    [SerializeField] private Vector3 rotationAxis = new Vector3(0, 0, 1);
    [SerializeField] private float rotationSpeed = 360f;

    [Header("Patrulla Lineal (Opcional)")]
    [SerializeField] private bool enablePatrol = false;
    [SerializeField] private Vector3 patrolOffset = new Vector3(5f, 0, 0);
    [SerializeField] private float patrolSpeed = 2.5f;

    private Vector3 originPos;
    private float patrolProgress = 0f;
    private bool movingForward = true;

    void Start()
    {
        originPos = transform.position;
    }

    void Update()
    {
        // Giro constante
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.Self);

        // Desplazamiento sobre riel si está activo
        if (enablePatrol)
        {
            float dist = patrolOffset.magnitude;
            if (dist > 0.01f)
            {
                float step = (patrolSpeed / dist) * Time.deltaTime;
                if (movingForward)
                {
                    patrolProgress += step;
                    if (patrolProgress >= 1f)
                    {
                        patrolProgress = 1f;
                        movingForward = false;
                    }
                }
                else
                {
                    patrolProgress -= step;
                    if (patrolProgress <= 0f)
                    {
                        patrolProgress = 0f;
                        movingForward = true;
                    }
                }

                transform.position = Vector3.Lerp(originPos, originPos + patrolOffset, Mathf.SmoothStep(0f, 1f, patrolProgress));
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (enablePatrol)
        {
            Vector3 start = Application.isPlaying ? originPos : transform.position;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(start, start + patrolOffset);
            Gizmos.DrawWireSphere(start + patrolOffset, 0.3f);
        }
    }
}
