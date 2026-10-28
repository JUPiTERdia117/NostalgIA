// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: LdrVolumeProfileSwap.cs
// Propósito: usar un perfil de postprocesado ajustado para LDR cuando el asset
// de URP activo no usa HDR (calidad móvil).
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Rendering;

namespace NostalgIA.Presentation.VFX
{
    /// <summary>
    /// Sustituye el perfil del <see cref="Volume"/> de la Estación por su variante LDR cuando el
    /// pipeline activo no admite HDR.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El Bloom extrae los píxeles cuyo brillo supera el umbral t con una rodilla suave k:
    /// b' = max(b − t, rodilla(b)), donde b = max(R, G, B). Con HDR, la emisión de los absorbibles
    /// y de las líneas de neón vale muchas veces 1 y el halo es amplio con t = 0,9. Sin HDR el búfer
    /// se recorta a 1, así que b − t ≤ 0,1 y el halo prácticamente desaparece. La variante LDR baja
    /// el umbral y sube la intensidad para que el exceso extraído (≈ 1 − 0,55) se parezca al que
    /// se obtiene con HDR, sin tocar el perfil de PC.
    /// </para>
    /// <para>
    /// El cambio se hace una sola vez en <c>Awake</c> sobre <see cref="Volume.sharedProfile"/>
    /// (los dos perfiles son assets), por lo que no se crea ningún perfil en ejecución.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class LdrVolumeProfileSwap : MonoBehaviour
    {
        [Tooltip("Perfil ajustado para pipelines sin HDR (umbral de Bloom más bajo).")]
        [SerializeField] private VolumeProfile ldrProfile;

        /// <summary>Aplica el perfil LDR si corresponde.</summary>
        private void Awake()
        {
            if (ldrProfile == null || !LdrEmissionFallback.PipelineIsLdr()) return;
            GetComponent<Volume>().sharedProfile = ldrProfile;
        }
    }
}
