// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de editor (NostalgIA.Tests.EditMode)
// Archivo: FrutigerLevelTests.cs
// Propósito: verificar el nivel experimental Frutiger Aero: shaders, materiales
// y montaje jugable de la escena generada (rama experimental/frutiger-aero).
// -----------------------------------------------------------------------------

using System.IO;
using System.Linq;
using NostalgIA.Core;
using NostalgIA.EditorTools.Experimental;
using NostalgIA.Gameplay.Level;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
using NostalgIA.Presentation.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.Tests.EditMode
{
    /// <summary>Pruebas sobre los assets que produce <see cref="FrutigerLevelBuilder"/>.</summary>
    public sealed class FrutigerLevelTests
    {
        private static string[] ShaderNames => FrutigerLevelBuilder.ShaderNames;

        /// <summary>Cada shader del nivel existe y compila sin errores en la plataforma activa.</summary>
        [TestCaseSource(nameof(ShaderNames))]
        public void Shader_CompilaSinErrores(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.IsNotNull(shader, $"No se encontró {shaderName}.");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), $"{shaderName} tiene errores.");
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                Assert.AreNotEqual("Error", message.severity.ToString(), $"{shaderName}: {message.message}");
        }

        /// <summary>
        /// Compatibilidad con el SRP Batcher: cada archivo de shader del nivel declara (o incluye)
        /// un único búfer UnityPerMaterial, y la biblioteca común no declara propiedades.
        /// </summary>
        [Test]
        public void Shaders_DeclaranUnityPerMaterial()
        {
            const string folder = "Assets/_Project/Art/Shaders/Frutiger";
            foreach (string path in Directory.GetFiles(folder, "*.shader"))
            {
                string text = File.ReadAllText(path);
                bool inline = text.Contains("CBUFFER_START(UnityPerMaterial)");
                bool included = text.Contains("AeroGlossInput.hlsl");
                Assert.IsTrue(inline || included, $"{Path.GetFileName(path)} no declara UnityPerMaterial.");
            }
            StringAssert.DoesNotContain("CBUFFER_START", File.ReadAllText(folder + "/FrutigerCommon.hlsl"));
        }

        /// <summary>El campo de burbujas usa impostores (malla Quad y palabra clave _IMPOSTOR).</summary>
        [Test]
        public void Escena_CampoDeBurbujasConImpostores()
        {
            WithScene(() =>
            {
                BubbleFieldRenderer[] fields = Object.FindObjectsByType<BubbleFieldRenderer>(FindObjectsSortMode.None);
                Assert.GreaterOrEqual(fields.Length, 2, "Faltan campos de burbujas.");
                bool anyImpostor = fields.Any(f =>
                {
                    var so = new SerializedObject(f);
                    var mesh = so.FindProperty("mesh").objectReferenceValue as Mesh;
                    var material = so.FindProperty("material").objectReferenceValue as Material;
                    return mesh != null && mesh.vertexCount == 4 && material != null && material.IsKeywordEnabled("_IMPOSTOR");
                });
                Assert.IsTrue(anyImpostor, "El campo cercano debe dibujarse con impostores de 2 triángulos.");
            });
        }

        /// <summary>
        /// La escena es jugable: jugador, checkpoints indexados en orden, volumen letal,
        /// trampolines, anclajes en la capa Grapple, absorbibles y ambiente.
        /// </summary>
        [Test]
        public void Escena_RecorridoJugableCompleto()
        {
            WithScene(() =>
            {
                Assert.IsNotNull(Object.FindFirstObjectByType<PlayerMotor>(), "Falta el jugador.");
                Assert.IsNotNull(Object.FindFirstObjectByType<KillVolume>(), "Falta el volumen letal del agua.");
                Assert.IsNotNull(Object.FindFirstObjectByType<FrutigerAtmosphere>(), "Falta FrutigerAtmosphere.");

                Checkpoint[] checkpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None)
                                                 .OrderBy(c => c.Index).ToArray();
                Assert.GreaterOrEqual(checkpoints.Length, 4);
                for (int i = 0; i < checkpoints.Length; i++)
                {
                    Assert.AreEqual(i, checkpoints[i].Index, "Índices de checkpoint consecutivos desde 0.");
                    Assert.AreEqual(Layers.Trigger, checkpoints[i].gameObject.layer);
                }

                Assert.GreaterOrEqual(Object.FindObjectsByType<BounceBubble>(FindObjectsSortMode.None).Length, 2);

                Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
                Assert.GreaterOrEqual(colliders.Count(c => c.gameObject.layer == Layers.Grapple), 2, "Faltan anclajes del gancho.");
                Absorbable[] absorbables = Object.FindObjectsByType<Absorbable>(FindObjectsSortMode.None);
                Assert.Greater(absorbables.Length, 0);
                Assert.IsTrue(absorbables.All(a => a.gameObject.layer == Layers.Absorbable), "Absorbibles fuera de su capa.");
            });
        }

        /// <summary>0 % Terrain y ningún material creado en ejecución: todos los renderers usan assets.</summary>
        [Test]
        public void Escena_SinTerrainYMaterialesComoAssets()
        {
            WithScene(() =>
            {
                Assert.AreEqual(0, Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).Length);
                foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null) continue;
                        Assert.IsTrue(AssetDatabase.Contains(material), $"{renderer.name} usa un material que no es asset.");
                    }
                }
            });
        }

        /// <summary>Abre la escena del nivel de forma aditiva, ejecuta la prueba y la cierra.</summary>
        private static void WithScene(System.Action test)
        {
            Assume.That(File.Exists(FrutigerLevelBuilder.ScenePath), "Ejecutar antes NostalgIA › Experimental › Construir nivel Frutiger Aero.");
            Scene scene = EditorSceneManager.OpenScene(FrutigerLevelBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                test();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
