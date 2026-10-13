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
/// Cambia dinámicamente el color del enemigo según su porcentaje de salud y muestra
/// retroalimentación visual completa (flash, popups numéricos de daño y barra de vida).
/// Al ser impactado por objetos lanzados, recibe el daño asignado a ese objeto específico
/// y permite que el objeto se destruya con su propio efecto de fragmentos.
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

    [Header("Impact Settings")]
    [Tooltip("Velocidad mínima de impacto de un objeto genérico para causar daño.")]
    [SerializeField] private float minImpactVelocity = 3.5f;
    [Tooltip("Daño por defecto si el objeto impactante no tiene daño asignado.")]
    [SerializeField] private int defaultImpactDamage = 1;

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

        // Comprobar si el objeto es arrojable (PoolableController o ThrowableDamage)
        PoolableController poolable = collision.gameObject.GetComponentInParent<PoolableController>();
        ThrowableDamage throwable = collision.gameObject.GetComponentInParent<ThrowableDamage>();

        if (poolable != null && poolable.IsDestroyed) return;
        if (throwable != null && throwable.IsDestroyed) return;

        float speed = collision.relativeVelocity.magnitude;
        float requiredVelocity = poolable != null ? poolable.MinImpactVelocity : (throwable != null ? throwable.MinImpactVelocity : minImpactVelocity);

        bool isThrownObject = (poolable != null && (poolable.isThrown || speed >= 1.5f)) ||
                              (throwable != null && (throwable.isThrown || speed >= 1.5f));

        if (isThrownObject || speed >= requiredVelocity)
        {
            int damageToApply = defaultImpactDamage;
            bool shouldBreak = false;

            if (throwable != null)
            {
                damageToApply = throwable.Damage;
                shouldBreak = throwable.DestroyOnImpact;
            }
            else if (poolable != null)
            {
                damageToApply = poolable.ImpactDamage;
                shouldBreak = poolable.DestroyOnEnemyHit;
            }
            else if (speed >= requiredVelocity * 2f)
            {
                damageToApply = defaultImpactDamage * 2;
            }

            Debug.Log($"[EnemyHealth] {gameObject.name} impactado por objeto lanzado {collision.gameObject.name}. Daño asignado: {damageToApply}");
            TakeDamage(damageToApply);

            // Destruir el objeto arrojadizo generando su propio estallido de fragmentos
            if (shouldBreak)
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
