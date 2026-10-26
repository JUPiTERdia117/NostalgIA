// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: IGrappleTether.cs
// Propósito: contrato entre el motor del jugador y el gancho durante el estado
// Grappling (TDD §5.1 y §6.4, tareas F1-A06 y F1-A10).
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
