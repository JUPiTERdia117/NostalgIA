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
        Crawling,
        Windup,
        Leaping,
        FacehuggerLatched,
        Detaching
    }

    [Header("Wall Crawling & Adhesion")]
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

    public SpiderState CurrentState => state;

    private SpiderState state = SpiderState.Crawling;
    private PlayerController player;
    private Transform playerCameraTransform;
    private EnemyHealth health;
    private Rigidbody rb;

    private Vector3 currentSurfaceNormal = Vector3.up;
    private Vector3 originalScale;
    private float lastJumpTime = -99f;
    private float lastMeleeTime = -99f;
    private float latchTimer;
    private float nextLatchDamageTime;

    // Cache de patas para animación de forcejeo
    private Transform[] legTransforms;
    private Quaternion[] originalLegRotations;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody>();
        originalScale = transform.localScale;

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
        lastJumpTime = Time.time + Random.Range(1f, 2.5f);
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

    private void Update()
    {
        if (health != null && health.IsDead) return;

        if (player == null || playerCameraTransform == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        if (player.dead)
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

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<PlayerController>();
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
        if (Physics.Raycast(forwardOrigin, transform.forward, out RaycastHit hitFwd, forwardWallCheckDistance, surfaceMask, QueryTriggerInteraction.Ignore))
        {
            bestHit = hitFwd;
            hasHit = true;
        }
        // B) Raycast hacia abajo de la araña (vientre)
        else if (Physics.Raycast(transform.position + transform.up * 0.1f, -transform.up, out RaycastHit hitDown, surfaceCheckDistance, surfaceMask, QueryTriggerInteraction.Ignore))
        {
            bestHit = hitDown;
            hasHit = true;
        }
        // C) Raycast frontal-inferior (anticipa esquinas y cornisas superiores)
        else if (Physics.Raycast(transform.position + transform.forward * 0.5f, -transform.up, out RaycastHit hitFwdDown, surfaceCheckDistance * 1.5f, surfaceMask, QueryTriggerInteraction.Ignore))
        {
            bestHit = hitFwdDown;
            hasHit = true;
        }
        // D) Raycast trasero-inferior
        else if (Physics.Raycast(transform.position - transform.forward * 0.4f, -transform.up, out RaycastHit hitBackDown, surfaceCheckDistance * 1.5f, surfaceMask, QueryTriggerInteraction.Ignore))
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
            if (flightTimer > 0.35f && Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 0.7f, surfaceMask, QueryTriggerInteraction.Ignore))
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
        if (Time.time >= nextLatchDamageTime && player != null && !player.dead)
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
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 0.7f, surfaceMask, QueryTriggerInteraction.Ignore))
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
            PlayerController target = collision.gameObject.GetComponentInParent<PlayerController>();
            if (target != null && !target.dead)
            {
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
