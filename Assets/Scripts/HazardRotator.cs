using UnityEngine;

/// <summary>
/// Hace rotar u oscilar un objeto u obstáculo continuo (cuchillas giratorias, rayos de energía).
/// </summary>
public class HazardRotator : MonoBehaviour
{
    [Tooltip("Velocidad de rotación en grados por segundo en cada eje.")]
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 90f, 0f);
    [Tooltip("Si es verdadero, oscila entre dos ángulos en vez de rotación continua.")]
    [SerializeField] private bool oscillate = false;
    [Tooltip("Ángulo máximo de oscilación.")]
    [SerializeField] private float oscillateAngle = 45f;
    [Tooltip("Frecuencia de oscilación.")]
    [SerializeField] private float oscillateFrequency = 1.5f;

    private Quaternion initialRotation;

    void Start()
    {
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        if (oscillate)
        {
            float angle = Mathf.Sin(Time.time * oscillateFrequency) * oscillateAngle;
            transform.localRotation = initialRotation * Quaternion.Euler(rotationSpeed.normalized * angle);
        }
        else
        {
            transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}
