// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: KillVolume.cs
// Propósito: volumen de vacío que mata al jugador al entrar (TDD §5.2,
// tarea F1-A11).
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Core.Damage;
using NostalgIA.Core.Events;
using UnityEngine;

namespace NostalgIA.Gameplay.Player
{
    /// <summary>
    /// Trigger que provoca una muerte por vacío al jugador que lo cruza.
    /// </summary>
    /// <remarks>
    /// Sustituye al umbral global de altura del prototipo (<c>y &lt; −15</c> comprobado cada frame
    /// en el controlador del juego): cada estación coloca sus volúmenes donde corresponde, sin
    /// costo por frame, porque la física solo notifica al entrar. El objeto debe estar en la
    /// capa <c>Trigger</c>, que únicamente interactúa con <c>Player</c>.
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public sealed class KillVolume : MonoBehaviour
    {
        /// <summary>Asegura que el collider sea un trigger.</summary>
        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
            gameObject.layer = Layers.Trigger;
        }

        /// <summary>Mata al jugador que entra.</summary>
        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out PlayerHealth health))
                health.TakeDamage(DamageInfo.Instakill(other.transform.position, DeathCause.Void));
        }
    }
}
