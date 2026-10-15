using UnityEngine;

/// <summary>
/// Zona de peligro por lava ardiente.
/// Aplica daño letal instantáneo al jugador si la toca, cae en ella o entra en su volumen,
/// provocando su muerte y reaparición en el último punto de control.
/// </summary>
public class LavaHazard : MonoBehaviour
{
    [Tooltip("Cantidad de daño infligido. 99 para muerte instantánea.")]
    [SerializeField] private int damage = 99;

    private PlayerController player;
    private Collider myCollider;
    private Bounds worldBounds;
    private bool hasCachedBounds = false;
    private float surfaceY;

    private void Awake()
    {
        myCollider = GetComponent<Collider>();
        CacheBounds();
        FindPlayer();
    }

    private void Start()
    {
        CacheBounds();
        if (player == null) FindPlayer();
    }

    private void CacheBounds()
    {
        if (myCollider == null) myCollider = GetComponent<Collider>();

        if (myCollider != null)
        {
            worldBounds = myCollider.bounds;
            surfaceY = worldBounds.max.y;
            hasCachedBounds = true;
        }
        else
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
            {
                worldBounds = rend.bounds;
                surfaceY = worldBounds.max.y;
                hasCachedBounds = true;
            }
            else
            {
                surfaceY = transform.position.y;
            }
        }
    }

    private void Update()
    {
        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        if (player.dead) return;

        // Si tenemos collider, refrescamos bounds periódicamente o usamos bounds directos
        if (myCollider != null)
        {
            worldBounds = myCollider.bounds;
            surfaceY = worldBounds.max.y;
            hasCachedBounds = true;
        }

        Vector3 playerPos = player.transform.position;

        if (hasCachedBounds)
        {
            float minX = Mathf.Min(worldBounds.min.x, worldBounds.max.x);
            float maxX = Mathf.Max(worldBounds.min.x, worldBounds.max.x);
            float minZ = Mathf.Min(worldBounds.min.z, worldBounds.max.z);
            float maxZ = Mathf.Max(worldBounds.min.z, worldBounds.max.z);

            bool insideX = playerPos.x >= minX - 0.2f && playerPos.x <= maxX + 0.2f;
            bool insideZ = playerPos.z >= minZ - 0.2f && playerPos.z <= maxZ + 0.2f;
            bool insideY = playerPos.y <= surfaceY + 0.4f && playerPos.y >= surfaceY - 8.0f;

            if (insideX && insideZ && insideY)
            {
                KillPlayer();
            }
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<PlayerController>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleContact(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleContact(collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandleContact(collision.gameObject);
    }

    private void HandleContact(GameObject obj)
    {
        PlayerController targetPlayer = obj.GetComponentInParent<PlayerController>();
        if (targetPlayer != null && !targetPlayer.dead)
        {
            targetPlayer.TakeDamage(damage);
            return;
        }

        EnemyHealth enemy = obj.GetComponentInParent<EnemyHealth>();
        if (enemy != null && !enemy.IsDead)
        {
            enemy.TakeDamage(damage);
        }
    }

    private void KillPlayer()
    {
        if (player != null && !player.dead)
        {
            Debug.Log($"[LavaHazard] ¡El jugador cayó en la lava ({gameObject.name})! Infligiendo daño letal...");
            player.TakeDamage(damage);
        }
    }
}
