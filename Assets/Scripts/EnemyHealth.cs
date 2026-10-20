using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Niveles de salud / tipos de enemigos con diferentes escalas y resistencias.
/// </summary>
public enum EnemyHealthTier
{
    Normal,     // Vida base estándar
    Resistente, // 1.5x de vida, ligeramente más robusto
    Elite,      // 2.5x de vida, más amenazante y resistente
    Jefe        // 4.0x de vida, tamaño colosal
}

/// <summary>
/// Gestiona la vida, los niveles de salud, el daño recibido y la muerte de los enemigos.
/// El sistema de daño por impacto se basa en la Fuerza física en el momento del impacto:
///   F = m * a (Masa del objeto * Aceleración en el impacto / desaceleración de choque / gravedad).
/// - Si un objeto pesado cae sobre un enemigo (aunque NO haya sido lanzado por el jugador),
///   su gran peso y aceleración de caída generan una fuerza F = m * a capaz de aplastarlo y matarlo.
/// - Siempre que el jugador lance un objeto (isThrown == true), se garantiza que inflija al menos
///   el daño mínimo al enemigo, escalando hacia arriba si la fuerza F = m * a es mayor.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health Tiers & Levels")]
    [SerializeField] private EnemyHealthTier healthTier = EnemyHealthTier.Normal;
    [SerializeField] private int baseMaxHealth = 3;
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int currentHealth;

    [Header("Color According to Health")]
    [Tooltip("Color a salud completa (si no se asigna, se obtiene del material original).")]
    [SerializeField] private Color fullHealthColor = Color.clear;
    [Tooltip("Color a media salud (herido / dañado).")]
    [SerializeField] private Color woundedColor = new Color(1.0f, 0.55f, 0.12f);
    [Tooltip("Color a baja salud (crítico / sangrante).")]
    [SerializeField] private Color criticalColor = new Color(0.85f, 0.08f, 0.08f);

    [Header("Physics Impact Settings (F = m * a)")]
    [Tooltip("Cantidad de Fuerza física (Newtons = kg * m/s^2) equivalente a 1 punto de daño.")]
    [SerializeField] private float forcePerDamageUnit = 45f;
    [Tooltip("Fuerza mínima (Newtons) para que un objeto NO lanzado por el jugador haga daño al caer o chocar.")]
    [SerializeField] private float minUnthrownForceThreshold = 25f;
    [Tooltip("Velocidad mínima de impacto de un objeto no lanzado para activar el cálculo de fuerza.")]
    [SerializeField] private float minImpactVelocity = 1.6f;
    [Tooltip("Daño mínimo garantizado siempre que el jugador lance un objeto.")]
    [SerializeField] private int minPlayerThrownDamage = 1;

    [Header("Visual Feedback")]
    [SerializeField] private Color damageFlashColor = Color.white;
    [SerializeField] private float flashDuration = 0.12f;
    [SerializeField] private bool showFloatingDamage = true;
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private float healthBarHeight = 1.1f;
    [SerializeField] private bool spawnGibsOnDeath = true;
    [SerializeField] private int gibCount = 5;

    [Header("Events")]
    public UnityEvent<int> onTakeDamage;
    public UnityEvent onDeath;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public EnemyHealthTier HealthTier => healthTier;
    public bool IsDead { get; private set; }

    private Renderer[] renderers;
    private MaterialPropertyBlock propBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private Coroutine flashCoroutine;
    private Coroutine punchCoroutine;
    private Vector3 originalBaseScale;
    private EnemyHealthBar healthBar;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        propBlock = new MaterialPropertyBlock();
        originalBaseScale = transform.localScale;

        // Detectar color base original del material si no se configuró manualmente
        if (fullHealthColor == Color.clear && renderers.Length > 0 && renderers[0].sharedMaterial != null)
        {
            if (renderers[0].sharedMaterial.HasProperty(BaseColorId))
            {
                fullHealthColor = renderers[0].sharedMaterial.GetColor(BaseColorId);
            }
            else if (renderers[0].sharedMaterial.HasProperty(ColorId))
            {
                fullHealthColor = renderers[0].sharedMaterial.GetColor(ColorId);
            }
            else
            {
                fullHealthColor = Color.white;
            }
        }

        ApplyHealthTier();
    }

    private void Start()
    {
        if (showHealthBar && healthBar == null)
        {
            healthBar = EnemyHealthBar.Attach(this, healthBarHeight);
        }
        UpdateHealthVisualColor();
    }

    public void SetHealthTier(EnemyHealthTier tier)
    {
        healthTier = tier;
        ApplyHealthTier();
        UpdateHealthVisualColor();
        if (healthBar != null) healthBar.UpdateBar();
    }

    private void ApplyHealthTier()
    {
        float multiplier = 1f;
        float scaleMultiplier = 1f;

        switch (healthTier)
        {
            case EnemyHealthTier.Normal:
                multiplier = 1f;
                scaleMultiplier = 1f;
                break;
            case EnemyHealthTier.Resistente:
                multiplier = 1.6f;
                scaleMultiplier = 1.15f;
                break;
            case EnemyHealthTier.Elite:
                multiplier = 2.5f;
                scaleMultiplier = 1.35f;
                break;
            case EnemyHealthTier.Jefe:
                multiplier = 4.0f;
                scaleMultiplier = 1.65f;
                break;
        }

        maxHealth = Mathf.Max(1, Mathf.RoundToInt(baseMaxHealth * multiplier));
        currentHealth = maxHealth;
        transform.localScale = originalBaseScale * scaleMultiplier;
    }

    [ContextMenu("Take Damage (1)")]
    public void TestTakeDamage() => TakeDamage(1);

    [ContextMenu("Take Damage (2)")]
    public void TestTakeDamage2() => TakeDamage(2);

    public void TakeDamage(int damage)
    {
        if (IsDead) return;

        currentHealth -= damage;
        Debug.Log($"[EnemyHealth] {gameObject.name} (Nivel: {healthTier}) recibió {damage} de daño. Vida restante: {currentHealth}/{maxHealth}", this);

        // 1. Mostrar texto emergente de daño
        if (showFloatingDamage)
        {
            DamagePopup.Create(transform.position + Vector3.up * (healthBarHeight * 0.7f), damage, damage >= 2);
        }

        // 2. Destello visual y sacudida de escala
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashAndColorRoutine());

        if (punchCoroutine != null) StopCoroutine(punchCoroutine);
        punchCoroutine = StartCoroutine(ScalePunchRoutine());

        // 3. Actualizar barra de vida
        if (healthBar != null)
        {
            healthBar.UpdateBar();
        }

        onTakeDamage?.Invoke(damage);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    [ContextMenu("Die")]
    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        currentHealth = 0;

        Debug.Log($"[EnemyHealth] {gameObject.name} ha muerto.", this);
        onDeath?.Invoke();

        if (spawnGibsOnDeath)
        {
            SpawnDebris();
        }

        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsDead) return;

        // Ignorar colisiones con el propio jugador o con otros enemigos
        if (collision.gameObject.CompareTag("Player") ||
            collision.gameObject.GetComponentInParent<PlayerController>() != null ||
            collision.gameObject.GetComponentInParent<EnemyHealth>() != null)
        {
            return;
        }

        PoolableController poolable = collision.gameObject.GetComponentInParent<PoolableController>();
        ThrowableDamage throwable = collision.gameObject.GetComponentInParent<ThrowableDamage>();

        if (poolable != null && poolable.IsDestroyed) return;
        if (throwable != null && throwable.IsDestroyed) return;

        Rigidbody incomingRb = collision.rigidbody != null
            ? collision.rigidbody
            : collision.gameObject.GetComponentInParent<Rigidbody>();

        // Solo evaluar objetos con física o componentes arrojables
        if (incomingRb == null && poolable == null && throwable == null) return;

        bool isThrownByPlayer = (poolable != null && poolable.isThrown) ||
                                (throwable != null && throwable.isThrown);

        float dt = Mathf.Max(0.005f, Time.fixedDeltaTime);
        float impactSpeed = collision.relativeVelocity.magnitude;

        // 1. Obtener la MASA efectiva (m) del objeto que impacta
        float explicitMass = incomingRb != null ? Mathf.Max(0.1f, incomingRb.mass) : 1.0f;
        Vector3 scale = collision.transform.lossyScale;
        float volumeEstimate = Mathf.Abs(scale.x * scale.y * scale.z);
        // Si es un bloque/obstáculo grande que cae y tenía masa por defecto (1), su volumen aporta peso real de aplastamiento
        float effectiveMass = (!isThrownByPlayer && volumeEstimate > 1.8f && explicitMass <= 1.5f)
            ? Mathf.Max(explicitMass, volumeEstimate * 2.2f)
            : explicitMass;

        // 2. Obtener la ACELERACIÓN en el momento del impacto (a)
        //    a) Desaceleración brusca de contacto: a_impact = dv / dt
        float contactDeceleration = impactSpeed / dt;
        //    b) Aceleración derivada del impulso físico resuelto por Unity: a_impulse = (J / dt) / m
        float impulseAcceleration = (collision.impulse.magnitude / dt) / Mathf.Max(0.1f, effectiveMass);
        //    c) Aceleración cinemática previa registrada en el objeto (incluyendo lanzamiento y caída libre)
        float trackedAccel = 0f;
        if (throwable != null)
        {
            trackedAccel = Mathf.Max(throwable.CurrentAcceleration, isThrownByPlayer ? throwable.LaunchAcceleration * 0.35f : 0f);
        }
        if (poolable != null)
        {
            trackedAccel = Mathf.Max(trackedAccel, Mathf.Max(poolable.CurrentAcceleration, isThrownByPlayer ? poolable.LaunchAcceleration * 0.35f : 0f));
        }

        //    d) Verificar si el objeto está cayendo sobre el enemigo (aplastamiento por gravedad y peso)
        Vector3 preVel = throwable != null ? throwable.PreImpactVelocity :
                         (poolable != null ? poolable.PreImpactVelocity :
                         (incomingRb != null ? incomingRb.linearVelocity : -collision.relativeVelocity));
        bool isFallingFromAbove = (collision.transform.position.y > transform.position.y + 0.15f) ||
                                  (preVel.y < -1.0f) ||
                                  (collision.relativeVelocity.y < -1.0f);

        float gravityAccel = isFallingFromAbove ? Physics.gravity.magnitude * (1f + Mathf.Abs(preVel.y) * 0.35f) : 0f;

        // Aceleración neta efectiva en el instante del choque
        float effectiveAcceleration = Mathf.Max(contactDeceleration * 0.35f, impulseAcceleration, trackedAccel * 0.5f) + gravityAccel;

        // 3. Calcular FUERZA F = m * a
        float forceMultiplier = throwable != null ? throwable.ForceMultiplier : 1.0f;
        float impactForce = effectiveMass * effectiveAcceleration * forceMultiplier;

        // Si un objeto muy pesado cae sobre el enemigo (aplastamiento por gran peso), amplificar efecto de aplastamiento
        if (!isThrownByPlayer && isFallingFromAbove && effectiveMass >= 2.5f && impactSpeed >= 1.1f)
        {
            impactForce *= 1.85f;
        }

        float minVelRequired = poolable != null ? poolable.MinImpactVelocity :
                               (throwable != null ? throwable.MinImpactVelocity : minImpactVelocity);

        bool qualifiesForUnthrownImpact = !isThrownByPlayer &&
                                          (impactSpeed >= minVelRequired || (isFallingFromAbove && effectiveMass >= 2.5f && impactSpeed >= 0.9f)) &&
                                          impactForce >= minUnthrownForceThreshold;

        if (!isThrownByPlayer && !qualifiesForUnthrownImpact)
        {
            return;
        }

        // 4. Convertir Fuerza (F = m * a) en puntos de daño
        int forceBasedDamage = Mathf.FloorToInt(impactForce / Mathf.Max(1f, forcePerDamageUnit));

        int damageToApply = forceBasedDamage;
        if (isThrownByPlayer)
        {
            // Garantizar SIEMPRE daño mínimo cuando el objeto es lanzado por el jugador
            int guaranteedMin = Mathf.Max(1, minPlayerThrownDamage);
            if (throwable != null) guaranteedMin = Mathf.Max(guaranteedMin, throwable.MinPlayerThrowDamage);
            else if (poolable != null) guaranteedMin = Mathf.Max(guaranteedMin, poolable.ImpactDamage);

            damageToApply = Mathf.Max(guaranteedMin, forceBasedDamage);
        }
        else if (isFallingFromAbove && effectiveMass >= 4.0f && impactSpeed >= 2.0f)
        {
            // Un objeto de gran peso que cae sobre un enemigo puede aplastarlo y matarlo de golpe
            damageToApply = Mathf.Max(forceBasedDamage, currentHealth);
        }
        else
        {
            damageToApply = Mathf.Max(1, forceBasedDamage);
        }

        if (damageToApply <= 0) return;

        Debug.Log($"[EnemyHealth] {gameObject.name} impactado por {collision.gameObject.name} | LanzadoPorJugador={isThrownByPlayer} | Masa(m)={effectiveMass:F2}kg | Acel(a)={effectiveAcceleration:F2}m/s² | Fuerza(F=m*a)={impactForce:F1}N -> Daño={damageToApply}");
        TakeDamage(damageToApply);

        bool shouldBreak = false;
        if (throwable != null) shouldBreak = throwable.DestroyOnImpact;
        else if (poolable != null) shouldBreak = poolable.DestroyOnEnemyHit;

        // Si fue lanzado por el jugador o si el impacto de caída fue suficientemente fuerte, romper en fragmentos
        if (shouldBreak && (isThrownByPlayer || impactForce >= forcePerDamageUnit * 1.2f))
        {
            Vector3 hitPoint = collision.contactCount > 0 ? collision.contacts[0].point : collision.transform.position;
            if (throwable != null)
            {
                throwable.BreakApart(hitPoint);
            }
            else if (poolable != null)
            {
                poolable.BreakApart(hitPoint);
            }
        }
    }

    /// <summary>
    /// Cambia el color del enemigo según su nivel de salud actual.
    /// Verde/Original -> Amarillo/Naranja (50%) -> Rojo Crítico (0-25%).
    /// </summary>
    public void UpdateHealthVisualColor()
    {
        if (renderers == null || renderers.Length == 0) return;

        float ratio = Mathf.Clamp01((float)currentHealth / maxHealth);
        Color targetColor;

        if (ratio > 0.5f)
        {
            targetColor = Color.Lerp(woundedColor, fullHealthColor, (ratio - 0.5f) * 2f);
        }
        else
        {
            targetColor = Color.Lerp(criticalColor, woundedColor, ratio * 2f);
        }

        // Tinte adicional según el Tier (por ejemplo, los élites tienen un matiz dorado/amenazante)
        if (healthTier == EnemyHealthTier.Elite)
        {
            targetColor = Color.Lerp(targetColor, new Color(0.9f, 0.8f, 0.2f), 0.25f);
        }
        else if (healthTier == EnemyHealthTier.Jefe)
        {
            targetColor = Color.Lerp(targetColor, new Color(0.9f, 0.1f, 0.9f), 0.3f);
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorId, targetColor);
            propBlock.SetColor(ColorId, targetColor);
            renderers[i].SetPropertyBlock(propBlock);
        }
    }

    private IEnumerator FlashAndColorRoutine()
    {
        // 1. Destello brillante blanco
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorId, damageFlashColor);
            propBlock.SetColor(ColorId, damageFlashColor);
            renderers[i].SetPropertyBlock(propBlock);
        }

        yield return new WaitForSeconds(flashDuration);

        // 2. Transición al nuevo color correspondiente a la salud restante
        UpdateHealthVisualColor();
        flashCoroutine = null;
    }

    private IEnumerator ScalePunchRoutine()
    {
        Vector3 targetScale = transform.localScale;
        Vector3 punchScale = targetScale * 1.25f;

        float t = 0f;
        while (t < 0.08f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(targetScale, punchScale, t / 0.08f);
            yield return null;
        }

        t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(punchScale, targetScale, t / 0.12f);
            yield return null;
        }

        transform.localScale = targetScale;
        punchCoroutine = null;
    }

    private void SpawnDebris()
    {
        Vector3 center = transform.position;
        for (int i = 0; i < gibCount; i++)
        {
            GameObject gib = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gib.name = "Gib_Debris";
            gib.transform.position = center + Random.insideUnitSphere * 0.35f;
            gib.transform.localScale = Vector3.one * Random.Range(0.12f, 0.28f);

            if (renderers != null && renderers.Length > 0 && renderers[0] != null && renderers[0].sharedMaterial != null)
            {
                gib.GetComponent<Renderer>().sharedMaterial = renderers[0].sharedMaterial;
            }

            Rigidbody rb = gib.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            Vector3 force = (Random.onUnitSphere + Vector3.up * 0.75f).normalized * Random.Range(3f, 7f);
            rb.AddForce(force, ForceMode.Impulse);
            rb.angularVelocity = Random.insideUnitSphere * 10f;

            Destroy(gib, Random.Range(1.5f, 2.5f));
        }
    }
}
