using System.Collections;
using UnityEngine;

/// <summary>
/// Plataforma inestable que tiembla al pisarla, cae o se desvanece tras un breve retraso,
/// y reaparece después de unos segundos.
/// </summary>
public class CrumblingPlatform : MonoBehaviour
{
    [SerializeField] private float delayBeforeFall = 1.3f;
    [SerializeField] private float respawnTime = 3.0f;
    [SerializeField] private float shakeIntensity = 0.08f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Collider[] colliders;
    private Renderer[] renderers;
    private bool isCrumbling = false;

    void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isCrumbling && collision.gameObject.CompareTag("Player"))
        {
            StartCoroutine(CrumbleSequence());
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isCrumbling && (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null))
        {
            StartCoroutine(CrumbleSequence());
        }
    }

    private IEnumerator CrumbleSequence()
    {
        isCrumbling = true;
        float elapsed = 0f;

        // Fase de temblor / advertencia
        while (elapsed < delayBeforeFall)
        {
            float xOffset = (Mathf.PerlinNoise(Time.time * 25f, 0f) * 2f - 1f) * shakeIntensity;
            float zOffset = (Mathf.PerlinNoise(0f, Time.time * 25f) * 2f - 1f) * shakeIntensity;
            transform.position = initialPosition + new Vector3(xOffset, 0, zOffset);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Desactivar colisiones y renderers (caída / desvanecimiento)
        SetPlatformActive(false);

        // Esperar tiempo de reaparición
        yield return new WaitForSeconds(respawnTime);

        // Restaurar estado original
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        SetPlatformActive(true);
        isCrumbling = false;
    }

    private void SetPlatformActive(bool active)
    {
        foreach (Collider col in colliders)
        {
            if (col != null) col.enabled = active;
        }
        foreach (Renderer rend in renderers)
        {
            if (rend != null) rend.enabled = active;
        }
    }
}
