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
    /// El perfil de PC no se toca. Cambio <see cref="Volume.sharedProfile"/> entre los dos assets, así
    /// que no creo nada en ejecución.
    /// </para>
    /// <para>
    /// Antes lo decidía una sola vez en <c>Awake</c>; ahora también escucho cuando cambia el asset de
    /// URP activo (cambio de calidad en el menú de opciones, o el editor regresando a la calidad de PC
    /// al dar Play) y vuelvo a elegir el perfil. Por eso guardo el perfil original (HDR) al despertar.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class LdrVolumeProfileSwap : MonoBehaviour
    {
        [Tooltip("Perfil ajustado para pipelines sin HDR (umbral de Bloom más bajo).")]
        [SerializeField] private VolumeProfile ldrProfile;

        private Volume volume;
        private VolumeProfile hdrProfile;

        /// <summary>Guardo el perfil original (el de HDR) y elijo el que toca con la calidad actual.</summary>
        private void Awake()
        {
            volume = GetComponent<Volume>();
            hdrProfile = volume.sharedProfile;
            Refresh();
        }

        private void OnEnable() => RenderPipelineManager.activeRenderPipelineAssetChanged += OnPipelineChanged;

        private void OnDisable() => RenderPipelineManager.activeRenderPipelineAssetChanged -= OnPipelineChanged;

        private void OnPipelineChanged(RenderPipelineAsset previous, RenderPipelineAsset next) => Refresh();

        /// <summary>Pone el perfil LDR si la calidad actual no tiene HDR, o regresa al original si sí tiene.</summary>
        public void Refresh()
        {
            if (volume == null || ldrProfile == null) return;
            volume.sharedProfile = LdrEmissionFallback.PipelineIsLdr() ? ldrProfile : hdrProfile;
        }
    }
}
