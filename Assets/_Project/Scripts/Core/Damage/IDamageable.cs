// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: IDamageable.cs
// Propósito: contratos de recepción de daño e impactos (TDD §5.2 y §6.3).
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Core.Damage
{
    /// <summary>Entidad que puede recibir daño (jugador, enemigos).</summary>
    public interface IDamageable
    {
        /// <summary>Indica si la entidad ya está muerta y debe ignorar nuevos golpes.</summary>
        bool IsDead { get; }

        /// <summary>Aplica un golpe.</summary>
        /// <param name="info">Descripción del golpe, por referencia de solo lectura.</param>
        void TakeDamage(in DamageInfo info);
    }

    /// <summary>
    /// Objeto que daña al jugador por contacto (enemigos, lava electrónica, Antivirus).
    /// </summary>
    /// <remarks>
    /// <c>PlayerContactDamage</c> consulta este contrato al tocar un collider. Devolver
    /// <c>false</c> permite que un peligro se desactive (por ejemplo, un enemigo muerto) sin
    /// retirar su collider.
    /// </remarks>
    public interface IContactHazard
    {
        /// <summary>Obtiene el golpe que produce el contacto en este momento.</summary>
        /// <param name="contactPoint">Punto de contacto con el jugador.</param>
        /// <param name="info">Golpe a aplicar.</param>
        /// <returns><c>true</c> si el contacto debe dañar ahora.</returns>
        bool TryGetContactDamage(Vector3 contactPoint, out DamageInfo info);
    }

    /// <summary>
    /// Receptor de impactos físicos de objetos lanzados (botones, mecanismos; Fase 2).
    /// </summary>
    public interface IImpactReceiver
    {
        /// <summary>Recibe un impacto.</summary>
        /// <param name="point">Punto de contacto.</param>
        /// <param name="force">Fuerza estimada del impacto en newtons (F = m·a).</param>
        void ReceiveImpact(Vector3 point, float force);
    }
}
