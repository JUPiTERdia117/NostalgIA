// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: FrutigerAtmosphere.cs
// Propósito: publica las variables globales del ambiente Frutiger Aero (cielo,
// sol, nivel del agua) y activa la refracción solo donde el pipeline la soporta.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NostalgIA.Presentation.Environment
{
    /// <summary>
    /// Fuente única de los parámetros de ambiente que comparten los shaders NOS/Aero*.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los colores del cielo se leen del material del skybox: así el agua, las burbujas y el
    /// vidrio reflejan exactamente el cielo que se ve, sin duplicar valores. Se publican con
    /// <see cref="Shader.SetGlobalColor(int, Color)"/>: las variables globales no forman parte
    /// del búfer por material y no rompen el SRP Batcher.
    /// </para>
    /// <para>
    /// <b>Refracción.</b> El agua y el vidrio leen la textura de opacos y la de profundidad de
    /// la cámara. El asset de URP de PC las genera; el de móvil no (ahorra una copia de la
    /// pantalla por frame). En este módulo se decidió activar la palabra clave global
    /// <c>_NOS_REFRACTION</c> solo si el asset activo las soporta, de modo que en móvil los
    /// shaders usan su variante de mezcla alfa en lugar de muestrear una textura vacía.
    /// </para>
    /// <para>
    /// Se ejecuta también en el editor para que la vista de escena muestre el aspecto final.
    /// </para>
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FrutigerAtmosphere : MonoBehaviour
    {
        [Tooltip("Material NOS/AeroSky de la escena (fuente de los colores del cielo).")]
        [SerializeField] private Material skyMaterial;

        [Tooltip("Color del sol en reflejos y destellos (HDR).")]
        [SerializeField, ColorUsage(false, true)] private Color sunTint = new Color(1.6f, 1.5f, 1.25f);

        [Tooltip("Altura del agua en metros (las cáusticas aparecen por debajo).")]
        [SerializeField] private float waterLevel = 0f;

        [Tooltip("Objetos solo para PC (por ejemplo, el Volume de destellos de lente); se apagan en móvil.")]
        [SerializeField] private GameObject[] desktopOnly = new GameObject[0];

        private static readonly int SkyZenithId = Shader.PropertyToID("_NOS_SkyZenith");
        private static readonly int SkyHorizonId = Shader.PropertyToID("_NOS_SkyHorizon");
        private static readonly int SkyGroundId = Shader.PropertyToID("_NOS_SkyGround");
        private static readonly int SunTintId = Shader.PropertyToID("_NOS_SunTint");
        private static readonly int WaterLevelId = Shader.PropertyToID("_NOS_WaterLevel");
        private static readonly int ZenithId = Shader.PropertyToID("_ZenithColor");
        private static readonly int HorizonId = Shader.PropertyToID("_HorizonColor");
        private static readonly int GroundId = Shader.PropertyToID("_GroundColor");
        private const string RefractionKeyword = "_NOS_REFRACTION";

        /// <summary>Publica las globales al activarse.</summary>
        private void OnEnable() => Apply();

        /// <summary>Reaplica al editar valores en el inspector.</summary>
        private void OnValidate() => Apply();

        /// <summary>Apaga la refracción al salir (otras escenas no la usan).</summary>
        private void OnDisable() => Shader.DisableKeyword(RefractionKeyword);

        /// <summary>Escribe colores, nivel del agua y palabra clave de refracción.</summary>
        public void Apply()
        {
            if (skyMaterial != null)
            {
                Shader.SetGlobalColor(SkyZenithId, skyMaterial.GetColor(ZenithId));
                Shader.SetGlobalColor(SkyHorizonId, skyMaterial.GetColor(HorizonId));
                Shader.SetGlobalColor(SkyGroundId, skyMaterial.GetColor(GroundId));
            }
            Shader.SetGlobalColor(SunTintId, sunTint);
            Shader.SetGlobalFloat(WaterLevelId, waterLevel);

            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            bool refraction = pipeline != null && pipeline.supportsCameraOpaqueTexture && pipeline.supportsCameraDepthTexture;
            if (refraction) Shader.EnableKeyword(RefractionKeyword);
            else Shader.DisableKeyword(RefractionKeyword);

            if (Application.isPlaying && Application.isMobilePlatform)
            {
                for (int i = 0; i < desktopOnly.Length; i++)
                    if (desktopOnly[i] != null) desktopOnly[i].SetActive(false);
            }
        }
    }
}
