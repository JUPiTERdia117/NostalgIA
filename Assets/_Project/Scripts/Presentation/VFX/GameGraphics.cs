// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: GameGraphics.cs
// Propósito: presets de calidad gráfica (bajo, medio y alto), guardados entre
// partidas y aplicados desde el arranque. Los elige la fila «Calidad gráfica» del
// menú de ajustes.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NostalgIA.Presentation.VFX
{
    /// <summary>Niveles de calidad gráfica que ofrece el menú.</summary>
    public enum GraphicsPreset
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>
    /// Aplica el preset de calidad gráfica: resolución de render, texturas, HDR, Bloom y sombras.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cada preset es un asset de URP en <c>Resources/GraphicsPresets</c> (uno por plataforma y nivel)
    /// con su HDR, sus sombras y su renderer (solo PC alto lleva SSAO). Ninguno pide la copia de
    /// color ni la textura de profundidad: solo las usaban los shaders de Verano, que ya no se usan.
    /// </para>
    /// <para>
    /// La resolución es la altura a la que se renderiza el mundo 3D (el lado corto de la pantalla).
    /// Se logra con la escala de render de URP, así el menú y el HUD siguen nítidos, y nunca supera
    /// la pantalla real: en un monitor 1080p los tres niveles de PC renderizan a 1080.
    /// </para>
    /// <para>
    /// Se usa una copia en memoria del asset para poder cambiarle la escala sin modificar el
    /// archivo del proyecto mientras se juega en el editor.
    /// </para>
    /// </remarks>
    public static class GameGraphics
    {
        private const string PresetKey = "NostalgIA.GraphicsPreset";
        private const string PresetFolder = "GraphicsPresets/";
        private const float MinRenderScale = 0.25f;

        // Altura de render objetivo por nivel (bajo, medio, alto).
        private static readonly int[] DesktopHeights = { 1080, 1440, 2160 };   // Full HD, 2K, 4K
        private static readonly int[] MobileHeights = { 720, 1080, 1440 };     // HD, Full HD, QHD+ («2K»)

        // Mipmaps que se saltan las texturas: 1 = mitad de resolución en bajo.
        private static readonly int[] TextureMipmapLimits = { 1, 0, 0 };

        private static readonly UniversalRenderPipelineAsset[] runtimeAssets = new UniversalRenderPipelineAsset[3];
        private static UniversalRenderPipelineAsset activeAsset;
        private static Bloom bloomOverride;
        private static int appliedShortSide;

        /// <summary>Se dispara después de aplicar un preset.</summary>
        public static event System.Action<GraphicsPreset> PresetChanged;

        /// <summary>True en teléfonos y tabletas; en el editor y en PC es false.</summary>
        public static bool IsMobile => Application.isMobilePlatform;

        /// <summary>Preset guardado. Si no hay ninguno: alto en PC y medio en teléfono.</summary>
        public static GraphicsPreset Current
        {
            get
            {
                int fallback = (int)(IsMobile ? GraphicsPreset.Medium : GraphicsPreset.High);
                return (GraphicsPreset)Mathf.Clamp(PlayerPrefs.GetInt(PresetKey, fallback), 0, 2);
            }
        }

        /// <summary>Altura de render que pide el preset en esta plataforma.</summary>
        public static int TargetHeight(GraphicsPreset preset) => (IsMobile ? MobileHeights : DesktopHeights)[(int)preset];

        /// <summary>Guarda el preset y lo aplica de inmediato.</summary>
        public static void Set(GraphicsPreset preset)
        {
            PlayerPrefs.SetInt(PresetKey, (int)preset);
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>Aplica el preset guardado.</summary>
        public static void Apply()
        {
            GraphicsPreset preset = Current;
            UniversalRenderPipelineAsset asset = GetRuntimeAsset(preset);
            if (asset == null) return;

            activeAsset = asset;
            UpdateRenderScale(force: true);
            QualitySettings.globalTextureMipmapLimit = TextureMipmapLimits[(int)preset];
            if (QualitySettings.renderPipeline != asset) QualitySettings.renderPipeline = asset;
            ApplyBloom(preset);

            PresetChanged?.Invoke(preset);
        }

        /// <summary>
        /// Recalcula la escala de render si cambió el tamaño de la pantalla (ventana, rotación).
        /// Lo llama cada frame <see cref="GameGraphicsWatcher"/>; no hace nada si no hubo cambio.
        /// </summary>
        internal static void UpdateRenderScale(bool force = false)
        {
            if (activeAsset == null) return;
            int shortSide = Mathf.Min(Screen.width, Screen.height);
            if (shortSide <= 0 || (!force && shortSide == appliedShortSide)) return;

            appliedShortSide = shortSide;
            activeAsset.renderScale = Mathf.Clamp((float)TargetHeight(Current) / shortSide, MinRenderScale, 1f);
        }

        private static UniversalRenderPipelineAsset GetRuntimeAsset(GraphicsPreset preset)
        {
            int index = (int)preset;
            if (runtimeAssets[index] != null) return runtimeAssets[index];

            string path = PresetFolder + (IsMobile ? "Mobile_" : "PC_") + preset + "_RPAsset";
            var source = Resources.Load<UniversalRenderPipelineAsset>(path);
            if (source == null)
            {
                Debug.LogWarning($"[GameGraphics] No encontré el asset de calidad en Resources/{path}.");
                return null;
            }

            UniversalRenderPipelineAsset copy = Object.Instantiate(source);
            copy.name = source.name;
            copy.hideFlags = HideFlags.DontSave;
            runtimeAssets[index] = copy;
            return copy;
        }

        /// <summary>
        /// Ajusta el Bloom encima de los perfiles de cada nivel con un Volume global de prioridad alta.
        /// Solo toca lo que cambia el preset; la intensidad y el color de cada nivel se respetan.
        /// </summary>
        private static void ApplyBloom(GraphicsPreset preset)
        {
            if (bloomOverride == null) CreateOverrideVolume();

            bloomOverride.intensity.overrideState = preset == GraphicsPreset.Low;     // Bajo: sin Bloom.
            bloomOverride.intensity.value = 0f;
            bloomOverride.highQualityFiltering.overrideState = true;
            bloomOverride.highQualityFiltering.value = preset == GraphicsPreset.High;
        }

        private static void CreateOverrideVolume()
        {
            var go = new GameObject("GraphicsPresetVolume");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<GameGraphicsWatcher>();

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "GraphicsPresetOverrides";
            profile.hideFlags = HideFlags.DontSave;
            bloomOverride = profile.Add<Bloom>();
            bloomOverride.active = true;
            volume.sharedProfile = profile;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStartup()
        {
            // En el editor las variables estáticas sobreviven entre sesiones de juego si el dominio
            // no se recarga: se limpian para crear todo de nuevo.
            System.Array.Clear(runtimeAssets, 0, runtimeAssets.Length);
            activeAsset = null;
            bloomOverride = null;
            appliedShortSide = 0;
            Apply();
        }
    }

    /// <summary>Vigila el tamaño de la pantalla para mantener la resolución del preset.</summary>
    [AddComponentMenu("")]
    internal sealed class GameGraphicsWatcher : MonoBehaviour
    {
        private void Update() => GameGraphics.UpdateRenderScale();
    }
}
