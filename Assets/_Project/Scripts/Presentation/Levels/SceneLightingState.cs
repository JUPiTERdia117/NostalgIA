// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: SceneLightingState.cs
// Propósito: aislar la iluminación de dos niveles cargados a la vez (sol, cielo,
// luz ambiental, niebla, reflejos, postprocesado y audio 2D).
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NostalgIA.Presentation.Levels
{
    /// <summary>
    /// Instantánea de la iluminación de una escena y control de los elementos que, por ser
    /// globales, se mezclarían entre dos niveles cargados al mismo tiempo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity aplica a todas las cámaras los <b>ajustes de iluminación de la escena activa</b>
    /// (cielo, luz ambiental, niebla, reflejo por defecto, sol), las <b>luces direccionales</b>
    /// (de alcance infinito) y los <b>Volumes globales</b> de todas las escenas cargadas. Por eso,
    /// al cargar el nivel siguiente, su sol iluminaba el nivel actual y sus perfiles de
    /// postprocesado se sumaban: la iluminación "cambiaba".
    /// </para>
    /// <para>
    /// Esta clase guarda una copia de los ajustes de la escena (para lo cual la hace activa
    /// durante un instante) y lista sus soles, Volumes y fuentes de audio no espaciales. Con ello
    /// la puerta mantiene dormido el nivel siguiente y, solo mientras se dibuja la cámara del
    /// portal, le "presta" su iluminación: lo que se ve por la puerta tiene el sol, el cielo y la
    /// niebla de su propio nivel.
    /// </para>
    /// <para>
    /// Las listas se llenan una sola vez (al preparar la puerta); los métodos que se llaman por
    /// frame solo recorren listas y escriben propiedades, sin asignar memoria.
    /// </para>
    /// </remarks>
    public sealed class SceneLightingState
    {
        private readonly List<Light> suns = new List<Light>(4);
        private readonly List<Volume> volumes = new List<Volume>(8);
        private readonly List<float> volumeWeights = new List<float>(8);
        private readonly List<AudioSource> flatAudio = new List<AudioSource>(16);

        // Copia de RenderSettings de la escena.
        private Material skybox;
        private SphericalHarmonicsL2 ambientProbe;
        private float ambientIntensity;
        private bool fog;
        private FogMode fogMode;
        private Color fogColor;
        private float fogDensity;
        private float fogStart;
        private float fogEnd;
        private Light sun;
        private Texture reflection;
        private DefaultReflectionMode reflectionMode;
        private Texture customReflection;
        private float reflectionIntensity;

        /// <summary>Escena descrita.</summary>
        public Scene Scene { get; }

        private SceneLightingState(Scene scene) => Scene = scene;

        /// <summary>
        /// Captura el estado de <paramref name="scene"/>. Si no es la escena activa, se activa un
        /// instante para leer sus ajustes y se restaura la anterior en la misma llamada.
        /// </summary>
        /// <param name="scene">Escena cargada.</param>
        /// <param name="excluded">Volume que no debe controlarse (por ejemplo, el destello de la puerta).</param>
        /// <returns>Estado capturado.</returns>
        public static SceneLightingState Capture(Scene scene, Volume excluded)
        {
            var state = new SceneLightingState(scene);
            var roots = new List<GameObject>(64);
            scene.GetRootGameObjects(roots);
            for (int r = 0; r < roots.Count; r++)
            {
                Light[] lights = roots[r].GetComponentsInChildren<Light>(true);
                for (int i = 0; i < lights.Length; i++)
                    if (lights[i].type == LightType.Directional && lights[i].enabled) state.suns.Add(lights[i]);

                Volume[] found = roots[r].GetComponentsInChildren<Volume>(true);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] == excluded || !found[i].enabled) continue;
                    state.volumes.Add(found[i]);
                    state.volumeWeights.Add(found[i].weight);
                }

                AudioSource[] sources = roots[r].GetComponentsInChildren<AudioSource>(true);
                for (int i = 0; i < sources.Length; i++)
                    if (sources[i].spatialBlend < 0.5f) state.flatAudio.Add(sources[i]);
            }

            Scene active = SceneManager.GetActiveScene();
            bool switchActive = active != scene;
            if (switchActive) SceneManager.SetActiveScene(scene);
            state.ReadRenderSettings();
            if (switchActive && active.IsValid()) SceneManager.SetActiveScene(active);
            return state;
        }

        /// <summary>Lee los ajustes de iluminación de la escena activa.</summary>
        private void ReadRenderSettings()
        {
            skybox = RenderSettings.skybox;
            ambientProbe = RenderSettings.ambientProbe;
            ambientIntensity = RenderSettings.ambientIntensity;
            fog = RenderSettings.fog;
            fogMode = RenderSettings.fogMode;
            fogColor = RenderSettings.fogColor;
            fogDensity = RenderSettings.fogDensity;
            fogStart = RenderSettings.fogStartDistance;
            fogEnd = RenderSettings.fogEndDistance;
            sun = RenderSettings.sun;
            reflectionMode = RenderSettings.defaultReflectionMode;
            customReflection = RenderSettings.customReflectionTexture;
            reflectionIntensity = RenderSettings.reflectionIntensity;
            reflection = ReflectionProbe.defaultTexture;
        }

        /// <summary>
        /// Escribe la copia en los ajustes globales. La luz ambiental se aplica como armónicos
        /// esféricos ya calculados (<see cref="RenderSettings.ambientProbe"/>), que es lo que leen los
        /// shaders: así no hay que recalcular el entorno, operación costosa.
        /// </summary>
        public void ApplyRenderSettings()
        {
            RenderSettings.skybox = skybox;
            RenderSettings.ambientProbe = ambientProbe;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.sun = sun;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            if (reflectionMode == DefaultReflectionMode.Custom || reflection == null)
            {
                RenderSettings.defaultReflectionMode = reflectionMode;
                RenderSettings.customReflectionTexture = customReflection;
            }
            else
            {
                // El reflejo generado del cielo de esta escena se aplica como reflejo personalizado.
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = reflection;
            }
        }

        /// <summary>Enciende o apaga los soles (luces direccionales) de la escena.</summary>
        /// <param name="enabled">Estado deseado.</param>
        public void SetSunsEnabled(bool enabled)
        {
            for (int i = 0; i < suns.Count; i++)
                if (suns[i] != null && suns[i].enabled != enabled) suns[i].enabled = enabled;
        }

        /// <summary>
        /// Mezcla los Volumes de la escena: peso = peso_original · t. Con t = 0 se desactivan para
        /// que el gestor de Volumes ni siquiera los evalúe.
        /// </summary>
        /// <param name="t">Factor entre 0 y 1.</param>
        public void SetVolumeBlend(float t)
        {
            bool enabled = t > 0.0001f;
            for (int i = 0; i < volumes.Count; i++)
            {
                Volume volume = volumes[i];
                if (volume == null) continue;
                volume.weight = volumeWeights[i] * t;
                if (volume.enabled != enabled) volume.enabled = enabled;
            }
        }

        /// <summary>Silencia o restaura el audio no espacial (música y ambiente 2D) de la escena.</summary>
        /// <param name="muted">Estado deseado.</param>
        public void SetFlatAudioMuted(bool muted)
        {
            for (int i = 0; i < flatAudio.Count; i++)
                if (flatAudio[i] != null) flatAudio[i].mute = muted;
        }

        /// <summary>Deja la escena sin influencia visual ni sonora sobre el nivel actual.</summary>
        public void SetDormant()
        {
            SetSunsEnabled(false);
            SetVolumeBlend(0f);
            SetFlatAudioMuted(true);
        }
    }
}
