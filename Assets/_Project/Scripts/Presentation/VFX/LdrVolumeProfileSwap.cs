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
    /// Cambia el perfil de postprocesado del <see cref="Volume"/> de la Estación por su versión LDR
    /// cuando la calidad activa no usa HDR.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El Bloom primero decide qué partes de la imagen brillan: toma b = max(R, G, B) de cada píxel
    /// y se queda con lo que pasa del umbral t, b − t (con una "rodilla" suave para que no haya un
    /// corte duro). Con HDR, la emisión de los absorbibles y de las líneas de neón vale varias veces 1,
    /// así que con t = 0,9 sobra brillo para un halo grande. Sin HDR la imagen está recortada a 1,
    /// entonces lo que sobra es como mucho 1 − t, casi nada, y el halo desaparece.
    /// </para>
    /// <para>
    /// VP_Primavera_LDR es una copia del perfil de Primavera con el Bloom ajustado para ese caso:
    /// umbral 0,55 (en vez de 0,9), intensidad 1,1 (en vez de 0,6) y dispersión 0,7 (en vez de 0,65).
    /// El perfil de PC no se toca. Hago el cambio una sola vez en <c>Awake</c> sobre
    /// <see cref="Volume.sharedProfile"/>; los dos perfiles son assets, así que no creo nada en ejecución.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class LdrVolumeProfileSwap : MonoBehaviour
    {
        [Tooltip("Perfil ajustado para pipelines sin HDR (umbral de Bloom más bajo).")]
        [SerializeField] private VolumeProfile ldrProfile;

        /// <summary>Si la calidad actual no tiene HDR y hay un perfil LDR asignado, lo pongo en el Volume.</summary>
        private void Awake()
        {
            if (ldrProfile == null || !LdrEmissionFallback.PipelineIsLdr()) return;
            GetComponent<Volume>().sharedProfile = ldrProfile;
        }
    }
}
