using UnityEngine;

/// <summary>Da movimiento suave a los prismas sin alterar su colisión de recogida.</summary>
public class Level1CollectibleMotion : MonoBehaviour
{
    private Vector3 startPosition;
    private float phase;

    void Start()
    {
        startPosition = transform.position;
        phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        transform.Rotate(Vector3.up, 42f * Time.deltaTime, Space.World);
        transform.position = startPosition + Vector3.up * (0.12f * Mathf.Sin(Time.time * 1.8f + phase));
    }
}
