using UnityEngine;

/// <summary>
/// Gestiona obstáculos mortales y zonas de peligro (abismo/vacío, sierras giratorias, trampas de pinchos).
/// </summary>
public class DeadlyHazard : MonoBehaviour
{
    public enum HazardType
    {
        LethalVoid,    // Mata instantáneamente al caer al vacío o lava
        Spikes,        // Trampas de púas/pinchos con daño y cooldown
        SpinningBlade, // Sierras o cuchillas giratorias
        Crusher        // Aplastadores
    }

    [Header("Configuración de Peligro")]
    [Tooltip("Tipo de obstáculo mortal")]
    [SerializeField] private HazardType hazardType = HazardType.LethalVoid;
    [Tooltip("Daño infligido al jugador")]
    [SerializeField] private int damageAmount = 999;
    [Tooltip("Tiempo de inmunidad temporal entre golpes para evitar daño por fotograma")]
    [SerializeField] private float damageCooldown = 1.0f;
    [Tooltip("Fuerza de empuje/knockback al recibir el impacto")]
    [SerializeField] private float knockbackForce = 10f;
    [Tooltip("Efecto visual o de partículas al dañar")]
    [SerializeField] private GameObject hitEffectPrefab;

    private float lastDamageTime = -10f;

    void OnTriggerEnter(Collider other)
    {
        HandleHazardImpact(other.gameObject, other.ClosestPoint(transform.position));
    }

    void OnTriggerStay(Collider other)
    {
        if (hazardType != HazardType.LethalVoid && Time.time - lastDamageTime >= damageCooldown)
        {
            HandleHazardImpact(other.gameObject, other.ClosestPoint(transform.position));
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        HandleHazardImpact(collision.gameObject, contactPoint);
    }

    private void HandleHazardImpact(GameObject target, Vector3 hitPoint)
    {
        PlayerController playerController = target.GetComponentInParent<PlayerController>();
        if (playerController == null && !target.CompareTag("Player")) return;

        if (Time.time - lastDamageTime < damageCooldown && hazardType != HazardType.LethalVoid)
        {
            return;
        }

        lastDamageTime = Time.time;

        GameObject playerObj = playerController != null ? playerController.gameObject : target;

        if (hazardType == HazardType.LethalVoid)
        {
            CharacterController cc = playerObj.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerObj.SendMessage("TakeDamage", 999, SendMessageOptions.DontRequireReceiver);

            if (cc != null) cc.enabled = true;
            return;
        }

        // Infligir daño al jugador llamando a TakeDamage(int)
        playerObj.SendMessage("TakeDamage", damageAmount, SendMessageOptions.DontRequireReceiver);

        // Efecto visual
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, hitPoint, Quaternion.identity);
        }

        // Si el jugador tiene CharacterController o Rigidbody, aplicar retroceso si no es vacío
        Vector3 pushDirection = (playerObj.transform.position - transform.position).normalized;
        pushDirection.y = 0.4f;

        if (playerObj.TryGetComponent<CharacterController>(out var charCtrl))
        {
            charCtrl.Move(pushDirection * (knockbackForce * 0.1f));
        }
        else if (playerObj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.AddForce(pushDirection * knockbackForce, ForceMode.Impulse);
        }
    }
}
