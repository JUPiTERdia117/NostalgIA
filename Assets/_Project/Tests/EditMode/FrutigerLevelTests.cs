// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de editor (NostalgIA.Tests.EditMode)
// Archivo: FrutigerLevelTests.cs
// Propósito: verifico que el Nivel 2 (Verano — Frutiger Aero) tenga sus shaders
// compatibles con SRP Batcher, océano poco profundo caminable, plataformas
// móviles, aspas giratorias, desbloqueos por combate, puerta de meta mirando
// hacia arriba, ranuras de audio preparadas y 0 % Terrain.
// -----------------------------------------------------------------------------

using System.IO;
using System.Linq;
using NostalgIA.Core;
using NostalgIA.EditorTools.Experimental;
using NostalgIA.Gameplay.Level;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
using NostalgIA.Presentation.Audio;
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
        /// La escena del Nivel 2 es jugable de principio a fin: jugador, entrada desde Tutorial,
        /// océano poco profundo sin daño por caída, plataformas móviles, aspas giratorias,
        /// desbloqueo del muro central y de la meta, puerta mirando hacia arriba, trampolines
        /// burbuja en el agua y ranuras de audio listas para el equipo.
        /// </summary>
        [Test]
        public void Escena_RecorridoJugableCompleto()
        {
            WithScene(() =>
            {
                Assert.IsNotNull(Object.FindFirstObjectByType<PlayerMotor>(FindObjectsInactive.Include), "Falta el jugador (contenido independiente).");
                Assert.IsNotNull(Object.FindFirstObjectByType<LevelEntrance>(), "Falta LevelEntrance para recibir al jugador desde Tutorial.");
                Assert.IsNotNull(Object.FindFirstObjectByType<WaterFallCushion>(), "Falta el amortiguador de caída al agua poco profunda.");
                Assert.IsNotNull(Object.FindFirstObjectByType<KillVolume>(), "Falta la red de seguridad fuera de límites.");
                Assert.IsNotNull(Object.FindFirstObjectByType<FrutigerAtmosphere>(), "Falta FrutigerAtmosphere.");

                Checkpoint[] checkpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None)
                                                 .OrderBy(c => c.Index).ToArray();
                Assert.GreaterOrEqual(checkpoints.Length, 4);
                for (int i = 0; i < checkpoints.Length; i++)
                {
                    Assert.AreEqual(i, checkpoints[i].Index, "Índices de checkpoint consecutivos desde 0.");
                    Assert.AreEqual(Layers.Trigger, checkpoints[i].gameObject.layer);
                }

                Assert.GreaterOrEqual(Object.FindObjectsByType<BounceBubble>(FindObjectsSortMode.None).Length, 6,
                    "Faltan burbujas trampolín para volver a subir desde el agua poco profunda.");
                Assert.GreaterOrEqual(Object.FindObjectsByType<MovingPlatform>(FindObjectsSortMode.None).Length, 2,
                    "Faltan las dos plataformas desplazables antes del lote de edificios.");
                Assert.GreaterOrEqual(Object.FindObjectsByType<RotatingHazardObstacle>(FindObjectsSortMode.None).Length, 1,
                    "Faltan las aspas giratorias dañinas.");
                Assert.GreaterOrEqual(Object.FindObjectsByType<DefeatUnlockController>(FindObjectsSortMode.None).Length, 1,
                    "Falta el controlador que baja el muro central y suelta la estrella al derrotar al Spawner.");
                Assert.GreaterOrEqual(Object.FindObjectsByType<LevelAudioSlot>(FindObjectsSortMode.None).Length, 4,
                    "Faltan las ranuras de audio preparadas a prueba de null.");

                LevelGate goalGate = Object.FindFirstObjectByType<LevelGate>();
                Assert.IsNotNull(goalGate, "Falta la puerta de meta del Nivel 2.");
                Assert.Greater(Vector3.Dot(goalGate.transform.forward, Vector3.up), 0.98f,
                    "La puerta de meta debe mirar hacia arriba para caer dentro de ella.");
                Assert.AreEqual(SceneNames.OtherDimension, new SerializedObject(goalGate).FindProperty("destinationSceneName").stringValue,
                    "La puerta de meta debe apuntar a OtherDimension mientras se define el Nivel 3.");

                Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
                Assert.IsTrue(colliders.Any(c => c.gameObject.name == "LechoMarino_Caminable" && !c.isTrigger),
                    "Falta el colisionador sólido del lecho marino poco profundo.");
                Assert.GreaterOrEqual(colliders.Count(c => c.gameObject.layer == Layers.Grapple), 10, "Faltan esferas/edificios de enganche.");
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
            Assume.That(File.Exists(FrutigerLevelBuilder.ScenePath), "Ejecutar antes NostalgIA › Niveles › Construir Nivel 2 — Verano (Frutiger Aero).");
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
