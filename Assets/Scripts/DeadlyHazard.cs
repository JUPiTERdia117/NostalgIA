using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona obstáculos mortales o dañinos (pozos de ácido/vacío, rayos láser, pinchos, cuchillas).
/// </summary>
public class DeadlyHazard : MonoBehaviour
{
    public enum HazardType
    {
        InstantKill,
        DamageOverTime,
        SingleHitDamage
    }

    [Header("Configuración de Peligro")]
    [SerializeField] private HazardType hazardType = HazardType.InstantKill;
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private float damageInterval = 1f;
    [Tooltip("Fuerza de empuje que aparta al jugador del peligro al hacerle daño.")]
    [SerializeField] private float knockbackForce = 5f;

    private float nextDamageTime;

    private void OnTriggerEnter(Collider other)
    {
        ProcessHazard(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        if (hazardType == HazardType.DamageOverTime && Time.time >= nextDamageTime)
        {
            ProcessHazard(other.gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessHazard(collision.gameObject);
    }

    private void ProcessHazard(GameObject target)
    {
        if (!target.CompareTag("Player"))
        {
            // También comprobar si el padre tiene la etiqueta Player
            if (target.transform.root.CompareTag("Player"))
            {
                target = target.transform.root.gameObject;
            }
            else
            {
                return;
            }
        }

        PlayerController player = target.GetComponent<PlayerController>();
        if (player == null)
            player = target.GetComponentInParent<PlayerController>();

        if (hazardType == HazardType.InstantKill)
        {
            if (player != null)
            {
                player.Die();
            }
            else
            {
                GameController gm = FindAnyObjectByType<GameController>();
                if (gm != null) gm.RespawnPlayer();
            }
        }
        else
        {
            nextDamageTime = Time.time + damageInterval;
            if (player != null)
            {
                player.TakeDamage(damageAmount);
            }
            else
            {
                target.SendMessage("TakeDamage", damageAmount, SendMessageOptions.DontRequireReceiver);
            }

            // Aplicar pequeño empuje si tiene CharacterController
            var cc = target.GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
            {
                Vector3 knockDir = (target.transform.position - transform.position).normalized + Vector3.up * 0.5f;
                cc.Move(knockDir * (knockbackForce * Time.deltaTime));
            }
        }
    }
}
