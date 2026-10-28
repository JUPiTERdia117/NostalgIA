using NostalgIA.Core.Damage;
using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
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
public class EnemyHealth : MonoBehaviour, IDamageable, IContactHazard
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

    [Header("Contacto con el jugador")]
    [Tooltip("Corazones que resta al jugador por contacto directo (0 lo desactiva).")]
    [SerializeField, Min(0)] private int contactDamageToPlayer = 1;

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
            collision.gameObject.GetComponentInParent<PlayerHealth>() != null ||
            collision.gameObject.GetComponentInParent<EnemyHealth>() != null)
        {
            return;
        }

        // Fase 1: Absorbable reúne el registro cinemático que antes estaba repartido entre
        // PoolableController y ThrowableDamage.
        Absorbable absorbable = collision.gameObject.GetComponentInParent<Absorbable>();
        if (absorbable != null && absorbable.IsDestroyed) return;

        Rigidbody incomingRb = collision.rigidbody != null
            ? collision.rigidbody
            : collision.gameObject.GetComponentInParent<Rigidbody>();

        // Solo evaluar objetos con física o absorbibles
        if (incomingRb == null && absorbable == null) return;

        bool isThrownByPlayer = absorbable != null && absorbable.IsThrown;

        float dt = Mathf.Max(0.005f, Time.fixedDeltaTime);
        float impactSpeed = collision.relativeVelocity.magnitude;

        // 1. Masa efectiva (m) del objeto que impacta
        float explicitMass = incomingRb != null ? Mathf.Max(0.1f, incomingRb.mass) : 1.0f;
        Vector3 scale = collision.transform.lossyScale;
        float volumeEstimate = Mathf.Abs(scale.x * scale.y * scale.z);
        // Un bloque grande con masa por defecto (1) aporta peso de aplastamiento según su volumen
        float effectiveMass = (!isThrownByPlayer && volumeEstimate > 1.8f && explicitMass <= 1.5f)
            ? Mathf.Max(explicitMass, volumeEstimate * 2.2f)
            : explicitMass;

        // 2. Aceleración en el impacto (a)
        //    a) Desaceleración de contacto: a = Δv / Δt
        float contactDeceleration = impactSpeed / dt;
        //    b) Aceleración derivada del impulso resuelto por el motor: a = (J / Δt) / m
        float impulseAcceleration = (collision.impulse.magnitude / dt) / Mathf.Max(0.1f, effectiveMass);
        //    c) Aceleración registrada por el absorbible (lanzamiento y caída libre)
        float trackedAccel = absorbable != null
            ? Mathf.Max(absorbable.CurrentAcceleration, isThrownByPlayer ? absorbable.LaunchAcceleration * 0.35f : 0f)
            : 0f;

        //    d) ¿Cae sobre el enemigo? (aplastamiento por gravedad y peso)
        Vector3 preVel = absorbable != null ? absorbable.PreImpactVelocity :
                         (incomingRb != null ? incomingRb.linearVelocity : -collision.relativeVelocity);
        bool isFallingFromAbove = (collision.transform.position.y > transform.position.y + 0.15f) ||
                                  (preVel.y < -1.0f) ||
                                  (collision.relativeVelocity.y < -1.0f);

        float gravityAccel = isFallingFromAbove ? Physics.gravity.magnitude * (1f + Mathf.Abs(preVel.y) * 0.35f) : 0f;

        // Aceleración neta efectiva en el instante del choque
        float effectiveAcceleration = Mathf.Max(contactDeceleration * 0.35f, impulseAcceleration, trackedAccel * 0.5f) + gravityAccel;

        // 3. Fuerza F = m · a
        float forceMultiplier = absorbable != null ? absorbable.ForceMultiplier : 1.0f;
        float impactForce = effectiveMass * effectiveAcceleration * forceMultiplier;

        // Objeto muy pesado cayendo sobre el enemigo: se amplifica el aplastamiento
        if (!isThrownByPlayer && isFallingFromAbove && effectiveMass >= 2.5f && impactSpeed >= 1.1f)
        {
            impactForce *= 1.85f;
        }

        float minVelRequired = absorbable != null ? absorbable.MinImpactVelocity : minImpactVelocity;

        bool qualifiesForUnthrownImpact = !isThrownByPlayer &&
                                          (impactSpeed >= minVelRequired || (isFallingFromAbove && effectiveMass >= 2.5f && impactSpeed >= 0.9f)) &&
                                          impactForce >= minUnthrownForceThreshold;

        if (!isThrownByPlayer && !qualifiesForUnthrownImpact)
        {
            return;
        }

        // 4. Conversión de fuerza en puntos de daño
        int forceBasedDamage = Mathf.FloorToInt(impactForce / Mathf.Max(1f, forcePerDamageUnit));

        int damageToApply;
        if (isThrownByPlayer)
        {
            // Daño mínimo garantizado cuando el objeto lo lanza el jugador
            int guaranteedMin = Mathf.Max(Mathf.Max(1, minPlayerThrownDamage), absorbable.ImpactDamage);
            damageToApply = Mathf.Max(guaranteedMin, forceBasedDamage);
        }
        else if (isFallingFromAbove && effectiveMass >= 4.0f && impactSpeed >= 2.0f)
        {
            // Un objeto de gran peso que cae sobre un enemigo puede aplastarlo de golpe
            damageToApply = Mathf.Max(forceBasedDamage, currentHealth);
        }
        else
        {
            damageToApply = Mathf.Max(1, forceBasedDamage);
        }

        if (damageToApply <= 0) return;

        TakeDamage(damageToApply);

        // Si fue lanzado por el jugador o el impacto fue fuerte, el objeto se rompe en fragmentos
        if (absorbable != null && absorbable.DestroyOnEnemyHit && (isThrownByPlayer || impactForce >= forcePerDamageUnit * 1.2f))
        {
            Vector3 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            absorbable.BreakApart(hitPoint);
        }
    }

    // ------------------------------------------------------------------ contratos de NostalgIA.Core

    /// <summary>Aplica un golpe descrito por <see cref="DamageInfo"/> (lava, peligros, Fase 2).</summary>
    public void TakeDamage(in DamageInfo info)
    {
        if (IsDead) return;
        if (info.Type == DamageType.Instakill) TakeDamage(currentHealth);
        else TakeDamage(info.Amount);
    }

    /// <summary>
    /// Daño por contacto al jugador: un corazón mientras el enemigo esté vivo (comportamiento
    /// del prototipo, que antes resolvía el controlador del jugador por etiqueta).
    /// </summary>
    public bool TryGetContactDamage(Vector3 contactPoint, out DamageInfo info)
    {
        info = DamageInfo.Normal(contactDamageToPlayer, transform.position);
        return !IsDead && isActiveAndEnabled && contactDamageToPlayer > 0;
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
