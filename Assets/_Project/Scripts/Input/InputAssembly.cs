// -----------------------------------------------------------------------------
// NostalgIA — Entrada (NostalgIA.Input)
// Archivo: InputAssembly.cs
// -----------------------------------------------------------------------------

namespace NostalgIA.Input
{
    /// <summary>
    /// Marcador del ensamblado <c>NostalgIA.Input</c>, que alojará el <c>InputReader</c>, los
    /// controles táctiles y la resolución del esquema activo.
    /// </summary>
    /// <remarks>
    /// Unity no compila un ensamblado definido por <c>.asmdef</c> si no contiene scripts. Por eso
    /// incluyo este marcador: así el ensamblado existe y las referencias entre ensamblados quedan
    /// establecidas.
    /// </remarks>
    public static class InputAssembly
    {
        /// <summary>Nombre del ensamblado, idéntico al declarado en su archivo <c>.asmdef</c>.</summary>
        public const string Name = "NostalgIA.Input";
    }
}
