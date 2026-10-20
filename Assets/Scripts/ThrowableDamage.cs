using UnityEngine;

/// <summary>
/// Calcula la cinemática de impacto (masa y aceleración F = m * a) de un objeto arrojable o físico
/// y genera un efecto de fragmentos de destrucción cuando impacta contra un enemigo.
/// </summary>
public class ThrowableDamage : MonoBehaviour
{
    [Header("Physics Force Damage (F = m * a)")]
    [Tooltip("Daño mínimo garantizado cuando el objeto es lanzado por el jugador.")]
    [SerializeField] private int minPlayerThrowDamage = 1;
    [Tooltip("Multiplicador de fuerza física al impactar.")]
    [SerializeField] private float forceMultiplier = 1.0f;
    [Tooltip("Si es verdadero, el objeto se destruye con fragmentos al chocar contra un enemigo.")]
    [SerializeField] private bool destroyOnImpact = true;
    [Tooltip("Cantidad de fragmentos que se desprenden al romperse.")]
    [SerializeField] private int debrisCount = 6;
    [Tooltip("Velocidad mínima de impacto para infligir daño cuando NO fue lanzado por el jugador.")]
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

    public int Damage { get => Mathf.Max(1, minPlayerThrowDamage); set => minPlayerThrowDamage = Mathf.Max(1, value); }
    public int MinPlayerThrowDamage => Mathf.Max(1, minPlayerThrowDamage);
    public float ForceMultiplier => Mathf.Max(0.1f, forceMultiplier);
    public bool DestroyOnImpact { get => destroyOnImpact; set => destroyOnImpact = value; }
    public float MinImpactVelocity => minImpactVelocity;
    public bool IsDestroyed => isDestroyed;
    public float CurrentAcceleration => Mathf.Max(currentAcceleration, peakRecentAcceleration);
    public float LaunchAcceleration => launchAcceleration;
    public Vector3 PreImpactVelocity => previousVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        previousVelocity = Vector3.zero;
        currentAcceleration = 0f;
        peakRecentAcceleration = 0f;
        peakAccelerationTimer = 0f;
    }

    private void FixedUpdate()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null) return;

        float dt = Mathf.Max(0.0001f, Time.fixedDeltaTime);
        Vector3 vel = rb.linearVelocity;

        // Aceleración cinemática instantánea a = |dv / dt| + componente gravitacional en caída
        Vector3 dv = vel - previousVelocity;
        float kinematicAccel = dv.magnitude / dt;

        // Si el objeto está cayendo libremente, incluir la aceleración de la gravedad (g = 9.81 m/s^2)
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

        // Si fue lanzado hace tiempo y ya casi se detuvo en el suelo, limpiar flag de lanzamiento
        if (isThrown && Time.time - thrownTimestamp > 0.6f && vel.sqrMagnitude < 0.25f)
        {
            isThrown = false;
            launchAcceleration = 0f;
        }
    }

    /// <summary>
    /// Registra que el objeto fue disparado por la aspiradora del jugador con un impulso dado.
    /// </summary>
    public void MarkAsThrownByPlayer(float launchImpulse)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        isThrown = true;
        thrownTimestamp = Time.time;
        float mass = rb != null ? Mathf.Max(0.1f, rb.mass) : 1f;
        float dt = Mathf.Max(0.005f, Time.fixedDeltaTime);
        // F = Impulse / dt => a = F / m
        launchAcceleration = (launchImpulse / dt) / mass;
        peakRecentAcceleration = Mathf.Max(peakRecentAcceleration, launchAcceleration);
        peakAccelerationTimer = 0.5f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDestroyed) return;

        // Si colisiona con un enemigo, EnemyHealth se encarga del cálculo F = m * a y destrucción
        // para asegurar sincronización y evitar doble aplicación de daño.
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
            fragment.name = "Throwable_Debris";
            fragment.transform.position = spawnPos + Random.insideUnitSphere * 0.25f;
            fragment.transform.localScale = Vector3.one * Random.Range(0.12f, 0.26f);

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
