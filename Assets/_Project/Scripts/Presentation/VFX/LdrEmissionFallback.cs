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
    /// Sustituye el material emisivo de un objeto por una variante pensada para el búfer de
    /// color de 8 bits cuando el pipeline activo no admite HDR.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los cubos absorbibles usan una emisión HDR muy intensa (componente azul ≈ 29,6) para que
    /// el Bloom del perfil de la Estación dibuje un halo amplio. Con HDR (calidad PC) el valor
    /// llega íntegro al Bloom; sin HDR (calidad móvil, TDD §8.4) cada canal se recorta a 1:
    /// (0,70; 1,03; 29,6) → (0,70; 1; 1). El resultado es un blanco grisáceo que apenas supera el
    /// umbral del Bloom (0,9), por lo que al entrar en modo de juego (el editor aplica la calidad
    /// predeterminada de la plataforma activa, iOS → móvil) el cubo parecía "apagarse".
    /// </para>
    /// <para>
    /// La variante LDR usa una emisión saturada con su canal máximo en 1: tras el recorte conserva
    /// el tono cian y ese canal sigue superando el umbral, así que el halo se mantiene. Se cambia
    /// el <see cref="Renderer.sharedMaterial"/> por otro asset (no se crea ningún material en
    /// ejecución), lo que conserva la compatibilidad con el SRP Batcher. La comprobación ocurre una
    /// sola vez en <c>Awake</c>.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LdrEmissionFallback : MonoBehaviour
    {
        [Tooltip("Renderer cuyo material se sustituye. Si se deja vacío se usa el del propio objeto.")]
        [SerializeField] private Renderer targetRenderer;
        [Tooltip("Material emisivo para pipelines sin HDR (emisión con su canal máximo en 1).")]
        [SerializeField] private Material ldrMaterial;

        /// <summary>Indica si el pipeline activo trabaja sin HDR.</summary>
        public static bool PipelineIsLdr()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            return asset != null && !asset.supportsHDR;
        }

        /// <summary>Aplica la variante LDR si corresponde.</summary>
        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (targetRenderer == null || ldrMaterial == null) return;
            if (PipelineIsLdr()) targetRenderer.sharedMaterial = ldrMaterial;
        }
    }
}
