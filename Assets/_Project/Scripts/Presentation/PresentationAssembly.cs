// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: PresentationAssembly.cs
// -----------------------------------------------------------------------------

namespace NostalgIA.Presentation
{
    /// <summary>
    /// Marcador del ensamblado <c>NostalgIA.Presentation</c>, que alojará los emisores de VFX,
    /// los controladores de shaders, el postprocesado, el audio, el HUD y los puentes de
    /// Animator (TDD §8, §11 y §12).
    /// </summary>
    /// <remarks>
    /// La presentación reacciona a los eventos publicados por la jugabilidad y no la referencia
    /// directamente, para que ambas pistas de trabajo del equipo puedan avanzar en paralelo sin
    /// dependencias cruzadas. El marcador garantiza que el ensamblado exista desde la Fase 0.
    /// </remarks>
    public static class PresentationAssembly
    {
        /// <summary>Nombre del ensamblado, idéntico al declarado en su archivo <c>.asmdef</c>.</summary>
        public const string Name = "NostalgIA.Presentation";
    }
}
