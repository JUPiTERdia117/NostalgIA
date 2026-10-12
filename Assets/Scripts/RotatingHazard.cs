using UnityEngine;

/// <summary>
/// Rota continuamente sobre un eje específico. Usado para vigas con púas, cuchillas y peligros cinéticos.
/// </summary>
public class RotatingHazard : MonoBehaviour
{
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private float rotationSpeed = 45f;

    void Update()
    {
        transform.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, Space.Self);
    }
}
