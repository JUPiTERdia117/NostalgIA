using UnityEngine;

/// <summary>Reliquia física que puede aspirarse, guardarse y lanzarse contra un centinela.</summary>
public sealed class Level1ThrownRelic : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        Level1Warden warden = collision.collider.GetComponentInParent<Level1Warden>();
        if (warden == null) return;
        warden.Stun(2.8f);
        Destroy(gameObject);
    }
}
