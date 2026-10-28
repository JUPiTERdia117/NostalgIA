// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: LevelRegionTools.cs
// Propósito: mantener cada nivel en su propia región del mundo para que dos niveles
// cargados a la vez (puerta de nivel) nunca se encimen ni se vean entre sí.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Text;
using NostalgIA.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.EditorTools.Levels
{
    /// <summary>
    /// Herramientas de regiones de nivel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Las puertas de nivel cargan el nivel siguiente sin moverlo (mover una escena cargada
    /// rompe el <i>static batching</i>, la navegación y las sondas de luz). Por eso la separación
    /// se resuelve al construir: cada nivel ocupa una región propia, y la distancia entre los
    /// límites de dos niveles debe superar el plano lejano de la cámara del jugador para que
    /// ninguno aparezca en la vista del otro. Con esa regla el sistema admite niveles de
    /// cualquier tamaño: basta con elegir regiones más separadas.
    /// </para>
    /// <para>
    /// El desplazamiento se aplica en el editor, sobre las raíces de la escena, antes de guardar;
    /// la precisión de coma flotante a 3 km (≈ 0,25 mm) es suficiente para el juego.
    /// </para>
    /// </remarks>
    public static class LevelRegionTools
    {
        /// <summary>Plano lejano de la cámara del jugador, en m (prefab del jugador).</summary>
        public const float PlayerFarClip = 1000f;

        /// <summary>Holgura adicional exigida entre regiones, en m.</summary>
        public const float Margin = 100f;

        /// <summary>Desplaza todas las raíces de una escena: p' = p + Δ.</summary>
        /// <param name="scene">Escena abierta en el editor.</param>
        /// <param name="offset">Desplazamiento Δ en metros.</param>
        public static void ShiftSceneRoots(Scene scene, Vector3 offset)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                root.transform.position += offset;
            Physics.SyncTransforms();
        }

        /// <summary>Límites de todo lo que se dibuja en una escena (sin partículas ni líneas).</summary>
        /// <param name="scene">Escena cargada.</param>
        /// <param name="bounds">Límites encontrados.</param>
        /// <returns><c>true</c> si la escena tiene geometría.</returns>
        public static bool TryGetSceneBounds(Scene scene, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer) continue;
                    if (!any) { bounds = renderer.bounds; any = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            }
            return any;
        }

        /// <summary>
        /// Distancia entre dos cajas alineadas a los ejes: g = |max(0, a_min − b_max, b_min − a_max)|
        /// por eje, combinada como norma euclídea. Es 0 si se tocan o se solapan.
        /// </summary>
        public static float Gap(Bounds a, Bounds b)
        {
            Vector3 d = Vector3.Max(Vector3.zero, Vector3.Max(a.min - b.max, b.min - a.max));
            return d.magnitude;
        }

        /// <summary>
        /// Abre de forma aditiva las escenas de Estaciones del build, mide sus regiones y reporta
        /// los pares cuya separación es menor que el plano lejano más la holgura.
        /// </summary>
        [MenuItem("NostalgIA/Niveles/Comprobar separación entre niveles", priority = 310)]
        public static void CheckSeparation()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var names = new List<string>();
            var regions = new List<Bounds>();
            var opened = new List<Scene>();

            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled || !entry.path.StartsWith(SceneNames.Paths.StationsFolder)) continue;
                Scene scene = SceneManager.GetSceneByPath(entry.path);
                if (!scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
                    opened.Add(scene);
                }
                if (TryGetSceneBounds(scene, out Bounds bounds))
                {
                    names.Add(Path.GetFileNameWithoutExtension(entry.path));
                    regions.Add(bounds);
                }
            }
            foreach (Scene scene in opened) EditorSceneManager.CloseScene(scene, true);

            var report = new StringBuilder();
            float required = PlayerFarClip + Margin;
            int problems = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                report.AppendLine($"{names[i]}: centro {regions[i].center}, tamaño {regions[i].size}");
                for (int j = i + 1; j < regions.Count; j++)
                {
                    float gap = Gap(regions[i], regions[j]);
                    if (gap >= required) continue;
                    problems++;
                    report.AppendLine($"   ✗ {names[i]} ↔ {names[j]}: separación {gap:0} m (mínimo {required:0} m)");
                }
            }
            report.Insert(0, problems == 0 ? "Regiones de nivel correctas.\n" : $"{problems} pares de niveles demasiado cerca.\n");
            File.WriteAllText("Temp/level_regions.txt", report.ToString());
            if (problems == 0) Debug.Log("[Niveles] " + report);
            else Debug.LogWarning("[Niveles] " + report);
        }
    }
}
