// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: StateContracts.cs
// Propósito: interfaces de solo lectura del estado continuo del jugador y de sus
// herramientas, consultadas por la capa de presentación.
// -----------------------------------------------------------------------------
//
// La jugabilidad registra sus componentes con estas interfaces en el
// ServiceLocator; la presentación (cámara, retícula, shaders, audio) las lee cada
// frame. Así evitamos publicar eventos por frame y la presentación no necesita
// referenciar el ensamblado de jugabilidad.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using UnityEngine;

namespace NostalgIA.Core.State
{
    /// <summary>Estado continuo de la locomoción del jugador.</summary>
    public interface IPlayerMotionState
    {
        /// <summary>Estado actual de la máquina de locomoción.</summary>
        LocomotionState State { get; }

        /// <summary>Posición de los pies en el mundo.</summary>
        Vector3 Position { get; }

        /// <summary>Velocidad total en m/s (horizontal y vertical).</summary>
        Vector3 Velocity { get; }

        /// <summary>Rapidez horizontal en m/s.</summary>
        float HorizontalSpeed { get; }

        /// <summary>Indica si el jugador está apoyado en el suelo.</summary>
        bool IsGrounded { get; }

        /// <summary>Indica si el jugador corre en este frame.</summary>
        bool IsSprinting { get; }

        /// <summary>Entrada de movimiento de este frame (x lateral, y avance), magnitud ≤ 1.</summary>
        Vector2 MoveInput { get; }

        /// <summary>Progreso restante del deslizamiento: 1 al empezar y 0 al terminar.</summary>
        float SlideProgress01 { get; }

        /// <summary>Velocidad de caminata configurada, en m/s.</summary>
        float WalkSpeed { get; }

        /// <summary>Velocidad de carrera configurada, en m/s.</summary>
        float SprintSpeed { get; }
    }

    /// <summary>Estado continuo del controlador de herramientas.</summary>
    public interface IToolState
    {
        /// <summary>Herramienta activa.</summary>
        ToolId ActiveTool { get; }

        /// <summary>Segundos restantes del enfriamiento compartido.</summary>
        float CooldownRemaining { get; }

        /// <summary>Duración del último enfriamiento iniciado, en segundos.</summary>
        float CooldownDuration { get; }

        /// <summary>Indica si las herramientas están bloqueadas por enfriamiento.</summary>
        bool IsOnCooldown { get; }
    }

    /// <summary>Estado continuo de la aspiradora.</summary>
    public interface IVacuumState
    {
        /// <summary>Objetos almacenados.</summary>
        int StoredCount { get; }

        /// <summary>Capacidad actual.</summary>
        int Capacity { get; }

        /// <summary>Llenado normalizado entre 0 y 1.</summary>
        float Fill01 { get; }

        /// <summary>Indica si la succión está activa.</summary>
        bool IsSuctioning { get; }
    }

    /// <summary>Estado continuo del gancho.</summary>
    public interface IGrappleState
    {
        /// <summary>Indica si el gancho está anclado y remolcando.</summary>
        bool IsAttached { get; }

        /// <summary>Punto de anclaje en el mundo (válido solo si <see cref="IsAttached"/>).</summary>
        Vector3 AnchorPoint { get; }

        /// <summary>Alcance máximo actual, en metros (incluye mejoras).</summary>
        float MaxRange { get; }
    }
}
