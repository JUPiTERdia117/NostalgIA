using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nido / generador que hace aparecer arañas periódicamente.
/// Posee vida propia y puede ser destruido por el jugador.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class SpiderSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject spiderPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnInterval = 6f;
    [SerializeField] private int maxActiveSpiders = 3;
    [SerializeField] private float initialSpawnDelay = 2f;
    [SerializeField] private float ejectionSpeed = 3.5f;

    [Header("Visual Pulse")]
    [SerializeField] private float pulseFrequency = 1.5f;
    [SerializeField] private float pulseAmplitude = 0.08f;

    private EnemyHealth health;
    private readonly List<GameObject> activeSpiders = new List<GameObject>();
    private float nextSpawnTime;
    private Vector3 originalScale;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        originalScale = transform.localScale;
    }

    private void Start()
    {
        nextSpawnTime = Time.time + initialSpawnDelay;
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;

        // Pulso orgánico continuo simulando latido
        float pulse = Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseAmplitude;
        transform.localScale = originalScale * (1f + pulse);

        // Limpiar referencias a arañas destruidas
        activeSpiders.RemoveAll(item => item == null);

        if (Time.time >= nextSpawnTime)
        {
            if (activeSpiders.Count < maxActiveSpiders)
            {
                StartCoroutine(SpawnSpiderRoutine());
            }
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    private IEnumerator SpawnSpiderRoutine()
    {
        // Contracción y dilatación fuerte al expulsar
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, originalScale * 1.25f, t / 0.25f);
            yield return null;
        }

        SpawnSpider();

        t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale * 1.25f, originalScale, t / 0.2f);
            yield return null;
        }
        transform.localScale = originalScale;
    }

    [ContextMenu("Test Spawn Spider")]
    public void SpawnSpider()
    {
        if (spiderPrefab == null)
        {
            Debug.LogWarning("[SpiderSpawner] No se ha asignado spiderPrefab.", this);
            return;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : (transform.position + transform.up * 1f);
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        GameObject newSpider = Instantiate(spiderPrefab, pos, rot);
        newSpider.name = $"Spider_Spawned_{activeSpiders.Count + 1}";
        activeSpiders.Add(newSpider);

        // Impulso de expulsión hacia afuera
        Rigidbody rb = newSpider.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 ejectDir = (transform.up + Random.insideUnitSphere * 0.5f).normalized;
            rb.isKinematic = false;
            rb.AddForce(ejectDir * ejectionSpeed, ForceMode.Impulse);
        }

        Debug.Log($"[SpiderSpawner] ¡Ha nacido una nueva araña! Arañas activas: {activeSpiders.Count}/{maxActiveSpiders}");
    }
}
