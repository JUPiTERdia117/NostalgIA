using UnityEngine;

/// <summary>
/// Rota continuamente el objeto sobre sus ejes locales o mundiales.
/// Utilizado para estrellas coleccionables, núcleos de energía y balizas.
/// </summary>
public class Rotator : MonoBehaviour
{
    [SerializeField] private Vector3 rotationAngles = new Vector3(0, 50, 0);
    [SerializeField] private Space space = Space.Self;

    private void Update()
    {
        transform.Rotate(rotationAngles * Time.deltaTime, space);
    }
}
