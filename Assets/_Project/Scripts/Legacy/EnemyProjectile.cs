using NostalgIA.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Proyectil disparado por enemigos (como la calavera flotante).
/// Viaja en línea recta y daña al jugador al impactar.
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private int damage = 1;
    [SerializeField] private float lifetime = 5f;

    [Header("Impact Feedback")]
    [SerializeField] private int impactGibs = 4;

    private bool hasImpacted;

    public void Initialize(Vector3 direction, int customDamage = 1, float customSpeed = 12f)
    {
        transform.forward = direction;
        damage = customDamage;
        speed = customSpeed;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (hasImpacted) return;
        transform.position += transform.forward * (speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleImpact(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleImpact(collision.gameObject);
    }

    private void HandleImpact(GameObject hitObject)
    {
        if (hasImpacted) return;

        // Ignorar a otros enemigos y disparos
        if (hitObject.CompareTag("Enemy") || hitObject.GetComponentInParent<EnemyHealth>() != null)
        {
            return;
        }

        hasImpacted = true;

        // Comprobar si impactó al jugador
        PlayerHealth player = hitObject.GetComponentInParent<PlayerHealth>();
        if (player != null && !player.IsDead)
        {
            player.TakeDamage(damage);
            Debug.Log($"[EnemyProjectile] ¡Proyectil impactó al jugador infligiendo {damage} de daño!");
        }

        SpawnImpactBurst();
        Destroy(gameObject);
    }

    private void SpawnImpactBurst()
    {
        Renderer rend = GetComponent<Renderer>();
        Material mat = rend != null ? rend.sharedMaterial : null;

        for (int i = 0; i < impactGibs; i++)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spark.name = "Spark_Debris";
            spark.transform.position = transform.position + Random.insideUnitSphere * 0.15f;
            spark.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);

            if (mat != null)
            {
                spark.GetComponent<Renderer>().sharedMaterial = mat;
            }

            Rigidbody rb = spark.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.AddForce(Random.onUnitSphere * Random.Range(2f, 5f), ForceMode.Impulse);
            Destroy(spark, 0.6f);
        }
    }
}
