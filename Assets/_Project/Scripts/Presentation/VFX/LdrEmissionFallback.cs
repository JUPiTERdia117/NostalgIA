// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: LdrEmissionFallback.cs
// Propósito: mantener el brillo de los objetos emisivos cuando el asset de URP
// activo no usa HDR (calidad móvil).
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NostalgIA.Presentation.VFX
{
    /// <summary>
    /// Cambia el material brillante de un objeto por una versión pensada para pantallas sin HDR
    /// cuando la calidad activa no usa HDR (calidad Móvil).
    /// </summary>
    /// <remarks>
    /// <para>
    /// El problema que resolví: los cubos absorbibles usan una emisión HDR muy fuerte (el canal azul
    /// vale ≈ 29,6) para que el Bloom de la Estación les dibuje un halo grande. En PC, con HDR, ese
    /// 29,6 llega entero al Bloom. En móvil no usamos HDR (decisión del TDD §8.4 para ahorrar memoria)
    /// y la imagen se guarda con 8 bits por canal, así que cada canal se recorta a 1:
    /// (0,70; 1,03; 29,6) → (0,70; 1; 1). Eso es un blanco azulado que apenas pasa el umbral del Bloom
    /// y el cubo se veía "apagado". Como el editor usa la calidad de la plataforma activa al dar Play
    /// (iOS → Móvil), lo notamos justo al entrar a modo juego.
    /// </para>
    /// <para>
    /// La versión LDR (Poolable_LDR.mat) tiene una emisión saturada con su canal más alto en 1:
    /// (0,08; 0,78; 1). Después del recorte conserva el tono cian y ese canal 1 sigue pasando el
    /// umbral, así que el halo aparece. Cambio el <see cref="Renderer.sharedMaterial"/> por otro asset
    /// que ya existe (no creo materiales en ejecución) y así el objeto sigue en el SRP Batcher.
    /// La revisión se hace una sola vez, en <c>Awake</c>.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LdrEmissionFallback : MonoBehaviour
    {
        [Tooltip("Renderer cuyo material se sustituye. Si se deja vacío se usa el del propio objeto.")]
        [SerializeField] private Renderer targetRenderer;
        [Tooltip("Material emisivo para pipelines sin HDR (emisión con su canal máximo en 1).")]
        [SerializeField] private Material ldrMaterial;

        /// <summary>
        /// Devuelve <c>true</c> si el asset de URP que está usando la calidad actual no tiene HDR.
        /// La dejé pública y estática porque <see cref="LdrVolumeProfileSwap"/> hace la misma pregunta.
        /// </summary>
        public static bool PipelineIsLdr()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            return asset != null && !asset.supportsHDR;
        }

        /// <summary>
        /// Si no asignaron un renderer en el Inspector, tomo el del propio objeto. Si falta el material
        /// LDR no hago nada (el objeto se queda con su material HDR, que en PC es el correcto).
        /// </summary>
        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (targetRenderer == null || ldrMaterial == null) return;
            if (PipelineIsLdr()) targetRenderer.sharedMaterial = ldrMaterial;
        }
    }
}
