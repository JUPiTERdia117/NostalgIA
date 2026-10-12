using UnityEngine;

/// <summary>
/// Rota continuamente el objeto sobre los ejes configurados.
/// Útil para estrellas coleccionables, cristales y aspas de peligro.
/// </summary>
public class ObjectRotator : MonoBehaviour
{
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 60f, 0f);
    [SerializeField] private bool useLocalSpace = true;

    void Update()
    {
        Space space = useLocalSpace ? Space.Self : Space.World;
        transform.Rotate(rotationSpeed * Time.deltaTime, space);
    }
}
