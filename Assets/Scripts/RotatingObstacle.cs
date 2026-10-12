using UnityEngine;

/// <summary>
/// Hace rotar un obstáculo de forma continua o con oscilación angular.
/// </summary>
public class RotatingObstacle : MonoBehaviour
{
    [Header("Configuración de Rotación")]
    [Tooltip("Eje y velocidad de rotación (grados por segundo)")]
    [SerializeField] private Vector3 rotationAxis = new Vector3(0f, 60f, 0f);
    [Tooltip("Espacio de rotación")]
    [SerializeField] private Space rotationSpace = Space.Self;

    [Header("Oscilación (Opcional)")]
    [Tooltip("Si es verdadero, el objeto oscilará como péndulo en lugar de girar 360 grados")]
    [SerializeField] private bool oscillate = false;
    [Tooltip("Ángulo máximo de oscilación")]
    [SerializeField] private float oscillationAngle = 45f;
    [Tooltip("Frecuencia de oscilación")]
    [SerializeField] private float oscillationFrequency = 2f;

    private Quaternion initialRotation;

    void Start()
    {
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        if (oscillate)
        {
            float angle = Mathf.Sin(Time.time * oscillationFrequency) * oscillationAngle;
            transform.localRotation = initialRotation * Quaternion.Euler(rotationAxis.normalized * angle);
        }
        else
        {
            transform.Rotate(rotationAxis * Time.deltaTime, rotationSpace);
        }
    }
}
