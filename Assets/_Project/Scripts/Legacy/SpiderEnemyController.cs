using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Player;
using NostalgIA.Presentation.Audio;
using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo araña mejorado que se adhiere sólidamente a paredes, techos y suelos,
/// y realiza un ataque estilo "abrazacaras" saltando directo al rostro del jugador
/// y aferrándose a su pantalla con forcejeo visual y daño continuo.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(Rigidbody))]
public class SpiderEnemyController : MonoBehaviour
{
    public enum SpiderState
    {
        Emerging,
        Crawling,
        Windup,
        Leaping,
        FacehuggerLatched,
        Detaching
    }

    [Header("Wall Crawling & Adhesion")]
    [Tooltip("Distancia máxima a la que detecta al jugador para iniciar persecución.")]
    [SerializeField] private float detectionRange = 18f;
    [SerializeField] private float crawlSpeed = 3.6f;
    [SerializeField] private float surfaceAlignSpeed = 14f;
    [SerializeField] private float adhereSpeed = 16f;
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] private float surfaceCheckDistance = 1.4f;
    [SerializeField] private float forwardWallCheckDistance = 0.9f;
    [SerializeField] private float groundOffset = 0.38f;
    [SerializeField] private LayerMask surfaceMask = ~0;

    [Header("Facehugger Leap Settings")]
    [SerializeField] private float jumpRange = 8.5f;
    [SerializeField] private float minJumpDistance = 2.2f;
    [SerializeField] private float jumpSpeed = 12f;
    [SerializeField] private float jumpWindupTime = 0.35f;
    [SerializeField] private float jumpCooldown = 4.0f;
    [SerializeField] private int leapHitDamage = 1;

    [Header("Facehugger Latch Settings")]
    [Tooltip("Distancia a la cámara para engancharse como abrazacaras.")]
    [SerializeField] private float latchDistance = 1.2f;
    [Tooltip("Tiempo máximo enganchado a la cara antes de soltarse.")]
    [SerializeField] private float maxLatchDuration = 2.5f;
    [Tooltip("Intervalo de daño mientras está pegado a la cara.")]
    [SerializeField] private float latchDamageInterval = 0.9f;
    [SerializeField] private int latchDamage = 1;
    [Tooltip("Fuerza con la que sale disparada hacia atrás al soltarse.")]
    [SerializeField] private float detachPushback = 7f;

    [Header("Melee Contact")]
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float meleeCooldown = 1.0f;

    [Header("Audio")]
    [Tooltip("Sonido cuando ataca la araña (DavidDumaisAudio - Small Monster Attack).")]
    [SerializeField] private AudioClip attackClip;
    [SerializeField, Range(0f, 1f)] private float attackVolume = 0.85f;

    public SpiderState CurrentState => state;

    private SpiderState state = SpiderState.Crawling;
    private PlayerHealth player;
    private Transform playerCameraTransform;
    private EnemyHealth health;
    private Rigidbody rb;
    private AudioSource audioSource;

    private Vector3 currentSurfaceNormal = Vector3.up;
    private Vector3 originalScale;
    private Vector3 emergeDirection = Vector3.forward;
    private float emergeRemainingDistance;
    private float lastJumpTime = -99f;
    private float lastMeleeTime = -99f;
    private float lastAttackSoundTime = -99f;
    private float latchTimer;
    private float nextLatchDamageTime;
    private readonly RaycastHit[] surfaceHits = new RaycastHit[8];

    // Cache de patas para animación de forcejeo
    private Transform[] legTransforms;
    private Quaternion[] originalLegRotations;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody>();
        originalScale = transform.localScale;
        SetupAudioSource();

        rb.isKinematic = true;
        rb.useGravity = false;

        // Excluir capas de jugador e ignorar raycasts
        surfaceMask &= ~(1 << LayerMask.NameToLayer("Player"));
        surfaceMask &= ~(1 << LayerMask.NameToLayer("Ignore Raycast"));

        // Cachear patas secundarias para sacudida visual
        CacheLegs();

        // Si recibe daño mientras está en la cara, soltarse inmediatamente
        health.onTakeDamage.AddListener((dmg) =>
        {
            if (state == SpiderState.FacehuggerLatched)
            {
                DetachFromFace();
            }
        });

        FindPlayer();
    }

    private void Start()
    {
        if (player == null) FindPlayer();
        if (state != SpiderState.Emerging)
        {
            lastJumpTime = Time.time + Random.Range(1f, 2.5f);
        }
    }

    /// <summary>
    /// Inicia la salida ordenada desde el caballo/generador: panza hacia el piso, mirando hacia el
    /// frente de salida y caminando hacia afuera antes de perseguir o atacar al jugador.
    /// </summary>
    public void BeginSpawnExit(Vector3 exitDirection, float walkOutDistance = 2.6f, Collider[] spawnerColliders = null)
    {
        Vector3 flatDir = Vector3.ProjectOnPlane(exitDirection, Vector3.up);
        if (flatDir.sqrMagnitude < 0.001f) flatDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (flatDir.sqrMagnitude < 0.001f) flatDir = Vector3.forward;
        emergeDirection = flatDir.normalized;

        emergeRemainingDistance = Mathf.Max(0.5f, walkOutDistance);
        currentSurfaceNormal = Vector3.up;
        state = SpiderState.Emerging;

        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        transform.rotation = Quaternion.LookRotation(emergeDirection, Vector3.up);

        // Ajustar altura inicial exactamente sobre el piso para que no aparezca flotando ni hundida
        if (RaycastSurface(transform.position + Vector3.up * 1.2f, Vector3.down, 3.5f, out RaycastHit floorHit))
        {
            transform.position = new Vector3(transform.position.x, floorHit.point.y + groundOffset, transform.position.z);
        }

        if (spawnerColliders != null)
        {
            Collider[] myColliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < myColliders.Length; i++)
            {
                if (myColliders[i] == null) continue;
                for (int j = 0; j < spawnerColliders.Length; j++)
                {
                    if (spawnerColliders[j] != null)
                    {
                        Physics.IgnoreCollision(myColliders[i], spawnerColliders[j], true);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Raycast de superficie que ignora colliders de otros enemigos (como el caballo) y del jugador,
    /// evitando que la araña intente trepar por el collider esférico del caballo y se voltee.
    /// </summary>
    private bool RaycastSurface(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit bestHit)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, surfaceHits, maxDistance, surfaceMask, QueryTriggerInteraction.Ignore);
        float closestDist = float.PositiveInfinity;
        bestHit = default;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            Collider col = surfaceHits[i].collider;
            if (col == null) continue;
            if (col.transform.IsChildOf(transform)) continue;
            if (col.GetComponentInParent<EnemyHealth>() != null) continue;
            if (col.GetComponentInParent<PlayerHealth>() != null) continue;

            if (surfaceHits[i].distance < closestDist)
            {
                closestDist = surfaceHits[i].distance;
                bestHit = surfaceHits[i];
                found = true;
            }
        }

        return found;
    }

    private void CacheLegs()
    {
        var legs = transform.GetComponentsInChildren<Transform>();
        var legList = new System.Collections.Generic.List<Transform>();
        var rotList = new System.Collections.Generic.List<Quaternion>();

        foreach (var t in legs)
        {
            if (t != transform && t.name.Contains("Leg"))
            {
                legList.Add(t);
                rotList.Add(t.localRotation);
            }
        }

        legTransforms = legList.ToArray();
        originalLegRotations = rotList.ToArray();
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

    private void PlayAttackSound()
    {
        if (Time.time - lastAttackSoundTime < 0.25f) return;
        lastAttackSoundTime = Time.time;

        bool playedLocally = false;
        if (attackClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(attackClip, attackVolume);
            playedLocally = true;
        }
        EventBus<EnemyAttacked>.Raise(new EnemyAttacked(EnemyKind.Bug, transform.position, playedLocally));
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;

        if (state == SpiderState.Emerging)
        {
            HandleEmergingState();
            return;
        }

        if (player == null || playerCameraTransform == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        if (player.IsDead)
        {
            if (state == SpiderState.FacehuggerLatched) DetachFromFace();
            return;
        }

        switch (state)
        {
            case SpiderState.Crawling:
                HandleCrawlingState();
                break;

            case SpiderState.FacehuggerLatched:
                HandleLatchedState();
                break;
        }
    }

    private void HandleEmergingState()
    {
        currentSurfaceNormal = Vector3.up;
        transform.rotation = Quaternion.LookRotation(emergeDirection, Vector3.up);

        float step = crawlSpeed * Time.deltaTime;
        transform.position += emergeDirection * step;
        emergeRemainingDistance -= step;

        if (RaycastSurface(transform.position + Vector3.up * 1.2f, Vector3.down, 3.5f, out RaycastHit floorHit))
        {
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, floorHit.point.y + groundOffset, adhereSpeed * Time.deltaTime);
            transform.position = pos;
        }

        AnimateLegsWalking();

        if (emergeRemainingDistance <= 0f)
        {
            state = SpiderState.Crawling;
            // Breve margen al terminar de salir antes de poder saltar hacia el jugador
            lastJumpTime = Time.time - jumpCooldown + 0.6f;
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<PlayerHealth>();
            Camera cam = playerObj.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                playerCameraTransform = cam.transform;
            }
            else if (Camera.main != null)
            {
                playerCameraTransform = Camera.main.transform;
            }
        }
    }

    private void HandleCrawlingState()
    {
        float distToPlayer = Vector3.Distance(transform.position, player.transform.position);

        if (distToPlayer > detectionRange)
        {
            return; // Esperar a que el jugador entre a su territorio
        }

        // Evaluar ataque de salto Abrazacaras
        if (distToPlayer >= minJumpDistance && distToPlayer <= jumpRange && Time.time - lastJumpTime >= jumpCooldown)
        {
            StartCoroutine(FacehuggerJumpRoutine());
            return;
        }

        // Caminata y adherencia avanzada a paredes
        CrawlAndStickToSurfaces();
    }

    /// <summary>
    /// Sistema de multi-raycasting para detectar y trepar fluidamente por paredes, techos y pisos.
    /// </summary>
    private void CrawlAndStickToSurfaces()
    {
        // 1. Raycasts de sondeo de superficie en múltiples direcciones
        RaycastHit bestHit = default;
        bool hasHit = false;

        // A) Raycast frontal: detecta muros que se avecinan para trepar antes de chocar
        Vector3 forwardOrigin = transform.position + transform.up * 0.2f;
        if (RaycastSurface(forwardOrigin, transform.forward, forwardWallCheckDistance, out RaycastHit hitFwd))
        {
            bestHit = hitFwd;
            hasHit = true;
        }
        // B) Raycast hacia abajo de la araña (vientre)
        else if (RaycastSurface(transform.position + transform.up * 0.1f, -transform.up, surfaceCheckDistance, out RaycastHit hitDown))
        {
            bestHit = hitDown;
            hasHit = true;
        }
        // C) Raycast frontal-inferior (anticipa esquinas y cornisas superiores)
        else if (RaycastSurface(transform.position + transform.forward * 0.5f, -transform.up, surfaceCheckDistance * 1.5f, out RaycastHit hitFwdDown))
        {
            bestHit = hitFwdDown;
            hasHit = true;
        }
        // D) Raycast trasero-inferior
        else if (RaycastSurface(transform.position - transform.forward * 0.4f, -transform.up, surfaceCheckDistance * 1.5f, out RaycastHit hitBackDown))
        {
            bestHit = hitBackDown;
            hasHit = true;
        }

        if (hasHit)
        {
            currentSurfaceNormal = bestHit.normal;

            // Alineación de la rotación perpendicular a la superficie
            Quaternion targetNormalRot = Quaternion.FromToRotation(transform.up, currentSurfaceNormal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetNormalRot, surfaceAlignSpeed * Time.deltaTime);

            // Adherencia magnética a la superficie
            Vector3 targetSurfacePos = bestHit.point + currentSurfaceNormal * groundOffset;
            transform.position = Vector3.Lerp(transform.position, targetSurfacePos, adhereSpeed * Time.deltaTime);
        }

        // 2. Dirección de avance hacia el jugador proyectada en el plano del muro
        Vector3 dirToPlayer = player.transform.position - transform.position;
        Vector3 surfaceMoveDir = Vector3.ProjectOnPlane(dirToPlayer, currentSurfaceNormal).normalized;

        if (surfaceMoveDir.sqrMagnitude > 0.001f)
        {
            // Rotar el frente hacia la dirección de avance
            Quaternion targetLook = Quaternion.LookRotation(surfaceMoveDir, currentSurfaceNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetLook, turnSpeed * Time.deltaTime);

            // Desplazar a lo largo de la superficie
            transform.position += surfaceMoveDir * (crawlSpeed * Time.deltaTime);

            // Marcha orgánica sutil
            AnimateLegsWalking();
        }
    }

    private void AnimateLegsWalking()
    {
        if (legTransforms == null) return;
        float wobble = Mathf.Sin(Time.time * 16f) * 8f;
        for (int i = 0; i < legTransforms.Length; i++)
        {
            if (legTransforms[i] == null) continue;
            float offset = (i % 2 == 0) ? wobble : -wobble;
            legTransforms[i].localRotation = originalLegRotations[i] * Quaternion.Euler(0, offset, 0);
        }
    }

    /// <summary>
    /// Rutina de salto parabólico de alta velocidad directo al rostro del jugador.
    /// </summary>
    private IEnumerator FacehuggerJumpRoutine()
    {
        state = SpiderState.Windup;

        // Anticipación: comprimirse y temblar
        float elapsed = 0f;
        while (elapsed < jumpWindupTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / jumpWindupTime;
            transform.localScale = Vector3.Lerp(originalScale, new Vector3(originalScale.x * 1.35f, originalScale.y * 0.45f, originalScale.z * 1.35f), t) + Random.insideUnitSphere * 0.02f;
            yield return null;
        }

        transform.localScale = originalScale;
        state = SpiderState.Leaping;
        PlayAttackSound();

        // Objetivo: La cara/cámara del jugador
        Vector3 targetFace = playerCameraTransform != null 
            ? playerCameraTransform.position 
            : (player.transform.position + Vector3.up * 1.5f);

        Vector3 leapDir = (targetFace - transform.position).normalized;
        Vector3 leapVelocity = leapDir * jumpSpeed + Vector3.up * 2.5f;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = leapVelocity;

        // Orientar el vientre y quelíceros directamente hacia el jugador
        transform.rotation = Quaternion.LookRotation(leapDir, Vector3.up) * Quaternion.Euler(-30f, 0, 0);

        Debug.Log("[SpiderEnemyController] ¡Ataque abrazacaras en pleno vuelo hacia la cara del jugador!");

        // Vuelo y detección de enganche al rostro
        float flightTimer = 0f;
        float maxFlightTime = 1.6f;

        while (state == SpiderState.Leaping && flightTimer < maxFlightTime)
        {
            flightTimer += Time.deltaTime;

            if (playerCameraTransform != null)
            {
                float distToFace = Vector3.Distance(transform.position, playerCameraTransform.position);
                if (distToFace <= latchDistance)
                {
                    AttachToFace();
                    yield break;
                }
            }

            // Aterrizaje en el suelo si no alcanzó al jugador
            if (flightTimer > 0.35f && RaycastSurface(transform.position, Vector3.down, 0.7f, out RaycastHit hit))
            {
                LandOnSurface(hit.normal, hit.point);
                yield break;
            }

            yield return null;
        }

        if (state == SpiderState.Leaping)
        {
            LandOnSurface(Vector3.up, transform.position);
        }
    }

    /// <summary>
    /// Se acopla frente a la cámara del jugador simulando un abrazacaras aferrado al rostro.
    /// </summary>
    public void AttachToFace()
    {
        if (state == SpiderState.FacehuggerLatched || player == null || playerCameraTransform == null) return;

        state = SpiderState.FacehuggerLatched;
        latchTimer = 0f;
        nextLatchDamageTime = Time.time + latchDamageInterval;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;

        // Hacerse hijo de la cámara para moverse con la mirada
        transform.SetParent(playerCameraTransform, true);
        transform.localPosition = new Vector3(0f, -0.1f, 0.42f);
        transform.localRotation = Quaternion.Euler(20f, 180f, 0f);

        // Daño instantáneo al atrapar la cara (usa leapHitDamage si está configurado)
        PlayAttackSound();
        int initialDamage = leapHitDamage > 0 ? leapHitDamage : latchDamage;
        player.TakeDamage(initialDamage);
        Debug.Log($"[SpiderEnemyController] ¡ABRAZACARAS ENGANCHADO AL ROSTRO DEL JUGADOR! Daño inicial: {initialDamage}");
    }

    private void HandleLatchedState()
    {
        latchTimer += Time.deltaTime;

        // Forcejeo visual: sacudida violenta y pataleo de patas en la pantalla
        transform.localPosition = new Vector3(
            Mathf.Sin(Time.time * 28f) * 0.02f,
            -0.1f + Mathf.Cos(Time.time * 24f) * 0.02f,
            0.42f + Mathf.Sin(Time.time * 35f) * 0.015f
        );

        // Agitar patas violentamente tapando la vista
        if (legTransforms != null)
        {
            float legFlail = Mathf.Sin(Time.time * 32f) * 25f;
            for (int i = 0; i < legTransforms.Length; i++)
            {
                if (legTransforms[i] == null) continue;
                float phase = (i % 2 == 0) ? legFlail : -legFlail;
                legTransforms[i].localRotation = originalLegRotations[i] * Quaternion.Euler(phase, 0, phase * 0.5f);
            }
        }

        // Daño periódico
        if (Time.time >= nextLatchDamageTime && player != null && !player.IsDead)
        {
            player.TakeDamage(latchDamage);
            nextLatchDamageTime = Time.time + latchDamageInterval;
        }

        // Desprenderse al cumplirse el tiempo máximo
        if (latchTimer >= maxLatchDuration)
        {
            DetachFromFace();
        }
    }

    /// <summary>
    /// Se desprende de la cara del jugador y sale despedida hacia atrás.
    /// </summary>
    public void DetachFromFace()
    {
        if (state != SpiderState.FacehuggerLatched) return;

        state = SpiderState.Detaching;
        transform.SetParent(null, true);

        // Restaurar rotaciones originales de las patas
        if (legTransforms != null)
        {
            for (int i = 0; i < legTransforms.Length; i++)
            {
                if (legTransforms[i] != null) legTransforms[i].localRotation = originalLegRotations[i];
            }
        }

        // Impulso hacia atrás saliendo de la pantalla
        rb.isKinematic = false;
        rb.useGravity = true;

        Vector3 pushbackDir = playerCameraTransform != null 
            ? (-playerCameraTransform.forward * detachPushback + Vector3.up * 3f)
            : (-transform.forward * detachPushback + Vector3.up * 3f);

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(pushbackDir, ForceMode.Impulse);
        rb.angularVelocity = Random.insideUnitSphere * 8f;

        Debug.Log("[SpiderEnemyController] La araña abrazacaras fue expulsada del rostro.");
        StartCoroutine(RecoverFromDetachRoutine());
    }

    private IEnumerator RecoverFromDetachRoutine()
    {
        yield return new WaitForSeconds(0.4f);

        float timer = 0f;
        while (timer < 2.0f)
        {
            timer += Time.deltaTime;
            if (RaycastSurface(transform.position, Vector3.down, 0.7f, out RaycastHit hit))
            {
                LandOnSurface(hit.normal, hit.point);
                yield break;
            }
            yield return null;
        }

        LandOnSurface(Vector3.up, transform.position);
    }

    private void LandOnSurface(Vector3 normal, Vector3 point)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;

        currentSurfaceNormal = normal;
        transform.position = point + normal * groundOffset;
        transform.rotation = Quaternion.FromToRotation(transform.up, normal) * transform.rotation;

        state = SpiderState.Crawling;
        lastJumpTime = Time.time;
        Debug.Log("[SpiderEnemyController] La araña aterrizó y reanudó el trepado.");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (health != null && health.IsDead) return;

        // Si salta hacia el jugador, engancharse de inmediato
        if (state == SpiderState.Leaping && collision.gameObject.CompareTag("Player"))
        {
            AttachToFace();
            return;
        }

        // Si choca con superficie estática mientras saltaba
        if (state == SpiderState.Leaping && !collision.gameObject.CompareTag("Player"))
        {
            LandOnSurface(collision.contacts[0].normal, collision.contacts[0].point);
        }

        // Daño por contacto melee normal mientras camina
        if (state == SpiderState.Crawling && collision.gameObject.CompareTag("Player") && Time.time - lastMeleeTime >= meleeCooldown)
        {
            PlayerHealth target = collision.gameObject.GetComponentInParent<PlayerHealth>();
            if (target != null && !target.IsDead)
            {
                PlayAttackSound();
                target.TakeDamage(contactDamage);
                lastMeleeTime = Time.time;
            }
        }
    }

    [ContextMenu("Test Facehugger Leap")]
    public void TestFacehuggerLeap()
    {
        if (player != null && state == SpiderState.Crawling)
        {
            StartCoroutine(FacehuggerJumpRoutine());
        }
    }

    [ContextMenu("Test Attach To Face")]
    public void TestAttachToFace()
    {
        AttachToFace();
    }

    [ContextMenu("Test Detach")]
    public void TestDetach()
    {
        DetachFromFace();
    }
}
