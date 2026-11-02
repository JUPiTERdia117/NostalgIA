// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: DamageInfo.cs
// Propósito: descripción inmutable de un golpe.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using UnityEngine;

namespace NostalgIA.Core.Damage
{
    /// <summary>
    /// Datos de un golpe: cantidad, tipo, origen y causa.
    /// </summary>
    /// <remarks>
    /// Es un <c>readonly struct</c> y se entrega por referencia de solo lectura
    /// (<c>TakeDamage(in DamageInfo)</c>), de modo que aplicar daño no asigna memoria. Los
    /// constructores estáticos <see cref="Normal"/> e <see cref="Instakill"/> documentan la
    /// intención en el punto de llamada.
    /// </remarks>
    public readonly struct DamageInfo
    {
        /// <summary>Cantidad de corazones (jugador) o puntos de vida (enemigos) a restar.</summary>
        public readonly int Amount;

        /// <summary>Tipo de daño; <see cref="DamageType.Instakill"/> ignora la invulnerabilidad.</summary>
        public readonly DamageType Type;

        /// <summary>Posición del origen del golpe, para indicadores direccionales.</summary>
        public readonly Vector3 SourcePosition;

        /// <summary>Causa de muerte que se informará si el golpe resulta letal.</summary>
        public readonly DeathCause Cause;

        /// <summary>Crea la descripción de un golpe.</summary>
        /// <param name="amount">Cantidad de daño (se fuerza a un mínimo de cero).</param>
        /// <param name="type">Tipo de daño.</param>
        /// <param name="sourcePosition">Origen del golpe.</param>
        /// <param name="cause">Causa de muerte asociada.</param>
        public DamageInfo(int amount, DamageType type, Vector3 sourcePosition, DeathCause cause)
        {
            Amount = amount < 0 ? 0 : amount;
            Type = type;
            SourcePosition = sourcePosition;
            Cause = cause;
        }

        /// <summary>Golpe normal: resta <paramref name="amount"/> y respeta la invulnerabilidad.</summary>
        public static DamageInfo Normal(int amount, Vector3 sourcePosition) =>
            new DamageInfo(amount, DamageType.Normal, sourcePosition, DeathCause.Hearts);

        /// <summary>Muerte instantánea (lava electrónica, Antivirus, vacío).</summary>
        public static DamageInfo Instakill(Vector3 sourcePosition, DeathCause cause = DeathCause.Instakill) =>
            new DamageInfo(int.MaxValue, DamageType.Instakill, sourcePosition, cause);
    }
}
