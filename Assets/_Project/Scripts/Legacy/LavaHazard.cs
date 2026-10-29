using NostalgIA.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Zona de peligro por lava ("Piscina Azul" con código animado bajando).
/// Aplica daño letal instantáneo al jugador si la toca, cae en ella o entra en su volumen,
/// provocando su muerte y reaparicion en el último punto de control.
/// </summary>
public class LavaHazard : MonoBehaviour
{
    [Tooltip("Cantidad de daño infligido. 99 para muerte instantánea.")]
    [SerializeField] private int damage = 99;
    [SerializeField] private bool applyBlueCodeShader = false;

    private static Material sharedBlueCodeMaterial;

    private PlayerHealth player;
    private Collider myCollider;
    private Bounds worldBounds;
    private bool hasCachedBounds = false;
    private float surfaceY;

    private void Awake()
    {
        myCollider = GetComponent<Collider>();
        CacheBounds();
        FindPlayer();
        if (applyBlueCodeShader)
        {
            ApplyBlueCodeVisuals();
        }
    }

    private void Start()
    {
        CacheBounds();
        if (player == null) FindPlayer();
        if (applyBlueCodeShader)
        {
            ApplyBlueCodeVisuals();
        }
    }

    public void ApplyBlueCodeVisuals()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            Shader blueShader = Shader.Find("Tutorial/BlueCodeLava");
            if (blueShader == null)
            {
                blueShader = Resources.Load<Shader>("BlueCodeLava");
            }

            if (blueShader != null)
            {
                if (sharedBlueCodeMaterial == null || sharedBlueCodeMaterial.shader != blueShader)
                {
                    sharedBlueCodeMaterial = new Material(blueShader);
                    sharedBlueCodeMaterial.name = "BlueCodePool_RuntimeMat";
                }
                rend.sharedMaterial = sharedBlueCodeMaterial;
            }
        }

        // Actualizar luces de resplandor a tono azul eléctrico
        foreach (Light l in GetComponentsInChildren<Light>(true))
        {
            l.color = new Color(0.05f, 0.45f, 1.0f, 1.0f);
        }
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

        if (player.IsDead) return;

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

            // Margen interior para no matar al jugador cuando pisa el borde seco junto a la piscina
            bool insideX = playerPos.x >= minX + 0.15f && playerPos.x <= maxX - 0.15f;
            bool insideZ = playerPos.z >= minZ + 0.15f && playerPos.z <= maxZ - 0.15f;
            // playerPos.y está en los pies de la cápsula; solo es letal si los pies bajan al nivel de la superficie de lava
            bool insideY = playerPos.y <= surfaceY + 0.12f && playerPos.y >= surfaceY - 3.0f;

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
            player = playerObj.GetComponent<PlayerHealth>();
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
        PlayerHealth targetPlayer = obj.GetComponentInParent<PlayerHealth>();
        if (targetPlayer != null && !targetPlayer.IsDead)
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
        if (player != null && !player.IsDead)
        {
            Debug.Log($"[LavaHazard] ¡El jugador cayó en la lava ({gameObject.name})! Infligiendo daño letal...");
            player.TakeDamage(damage);
        }
    }
}
