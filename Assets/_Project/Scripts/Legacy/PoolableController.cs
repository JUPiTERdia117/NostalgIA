using UnityEngine;

/// <summary>
/// Controla la animación y el estado de un objeto que puede ser absorbido.
/// Registra su masa y aceleración (F = m * a) para el sistema de daño físico contra enemigos
/// y genera fragmentos al destruirse.
/// </summary>
public class PoolableController : MonoBehaviour
{
    [Tooltip("Indica si hay una trayectoria de absorción iniciada. El script gestiona este estado automáticamente.")]
    public bool beingPulled = false;
    [Tooltip("Aspiradora que atrae este objeto. Se asigna al detectarlo con el rayo y se borra al terminar o cancelar la absorción.")]
    public ObjectVacum pooller;

    [Tooltip("Duración de la trayectoria en segundos, independiente de la distancia. Puede terminar antes por cercanía.")]
    [SerializeField, Min(0.01f)] private float absorptionDuration = 1.0f;
    [Tooltip("Radio máximo de la espiral en unidades de Unity. Empieza y termina en cero; cero elimina la espiral.")]
    [SerializeField, Min(0f)] private float spiralRadius = 0.5f;
    [Tooltip("Número de vueltas de la espiral durante una trayectoria completa de absorción.")]
    [SerializeField, Min(0f)] private float spiralTurns = 2f;
    [Tooltip("Segundos que tarda en recuperar su tamaño original al interrumpir la absorción.")]
    [SerializeField, Min(0.01f)] private float scaleRestoreDuration = 0.2f;
    [Tooltip("Distancia al centro de la aspiradora que completa la absorción y guarda el objeto, en unidades de Unity.")]
    [SerializeField, Min(0f)] private float absorptionDistance = 0.2f;

    [Header("Combat & Physics Force (F = m * a)")]
    [Tooltip("Daño mínimo garantizado cuando este objeto es lanzado por el jugador contra un enemigo.")]
    [SerializeField] private int impactDamage = 1;
    [Tooltip("Si es verdadero, el objeto se destruye con fragmentos al chocar con un enemigo.")]
    [SerializeField] private bool destroyOnEnemyHit = true;
    [Tooltip("Cantidad de fragmentos que se generan al romperse contra un enemigo.")]
    [SerializeField] private int debrisCount = 6;
    [Tooltip("Velocidad mínima requerida de impacto para causar daño cuando no fue lanzado.")]
    [SerializeField] private float minImpactVelocity = 1.8f;

    [HideInInspector] public bool isThrown = false;
    private bool isDestroyed = false;

    private Rigidbody rb;
    private Vector3 previousVelocity;
    private float currentAcceleration;
    private float peakRecentAcceleration;
    private float peakAccelerationTimer;
    private float launchAcceleration;
    private float thrownTimestamp = -99f;

    public int ImpactDamage { get => impactDamage > 0 ? impactDamage : 1; set => impactDamage = value; }
    public bool DestroyOnEnemyHit { get => destroyOnEnemyHit; set => destroyOnEnemyHit = value; }
    public float MinImpactVelocity => minImpactVelocity;
    public bool IsDestroyed => isDestroyed;
    public float CurrentAcceleration => Mathf.Max(currentAcceleration, peakRecentAcceleration);
    public float LaunchAcceleration => launchAcceleration;
    public Vector3 PreImpactVelocity => previousVelocity;

    // Escala local de referencia, incluidos tamaños no uniformes.
    private Vector3 originalScale;
    // Origen fijo de la trayectoria actual, en coordenadas del mundo.
    private Vector3 startPosition;
    // Escala al iniciar la absorción, que puede ser menor si se interrumpió una recuperación.
    private Vector3 pullStartScale;
    // Escala desde la que se recupera el tamaño al dejar de absorber.
    private Vector3 restoreStartScale;
    // Primer eje perpendicular a la dirección inicial de absorción.
    private Vector3 spiralRight;
    // Segundo eje perpendicular; junto con spiralRight forma el plano de la espiral.
    private Vector3 spiralUp;
    // Referencia usada para detectar un cambio de aspiradora durante la trayectoria.
    private ObjectVacum activePuller;
    // Progreso normalizado de absorción: cero al iniciar y uno al llegar.
    private float progress;
    // Progreso normalizado de recuperación; uno significa tamaño ya restaurado.
    private float restoreProgress = 1f;

    /// <summary>
    /// Conserva la escala local original para restaurarla tras la absorción o su cancelación.
    /// </summary>
    void Awake()
    {
        originalScale = transform.localScale;
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Avanza la absorción, recupera gradualmente el tamaño y registra la aceleración física (a = dv/dt).
    /// </summary>
    void FixedUpdate()
    {
        TrackPhysicsKinematics();

        if (pooller == null || !pooller.vacuumActive)
        {
            // Captura la escala una sola vez al interrumpir la trayectoria.
            if (beingPulled)
            {
                restoreStartScale = transform.localScale;
                restoreProgress = 0f;
                ResetPull();
            }

            restoreProgress = Mathf.Clamp01(restoreProgress + Time.fixedDeltaTime / Mathf.Max(0.01f, scaleRestoreDuration));
            transform.localScale = Vector3.Lerp(restoreStartScale, originalScale, restoreProgress);
            return;
        }

        // Una nueva aspiradora o un nuevo intento requiere reiniciar la trayectoria.
        if (!beingPulled || activePuller != pooller)
        {
            BeginPull();
        }

        MoveToVacuum();
    }

    private void TrackPhysicsKinematics()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null) return;

        float dt = Mathf.Max(0.0001f, Time.fixedDeltaTime);
        Vector3 vel = rb.linearVelocity;
        Vector3 dv = vel - previousVelocity;
        float kinematicAccel = dv.magnitude / dt;

        if (vel.y < -0.5f && rb.useGravity)
        {
            kinematicAccel = Mathf.Max(kinematicAccel, Physics.gravity.magnitude + Mathf.Abs(vel.y) * 1.5f);
        }

        currentAcceleration = kinematicAccel;
        if (kinematicAccel >= peakRecentAcceleration)
        {
            peakRecentAcceleration = kinematicAccel;
            peakAccelerationTimer = 0.25f;
        }
        else if (peakAccelerationTimer > 0f)
        {
            peakAccelerationTimer -= dt;
        }
        else
        {
            peakRecentAcceleration = kinematicAccel;
        }

        previousVelocity = vel;

        if (isThrown && Time.time - thrownTimestamp > 0.6f && vel.sqrMagnitude < 0.25f)
        {
            isThrown = false;
            launchAcceleration = 0f;
        }
    }

    /// <summary>
    /// Registra que el objeto fue lanzado por el jugador con el impulso indicado.
    /// </summary>
    public void MarkAsThrownByPlayer(float launchImpulse)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        isThrown = true;
        thrownTimestamp = Time.time;
        float mass = rb != null ? Mathf.Max(0.1f, rb.mass) : 1f;
        float dt = Mathf.Max(0.005f, Time.fixedDeltaTime);
        launchAcceleration = (launchImpulse / dt) / mass;
        peakRecentAcceleration = Mathf.Max(peakRecentAcceleration, launchAcceleration);
        peakAccelerationTimer = 0.5f;
    }

    /// <summary>
    /// Inicia una trayectoria desde la posición y escala actuales, sin saltos al retomar la absorción.
    /// </summary>
    void BeginPull()
    {
        startPosition = transform.position;
        pullStartScale = transform.localScale;
        activePuller = pooller;
        progress = 0f;

        Vector3 direction = pooller.transform.position - startPosition;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        // Si la dirección es vertical, usa otro eje de referencia para evitar un producto cruzado nulo.
        Vector3 reference = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f
            ? Vector3.right
            : Vector3.up;
        spiralRight = Vector3.Cross(direction, reference).normalized;
        spiralUp = Vector3.Cross(direction, spiralRight).normalized;

        beingPulled = true;
    }

    /// <summary>
    /// Combina avance hacia la aspiradora, desplazamiento en espiral y reducción de tamaño.
    /// </summary>
    void MoveToVacuum()
    {
        Vector3 destination = pooller.transform.position;
        float absorbRadius = Mathf.Max(0f, absorptionDistance);
        if ((transform.position - destination).sqrMagnitude <= absorbRadius * absorbRadius)
        {
            FinishAbsorption();
            return;
        }

        // El progreso depende del tiempo, no de la distancia; limita el divisor para evitar cero.
        progress = Mathf.Clamp01(progress + Time.fixedDeltaTime / Mathf.Max(0.01f, absorptionDuration));

        if (progress >= 1f)
        {
            transform.position = destination;
            FinishAbsorption();
            return;
        }

        // El seno hace crecer y desaparecer el radio sin saltos en los extremos de la trayectoria.
        float angle = progress * spiralTurns * Mathf.PI * 2f;
        float radius = spiralRadius * Mathf.Sin(progress * Mathf.PI);
        Vector3 offset = (spiralRight * Mathf.Cos(angle) + spiralUp * Mathf.Sin(angle)) * radius;

        // El destino se actualiza para seguir a la aspiradora aunque el jugador se mueva.
        transform.position = Vector3.Lerp(startPosition, destination, progress) + offset;
        transform.localScale = pullStartScale * (1f - progress);

        if ((transform.position - destination).sqrMagnitude <= absorbRadius * absorbRadius)
        {
            FinishAbsorption();
        }
    }

    /// <summary>
    /// Restaura el tamaño antes de guardar el volumen de colisión y desactivar el objeto.
    /// </summary>
    void FinishAbsorption()
    {
        transform.localScale = originalScale;

        if ((pooller.currentCapacity) == 0)
        {
            ResetPull();
            return;
        }
        pooller.PoolObject(gameObject);
    }

    /// <summary>
    /// Limpia la trayectoria y sus referencias sin cambiar la posición ni la escala actuales.
    /// </summary>
    void ResetPull()
    {
        beingPulled = false;
        pooller = null;
        activePuller = null;
        progress = 0f;
    }

    /// <summary>
    /// Deja el objeto con su tamaño y estado iniciales para que pueda volver a lanzarse y absorberse.
    /// </summary>
    void OnDisable()
    {
        ResetPull();
        transform.localScale = originalScale;
        restoreProgress = 1f;
        pooller = null;
        isThrown = false;
        previousVelocity = Vector3.zero;
        currentAcceleration = 0f;
        peakRecentAcceleration = 0f;
        launchAcceleration = 0f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDestroyed) return;

        // Si colisiona con un enemigo, EnemyHealth se encarga del cálculo y aplicación del daño
        // y ejecuta BreakApart() sobre este objeto para evitar doble impacto o desincronización.
        EnemyHealth enemy = collision.gameObject.GetComponentInParent<EnemyHealth>();
        if (enemy != null) return;
    }

    /// <summary>
    /// Destruye el objeto creando un estallido de fragmentos físicos con su propio material.
    /// </summary>
    public void BreakApart(Vector3? contactPoint = null)
    {
        if (isDestroyed) return;
        isDestroyed = true;

        Vector3 spawnPos = contactPoint ?? transform.position;
        Renderer rend = GetComponentInChildren<Renderer>();
        Material mat = rend != null ? rend.sharedMaterial : null;

        for (int i = 0; i < debrisCount; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.name = "Object_Fragment";
            fragment.transform.position = spawnPos + Random.insideUnitSphere * 0.25f;
            fragment.transform.localScale = Vector3.one * Random.Range(0.12f, 0.28f);

            if (mat != null)
            {
                fragment.GetComponent<Renderer>().sharedMaterial = mat;
            }

            Rigidbody fragRb = fragment.AddComponent<Rigidbody>();
            fragRb.mass = 0.15f;
            Vector3 force = (Random.onUnitSphere + Vector3.up * 0.6f).normalized * Random.Range(4f, 8f);
            fragRb.AddForce(force, ForceMode.Impulse);
            fragRb.angularVelocity = Random.insideUnitSphere * 12f;

            Destroy(fragment, Random.Range(1.2f, 2.2f));
        }

        Destroy(gameObject);
    }
}
