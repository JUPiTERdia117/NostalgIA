// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: GameplayAssembly.cs
// -----------------------------------------------------------------------------

namespace NostalgIA.Gameplay
{
    /// <summary>
    /// Marcador del ensamblado <c>NostalgIA.Gameplay</c>, que alojará el motor del jugador, las
    /// herramientas, la vida, los enemigos y la lógica de las Estaciones.
    /// </summary>
    /// <remarks>
    /// Este ensamblado solo puede referenciar a <c>NostalgIA.Core</c> y <c>NostalgIA.Input</c>.
    /// Se excluye a propósito <c>NostalgIA.Presentation</c>: la jugabilidad comunica sus cambios
    /// publicando eventos y nunca invoca directamente partículas, audio, materiales ni interfaz
    /// El marcador garantiza que el ensamblado exista aunque todavía no tenga otros scripts.
    /// </remarks>
    public static class GameplayAssembly
    {
        /// <summary>Nombre del ensamblado, idéntico al declarado en su archivo <c>.asmdef</c>.</summary>
        public const string Name = "NostalgIA.Gameplay";
    }
}
