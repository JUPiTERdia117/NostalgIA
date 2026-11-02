// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: IGrappleTether.cs
// Propósito: contrato entre el motor del jugador y el gancho durante el estado
// Grappling.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Gameplay.Player
{
    /// <summary>
    /// Cable del gancho visto desde el motor: aporta la aceleración de tracción y decide cuándo
    /// termina el remolque.
    /// </summary>
    /// <remarks>
    /// Separar este contrato permite que <see cref="PlayerMotor"/> integre el movimiento (un único
    /// responsable de llamar a <c>CharacterController.Move</c>) mientras <c>GrappleTool</c> conserva
    /// la física de tracción validada en el prototipo.
    /// </remarks>
    public interface IGrappleTether
    {
        /// <summary>Indica si el cable sigue anclado.</summary>
        bool IsAttached { get; }

        /// <summary>Aceleración del control aéreo durante el remolque, en m/s².</summary>
        float AirControl { get; }

        /// <summary>Fracción de velocidad conservada al soltar (0–1).</summary>
        float ReleaseMomentumRetention { get; }

        /// <summary>Punto de anclaje actual en el mundo.</summary>
        Vector3 AnchorPoint { get; }

        /// <summary>Aceleración del desvío perpendicular al cable mientras jala, en m/s².</summary>
        float SteerAcceleration { get; }

        /// <summary>Velocidad máxima de desvío perpendicular que puede dar la entrada, en m/s.</summary>
        float SteerMaxSpeed { get; }

        /// <summary>Amortiguación de la velocidad perpendicular sin entrada, en 1/s.</summary>
        float LateralDamping { get; }

        /// <summary>Aceleración del control aéreo reforzado después de soltar, en m/s².</summary>
        float ReleaseAirAcceleration { get; }

        /// <summary>Duración del control aéreo reforzado después de soltar, en segundos.</summary>
        float ReleaseAirControlTime { get; }

        /// <summary>Velocidad vertical mínima al saltar mientras el cable jala, en m/s.</summary>
        float JumpLaunchSpeed { get; }

        /// <summary>Impulso extra hacia donde mira la cámara al saltar con el cable, en m/s.</summary>
        float JumpForwardBoost { get; }

        /// <summary>
        /// Devuelve (una sola vez) la velocidad vertical mínima que hay que dar al soltar por llegada
        /// al ancla, o cero si se soltó por otra razón.
        /// </summary>
        float ConsumeReleaseLift();

        /// <summary>
        /// Indica (una sola vez) si el último suelte fue por rebase del ancla: en ese caso el motor
        /// no aplica la retención de impulso y la velocidad queda intacta.
        /// </summary>
        bool ConsumeFreeRelease();

        /// <summary>Aplica la tracción de un subpaso.</summary>
        /// <param name="velocity">Velocidad actual del jugador, en m/s.</param>
        /// <param name="deltaTime">Duración del subpaso, en segundos.</param>
        /// <param name="playerGravity">Gravedad del jugador, en m/s².</param>
        /// <returns>Velocidad tras la tracción.</returns>
        Vector3 Accelerate(Vector3 velocity, float deltaTime, float playerGravity);

        /// <summary>Comprueba la llegada o el rebase del ancla tras mover al jugador.</summary>
        /// <param name="previousCenter">Centro de la cápsula antes del subpaso.</param>
        /// <param name="currentCenter">Centro de la cápsula después del subpaso.</param>
        void CheckArrival(Vector3 previousCenter, Vector3 currentCenter);

        /// <summary>Suelta el cable (idempotente).</summary>
        void Release();
    }
}
