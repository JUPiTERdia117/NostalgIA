using UnityEngine;

/// <summary>Guardián facetado que persigue al jugador sobre la plataforma donde ambos se encuentran.</summary>
public sealed class Level1Warden : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField, Min(0.1f)] private float moveSpeed = 2.1f;
    [SerializeField, Min(1f)] private float noticeDistance = 20f;
    [SerializeField, Min(0.5f)] private float strikeDistance = 1.8f;
    [SerializeField, Min(0.2f)] private float strikeCooldown = 1.55f;

    private Level1WorldFlow flow;
    private PlayerController player;
    private float nextStrike;
    private float stunnedUntil;
    private float baseHover = 0.8f;
    private float phase;

    private void Start()
    {
        flow = Level1WorldFlow.Instance;
        player = flow != null ? flow.Player : null;
        if (visualRoot == null) visualRoot = transform.Find("Forma");
        phase = Random.value * 6.28f;
    }

    private void Update()
    {
        if (flow == null || flow.IsComplete || player == null || player.dead) return;

        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        if (visualRoot != null)
        {
            float targetScale = Time.time < stunnedUntil ? 1.14f : 1f;
            visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, Vector3.one * targetScale, Time.deltaTime * 8f);
            Vector3 local = visualRoot.localPosition;
            local.y = baseHover + Mathf.Sin(Time.time * 2.2f + phase) * 0.075f;
            visualRoot.localPosition = local;
        }

        if (distance > noticeDistance || Time.time < stunnedUntil) return;

        if (offset.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(offset.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 180f * Time.deltaTime);
        }

        if (distance <= strikeDistance)
        {
            if (Time.time >= nextStrike)
            {
                nextStrike = Time.time + strikeCooldown;
                flow.DamagePlayer(1);
                if (visualRoot != null) visualRoot.localScale = Vector3.one * 1.12f;
            }
            return;
        }

        Vector3 step = offset.normalized * (moveSpeed * Time.deltaTime);
        Vector3 candidate = transform.position + step;
        if (TryGetWalkableGround(candidate, out float groundHeight))
        {
            candidate.y = groundHeight;
            transform.position = candidate;
        }
    }

    public void Stun(float seconds)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        nextStrike = Mathf.Max(nextStrike, stunnedUntil);
        flow?.Notify("¡Reliquia acertada! El centinela está desorientado", new Color(0.56f, 0.90f, 1f, 1f));
    }

    private bool TryGetWalkableGround(Vector3 position, out float height)
    {
        height = transform.position.y;
        Ray ray = new Ray(position + Vector3.up * 5f, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closest = float.PositiveInfinity;
        bool found = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.distance >= closest || hit.collider.GetComponentInParent<Level1Walkable>() == null) continue;
            closest = hit.distance;
            height = hit.point.y;
            found = true;
        }
        return found && Mathf.Abs(height - transform.position.y) <= 2.5f;
    }
}
