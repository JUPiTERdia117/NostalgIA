// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: RotatingHazardObstacle.cs
// Propósito: aspas giratorias estilo circuito de obstáculos que dañan y empujan
// tangencialmente al jugador al contacto, con cero asignaciones por frame.
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Core.Damage;
using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Player;
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Obstáculo rotatorio de 2 o 4 brazos (como las aspas de la Isla Derecha en Verano).
    /// Implementa <see cref="IContactHazard"/> y además barre su volumen con <c>OverlapBoxNonAlloc</c>
    /// para detectar al jugador aunque se quede quieto esperando a que pase el brazo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RotatingHazardObstacle : MonoBehaviour, IContactHazard
    {
        [Header("Giro")]
        [Tooltip("Velocidad angular alrededor del eje vertical local, en grados por segundo (signo = sentido).")]
        [SerializeField] private float angularSpeedDegrees = 68f;

        [Header("Geometría de barrido")]
        [Tooltip("Si tiene 4 aspas en cruz (true) evalúa dos cajas perpendiculares; si es una sola barra (false), solo una.")]
        [SerializeField] private bool fourBladesCross = true;
        [Tooltip("Semilongitud de cada brazo desde el centro hasta la punta, en metros.")]
        [SerializeField, Min(0.5f)] private float bladeRadius = 9f;
        [Tooltip("Semiancho y semialto del brazo para la detección de golpe, en metros.")]
        [SerializeField] private Vector2 bladeCrossHalfSize = new Vector2(0.38f, 0.32f);
        [Tooltip("Altura local del eje de las aspas sobre el pivote del objeto, en metros.")]
        [SerializeField] private float bladeLocalHeight = 0.65f;

        [Header("Daño y empuje")]
        [Tooltip("Corazones que resta al tocar al jugador.")]
        [SerializeField, Min(1)] private int contactDamage = 1;
        [Tooltip("Impulso horizontal tangencial al golpear al jugador, en m/s.")]
        [SerializeField, Min(0f)] private float knockbackHorizontalSpeed = 11.5f;
        [Tooltip("Impulso vertical al golpear para despegar al jugador del suelo y evitar que quede atrapado, en m/s.")]
        [SerializeField, Min(0f)] private float knockbackVerticalSpeed = 5.4f;
        [Tooltip("Tiempo mínimo entre empujes consecutivos de este mismo obstáculo, en segundos.")]
        [SerializeField, Min(0.05f)] private float hitCooldown = 0.45f;

        // Búfer estático de 4 ranuras para consultar únicamente la capa Player sin generar basura.
        private readonly Collider[] playerHits = new Collider[4];
        private float lastHitTime = float.NegativeInfinity;

        /// <summary>Configura las dimensiones y velocidades desde el constructor de escena.</summary>
        public void Configure(float speedDegPerSec, bool crossFourBlades, float radius, Vector2 crossHalf, float localHeight,
                              int damage = 1, float horizKnockback = 11.5f, float vertKnockback = 5.4f)
        {
            angularSpeedDegrees = speedDegPerSec;
            fourBladesCross = crossFourBlades;
            bladeRadius = Mathf.Max(0.5f, radius);
            bladeCrossHalfSize = crossHalf;
            bladeLocalHeight = localHeight;
            contactDamage = Mathf.Max(1, damage);
            knockbackHorizontalSpeed = horizKnockback;
            knockbackVerticalSpeed = vertKnockback;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            transform.Rotate(0f, angularSpeedDegrees * dt, 0f, Space.Self);
            Physics.SyncTransforms();

            if (Time.time - lastHitTime < hitCooldown) return;

            SweepArm(transform.rotation);
            if (fourBladesCross && Time.time - lastHitTime >= hitCooldown)
            {
                SweepArm(transform.rotation * Quaternion.Euler(0f, 90f, 0f));
            }
        }

        /// <summary>
        /// Evalúa una barra completa de extremo a extremo (−R a +R sobre su eje Z local) con una sola caja.
        /// </summary>
        private void SweepArm(Quaternion armRotation)
        {
            Vector3 center = transform.TransformPoint(new Vector3(0f, bladeLocalHeight, 0f));
            Vector3 halfExtents = new Vector3(bladeCrossHalfSize.x, bladeCrossHalfSize.y, bladeRadius);

            int count = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                playerHits,
                armRotation,
                Layers.Masks.Player,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider col = playerHits[i];
                if (col == null) continue;
                if (TryStrikePlayer(col.gameObject)) break;
            }
        }

        /// <summary>
        /// Aplica daño y calcula la dirección tangencial ω × r para lanzar al jugador en el sentido real
        /// en el que venía girando el aspa.
        /// </summary>
        private bool TryStrikePlayer(GameObject target)
        {
            PlayerHealth health = target.GetComponentInParent<PlayerHealth>();
            if (health == null || health.IsDead) return false;

            lastHitTime = Time.time;
            Vector3 pivot = transform.position + Vector3.up * bladeLocalHeight;
            Vector3 radial = target.transform.position - pivot;
            radial.y = 0f;
            if (radial.sqrMagnitude < 0.001f) radial = transform.forward;
            radial.Normalize();

            // En coordenadas de mano izquierda de Unity, un giro positivo alrededor de +Y mueve un punto r
            // en la dirección Cross(Vector3.up, r). Si angularSpeedDegrees < 0 se invierte el signo.
            float spinSign = angularSpeedDegrees >= 0f ? 1f : -1f;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial) * spinSign;
            Vector3 pushDirection = (tangent * 0.8f + radial * 0.55f).normalized;

            DamageInfo info = DamageInfo.Normal(contactDamage, pivot);
            health.TakeDamage(in info);

            if (target.TryGetComponent(out PlayerMotor motor))
            {
                motor.Launch(knockbackVerticalSpeed, pushDirection * knockbackHorizontalSpeed);
            }

            EventBus<HazardTriggered>.Raise(new HazardTriggered(transform, target.transform.position));
            return true;
        }

        /// <summary>
        /// Respaldo para <see cref="PlayerContactDamage"/> si el CharacterController choca él mismo contra el brazo.
        /// </summary>
        public bool TryGetContactDamage(Vector3 contactPoint, out DamageInfo info)
        {
            info = DamageInfo.Normal(contactDamage, contactPoint);
            if (Time.time - lastHitTime < hitCooldown) return false;
            lastHitTime = Time.time;
            EventBus<HazardTriggered>.Raise(new HazardTriggered(transform, contactPoint));
            return true;
        }
    }
}
