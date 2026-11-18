// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.EditorTools)
// Archivo: WarmupManifestBuilder.cs
// Propósito: genera las listas de precalentado (WarmupManifest) de cada nivel del
// catálogo. Se puede lanzar a mano y se ejecuta sola antes de cada build.
// -----------------------------------------------------------------------------

using System.IO;
using NostalgIA.Core.Levels;
using NostalgIA.Presentation.Levels;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.EditorTools
{
    /// <summary>
    /// Abre cada nivel del catálogo, anota sus pares (malla, material) y los guarda en
    /// <c>Assets/_Project/Resources/Warmup/&lt;escena&gt;.asset</c>.
    /// </summary>
    /// <remarks>
    /// Las listas solo sirven para la pantalla de carga del Tutorial, que precalienta con ellas los
    /// shaders de los niveles siguientes. Como el contenido de los niveles cambia, se regeneran
    /// solas antes de cada build; desde el menú se pueden actualizar para probar en el editor.
    /// </remarks>
    public sealed class WarmupManifestBuilder : IPreprocessBuildWithReport
    {
        private const string Folder = "Assets/_Project/Resources/Warmup";

        /// <summary>Corre antes que el resto de pasos de build.</summary>
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report) => BuildAll();

        [MenuItem("NostalgIA/Generar listas de precalentado")]
        private static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
        }

        /// <summary>Genera la lista de cada nivel del catálogo (menos el primero, que no la necesita).</summary>
        public static void BuildAll()
        {
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);

            for (int i = 1; i < LevelCatalog.Levels.Length; i++)
            {
                string sceneName = LevelCatalog.Levels[i].Scene;
                string path = FindScenePath(sceneName);
                if (path == null)
                {
                    Debug.LogWarning($"[Precalentado] No encontré la escena {sceneName}.");
                    continue;
                }

                // Si la escena ya está abierta se usa tal cual; si no, se abre aparte y se cierra al terminar.
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                var entries = LevelShaderWarmup.Describe(scene);
                if (opened) EditorSceneManager.CloseScene(scene, true);

                string assetPath = $"{Folder}/{sceneName}.asset";
                var manifest = AssetDatabase.LoadAssetAtPath<WarmupManifest>(assetPath);
                if (manifest == null)
                {
                    manifest = ScriptableObject.CreateInstance<WarmupManifest>();
                    AssetDatabase.CreateAsset(manifest, assetPath);
                }
                manifest.Set(sceneName, entries);
                EditorUtility.SetDirty(manifest);
                Debug.Log($"[Precalentado] {sceneName}: {entries.Count} dibujos.");
            }
            AssetDatabase.SaveAssets();
        }

        private static string FindScenePath(string sceneName)
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:Scene {sceneName}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == sceneName) return path;
            }
            return null;
        }
    }
}
