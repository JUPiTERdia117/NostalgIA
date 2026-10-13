using UnityEngine;

/// <summary>
/// Asigna una cantidad de daño a un objeto y genera un efecto de fragmentos
/// de destrucción cuando impacta y se destruye contra un enemigo.
/// </summary>
public class ThrowableDamage : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Cantidad de daño asignada que inflige este objeto al impactar.")]
    [SerializeField] private int damage = 2;
    [Tooltip("Si es verdadero, el objeto se destruye con fragmentos al chocar contra un enemigo.")]
    [SerializeField] private bool destroyOnImpact = true;
    [Tooltip("Cantidad de fragmentos que se desprenden al romperse.")]
    [SerializeField] private int debrisCount = 6;
    [Tooltip("Velocidad mínima de impacto para infligir daño.")]
    [SerializeField] private float minImpactVelocity = 2.0f;

    [HideInInspector] public bool isThrown = false;
    private bool isDestroyed = false;

    public int Damage { get => damage > 0 ? damage : 1; set => damage = value; }
    public bool DestroyOnImpact { get => destroyOnImpact; set => destroyOnImpact = value; }
    public float MinImpactVelocity => minImpactVelocity;
    public bool IsDestroyed => isDestroyed;

    private void OnCollisionEnter(Collision collision)
    {
        if (isDestroyed) return;

        // Si colisiona con un enemigo, EnemyHealth se encarga del impacto y destrucción
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

            Rigidbody rb = fragment.AddComponent<Rigidbody>();
            rb.mass = 0.15f;
            Vector3 force = (Random.onUnitSphere + Vector3.up * 0.6f).normalized * Random.Range(4f, 8f);
            rb.AddForce(force, ForceMode.Impulse);
            rb.angularVelocity = Random.insideUnitSphere * 12f;

            Destroy(fragment, Random.Range(1.2f, 2.2f));
        }

        Destroy(gameObject);
    }
}
