// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: ContactHazard.cs
// Propósito: obstáculos que empujan (y opcionalmente dañan) o aplastan al jugador
// al tocarlo.
// -----------------------------------------------------------------------------
using NostalgIA.Core;
using NostalgIA.Core.Damage;
using NostalgIA.Gameplay.Player;
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Volumen de peligro de nivel. En modo <see cref="HazardEffect.Knockback"/> lanza al jugador
    /// en la dirección en que se mueve el obstáculo en el punto de contacto; en modo
    /// <see cref="HazardEffect.Instakill"/> provoca muerte instantánea (por ejemplo al ser aplastado).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ContactHazard : MonoBehaviour
    {
        /// <summary>Efecto del peligro sobre el jugador.</summary>
        public enum HazardEffect
        {
            /// <summary>Empuja al jugador (y opcionalmente resta corazones).</summary>
            Knockback,
            /// <summary>Muerte instantánea.</summary>
            Instakill
        }

        [Tooltip("Efecto al tocar al jugador.")]
        [SerializeField] private HazardEffect effect = HazardEffect.Knockback;

        [Tooltip("Velocidad horizontal del empujón (m/s).")]
        [SerializeField] private float knockbackSpeed = 12f;

        [Tooltip("Velocidad vertical del empujón (m/s); despega al jugador del suelo.")]
        [SerializeField] private float knockbackLift = 4.5f;

        [Tooltip("Corazones que resta el empujón (0 = solo empuja).")]
        [SerializeField, Min(0)] private int damage;

        [Tooltip("Aplastador: solo mata mientras avanza/aplasta (requiere HazardMotion tipo Crusher).")]
        [SerializeField] private bool onlyWhileCrushing;

        [Tooltip("Tiempo mínimo entre dos golpes al jugador (s).")]
        [SerializeField, Min(0.05f)] private float hitCooldown = 0.6f;

        private HazardMotion motion;
        private float lastHitTime = -99f;

        /// <summary>Configura el peligro desde herramientas de editor.</summary>
        public void Configure(HazardEffect newEffect, float speed, float lift, int hearts, bool crushingOnly)
        {
            effect = newEffect;
            knockbackSpeed = speed;
            knockbackLift = lift;
            damage = hearts;
            onlyWhileCrushing = crushingOnly;
        }

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
            gameObject.layer = Layers.Hazard;
        }

        private void Awake() => motion = GetComponentInParent<HazardMotion>();

        private void OnTriggerEnter(Collider other) => TryHit(other);

        private void OnTriggerStay(Collider other) => TryHit(other);

        private void TryHit(Collider other)
        {
            if (Time.time - lastHitTime < hitCooldown) return;
            if (!other.TryGetComponent(out PlayerHealth health) || health.IsDead) return;
            if (onlyWhileCrushing && motion != null && !motion.IsCrushing) return;

            lastHitTime = Time.time;
            Vector3 playerPos = other.transform.position;

            if (effect == HazardEffect.Instakill)
            {
                health.TakeDamage(DamageInfo.Instakill(playerPos));
                return;
            }

            Vector3 dir = Vector3.zero;
            if (motion != null) dir = Vector3.ProjectOnPlane(motion.PointVelocity(playerPos), Vector3.up);
            if (dir.sqrMagnitude < 0.25f) dir = Vector3.ProjectOnPlane(playerPos - transform.position, Vector3.up);
            if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
            dir.Normalize();

            if (other.TryGetComponent(out PlayerMotor motor))
                motor.ApplyKnockback(dir * knockbackSpeed + Vector3.up * knockbackLift);
            if (damage > 0) health.TakeDamage(DamageInfo.Normal(damage, transform.position));
        }
    }
}
