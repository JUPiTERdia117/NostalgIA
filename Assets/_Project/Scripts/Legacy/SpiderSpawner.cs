using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Enemies;
using NostalgIA.Gameplay.Player;
using NostalgIA.Presentation.Audio;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nido / generador que hace aparecer arañas periódicamente.
/// Posee vida propia y puede ser destruido por el jugador.
/// </summary>
/// <remarks>
/// Animaciones del Troyano (si el modelo tiene <see cref="EnemyAnimation"/>):
/// <list type="bullet">
/// <item><b>Idle</b> mientras no detecta al jugador; en ese tiempo no suelta arañas.</item>
/// <item>Al detectarlo reproduce <b>Spawn</b>: se abre la puerta de la espalda y la araña sale
/// en el momento <see cref="spawnMoment"/> del clip (fracción de 0 a 1). Luego vuelve a <b>Idle</b>
/// y repite cada <see cref="spawnInterval"/> segundos mientras el jugador siga cerca.</item>
/// <item>Al morir, <see cref="EnemyHealth"/> reproduce <b>Death</b> antes de los fragmentos.</item>
/// </list>
/// Con animaciones, el latido por escala del prototipo se desactiva.
/// </remarks>
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

    [Header("Detección")]
    [Tooltip("Distancia a la que detecta al jugador y empieza a soltar arañas.")]
    [SerializeField] private float detectionRange = 18f;
    [Tooltip("Momento del clip Spawn (0 a 1) en el que sale la araña, con la puerta ya abierta.")]
    [SerializeField, Range(0f, 1f)] private float spawnMoment = 0.5f;

    [Header("Visual Pulse")]
    [SerializeField] private float pulseFrequency = 1.5f;
    [SerializeField] private float pulseAmplitude = 0.08f;

    [Header("Audio")]
    [Tooltip("Sonido cada vez que sale una araña (Yodguard - small insect monster 6).")]
    [SerializeField] private AudioClip spawnClip;
    [SerializeField, Range(0f, 1f)] private float spawnVolume = 0.85f;

    private EnemyHealth health;
    private AudioSource audioSource;
    private readonly List<GameObject> activeSpiders = new List<GameObject>();
    private float nextSpawnTime;
    private Vector3 originalScale;
    private EnemyAnimation anim;
    private PlayerHealth player;
    private bool playerDetected;
    private bool isSpawning;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        originalScale = transform.localScale;
        anim = GetComponent<EnemyAnimation>();
        SetupAudioSource();
    }

    private void SetupAudioSource()
    {
        if (!TryGetComponent(out audioSource))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0.2f;
        audioSource.minDistance = 4f;
        audioSource.maxDistance = 35f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.outputAudioMixerGroup = GameAudio.Sfx;
    }

    private void Start()
    {
        nextSpawnTime = Time.time + initialSpawnDelay;
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;
        bool animated = anim != null && anim.IsAnimated;

        // Pulso orgánico continuo simulando latido (solo sin animaciones)
        if (!animated)
        {
            float pulse = Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseAmplitude;
            transform.localScale = originalScale * (1f + pulse);
        }

        // Limpiar referencias a arañas destruidas
        activeSpiders.RemoveAll(item => item == null);

        // Sin jugador cerca: reposo y sin arañas.
        if (!IsPlayerInRange())
        {
            playerDetected = false;
            if (!isSpawning && anim != null) anim.Play("Idle");
            return;
        }

        // Al detectarlo por primera vez, abre la puerta de inmediato.
        if (!playerDetected)
        {
            playerDetected = true;
            nextSpawnTime = Mathf.Min(nextSpawnTime, Time.time);
        }

        if (!isSpawning && Time.time >= nextSpawnTime)
        {
            if (activeSpiders.Count < maxActiveSpiders)
            {
                StartCoroutine(animated ? AnimatedSpawnRoutine() : SpawnSpiderRoutine());
            }
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    /// <summary>El jugador está vivo y dentro de la distancia de detección.</summary>
    private bool IsPlayerInRange()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.GetComponent<PlayerHealth>();
            if (player == null) return false;
        }
        return !player.IsDead && Vector3.Distance(transform.position, player.transform.position) <= detectionRange;
    }

    /// <summary>Abre la puerta con la animación Spawn, suelta la araña a mitad y vuelve a reposo.</summary>
    private IEnumerator AnimatedSpawnRoutine()
    {
        isSpawning = true;
        anim.Play("Spawn", 0, true);
        float length = anim.Length("Spawn");

        yield return new WaitForSeconds(length * spawnMoment);
        if (health != null && health.IsDead) yield break;
        SpawnSpider();

        yield return new WaitForSeconds(length * (1f - spawnMoment));
        if (health != null && health.IsDead) yield break;
        anim.Play("Idle");
        isSpawning = false;
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
        if (transform.parent != null) newSpider.transform.SetParent(transform.parent, true);
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

        bool playedLocally = false;
        if (spawnClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(spawnClip, spawnVolume);
            playedLocally = true;
        }
        EventBus<EnemySpawned>.Raise(new EnemySpawned(EnemyKind.Bug, pos, playedLocally));

        Debug.Log($"[SpiderSpawner] ¡Ha nacido una nueva araña! Arañas activas: {activeSpiders.Count}/{maxActiveSpiders}");
    }
}
