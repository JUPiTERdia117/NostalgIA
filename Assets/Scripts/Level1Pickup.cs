using UnityEngine;

/// <summary>Cristal de energía que actualiza el contador de la misión al tocarse.</summary>
public sealed class Level1Pickup : MonoBehaviour
{
    private Vector3 startPosition;
    private float phase;

    private void Start()
    {
        startPosition = transform.position;
        phase = Random.value * 6.28f;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, 35f * Time.deltaTime, Space.World);
        Vector3 position = startPosition;
        position.y += Mathf.Sin(Time.time * 2.3f + phase) * 0.12f;
        transform.position = position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Level1WorldFlow.Instance?.CollectCore();
        Destroy(gameObject);
    }
}
