// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: LevelGateVisuals.cs
// Propósito: colorea el degradado vertical (arriba → abajo) de la superficie
// del portal entre niveles.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Presentation.Levels
{
    /// <summary>
    /// Aplica los colores HDR superior e inferior al material de la superficie de la puerta
    /// (<c>NOS/LevelGatePortal</c>) mediante <see cref="MaterialPropertyBlock"/>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class LevelGateVisuals : MonoBehaviour
    {
        private static readonly int ColorAId = Shader.PropertyToID("_ColorA");
        private static readonly int ColorBId = Shader.PropertyToID("_ColorB");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int OpenId = Shader.PropertyToID("_Open");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        // Equivale al brillo que tenía el portal antes (glowIntensity 2.4 × idleGlow 0.55).
        private const float DefaultGlowIntensity = 1.32f;

        [Header("Color de la luz")]
        [Tooltip("Color superior del degradado.")]
        [SerializeField, ColorUsage(false, true)] private Color colorTop = new Color(1f, 0.86f, 0.55f, 1f);
        [Tooltip("Color inferior del degradado.")]
        [SerializeField, ColorUsage(false, true)] private Color colorBottom = new Color(1f, 0.6f, 0.78f, 1f);

        [Header("Piezas")]
        [Tooltip("Superficie de la abertura del portal.")]
        [SerializeField] private Renderer surface;

        private MaterialPropertyBlock block;

        private void OnEnable() => ApplyColors();

#if UNITY_EDITOR
        private void OnValidate() => ApplyColors();
#endif

        /// <summary>Escribe el degradado de color en la superficie del portal.</summary>
        private void ApplyColors()
        {
            if (surface == null) return;
            block ??= new MaterialPropertyBlock();
            surface.GetPropertyBlock(block);
            block.SetColor(ColorAId, colorTop);
            block.SetColor(ColorBId, colorBottom);
            block.SetFloat(GlowIntensityId, DefaultGlowIntensity);
            block.SetFloat(OpenId, 0f);
            block.SetFloat(DissolveId, 0f);
            surface.SetPropertyBlock(block);
        }
    }
}
