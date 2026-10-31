// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: LevelsTestSetup.cs
// Propósito: rama Levels_test. Quita los niveles anteriores al Tutorial,
// construye los niveles 2 y 3 nuevos y los encadena con la puerta de nivel.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NostalgIA.Core;
using NostalgIA.Gameplay.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.EditorTools.Levels
{
    /// <summary>
    /// Cadena de la rama Levels_test: Tutorial → Nivel 2 (islas) → Nivel 3 (laberinto de la IA)
    /// → estación final. Cada paso usa la puerta de nivel actual (<see cref="LevelGate"/>).
    /// </summary>
    public static class LevelsTestSetup
    {
        private const string MenuRoot = "NostalgIA/Niveles/Levels_test/";

        /// <summary>Escenas de los niveles anteriores que se reemplazan.</summary>
        private static readonly string[] OldLevelAssets =
        {
            "Assets/_Project/Scenes/Stations/20_Verano_Frutiger.unity",
            "Assets/_Project/Scenes/Stations/dreamcore.unity",
            "Assets/_Project/Scenes/Stations/dreamcore",
            "Assets/_Project/Scenes/Stations/40_Invierno_IA.unity",
            "Assets/_Project/Scenes/Stations/40_Invierno_IA",
            "Assets/_Project/Art/Meshes/Invierno/LavaBlue_UVMundo.asset",
        };

        /// <summary>Hace todo en orden y deja abierto el Tutorial.</summary>
        [MenuItem(MenuRoot + "Construir y conectar todo", priority = 350)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // Abro una escena vacía para no borrar la escena que esté abierta en ese momento.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DeleteOldLevels();
            Nivel2IslasBuilder.BuildScene();
            Nivel3LaberintoBuilder.BuildScene();
            ConnectTutorial();
            FixBuildList();
            EditorSceneManager.OpenScene(SceneNames.Paths.Tutorial, OpenSceneMode.Single);
            Debug.Log("[Levels_test] Listo: Tutorial → Nivel 2 → Nivel 3 → estación final.");
        }

        /// <summary>Borra las escenas de los niveles anteriores (siguen en el historial de Git).</summary>
        private static void DeleteOldLevels()
        {
            foreach (string path in OldLevelAssets)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(path) == null && !AssetDatabase.IsValidFolder(path)) continue;
                AssetDatabase.DeleteAsset(path);
                Debug.Log("[Levels_test] Borrado " + path);
            }
        }

        /// <summary>La puerta del Tutorial ahora lleva al Nivel 2.</summary>
        [MenuItem(MenuRoot + "Conectar Tutorial con Nivel 2", priority = 353)]
        public static void ConnectTutorial()
        {
            Scene tutorial = EditorSceneManager.OpenScene(SceneNames.Paths.Tutorial, OpenSceneMode.Single);
            LevelGate gate = tutorial.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelGate>(true)).FirstOrDefault();
            if (gate == null)
            {
                Debug.LogError("[Levels_test] El Tutorial no tiene puerta de nivel.");
                return;
            }
            gate.Configure(SceneNames.Nivel2, SceneNames.Paths.Nivel2, gate.Aperture);
            gate.gameObject.name = "LevelGate_Nivel2";
            EditorUtility.SetDirty(gate);
            EditorSceneManager.MarkSceneDirty(tutorial);
            EditorSceneManager.SaveScene(tutorial);
        }

        /// <summary>Quita de la lista del build las escenas borradas y deja el orden del recorrido.</summary>
        private static void FixBuildList()
        {
            string[] order =
            {
                SceneNames.Paths.Boot, SceneNames.Paths.Tutorial, SceneNames.Paths.Nivel2,
                SceneNames.Paths.Nivel3, SceneNames.Paths.EstacionFinal,
            };
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in order)
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
                if (File.Exists(existing.path) && scenes.All(s => s.path != existing.path)) scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
