// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: ToolContracts.cs
// Propósito: contrato común de las herramientas y entrada ya filtrada por el
// controlador (TDD §6.1 y §7.3, tarea F1-A08).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Aim;

namespace NostalgIA.Gameplay.Tools
{
    /// <summary>
    /// Entrada de herramienta de un frame, ya resuelta por <see cref="ToolController"/> según la
    /// herramienta activa y el enfriamiento (TDD §7.3).
    /// </summary>
    /// <remarks>
    /// Las acciones <c>Absorb</c>, <c>Fire</c> y <c>Grapple</c> comparten controles (clic derecho
    /// absorbe o engancha; RT dispara o engancha). El controlador traslada a cada herramienta
    /// solo las que le corresponden, por eso la herramienta nunca consulta el dispositivo.
    /// </remarks>
    public readonly struct ToolInput
    {
        /// <summary>Absorber está mantenido (solo aspiradora).</summary>
        public readonly bool AbsorbHeld;
        /// <summary>Se pulsó disparar en este frame (solo aspiradora).</summary>
        public readonly bool FirePressed;
        /// <summary>Se pulsó el gancho en este frame (solo gancho).</summary>
        public readonly bool GrapplePressed;

        /// <summary>Crea la entrada filtrada.</summary>
        public ToolInput(bool absorbHeld, bool firePressed, bool grapplePressed)
        {
            AbsorbHeld = absorbHeld;
            FirePressed = firePressed;
            GrapplePressed = grapplePressed;
        }

        /// <summary>Entrada vacía (enfriamiento, herramienta inactiva).</summary>
        public static ToolInput None => default;
    }

    /// <summary>Contrato común de las herramientas intercambiables del dron (TDD §6.1).</summary>
    public interface ITool
    {
        /// <summary>Identificador de la herramienta.</summary>
        ToolId Id { get; }

        /// <summary>Alcance actual de la herramienta, en metros (incluye mejoras).</summary>
        float Range { get; }

        /// <summary>Se llama al seleccionar la herramienta.</summary>
        void Equip();

        /// <summary>Se llama al cambiar a otra herramienta; debe cancelar acciones en curso.</summary>
        void Unequip();

        /// <summary>Procesa la entrada ya filtrada para este frame.</summary>
        /// <param name="input">Entrada de herramienta.</param>
        /// <param name="aim">Muestra de puntería del frame.</param>
        /// <param name="deltaTime">Δt del frame, en segundos.</param>
        void Tick(in ToolInput input, in AimSample aim, float deltaTime);

        /// <summary>Restablece la herramienta (reaparición): cancela acciones en curso.</summary>
        void ResetTool();
    }
}
