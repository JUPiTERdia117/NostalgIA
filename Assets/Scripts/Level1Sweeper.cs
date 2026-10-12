using UnityEngine;

/// <summary>Giro constante del brazo de una trampa de barrido.</summary>
public sealed class Level1Sweeper : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 56f;

    private void Update()
    {
        transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
