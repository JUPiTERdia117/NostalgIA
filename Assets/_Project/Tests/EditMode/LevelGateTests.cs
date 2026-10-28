// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de editor (NostalgIA.Tests.EditMode)
// Archivo: LevelGateTests.cs
// Propósito: verificar la puerta entre niveles: geometría del portal, shaders,
// prefab, conexión del Tutorial con Verano y separación de regiones.
// -----------------------------------------------------------------------------

using System.IO;
using System.Linq;
using NostalgIA.Core;
using NostalgIA.Core.Levels;
using NostalgIA.EditorTools.Levels;
using NostalgIA.Gameplay.Level;
using NostalgIA.Gameplay.Player;
using NostalgIA.Presentation.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NostalgIA.Tests.EditMode
{
    /// <summary>Pruebas de <see cref="LevelGate"/>, <see cref="PortalMath"/> y sus assets.</summary>
    public sealed class LevelGateTests
    {
        private static readonly string[] Shaders = { "NOS/LevelGatePortal", "NOS/LevelGateGlow" };

        private GameObject from;
        private GameObject to;

        [SetUp]
        public void Montar()
        {
            from = new GameObject("Desde");
            to = new GameObject("Hacia");
            from.transform.SetPositionAndRotation(new Vector3(10f, 2f, -4f), Quaternion.Euler(0f, 30f, 0f));
            to.transform.SetPositionAndRotation(new Vector3(3000f, 5f, 40f), Quaternion.Euler(0f, -75f, 0f));
        }

        [TearDown]
        public void Desmontar()
        {
            Object.DestroyImmediate(from);
            Object.DestroyImmediate(to);
        }

        /// <summary>Un punto delante de la puerta queda detrás del marco de llegada, a la misma distancia.</summary>
        [Test]
        public void Transferencia_FrenteSeConvierteEnEspalda()
        {
            Matrix4x4 m = PortalMath.Transfer(from.transform, to.transform);
            Vector3 front = from.transform.TransformPoint(new Vector3(0.4f, 1.2f, 2f));
            Vector3 mapped = m.MultiplyPoint3x4(front);
            Vector3 local = to.transform.InverseTransformPoint(mapped);
            Assert.AreEqual(-0.4f, local.x, 1e-3f, "El lado izquierdo se refleja al atravesar.");
            Assert.AreEqual(1.2f, local.y, 1e-3f, "La altura se conserva.");
            Assert.AreEqual(-2f, local.z, 1e-3f, "El frente de la puerta corresponde a la espalda de la llegada.");
        }

        /// <summary>Caminar hacia la puerta equivale a salir hacia el interior del nivel nuevo.</summary>
        [Test]
        public void Transferencia_DireccionDeAvanceEntraAlNivel()
        {
            Matrix4x4 m = PortalMath.Transfer(from.transform, to.transform);
            Vector3 walk = -from.transform.forward;
            Vector3 mapped = m.MultiplyVector(walk);
            Assert.Greater(Vector3.Dot(mapped, to.transform.forward), 0.999f);

            Quaternion look = Quaternion.LookRotation(walk, Vector3.up);
            Quaternion mappedLook = PortalMath.TransferRotation(from.transform, to.transform, look);
            Assert.Greater(Vector3.Dot(mappedLook * Vector3.forward, to.transform.forward), 0.999f);
        }

        /// <summary>Ir y volver por el mismo par de marcos es la identidad.</summary>
        [Test]
        public void Transferencia_IdaYVueltaEsIdentidad()
        {
            Matrix4x4 there = PortalMath.Transfer(from.transform, to.transform);
            Matrix4x4 back = PortalMath.Transfer(to.transform, from.transform);
            Vector3 p = new Vector3(13f, 4f, 7f);
            Vector3 roundTrip = (back * there).MultiplyPoint3x4(p);
            Assert.Less((roundTrip - p).magnitude, 1e-3f);
        }

        /// <summary>La prueba de abertura respeta ancho, alto y margen.</summary>
        [Test]
        public void Abertura_LimitesYMargen()
        {
            var aperture = new Vector2(3.2f, 4.4f);
            Assert.IsTrue(PortalMath.InsideAperture(new Vector3(1.5f, 4f, 0f), aperture, 0f));
            Assert.IsFalse(PortalMath.InsideAperture(new Vector3(1.7f, 2f, 0f), aperture, 0f));
            Assert.IsFalse(PortalMath.InsideAperture(new Vector3(0f, 4.6f, 0f), aperture, 0.1f));
            Assert.IsTrue(PortalMath.InsideAperture(new Vector3(0f, -0.05f, 0f), aperture, 0.1f));
        }

        /// <summary>Los shaders de la puerta compilan y declaran UnityPerMaterial (SRP Batcher).</summary>
        [TestCaseSource(nameof(Shaders))]
        public void Shader_CompilaYUsaSrpBatcher(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.IsNotNull(shader, $"No se encontró {shaderName}.");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), $"{shaderName} tiene errores.");
            string path = AssetDatabase.GetAssetPath(shader);
            StringAssert.Contains("CBUFFER_START(UnityPerMaterial)", File.ReadAllText(path));
        }

        /// <summary>El prefab tiene jugabilidad y presentación, luz sin sombras y materiales como assets.</summary>
        [Test]
        public void Prefab_CompletoYSinMaterialesTemporales()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelGateBuilder.PrefabPath);
            Assume.That(prefab != null, "Ejecutar NostalgIA › Niveles › Construir prefab de puerta de nivel.");
            Assert.IsNotNull(prefab.GetComponent<LevelGate>());
            Assert.IsNotNull(prefab.GetComponent<LevelGateVisuals>());

            Light light = prefab.GetComponentInChildren<Light>(true);
            Assert.IsNotNull(light);
            Assert.AreEqual(LightType.Spot, light.type);
            Assert.AreEqual(LightShadows.None, light.shadows, "El foco de la puerta no debe calcular sombras.");

            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    Assert.IsTrue(material != null && AssetDatabase.Contains(material), $"{renderer.name} sin material de asset.");

            var visuals = new SerializedObject(prefab.GetComponent<LevelGateVisuals>());
            foreach (string field in new[] { "spillLight", "shaft", "dust", "surface", "arrival", "arrivalSurface", "flashVolume" })
                Assert.IsNotNull(visuals.FindProperty(field).objectReferenceValue, $"LevelGateVisuals.{field} sin asignar.");
        }

        /// <summary>
        /// El Tutorial tiene la puerta hacia Verano, la puerta dimensional anterior está apagada y la
        /// escena de Verano está en el build.
        /// </summary>
        [Test]
        public void Tutorial_ConectadoConVerano()
        {
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.path == SceneNames.Paths.VeranoFrutiger && s.enabled),
                          "La escena de Verano debe estar en la lista del build para cargarse en segundo plano.");
            WithScene(SceneNames.Paths.Tutorial, scene =>
            {
                LevelGate gate = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelGate>(true)).FirstOrDefault();
                Assert.IsNotNull(gate, "Falta la puerta de nivel en el Tutorial.");
                Assert.AreEqual(SceneNames.VeranoFrutiger, new SerializedObject(gate).FindProperty("destinationSceneName").stringValue);
                bool oldDoorActive = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                    .Any(c => c != null && c.GetType().Name == "TutorialDoor" && c.gameObject.activeInHierarchy);
                Assert.IsFalse(oldDoorActive, "La puerta dimensional anterior cargaría otra escena a la vez.");
            });
        }

        /// <summary>Verano tiene entrada de llegada y su jugador propio queda apagado al llegar por una puerta.</summary>
        [Test]
        public void Verano_EntradaYContenidoIndependiente()
        {
            WithScene(SceneNames.Paths.VeranoFrutiger, scene =>
            {
                GameObject[] roots = scene.GetRootGameObjects();
                Assert.IsTrue(roots.Any(r => r.GetComponentInChildren<LevelEntrance>(true) != null), "Falta LevelEntrance.");
                StandaloneLevelContent standalone = roots.Select(r => r.GetComponentInChildren<StandaloneLevelContent>(true)).FirstOrDefault(c => c != null);
                Assert.IsNotNull(standalone, "Falta StandaloneLevelContent.");
                Assert.IsNotNull(standalone.Content);
                Assert.IsFalse(standalone.Content.activeSelf, "El contenido independiente se guarda inactivo.");
                Assert.IsNotNull(standalone.Content.GetComponentInChildren<PlayerMotor>(true), "El jugador propio va dentro del grupo independiente.");
            });
        }

        /// <summary>Tutorial y Verano están separados más que el plano lejano de la cámara.</summary>
        [Test]
        public void Regiones_TutorialYVeranoNoSeVen()
        {
            Bounds tutorial = default;
            Bounds verano = default;
            WithScene(SceneNames.Paths.Tutorial, scene => Assert.IsTrue(LevelRegionTools.TryGetSceneBounds(scene, out tutorial)));
            WithScene(SceneNames.Paths.VeranoFrutiger, scene => Assert.IsTrue(LevelRegionTools.TryGetSceneBounds(scene, out verano)));
            float gap = LevelRegionTools.Gap(tutorial, verano);
            Assert.GreaterOrEqual(gap, LevelRegionTools.PlayerFarClip, $"Separación {gap:0} m.");
        }

        /// <summary>
        /// Abre una escena de forma aditiva, ejecuta la prueba y la cierra. Si la escena ya estaba
        /// abierta en el editor se usa tal cual y no se cierra.
        /// </summary>
        private static void WithScene(string path, System.Action<Scene> test)
        {
            Assume.That(File.Exists(path), $"No existe {path}.");
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                test(scene);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
