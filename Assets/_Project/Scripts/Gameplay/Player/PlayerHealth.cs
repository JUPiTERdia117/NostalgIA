// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: PlayerHealth.cs
// Propósito: corazones del jugador, invulnerabilidad temporal, muerte
// instantánea y caída letal (GDD 2.5, TDD §5.2, tarea F1-A11).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Damage;
using NostalgIA.Core.Events;
using UnityEngine;

namespace NostalgIA.Gameplay.Player
{
    /// <summary>
    /// Vida del jugador expresada en corazones.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reglas (GDD 2.5 y TDD §5.2):
    /// </para>
    /// <list type="bullet">
    /// <item>El daño <see cref="DamageType.Normal"/> resta corazones y abre una ventana de
    /// invulnerabilidad; durante ella se ignoran golpes normales.</item>
    /// <item>El daño <see cref="DamageType.Instakill"/> ignora corazones e invulnerabilidad.</item>
    /// <item>Un aterrizaje con rapidez vertical ≥ <c>lethalFallSpeed</c> se trata como caída al
    /// vacío (GDD: «caer desde alturas extremas»). En caída libre desde el reposo,
    /// <c>v = √(2·g·h)</c>; con 24 m/s y g = 9,81 m/s² la altura equivalente es ≈ 29 m.</item>
    /// </list>
    /// <para>
    /// La invulnerabilidad se mide con <see cref="Time.time"/> (tiempo escalado): durante la pausa
    /// no transcurre, igual que el resto del juego.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        /// <summary>
        /// Umbral heredado de la API entera: un golpe de esta cantidad o mayor se trata como
        /// muerte instantánea (el prototipo usaba 99 para lava y caídas).
        /// </summary>
        public const int LegacyInstakillThreshold = 90;

        [Tooltip("Parámetros de vida. Si falta, se toma el asset del PlayerMotor del mismo objeto.")]
        [SerializeField] private PlayerSettings settings;

        private int currentHearts;
        private float lastDamageTime = float.NegativeInfinity;

        /// <summary>Corazones actuales.</summary>
        public int CurrentHearts => currentHearts;

        /// <summary>Corazones máximos.</summary>
        public int MaxHearts => settings.maxHearts;

        /// <inheritdoc />
        public bool IsDead { get; private set; }

        /// <summary>Indica si está dentro de la ventana de invulnerabilidad.</summary>
        public bool IsInvulnerable => Time.time - lastDamageTime < settings.invulnerabilityDuration;

        /// <summary>Resuelve los parámetros y llena los corazones.</summary>
        private void Awake()
        {
            if (settings == null && TryGetComponent(out PlayerMotor motor)) settings = motor.Settings;
            if (settings == null) settings = ScriptableObject.CreateInstance<PlayerSettings>();
            currentHearts = settings.maxHearts;
        }

        /// <summary>Escucha los aterrizajes para la caída letal.</summary>
        private void OnEnable() => EventBus<PlayerLanded>.Subscribe(OnLanded);

        /// <summary>Cancela la suscripción.</summary>
        private void OnDisable() => EventBus<PlayerLanded>.Unsubscribe(OnLanded);

        /// <inheritdoc />
        public void TakeDamage(in DamageInfo info)
        {
            if (IsDead) return;

            if (info.Type == DamageType.Instakill)
            {
                Kill(info.Cause == DeathCause.Hearts ? DeathCause.Instakill : info.Cause);
                return;
            }

            if (info.Amount <= 0 || IsInvulnerable) return;

            lastDamageTime = Time.time;
            int applied = Mathf.Min(info.Amount, currentHearts);
            currentHearts -= applied;
            EventBus<PlayerDamaged>.Raise(new PlayerDamaged(applied, currentHearts, settings.maxHearts, info.SourcePosition));

            if (currentHearts <= 0) Kill(DeathCause.Hearts);
        }

        /// <summary>
        /// API de compatibilidad con los enemigos heredados que aplican daño entero.
        /// Cantidades ≥ <see cref="LegacyInstakillThreshold"/> equivalen a muerte instantánea.
        /// </summary>
        /// <param name="damage">Corazones a restar.</param>
        public void TakeDamage(int damage)
        {
            DamageInfo info = damage >= LegacyInstakillThreshold
                ? DamageInfo.Instakill(transform.position)
                : DamageInfo.Normal(damage, transform.position);
            TakeDamage(in info);
        }

        /// <summary>Mata al jugador de inmediato.</summary>
        /// <param name="cause">Causa informada en <see cref="PlayerDied"/>.</param>
        public void Kill(DeathCause cause)
        {
            if (IsDead) return;
            IsDead = true;
            currentHearts = 0;
            EventBus<PlayerDied>.Raise(new PlayerDied(cause, transform.position));
        }

        /// <summary>Restablece los corazones al reaparecer. Lo llama <c>RespawnService</c>.</summary>
        public void Revive()
        {
            IsDead = false;
            currentHearts = settings.maxHearts;
            // Breve invulnerabilidad al reaparecer para no recibir daño en el mismo frame.
            lastDamageTime = Time.time;
        }

        /// <summary>Caída letal: compara la rapidez de impacto con el umbral configurado.</summary>
        private void OnLanded(in PlayerLanded evt)
        {
            if (settings.lethalFallSpeed > 0f && evt.ImpactSpeed >= settings.lethalFallSpeed)
                Kill(DeathCause.Void);
        }

        /// <summary>Herramienta de prueba en el inspector.</summary>
        [ContextMenu("Recibir 1 de daño")]
        private void DebugDamage() => TakeDamage(1);

        /// <summary>Herramienta de prueba en el inspector.</summary>
        [ContextMenu("Morir")]
        private void DebugKill() => Kill(DeathCause.Instakill);
    }
}
