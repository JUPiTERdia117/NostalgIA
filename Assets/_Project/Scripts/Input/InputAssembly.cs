// -----------------------------------------------------------------------------
// NostalgIA — Entrada (NostalgIA.Input)
// Archivo: InputAssembly.cs
// -----------------------------------------------------------------------------

namespace NostalgIA.Input
{
    /// <summary>
    /// Marcador del ensamblado <c>NostalgIA.Input</c>, que alojará el <c>InputReader</c>, los
    /// controles táctiles y la resolución del esquema activo (TDD §7).
    /// </summary>
    /// <remarks>
    /// Unity no compila un ensamblado definido por <c>.asmdef</c> si no contiene scripts. En este
    /// módulo se decidió incluir este marcador desde la Fase 0 para que el ensamblado exista, las
    /// referencias entre ensamblados queden establecidas y el validador del proyecto pueda
    /// comprobar las reglas de dependencia desde el inicio.
    /// </remarks>
    public static class InputAssembly
    {
        /// <summary>Nombre del ensamblado, idéntico al declarado en su archivo <c>.asmdef</c>.</summary>
        public const string Name = "NostalgIA.Input";
    }
}
