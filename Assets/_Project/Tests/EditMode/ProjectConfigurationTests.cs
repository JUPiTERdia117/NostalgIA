// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de modo de edición (NostalgIA.Tests.EditMode)
// Archivo: ProjectConfigurationTests.cs
// Propósito: verificar la configuración del proyecto establecida en la Fase 0
// (tareas F0-A05, F0-A06 y F0-B02).
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NostalgIA.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NostalgIA.Tests.EditMode
{
    /// <summary>
    /// Pruebas de Build Settings, Player Settings, escena de arranque y archivos obligatorios del repositorio.
    /// </summary>
    public sealed class ProjectConfigurationTests
    {
        /// <summary>Devuelve las rutas de las escenas habilitadas en el build, en orden.</summary>
        private static List<string> EnabledBuildScenes()
        {
            var paths = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) paths.Add(scene.path);
            }
            return paths;
        }

        /// <summary>La escena de arranque es la primera del build.</summary>
        [Test]
        public void BuildSettings_LaPrimeraEscenaEsBoot()
        {
            List<string> scenes = EnabledBuildScenes();
            Assert.IsNotEmpty(scenes);
            Assert.AreEqual(SceneNames.Paths.Boot, scenes[0]);
        }

        /// <summary>
        /// El build incluye el Tutorial y la dimensión enlazada, que el portal carga por nombre de
        /// forma aditiva; sin ella el portal fallaría en el build.
        /// </summary>
        [Test]
        public void BuildSettings_IncluyenTutorialYOtraDimension()
        {
            List<string> scenes = EnabledBuildScenes();
            CollectionAssert.Contains(scenes, SceneNames.Paths.Tutorial);
            CollectionAssert.Contains(scenes, SceneNames.Paths.OtherDimension);
        }

        /// <summary>Ninguna escena de prueba entra al build y todas las registradas existen.</summary>
        [Test]
        public void BuildSettings_SinEscenasDePruebaNiRutasRotas()
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path), $"No existe {scene.path}.");
                Assert.IsFalse(scene.path.StartsWith(SceneNames.Paths.SandboxFolder), $"Escena de prueba en el build: {scene.path}.");
            }
        }

        /// <summary>
        /// Los nombres usados para cargar escenas coinciden con el nombre de archivo de sus rutas;
        /// <c>SceneManager</c> identifica las escenas por ese nombre.
        /// </summary>
        [Test]
        public void SceneNames_CoincidenConLosArchivos()
        {
            Assert.AreEqual(SceneNames.Boot, Path.GetFileNameWithoutExtension(SceneNames.Paths.Boot));
            Assert.AreEqual(SceneNames.Tutorial, Path.GetFileNameWithoutExtension(SceneNames.Paths.Tutorial));
            Assert.AreEqual(SceneNames.OtherDimension, Path.GetFileNameWithoutExtension(SceneNames.Paths.OtherDimension));
        }

        /// <summary>
        /// La escena de arranque contiene el componente <c>BootSceneLoader</c> y apunta a una
        /// escena que está en el build.
        /// </summary>
        /// <remarks>
        /// Se inspecciona el texto YAML de la escena para no abrirla ni alterar la escena activa del editor.
        /// </remarks>
        [Test]
        public void EscenaBoot_CargaUnaEscenaDelBuild()
        {
            string[] scriptGuids = AssetDatabase.FindAssets("BootSceneLoader t:MonoScript");
            Assert.IsNotEmpty(scriptGuids, "No se encontró el script BootSceneLoader.");

            string sceneText = File.ReadAllText(SceneNames.Paths.Boot);
            StringAssert.Contains($"guid: {scriptGuids[0]}", sceneText, "La escena de arranque no contiene BootSceneLoader.");

            Match target = Regex.Match(sceneText, @"firstSceneName: (\S+)");
            Assert.IsTrue(target.Success, "No se encontró el campo firstSceneName.");

            bool inBuild = false;
            foreach (string path in EnabledBuildScenes())
            {
                if (Path.GetFileNameWithoutExtension(path) == target.Groups[1].Value) inBuild = true;
            }
            Assert.IsTrue(inBuild, $"La escena '{target.Groups[1].Value}' no está en el build.");
        }

        /// <summary>Player Settings de la tarea F0-A06.</summary>
        [Test]
        public void PlayerSettings_CumplenLaPlaneacion()
        {
            Assert.AreEqual("NostalgIA", PlayerSettings.productName);
            Assert.GreaterOrEqual((int)PlayerSettings.Android.minSdkVersion, 29, "La planeación exige Android 10 (API 29) como mínimo.");
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortrait, "El juego es solo horizontal.");
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortraitUpsideDown, "El juego es solo horizontal.");
        }

        /// <summary>El validador de texto requiere serialización forzada de texto.</summary>
        [Test]
        public void Serializacion_EsDeTexto()
        {
            Assert.AreEqual(SerializationMode.ForceText, EditorSettings.serializationMode);
        }

        /// <summary>
        /// Restricción 2 del curso: el registro de atribuciones existe en la raíz del repositorio y
        /// nombra cada carpeta de terceros presente en <c>Assets/ThirdParty/</c>.
        /// </summary>
        [Test]
        public void Creditos_RegistranTodoElContenidoDeTerceros()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string creditsPath = Path.Combine(projectRoot, "CREDITS.md");
            Assert.IsTrue(File.Exists(creditsPath), "Falta CREDITS.md en la raíz del repositorio.");

            string credits = File.ReadAllText(creditsPath);
            string thirdParty = Path.Combine(Application.dataPath, "ThirdParty");
            if (!Directory.Exists(thirdParty)) return;

            foreach (string folder in Directory.GetDirectories(thirdParty))
            {
                string relative = "Assets/ThirdParty/" + Path.GetFileName(folder);
                StringAssert.Contains(relative, credits, $"CREDITS.md no registra {relative}.");
            }
        }
    }
}
