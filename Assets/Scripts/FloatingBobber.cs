using UnityEngine;

/// <summary>
/// Hace flotar y oscilar suavemente un objeto con una onda sinusoidal vertical.
/// </summary>
public class FloatingBobber : MonoBehaviour
{
    [SerializeField] private float bobAmplitude = 0.25f;
    [SerializeField] private float bobFrequency = 2f;
    [SerializeField] private float timeOffset = 0f;

    private Vector3 initialPos;

    private void Start()
    {
        initialPos = transform.position;
        if (timeOffset == 0f)
        {
            timeOffset = Random.Range(0f, 6.28f);
        }
    }

    private void Update()
    {
        float newY = initialPos.y + Mathf.Sin((Time.time * bobFrequency) + timeOffset) * bobAmplitude;
        transform.position = new Vector3(initialPos.x, newY, initialPos.z);
    }
}
