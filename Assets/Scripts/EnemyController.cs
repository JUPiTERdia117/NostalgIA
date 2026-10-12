using UnityEngine;

/// <summary>Sentinela básica: vigila su isla, persigue al jugador cercano y lo golpea.</summary>
public class EnemyController : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float moveSpeed = 1.9f;
    [SerializeField, Min(1f)] private float aggroRange = 11f;
    [SerializeField, Min(0.5f)] private float attackRange = 1.8f;
    [SerializeField, Min(0.2f)] private float attackCooldown = 1.4f;
    [SerializeField, Min(0.1f)] private float leashRadius = 3.5f;
    [SerializeField, Min(0.1f)] private float stunDuration = 2.8f;

    private PlayerController target;
    private Vector3 homePosition;
    private float nextAttackTime;
    private float attackImpactTime;
    private float stunnedUntil;
    private bool attackPending;

    void Start()
    {
        homePosition = transform.position;
        nextAttackTime = Time.time + 0.8f;
        FindTarget();
    }

    void Update()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            FindTarget();
        if (target == null || target.dead) return;

        if (Time.time < stunnedUntil)
        {
            attackPending = false;
            return;
        }

        Vector3 offset = target.transform.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance > aggroRange) return;

        Vector3 direction = distance > 0.01f ? offset / distance : transform.forward;
        if (distance > attackRange)
        {
            Vector3 next = transform.position + direction * (moveSpeed * Time.deltaTime);
            Vector3 fromHome = next - homePosition;
            fromHome.y = 0f;
            if (fromHome.sqrMagnitude > leashRadius * leashRadius)
                next = homePosition + fromHome.normalized * leashRadius;
            next.y = homePosition.y;
            transform.position = next;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up), Time.deltaTime * 7f);
            attackPending = false;
            return;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(direction, Vector3.up), Time.deltaTime * 8f);
        if (!attackPending && Time.time >= nextAttackTime)
        {
            attackPending = true;
            attackImpactTime = Time.time + 0.24f;
            nextAttackTime = Time.time + attackCooldown;
        }
        if (attackPending && Time.time >= attackImpactTime)
        {
            attackPending = false;
            Vector3 impactOffset = target.transform.position - transform.position;
            impactOffset.y = 0f;
            if (impactOffset.sqrMagnitude <= attackRange * attackRange)
                target.TakeDamage(1);
        }
    }

    private void FindTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) target = player.GetComponent<PlayerController>();
    }

    public void Stun(float duration = -1f)
    {
        stunnedUntil = Mathf.Max(stunnedUntil,
            Time.time + (duration > 0f ? duration : stunDuration));
        attackPending = false;
    }

    void OnTriggerEnter(Collider other)
    {
        PoolableController projectile = other.GetComponentInParent<PoolableController>();
        if (projectile != null && other.attachedRigidbody != null &&
            !other.attachedRigidbody.isKinematic)
            Stun();
    }
}
