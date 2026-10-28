// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de editor (NostalgIA.Tests.EditMode)
// Archivo: Phase1VisualTests.cs
// Propósito: verificar los assets del acabado visual de la Fase 1: shaders,
// materiales, perfiles de postprocesado, glifos, iconos táctiles, partículas y
// sustitución del material de ProBuilder (tareas F1-B02, F1-B03, F1-B05).
// -----------------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using NostalgIA.Core;
using NostalgIA.EditorTools.Setup;
using NostalgIA.Input;
using NostalgIA.Input.TouchControls;
using NostalgIA.Presentation.CameraFX;
using NostalgIA.Presentation.UI;
using NostalgIA.Presentation.VFX;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace NostalgIA.Tests.EditMode
{
    /// <summary>Pruebas sobre los assets que produce <see cref="Phase1VisualBuilder"/>.</summary>
    public sealed class Phase1VisualTests
    {
        // ------------------------------------------------------------------ shaders
        /// <summary>Los shaders propios existen y compilan sin errores en la plataforma activa.</summary>
        [TestCase(Phase1VisualBuilder.Paths.StationKitShader)]
        [TestCase(Phase1VisualBuilder.Paths.DataParticleShader)]
        public void Shader_CompilaSinErrores(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.IsNotNull(shader, $"No se encontró el shader {shaderName}.");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), $"{shaderName} tiene errores de compilación.");
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                Assert.AreNotEqual("Error", message.severity.ToString(), $"{shaderName}: {message.message}");
        }

        /// <summary>
        /// Compatibilidad con el SRP Batcher: cada pase de los shaders declara su búfer
        /// <c>UnityPerMaterial</c> (en el propio pase o en el include compartido), y la biblioteca
        /// común no declara propiedades de material.
        /// </summary>
        [Test]
        public void Shaders_DeclaranUnityPerMaterial()
        {
            const string root = "Assets/_Project/Art/Shaders/";
            StringAssert.Contains("CBUFFER_START(UnityPerMaterial)", File.ReadAllText(root + "Environment/StationKitInput.hlsl"));
            StringAssert.Contains("CBUFFER_START(UnityPerMaterial)", File.ReadAllText(root + "Effects/NOS_DataParticle.shader"));

            string kit = File.ReadAllText(root + "Environment/NOS_StationKit.shader");
            int passes = Regex.Matches(kit, @"\bPass\s*\{").Count;
            int inputs = Regex.Matches(kit, "StationKitInput.hlsl").Count;
            Assert.AreEqual(passes, inputs, "Cada pase de NOS/StationKit debe incluir StationKitInput.hlsl (mismo CBUFFER en todos).");

            string common = File.ReadAllText(root + "Include/NostalgIA_Common.hlsl");
            StringAssert.DoesNotContain("CBUFFER_START", common, "La biblioteca común no debe declarar propiedades de material.");
        }

        // ------------------------------------------------------------------ materiales
        /// <summary>Los materiales de la fase existen como assets y usan el shader esperado.</summary>
        [TestCase(Phase1VisualBuilder.Paths.KitPrimavera, Phase1VisualBuilder.Paths.StationKitShader)]
        [TestCase(Phase1VisualBuilder.Paths.KitVerano, Phase1VisualBuilder.Paths.StationKitShader)]
        [TestCase(Phase1VisualBuilder.Paths.VfxDataSquare, Phase1VisualBuilder.Paths.DataParticleShader)]
        [TestCase(Phase1VisualBuilder.Paths.VfxDataSpark, Phase1VisualBuilder.Paths.DataParticleShader)]
        [TestCase(Phase1VisualBuilder.Paths.VfxDataBits, Phase1VisualBuilder.Paths.DataParticleShader)]
        public void Material_ExisteConSuShader(string path, string shaderName)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsNotNull(material, $"Falta {path}.");
            Assert.AreEqual(shaderName, material.shader.name);
        }

        /// <summary>El Tutorial ya no referencia el material gris de ProBuilder y sí el de Primavera (F1-B02).</summary>
        [Test]
        public void Tutorial_SinMaterialDeProBuilderYConMaterialDeEstacion()
        {
            string scene = File.ReadAllText(SceneNames.Paths.Tutorial);
            foreach (string guid in AssetDatabase.FindAssets("ProBuilderDefault t:Material", new[] { "Packages/com.unity.probuilder" }))
            {
                if (Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) != "ProBuilderDefault") continue;
                StringAssert.DoesNotContain(guid, scene, "El Tutorial aún usa el material por defecto de ProBuilder.");
            }
            string kitGuid = AssetDatabase.AssetPathToGUID(Phase1VisualBuilder.Paths.KitPrimavera);
            StringAssert.Contains(kitGuid, scene, "El Tutorial no usa M_Primavera_Kit.");
        }

        // ------------------------------------------------------------------ postprocesado
        /// <summary>VP_Primavera contiene los overrides del TDD §8.4 con sus valores.</summary>
        [Test]
        public void VolumePrimavera_TieneLosOverridesDelTDD()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Phase1VisualBuilder.Paths.VolumePrimavera);
            Assert.IsNotNull(profile, "Falta VP_Primavera.");
            Assert.IsTrue(profile.TryGet(out Tonemapping tonemap) && tonemap.mode.overrideState);
            Assert.AreEqual(TonemappingMode.Neutral, tonemap.mode.value);
            Assert.IsTrue(profile.TryGet(out Bloom bloom));
            Assert.AreEqual(0.9f, bloom.threshold.value, 1e-4f);
            Assert.AreEqual(0.6f, bloom.intensity.value, 1e-4f);
            Assert.IsTrue(profile.TryGet(out ColorAdjustments grading));
            Assert.AreEqual(20f, grading.contrast.value, 1e-4f);
            Assert.AreEqual(-10f, grading.saturation.value, 1e-4f);
            Assert.IsTrue(profile.TryGet(out Vignette vignette));
            Assert.AreEqual(0.2f, vignette.intensity.value, 1e-4f);

            string scene = File.ReadAllText(SceneNames.Paths.Tutorial);
            StringAssert.Contains(AssetDatabase.AssetPathToGUID(Phase1VisualBuilder.Paths.VolumePrimavera), scene,
                "El Tutorial no tiene el Volume global con VP_Primavera.");
        }

        /// <summary>VP_Feedback tiene viñeta y aberración cromática (TDD §8.4: 0,6).</summary>
        [Test]
        public void VolumeFeedback_TieneVinetaYAberracion()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Phase1VisualBuilder.Paths.VolumeFeedback);
            Assert.IsNotNull(profile, "Falta VP_Feedback.");
            Assert.IsTrue(profile.TryGet(out Vignette vignette) && vignette.color.overrideState);
            Assert.IsTrue(profile.TryGet(out ChromaticAberration aberration));
            Assert.AreEqual(0.6f, aberration.intensity.value, 1e-4f);
        }

        /// <summary>El asset de URP móvil no usa HDR (TDD §8.4).</summary>
        [Test]
        public void PipelineMovil_SinHDR()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Phase1VisualBuilder.Paths.MobilePipeline);
            Assert.IsNotNull(asset);
            Assert.IsFalse(asset.supportsHDR);
        }

        // ------------------------------------------------------------------ interfaz
        /// <summary>El juego de glifos cubre todas las acciones del mapa Gameplay en los tres esquemas.</summary>
        [Test]
        public void Glifos_CubrenTodasLasAccionesEnLosTresEsquemas()
        {
            var set = AssetDatabase.LoadAssetAtPath<InputGlyphSet>(Phase1VisualBuilder.Paths.GlyphSet);
            Assert.IsNotNull(set, "Falta InputGlyphSet_Default.");
            foreach (string action in InputActionNames.GameplayActions)
            {
                Assert.IsTrue(set.TryGet(action, out InputGlyphSet.Entry entry), $"Sin glifos para {action}.");
                Assert.IsFalse(string.IsNullOrEmpty(entry.displayName), $"{action}: sin nombre legible.");
                Assert.IsFalse(string.IsNullOrEmpty(entry.keyboardMouse), $"{action}: sin etiqueta de teclado.");
                Assert.IsFalse(string.IsNullOrEmpty(entry.gamepad), $"{action}: sin etiqueta de mando.");
                Assert.IsFalse(string.IsNullOrEmpty(entry.touch), $"{action}: sin etiqueta táctil.");
            }
        }

        /// <summary>Cada botón táctil muestra un icono y el pad conoce los iconos de ambas herramientas.</summary>
        [Test]
        public void ControlesTactiles_TienenIconos()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1Builder.Paths.TouchControls);
            Assert.IsNotNull(prefab);
            Transform safeArea = prefab.transform.Find("SafeArea");
            foreach (string button in new[] { "ToolPad", "JumpButton", "CrouchButton", "SwitchToolButton", "PauseButton" })
            {
                Transform icon = safeArea.Find(button + "/Icon");
                Assert.IsNotNull(icon, $"{button} no tiene icono.");
                Assert.IsNotNull(icon.GetComponent<Image>().sprite, $"{button}: icono sin sprite.");
                Assert.IsFalse(icon.GetComponent<Image>().raycastTarget, $"{button}: el icono no debe interceptar toques.");
            }

            var pad = safeArea.Find("ToolPad").GetComponent<TouchToolPad>();
            var so = new SerializedObject(pad);
            Assert.IsNotNull(so.FindProperty("vacuumIcon").objectReferenceValue);
            Assert.IsNotNull(so.FindProperty("grappleIcon").objectReferenceValue);
        }

        // ------------------------------------------------------------------ prefabs
        /// <summary>La cámara del jugador procesa volúmenes y la herramienta tiene el vórtice.</summary>
        [Test]
        public void Player_CamaraConPostprocesadoYVortice()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1Builder.Paths.Player);
            var cameraData = prefab.GetComponentInChildren<UniversalAdditionalCameraData>(true);
            Assert.IsNotNull(cameraData);
            Assert.IsTrue(cameraData.renderPostProcessing, "La cámara del jugador debe tener el postprocesado activo.");

            var vortex = prefab.GetComponentInChildren<SuctionVortexFx>(true);
            Assert.IsNotNull(vortex, "Falta SuctionVortexFx en la herramienta.");
            var ps = vortex.GetComponent<ParticleSystem>();
            Assert.LessOrEqual(ps.main.maxParticles, 60, "Presupuesto del vórtice (TDD §8.2): 60 partículas.");
            Assert.IsFalse(ps.main.playOnAwake);
            AssertParticleRenderer(ps);
        }

        /// <summary>GameRoot contiene el director de ráfagas, el volumen de retroalimentación y la pausa.</summary>
        [Test]
        public void GameRoot_TieneRafagasPostFXYPausa()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1Builder.Paths.GameRoot);
            var director = prefab.GetComponentInChildren<GameplayVfxDirector>(true);
            Assert.IsNotNull(director);
            var so = new SerializedObject(director);
            foreach (string field in new[] { "launchBurst", "anchorSparks", "collectBits" })
            {
                var ps = so.FindProperty(field).objectReferenceValue as ParticleSystem;
                Assert.IsNotNull(ps, $"GameplayVfxDirector.{field} sin asignar.");
                Assert.AreEqual(ParticleSystemSimulationSpace.World, ps.main.simulationSpace, $"{field}: las ráfagas deben simular en mundo.");
                Assert.IsFalse(ps.emission.enabled, $"{field}: solo debe emitir con Emit().");
                AssertParticleRenderer(ps);
            }

            var postFx = prefab.GetComponentInChildren<PostFXController>(true);
            Assert.IsNotNull(postFx);
            var volume = new SerializedObject(postFx).FindProperty("feedbackVolume").objectReferenceValue as Volume;
            Assert.IsNotNull(volume);
            Assert.IsTrue(volume.isGlobal);
            Assert.Greater(volume.priority, 0f, "El volumen de retroalimentación debe imponerse al de la Estación.");
            Assert.AreEqual(0f, volume.weight);

            var pause = prefab.GetComponentInChildren<PauseOverlayPresenter>(true);
            Assert.IsNotNull(pause, "Falta la capa de pausa.");
            Assert.IsFalse(pause.GetComponent<Canvas>().enabled, "La capa de pausa debe empezar oculta.");
            Assert.IsNotNull(new SerializedObject(pause).FindProperty("resumeButton").objectReferenceValue);
        }

        /// <summary>
        /// Un renderer de partículas propio usa NOS/DataParticle, no proyecta sombras y declara los
        /// canales que el shader espera (StableRandom.x en TEXCOORD0.z).
        /// </summary>
        private static void AssertParticleRenderer(ParticleSystem ps)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            Assert.IsNotNull(renderer.sharedMaterial, $"{ps.name}: sin material.");
            Assert.AreEqual(Phase1VisualBuilder.Paths.DataParticleShader, renderer.sharedMaterial.shader.name);
            Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode);
            var streams = new System.Collections.Generic.List<ParticleSystemVertexStream>();
            renderer.GetActiveVertexStreams(streams);
            CollectionAssert.Contains(streams, ParticleSystemVertexStream.StableRandomX, $"{ps.name}: falta StableRandomX.");
            CollectionAssert.Contains(streams, ParticleSystemVertexStream.UV, $"{ps.name}: falta UV.");
        }
    }
}
