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
    private Bounds worldBounds;
    private bool hasCachedBounds = false;
    private float surfaceY;

    private void Awake()
    {
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
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            worldBounds = col.bounds;
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

        // Detección precisa: los pies del jugador deben estar estrictamente dentro del perímetro de la lava
        // y a la altura de la superficie o por debajo (sumergido).
        Vector3 playerPos = player.transform.position;

        if (hasCachedBounds)
        {
            bool insideX = playerPos.x >= worldBounds.min.x && playerPos.x <= worldBounds.max.x;
            bool insideZ = playerPos.z >= worldBounds.min.z && playerPos.z <= worldBounds.max.z;
            bool insideY = playerPos.y <= surfaceY + 0.1f && playerPos.y >= surfaceY - 5.0f;

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
