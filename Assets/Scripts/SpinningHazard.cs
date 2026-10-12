using UnityEngine;

/// <summary>Gira una cuchilla de barrido colocada sobre el recorrido.</summary>
public class SpinningHazard : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 72f;

    void Update()
    {
        transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
