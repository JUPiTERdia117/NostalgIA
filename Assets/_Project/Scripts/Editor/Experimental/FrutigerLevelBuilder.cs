// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: FrutigerLevelBuilder.cs
// Propósito: construye el Nivel 2 oficial (20_Verano_Frutiger — Estación Verano,
// estética Frutiger Aero) siguiendo el boceto de diseño, materiales y mallas
// compartidas, placeholders TEST_, ranuras de audio y enlace con el Tutorial.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NostalgIA.Core;
using NostalgIA.EditorTools.Levels;
using NostalgIA.EditorTools.Setup;
using NostalgIA.Gameplay.Level;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
using NostalgIA.Presentation.Audio;
using NostalgIA.Presentation.Environment;
using NostalgIA.Presentation.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NostalgIA.EditorTools.Experimental
{
    /// <summary>
    /// Constructor del Nivel 2 (Verano — Frutiger Aero). Genera la escena completa en su región
    /// propia a 3 km del Tutorial, conservando todos los materiales y mallas como assets en disco.
    /// </summary>
    public static class FrutigerLevelBuilder
    {
        private const string ExperimentalMenuRoot = "NostalgIA/Experimental/";
        private const string LevelsMenuRoot = "NostalgIA/Niveles/";
        public const string ScenePath = "Assets/_Project/Scenes/Stations/20_Verano_Frutiger.unity";
        public const string AutoBuildMarkerPath = "Temp/verano_level2_autobuild.once";

        /// <summary>
        /// Origen de la región del mundo que ocupa Verano: a 3 km en +X respecto al Tutorial para que
        /// ambas escenas puedan convivir durante el cruce de la puerta sin verse ni encimarse.
        /// </summary>
        public static readonly Vector3 RegionOrigin = new Vector3(3000f, 0f, 0f);

        private const string MaterialsRoot = "Assets/_Project/Art/Materials/Verano/Frutiger";
        private const string MeshesRoot = "Assets/_Project/Art/Meshes/Frutiger";
        public const string VolumePath = "Assets/_Project/Rendering/Volumes/VP_Verano.asset";
        public const string VolumePcPath = "Assets/_Project/Rendering/Volumes/VP_Verano_PC.asset";

        // Rutas de prefabs de jugabilidad y enemigos existentes en el proyecto.
        private const string StarPrefabPath = "Assets/_Project/Prefabs/Level/Star.prefab";
        private const string FlyingEmailPrefabPath = "Assets/_Project/Prefabs/Enemies/FlyingEmail.prefab";
        private const string HexbugPrefabPath = "Assets/_Project/Prefabs/Enemies/Hexbug.prefab";
        private const string HorsePrefabPath = "Assets/_Project/Prefabs/Enemies/Horse.prefab";
        private const string WinRarPrefabPath = "Assets/_Project/Prefabs/Enemies/WinRar.prefab";

        /// <summary>Shaders del kit Frutiger Aero verificados también por las pruebas de editor.</summary>
        public static readonly string[] ShaderNames =
        {
            "NOS/AeroSky", "NOS/AeroGloss", "NOS/AeroBubble", "NOS/AeroBubbleSprite", "NOS/AeroWater",
            "NOS/AeroGlass", "NOS/AeroRibbon", "NOS/AeroGodRay", "NOS/AeroRainbow", "NOS/AeroFoam", "NOS/AeroFish"
        };

        // Paleta de Verano (Guía de estilo §5.2) y acentos semánticos de lectura rápida.
        private static readonly Color SkyCyan = Hex(0x2EC5FF);
        private static readonly Color DeepBlue = Hex(0x0077D9);
        private static readonly Color Lime = Hex(0x7ED957);
        private static readonly Color Turquoise = Hex(0x3EE6D2);
        private static readonly Color Cloud = Hex(0xF4FBFF);
        private static readonly Color SunAccent = Hex(0xFFE27A);
        private static readonly Color GrappleBlue = Hex(0x4AD4FF);
        private static readonly Color BuildingAeroBlue = Hex(0x63DCFF);
        private static readonly Color MovingPurple = Hex(0xA63AFF);
        private static readonly Color HazardCoral = Hex(0xFF334B);
        private static readonly Color AbsorbableCyan = Hex(0x3FE0FF);
        private static readonly Color CheckpointGreen = Hex(0x38E54D);

        /// <summary>Dirección hacia el sol: 60° a la derecha del avance inicial y 42° de elevación.</summary>
        private static readonly Vector3 TowardSun = new Vector3(0.644f, 0.669f, 0.372f).normalized;

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly List<Checkpoint> Checkpoints = new List<Checkpoint>();
        private static Scene scene;
        private static Transform root;
        private static Transform safeGoalReturnAnchor;

        // ================================================================== auto-ejecución única al recargar el editor
        [InitializeOnLoadMethod]
        private static void CheckPendingAutoBuild()
        {
            if (!File.Exists(AutoBuildMarkerPath)) return;
            EditorApplication.delayCall += TryRunPendingAutoBuild;
        }

        private static void TryRunPendingAutoBuild()
        {
            if (!File.Exists(AutoBuildMarkerPath)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRunPendingAutoBuild;
                return;
            }

            File.Delete(AutoBuildMarkerPath);
            BuildAndConnectTutorial();
        }

        // ================================================================== menús
        [MenuItem(LevelsMenuRoot + "Construir Nivel 2 — Verano (Frutiger Aero)", priority = 302)]
        public static void BuildMenu() => Build();

        [MenuItem(LevelsMenuRoot + "Construir Nivel 2 y Conectar Tutorial", priority = 303)]
        public static void BuildAndConnectTutorial()
        {
            Build();
            EnsureTutorialConnected();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[Niveles] Nivel 2 (20_Verano_Frutiger) construido y conectado con Tutorial.");
        }

        [MenuItem(ExperimentalMenuRoot + "Construir nivel Frutiger Aero", priority = 200)]
        public static void Build()
        {
            if (SceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            foreach (string name in ShaderNames)
            {
                if (Shader.Find(name) == null)
                {
                    Debug.LogError($"[Frutiger] Falta el shader {name} (¿errores de compilación?).");
                    return;
                }
            }

            Phase1Builder.EnsureFolder(MaterialsRoot);
            Phase1Builder.EnsureFolder(MeshesRoot);
            CreateMaterials();
            CreateVolumes();

            // Me aseguro de que el prefab de la puerta exista antes de colocar la meta.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LevelGateBuilder.PrefabPath) == null)
            {
                LevelGateBuilder.BuildPrefab();
            }

            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            root = new GameObject("Verano_Frutiger").transform;
            safeGoalReturnAnchor = null;

            BuildAtmosphereAndProbes();
            BuildInfiniteShallowOcean();
            BuildLevel2Layout();
            BuildDecor();
            BuildAudioSystem();
            BuildPlayerAndSystems();
            BuildEntrance();
            LevelRegionTools.ShiftSceneRoots(scene, RegionOrigin);

            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            LevelGateBuilder.EnsureInBuild(ScenePath);
            LevelGateBuilder.EnsureInBuild(SceneNames.Paths.OtherDimension);
            Debug.Log("[Frutiger] Nivel 2 (Verano) construido en " + ScenePath);
        }

        /// <summary>
        /// Verifica que <c>Tutorial.unity</c> tenga activa <c>LevelGate_Verano</c> apuntando a este nivel
        /// y que <c>OtherDimension.unity</c> tenga un <see cref="LevelEntrance"/> limpio para recibir
        /// la puerta de meta del Nivel 2.
        /// </summary>
        private static void EnsureTutorialConnected()
        {
            if (File.Exists(SceneNames.Paths.Tutorial))
            {
                Scene tutorial = EditorSceneManager.OpenScene(SceneNames.Paths.Tutorial, OpenSceneMode.Single);
                LevelGate gate = tutorial.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<LevelGate>(true))
                    .FirstOrDefault(g => g.gameObject.name == LevelGateBuilder.TutorialGateName);

                bool oldDoorActive = tutorial.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                    .Any(c => c != null && c.GetType().Name == "TutorialDoor" && c.gameObject.activeInHierarchy);

                if (gate == null || oldDoorActive)
                {
                    LevelGateBuilder.ConnectTutorial();
                }
                else
                {
                    gate.Configure(SceneNames.VeranoFrutiger, SceneNames.Paths.VeranoFrutiger, LevelGateBuilder.Aperture);
                    EditorSceneManager.MarkSceneDirty(tutorial);
                    EditorSceneManager.SaveScene(tutorial);
                }
                LevelGateBuilder.EnsureInBuild(SceneNames.Paths.Tutorial);
            }

            if (File.Exists(SceneNames.Paths.OtherDimension))
            {
                Scene otherDim = EditorSceneManager.OpenScene(SceneNames.Paths.OtherDimension, OpenSceneMode.Single);
                bool hasEntrance = otherDim.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<LevelEntrance>(true))
                    .Any();
                if (!hasEntrance)
                {
                    var entranceGo = new GameObject("LevelEntrance");
                    entranceGo.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, 4.0f), Quaternion.identity);
                    entranceGo.AddComponent<LevelEntrance>().Configure(string.Empty, LevelGateBuilder.Aperture);
                    EditorSceneManager.MarkSceneDirty(otherDim);
                    EditorSceneManager.SaveScene(otherDim);
                }
                LevelGateBuilder.EnsureInBuild(SceneNames.Paths.OtherDimension);
            }

            LevelGateBuilder.EnsureInBuild(ScenePath);
        }

        /// <summary>Diagnóstico de shaders del kit Frutiger Aero en la plataforma actual.</summary>
        [MenuItem(ExperimentalMenuRoot + "Diagnóstico de shaders", priority = 201)]
        public static void ShaderReport()
        {
            var report = new System.Text.StringBuilder();
            foreach (string name in ShaderNames)
            {
                Shader shader = Shader.Find(name);
                if (shader == null) { report.AppendLine($"{name}: NO ENCONTRADO"); continue; }
                report.AppendLine($"{name}: error={ShaderUtil.ShaderHasError(shader)} soportado={shader.isSupported}");
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                    report.AppendLine($"   [{message.severity}] {message.message} ({message.file}:{message.line}) {message.messageDetails}");
            }
            File.WriteAllText("Temp/frutiger_shaders.txt", report.ToString());
            Debug.Log("[Frutiger] Diagnóstico de shaders:\n" + report);
        }

        /// <summary>Puntos de cámara actualizados al recorrido oficial del Nivel 2.</summary>
        private static readonly (string name, Vector3 eye, Vector3 target)[] Views =
        {
            ("01_isla_inicio", new Vector3(2f, 6.2f, -10f), new Vector3(0f, 3.5f, 35f)),
            ("02_bifurcacion_islas", new Vector3(0f, 11f, 14f), new Vector3(0f, 4f, 42f)),
            ("03_isla_derecha_aspas", new Vector3(6f, 8.5f, 22f), new Vector3(17f, 3.5f, 36f)),
            ("04_plataformas_moviles", new Vector3(0f, 13f, 48f), new Vector3(0f, 11f, 82f)),
            ("05_lote_edificios", new Vector3(-28f, 26f, 76f), new Vector3(2f, 15f, 120f)),
            ("06_arena_central_spawner", new Vector3(-12f, 22f, 102f), new Vector3(2f, 16f, 118f)),
            ("07_plataforma_final_y_meta", new Vector3(-14f, 27f, 150f), new Vector3(0f, 14f, 178f)),
            ("08_agua_poco_profunda", new Vector3(-14f, 3.5f, 102f), new Vector3(6f, 2f, 122f)),
            ("09_cenital_completo", new Vector3(0f, 250f, 88f), new Vector3(0f, 0f, 88f))
        };

        [MenuItem(ExperimentalMenuRoot + "Capturar vistas del nivel", priority = 202)]
        public static void CaptureViews()
        {
            var go = new GameObject("CamaraCaptura") { hideFlags = HideFlags.HideAndDontSave };
            Camera cam = go.AddComponent<Camera>();
            UniversalAdditionalCameraData data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cam.fieldOfView = 70f;
            cam.farClipPlane = 1000f;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                foreach ((string name, Vector3 eye, Vector3 target) in Views)
                {
                    go.transform.SetPositionAndRotation(eye + RegionOrigin, Quaternion.LookRotation(target - eye));
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes($"Temp/frutiger_view_{name}.png", tex.EncodeToPNG());
                }
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            Debug.Log("[Frutiger] Vistas guardadas en Temp/frutiger_view_*.png");
        }

        // ================================================================== materiales (todos como assets)
        private static void CreateMaterials()
        {
            Materials.Clear();

            Material sky = Mat("M_Frutiger_Sky", "NOS/AeroSky");
            sky.SetColor("_ZenithColor", new Color(0.11f, 0.56f, 0.95f));
            sky.SetColor("_HorizonColor", new Color(0.74f, 0.94f, 1f));
            sky.SetColor("_GroundColor", new Color(0.22f, 0.66f, 0.84f));

            // Islas naturales (No enganche): orilla de arena cálida junto al agua y pasto lima arriba.
            Gloss("M_Frutiger_Island", Lime, Hex(0xFFE9A8), new Vector2(0.15f, 1.75f), Hex(0xEBD9A6), 0.55f, 0.35f, 0.35f, true, true,
                speckle: new Color(1f, 0.97f, 0.78f, 1f), speckleAmount: 0.18f).SetFloat("_SpeckleDensity", 3.5f);

            Material skyIsland = Gloss("M_Frutiger_SkyIsland", Lime, Hex(0xF2E3BE), new Vector2(15f, 18.5f), Hex(0xF4E6C4), 0.85f, 0.45f, 0.5f, true, false,
                speckle: new Color(1f, 0.95f, 0.6f, 1f), speckleAmount: 0.18f);
            skyIsland.SetFloat("_SpeckleDensity", 3.5f);
            skyIsland.SetFloat("_Wrap", 0.45f);

            // Arena poco profunda (-0,38 m): ajusté el rango de altura cerca de 0 para que las cáusticas
            // brillen claras a través de los 38 cm de agua cristalina.
            Gloss("M_Frutiger_Seabed", Hex(0xFAF0CE), Hex(0xBCEAD8), new Vector2(-1.2f, 0.15f), Hex(0xF7EBC4), 0f, 0.15f, 0.12f, false, true);

            // Superficie blanca brillante (No enganche, como la plataforma final del WinRAR).
            Material platformWhite = Gloss("M_Frutiger_Platform", Cloud, Cloud, new Vector2(0f, 1f), Hex(0xAEC8DE), 0.85f, 0.82f, 1.5f, false, false);
            platformWhite.SetFloat("_SlopeSharpness", 9f);
            platformWhite.SetFloat("_ClearcoatPower", 900f);

            // Plataformas y esferas azules con enganche (color azul claro del boceto = capa Grapple).
            Material grapplePad = Gloss("M_Frutiger_GrapplePad", GrappleBlue, Hex(0x1697EC), new Vector2(0f, 4f), Hex(0x0D7AD4), 0.9f, 0.88f, 1.8f, false, false);
            grapplePad.SetFloat("_SlopeSharpness", 8f);
            grapplePad.SetColor("_EmissionColor", GrappleBlue * 0.22f);

            // Esferas de enganche aéreas: azul brillante con pulso suave para distinguirse en el cielo.
            Material anchor = Gloss("M_Frutiger_Anchor", Hex(0x5CE1FF), Hex(0x1499FF), new Vector2(0f, 1f), Hex(0x5CE1FF), 0f, 0.9f, 1.9f, false, false);
            anchor.SetColor("_EmissionColor", Hex(0x3AD0FF) * 0.65f);
            anchor.SetFloat("_EmissionPulse", 0.55f);

            // Edificios temporales TEST_ de Frutiger Aero: bloque arquitectónico cian brillante (capa Grapple).
            Material buildingTest = Gloss("M_Frutiger_BuildingTest", BuildingAeroBlue, Hex(0x188FE0), new Vector2(0f, 20f), Hex(0x3BC4F7), 0.78f, 0.86f, 1.75f, false, false);
            buildingTest.SetFloat("_Wrap", 0.35f);
            buildingTest.SetFloat("_RimStrength", 0.55f);
            buildingTest.SetColor("_RimColor", Color.white);

            // Plataformas móviles (contorno morado en el diagrama de diseño).
            Material movingMat = Gloss("M_Frutiger_MovingPlatform", Hex(0xF3EAFF), Hex(0xD8B8FF), new Vector2(0f, 2f), MovingPurple, 1f, 0.88f, 1.7f, false, false);
            movingMat.SetFloat("_SlopeSharpness", 8.5f);
            movingMat.SetColor("_EmissionColor", MovingPurple * 0.32f);

            // Obstáculos dañinos y sellos bloqueadores (rojo coral brillante).
            Material hazardMat = Gloss("M_Frutiger_HazardRed", HazardCoral, Hex(0xC81432), new Vector2(0f, 2f), Hex(0xFF6B7A), 0.6f, 0.85f, 1.5f, false, false);
            hazardMat.SetColor("_EmissionColor", HazardCoral * 0.35f);
            hazardMat.SetFloat("_EmissionPulse", 0.9f);

            Gloss("M_Frutiger_Canopy", Lime, Hex(0x3EB86A), new Vector2(0f, 14f), Lime, 0f, 0.7f, 1.2f, false, false);
            Gloss("M_Frutiger_CanopyAqua", Turquoise, Hex(0x1FA9B8), new Vector2(0f, 14f), Turquoise, 0f, 0.7f, 1.2f, false, false);
            Gloss("M_Frutiger_Trunk", Hex(0xF2E6D2), Hex(0xD9B98C), new Vector2(0f, 6f), Hex(0xD9B98C), 0f, 0.4f, 0.4f, false, false);

            Material cloud = Gloss("M_Frutiger_Cloud", Color.white, Hex(0xBFE3FF), new Vector2(28f, 40f), Color.white, 0f, 0.05f, 0.1f, false, false);
            cloud.SetFloat("_Wrap", 0.6f);
            cloud.SetFloat("_RimStrength", 0.9f);
            cloud.SetFloat("_RimPower", 2f);
            cloud.SetColor("_RimColor", Color.white);

            Material chrome = Gloss("M_Frutiger_Chrome", Color.white, Hex(0xBFD9EE), new Vector2(0f, 8f), Color.white, 0f, 1f, 2.5f, false, false);
            chrome.SetFloat("_RimStrength", 1.1f);

            // Objetos absorbibles (verde/turquesa luminoso legible sobre arena y azoteas).
            Material absorbable = Gloss("M_Frutiger_Absorbable", Hex(0x52F296), AbsorbableCyan, new Vector2(0f, 1f), AbsorbableCyan, 0f, 0.92f, 1.7f, false, false);
            absorbable.SetColor("_EmissionColor", Hex(0x3FF5B8) * 0.35f);

            Material checkpoint = Gloss("M_Frutiger_Checkpoint", CheckpointGreen, CheckpointGreen, new Vector2(0f, 1f), CheckpointGreen, 0f, 0.8f, 1.2f, false, false);
            checkpoint.SetColor("_EmissionColor", CheckpointGreen * 0.6f);
            checkpoint.SetFloat("_EmissionPulse", 0.8f);

            // Agua poco profunda: olas suaves de baja amplitud para que la arena caminable nunca tape los pies de más.
            Material water = Mat("M_Frutiger_Water", "NOS/AeroWater");
            water.SetColor("_ShallowColor", Turquoise);
            water.SetColor("_DeepColor", DeepBlue);
            water.SetVector("_WaveA", new Vector4(1f, 0.3f, 0.045f, 18f));
            water.SetVector("_WaveB", new Vector4(-0.4f, 1f, 0.035f, 12f));
            water.SetVector("_WaveC", new Vector4(0.7f, -0.6f, 0.025f, 9f));

            Material bubble = Mat("M_Frutiger_Bubble", "NOS/AeroBubble");
            bubble.enableInstancing = true;
            bubble.SetFloat("_Iridescence", 1.5f);
            bubble.SetFloat("_Impostor", 1f);
            SetKeyword(bubble, "_IMPOSTOR", true);

            Material hero = Mat("M_Frutiger_BubbleHero", "NOS/AeroBubble");
            hero.enableInstancing = true;
            hero.SetFloat("_DriftAmplitude", 2.5f);
            hero.SetFloat("_DriftSpeed", 0.18f);
            hero.SetFloat("_FilmThickness", 0.75f);
            hero.SetFloat("_Iridescence", 1.6f);
            hero.SetFloat("_WobbleAmount", 0.015f);

            Material bounce = Mat("M_Frutiger_BounceBubble", "NOS/AeroBubble");
            bounce.SetFloat("_DriftAmplitude", 0f);
            bounce.SetFloat("_WobbleAmount", 0.045f);
            bounce.SetFloat("_EdgeAlpha", 0.92f);
            bounce.SetFloat("_BaseAlpha", 0.1f);
            bounce.SetColor("_Tint", new Color(0.92f, 0.98f, 1f));
            bounce.SetFloat("_Iridescence", 2.1f);

            Material pad = Gloss("M_Frutiger_BouncePad", SunAccent, SunAccent, new Vector2(0f, 1f), SunAccent, 0f, 0.8f, 1.4f, false, false);
            pad.SetColor("_EmissionColor", SunAccent * 0.5f);
            pad.SetFloat("_EmissionPulse", 1.2f);

            Mat("M_Frutiger_BubbleSprite", "NOS/AeroBubbleSprite");

            Material glass = Mat("M_Frutiger_Glass", "NOS/AeroGlass");
            glass.SetColor("_Tint", new Color(0.55f, 0.85f, 1f));

            Material marble = Mat("M_Frutiger_GlassMarble", "NOS/AeroGlass");
            marble.SetColor("_Tint", new Color(0.6f, 1f, 0.75f));
            marble.SetFloat("_Opacity", 0.3f);
            marble.SetFloat("_TopGloss", 0.15f);

            Ribbon("M_Frutiger_RibbonA", new Color(0.1f, 0.85f, 1f), Lime);
            Ribbon("M_Frutiger_RibbonB", new Color(0.7f, 0.95f, 1f), SkyCyan);
            Ribbon("M_Frutiger_RibbonC", Lime, SunAccent);
            Mat("M_Frutiger_GodRay", "NOS/AeroGodRay");
            Mat("M_Frutiger_Rainbow", "NOS/AeroRainbow");
            Mat("M_Frutiger_Foam", "NOS/AeroFoam");
            Mat("M_Frutiger_Fish", "NOS/AeroFish").enableInstancing = true;

            foreach (Material m in Materials.Values) EditorUtility.SetDirty(m);
        }

        private static Material Gloss(string name, Color top, Color bottom, Vector2 heightRange, Color side, float sideMix,
                                      float gloss, float clearcoat, bool flat, bool caustics,
                                      Color speckle = default, float speckleAmount = 0f)
        {
            Material m = Mat(name, "NOS/AeroGloss");
            m.SetColor("_ColorTop", top);
            m.SetColor("_ColorBottom", bottom);
            m.SetVector("_HeightRange", new Vector4(heightRange.x, heightRange.y, 0f, 0f));
            side.a = sideMix;
            m.SetColor("_SideColor", side);
            m.SetColor("_SpeckleColor", speckle);
            m.SetFloat("_SpeckleAmount", speckleAmount);
            m.SetFloat("_Gloss", gloss);
            m.SetFloat("_ClearcoatStrength", clearcoat);
            m.SetFloat("_FlatShading", flat ? 1f : 0f);
            m.SetFloat("_Caustics", caustics ? 1f : 0f);
            SetKeyword(m, "_FLAT_SHADING", flat);
            SetKeyword(m, "_CAUSTICS", caustics);
            m.enableInstancing = true;
            return m;
        }

        private static void Ribbon(string name, Color a, Color b)
        {
            Material m = Mat(name, "NOS/AeroRibbon");
            m.SetColor("_ColorA", a);
            m.SetColor("_ColorB", b);
            m.SetFloat("_Intensity", 0.75f);
            m.SetFloat("_Filaments", 0.9f);
        }

        private static Material Mat(string name, string shaderName)
        {
            string path = $"{MaterialsRoot}/{name}.mat";
            Shader shader = Shader.Find(shaderName);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }
            Materials[name] = m;
            return m;
        }

        private static void SetKeyword(Material m, string keyword, bool on)
        {
            if (on) m.EnableKeyword(keyword);
            else m.DisableKeyword(keyword);
        }

        // ================================================================== postprocesado
        private static void CreateVolumes()
        {
            VolumeProfile profile = LoadOrCreateProfile(VolumePath);
            Override<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
            ColorAdjustments grading = Override<ColorAdjustments>(profile);
            grading.postExposure.Override(0.05f);
            grading.contrast.Override(10f);
            grading.saturation.Override(18f);
            Bloom bloom = Override<Bloom>(profile);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.75f);
            bloom.tint.Override(new Color(0.9f, 1f, 1f));
            bloom.highQualityFiltering.Override(false);
            Vignette vignette = Override<Vignette>(profile);
            vignette.color.Override(new Color(0f, 0.15f, 0.3f));
            vignette.intensity.Override(0.12f);
            EditorUtility.SetDirty(profile);

            VolumeProfile pc = LoadOrCreateProfile(VolumePcPath);
            Override<ScreenSpaceLensFlare>(pc).intensity.Override(0.6f);
            EditorUtility.SetDirty(pc);
        }

        private static VolumeProfile LoadOrCreateProfile(string path)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static T Override<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
            {
                component.active = true;
                return component;
            }
            component = profile.Add<T>(false);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ================================================================== ambiente e iluminación lista para hornear
        private static void BuildAtmosphereAndProbes()
        {
            Transform group = Group("Ambiente");

            var sunGo = new GameObject("Sol");
            sunGo.transform.SetParent(group, false);
            sunGo.transform.rotation = Quaternion.LookRotation(-TowardSun);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.useColorTemperature = true;
            sun.colorTemperature = 5600f;
            sun.color = new Color(1f, 0.98f, 0.92f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            // Dejo el sol en Mixed para que los objetos estáticos reciban lightmaps horneados y los
            // objetos dinámicos (enemigos, herramientas) se iluminen mediante Light Probes sin ray tracing.
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            RenderSettings.sun = sun;

            RenderSettings.skybox = Materials["M_Frutiger_Sky"];
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.82f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.86f, 0.92f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.68f, 0.64f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.94f, 1f);
            RenderSettings.fogStartDistance = 95f;
            RenderSettings.fogEndDistance = 420f;

            Volume volume = new GameObject("Volume_Verano").AddComponent<Volume>();
            volume.transform.SetParent(group, false);
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);

            Volume pcVolume = new GameObject("Volume_Verano_PC").AddComponent<Volume>();
            pcVolume.transform.SetParent(group, false);
            pcVolume.isGlobal = true;
            pcVolume.priority = 1f;
            pcVolume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePcPath);

            FrutigerAtmosphere atmosphere = new GameObject("FrutigerAtmosphere").AddComponent<FrutigerAtmosphere>();
            atmosphere.transform.SetParent(group, false);
            var so = new SerializedObject(atmosphere);
            so.FindProperty("skyMaterial").objectReferenceValue = Materials["M_Frutiger_Sky"];
            SerializedProperty desktop = so.FindProperty("desktopOnly");
            desktop.arraySize = 1;
            desktop.GetArrayElementAtIndex(0).objectReferenceValue = pcVolume.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            atmosphere.Apply();

            // Sonda de reflejos horneada (cero reflejos en tiempo real ni ray tracing).
            var reflGo = new GameObject("ReflectionProbe_Verano");
            reflGo.transform.SetParent(group, false);
            reflGo.transform.position = new Vector3(0f, 18f, 95f);
            ReflectionProbe refl = reflGo.AddComponent<ReflectionProbe>();
            refl.mode = ReflectionProbeMode.Baked;
            refl.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            refl.boxProjection = false;
            refl.size = new Vector3(340f, 90f, 340f);
            refl.resolution = 128;
            refl.hdr = true;

            // Red de sondas de luz en dos capas de altura (sobre el agua/islas y sobre las azoteas)
            // para que el dron, los orbes y los enemigos reciban rebote turquesa-solar precalculado.
            var probesGo = new GameObject("LightProbeGroup_Verano");
            probesGo.transform.SetParent(group, false);
            probesGo.transform.position = Vector3.zero;
            LightProbeGroup probeGroup = probesGo.AddComponent<LightProbeGroup>();
            var probePositions = new List<Vector3>();
            float[] xs = { -26f, -10f, 0f, 10f, 26f };
            float[] zs = { -5f, 25f, 55f, 88f, 118f, 148f, 178f };
            float[] ys = { 2.2f, 12.5f, 23f };
            foreach (float z in zs)
                foreach (float x in xs)
                    foreach (float y in ys)
                        probePositions.Add(new Vector3(x, y, z));
            probeGroup.probePositions = probePositions.ToArray();
        }

        // ================================================================== océano infinito poco profundo (caminable)
        private static void BuildInfiniteShallowOcean()
        {
            Transform group = Group("Oceano_PocoProfundo");

            // Malla detallada de olas cerca de las islas y edificios.
            Mesh grid = SaveMesh(FrutigerMeshes.WaterGrid("FR_WaterGrid", 580f, 160), false);
            GameObject waterNear = Renderer("Agua_Cercana", group, grid, Materials["M_Frutiger_Water"], new Vector3(0f, 0f, 90f), Vector3.one, false);
            waterNear.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Falda exterior hasta cubrir por completo la niebla (420 m), dando sensación de océano
            // infinito en 360° sin acercarse a menos de 2,2 km de la región del Tutorial.
            Mesh horizonGrid = SaveMesh(FrutigerMeshes.WaterGrid("FR_WaterHorizon", 1100f, 48), false);
            GameObject waterFar = Renderer("Agua_HorizonteInfinito", group, horizonGrid, Materials["M_Frutiger_Water"], new Vector3(0f, -0.04f, 90f), Vector3.one, false);
            waterFar.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Lecho marino poco profundo (-0,38 m): el jugador camina con el agua a los tobillos sin peligro.
            Mesh cube = Builtin(PrimitiveType.Cube);
            GameObject seabed = Renderer("LechoMarino_Caminable", group, cube, Materials["M_Frutiger_Seabed"],
                new Vector3(0f, -0.88f, 90f), new Vector3(920f, 1f, 920f), false);
            seabed.AddComponent<BoxCollider>();
            MarkStaticBaked(seabed);

            // Colchón de entrada al agua en y = 0: amortigua caídas desde cualquier edificio para que
            // saltar o caer al agua jamás active muerte por velocidad vertical.
            var cushionGo = new GameObject("AmortiguadorCaida_Agua");
            cushionGo.transform.SetParent(group, false);
            cushionGo.transform.position = new Vector3(0f, 0.1f, 90f);
            cushionGo.layer = Layers.Trigger;
            BoxCollider cushionBox = cushionGo.AddComponent<BoxCollider>();
            cushionBox.isTrigger = true;
            cushionBox.size = new Vector3(920f, 1.4f, 920f);
            cushionGo.AddComponent<WaterFallCushion>();

            // Red de seguridad muy profunda (-45 m) solo por si algún objeto o jugador saliera de los
            // 920 m del mapa; nunca se toca al caminar por el agua poco profunda.
            var outOfBoundsKill = new GameObject("KillVolume_FueraDeLimites");
            outOfBoundsKill.transform.SetParent(group, false);
            outOfBoundsKill.transform.position = new Vector3(0f, -45f, 90f);
            outOfBoundsKill.layer = Layers.Trigger;
            BoxCollider killBox = outOfBoundsKill.AddComponent<BoxCollider>();
            killBox.isTrigger = true;
            killBox.size = new Vector3(1300f, 6f, 1300f);
            outOfBoundsKill.AddComponent<KillVolume>();
        }

        // ================================================================== layout estricto del Nivel 2
        private static void BuildLevel2Layout()
        {
            Checkpoints.Clear();
            Transform route = Group("Recorrido_Nivel2");
            Mesh foamRing = SaveMesh(FrutigerMeshes.FoamRing("FR_FoamRing"), false);

            // ------------------------------------------------------------------
            // ZONA 1: LAS TRES ISLAS Y PLATAFORMAS DE SALTO
            // (Con el jugador mirando a +Z: -X queda a la IZQUIERDA y +X a la DERECHA)
            // ------------------------------------------------------------------
            Transform islandsGroup = ChildGroup(route, "01_Islas");

            // Isla 1 (Inicio): elongada ligeramente como en el boceto, superficie No enganche.
            GameObject islaInicio = Island(islandsGroup, "Isla_01_Inicio", new Vector3(2f, 0f, 4f),
                radius: 13.5f, top: 2.1f, seed: 101, foamRing: foamRing, scaleXZ: new Vector2(1.0f, 1.22f), yaw: -14f);

            Vector3 startSpawn = Ground(2.8f, -2.2f);
            AddCheckpoint(islandsGroup, startSpawn, 0f, "Isla Inicio");

            // Punto seguro al que regresa el jugador al completar la puerta de meta mientras el Nivel 3 sigue pendiente.
            var returnGo = new GameObject("PuntoRetorno_MetaTemporal");
            returnGo.transform.SetParent(islandsGroup, false);
            returnGo.transform.SetPositionAndRotation(startSpawn + Vector3.up * 0.2f, Quaternion.identity);
            safeGoalReturnAnchor = returnGo.transform;

            // Marco decorativo alrededor de la Puerta de Inicio (donde aterriza LevelGate_Verano desde el Tutorial).
            BuildDecorativeStartFrame(islandsGroup, Ground(2.8f, -6.2f), Quaternion.identity);

            // 3 objetos absorbibles iniciales en racimo sobre la Isla 1 (los 3 puntos verdes del boceto).
            AddAbsorbableOrb(islandsGroup, "Objeto_Inicio_1", Ground(1.1f, 3.8f) + Vector3.up * 0.55f, 0.44f);
            AddAbsorbableOrb(islandsGroup, "Objeto_Inicio_2", Ground(3.3f, 4.6f) + Vector3.up * 0.55f, 0.44f);
            AddAbsorbableOrb(islandsGroup, "Objeto_Inicio_3", Ground(2.0f, 6.4f) + Vector3.up * 0.55f, 0.44f);

            // Dos pequeñas plataformas laterales junto a la Isla 1 (dibujadas abajo a la izquierda de la isla inicial).
            GrapplePad(islandsGroup, "Plataforma_InicioLateral_A", new Vector3(13.5f, 1.3f, -1.5f), new Vector3(4.2f, 0.65f, 4.2f));
            GrapplePad(islandsGroup, "Plataforma_InicioLateral_B", new Vector3(11.8f, 1.7f, 10.5f), new Vector3(4.2f, 0.65f, 4.2f));

            // Plataformas de salto entre la Isla 1 y las dos islas de ruta + esfera de enganche de apoyo.
            GrapplePad(islandsGroup, "Plataforma_PasoIzq_1", new Vector3(-5.8f, 2.5f, 18.5f), new Vector3(4.6f, 0.7f, 4.6f));
            GrapplePad(islandsGroup, "Plataforma_PasoIzq_2", new Vector3(-11.2f, 3.0f, 24.8f), new Vector3(4.6f, 0.7f, 4.6f));
            GrapplePad(islandsGroup, "Plataforma_PasoCentro", new Vector3(0.5f, 2.8f, 25.5f), new Vector3(4.8f, 0.7f, 4.8f));
            GrapplePad(islandsGroup, "Plataforma_PasoDer_1", new Vector3(8.8f, 2.5f, 20.5f), new Vector3(4.6f, 0.7f, 4.6f));
            AddGrappleSphere(islandsGroup, "EsferaEnganche_Islas_1", new Vector3(-8.2f, 7.2f, 22f), 1.7f);

            // Isla Izquierda (-X): enemigo volador (Spam Mail) + enemigo terrestre (Bug), superficie No enganche.
            GameObject islaIzq = Island(islandsGroup, "Isla_02_Izquierda", new Vector3(-16.5f, 0f, 36.5f),
                radius: 12.2f, top: 3.4f, seed: 204, foamRing: foamRing, scaleXZ: new Vector2(1.05f, 1.05f), yaw: 8f);
            SpawnEnemy(islandsGroup, FlyingEmailPrefabPath, "Enemigo_SpamMail_IslaIzq", new Vector3(-18.2f, 7.0f, 38.0f), Quaternion.Euler(0f, 160f, 0f));
            GameObject bugIslaIzq = SpawnEnemy(islandsGroup, HexbugPrefabPath, "Enemigo_Bug_IslaIzq", Ground(-14.5f, 35.5f) + Vector3.up * 0.4f, Quaternion.Euler(0f, 180f, 0f));
            ConstrainToSurface(bugIslaIzq, islaIzq.transform, new Vector2(7.5f, 7.5f));
            // Dos orbes extra en la orilla de Isla Izquierda por si el jugador gastó los 3 primeros.
            AddAbsorbableOrb(islandsGroup, "Objeto_IslaIzq_1", Ground(-21.0f, 31.5f) + Vector3.up * 0.55f, 0.42f);
            AddAbsorbableOrb(islandsGroup, "Objeto_IslaIzq_2", Ground(-19.5f, 42.0f) + Vector3.up * 0.55f, 0.42f);

            // Isla Derecha (+X): aspas giratorias tipo circuito de obstáculos que hacen daño al tocar al jugador.
            Island(islandsGroup, "Isla_03_Derecha", new Vector3(16.0f, 0f, 36.5f),
                radius: 12.8f, top: 3.2f, seed: 309, foamRing: foamRing, scaleXZ: new Vector2(1.0f, 1.15f), yaw: 6f);
            BuildTestRotatingSpinner(islandsGroup, "TEST_AspasGiratorias_IslaDer", Ground(16.0f, 36.5f),
                bladeRadius: 9.2f, speedDegPerSec: 66f, fourBlades: true);

            // ------------------------------------------------------------------
            // ZONA 2: DOS PLATAFORMAS FLOTANTES + DOS PLATAFORMAS MÓVILES + ESFERAS
            // ------------------------------------------------------------------
            Transform floatingGroup = ChildGroup(route, "02_Plataformas_y_Moviles");

            // Escalones de salida desde las islas hacia las dos plataformas flotantes principales.
            GrapplePad(floatingGroup, "PlataformaFlotante_SalidaIzq", new Vector3(-13.5f, 4.5f, 51.0f), new Vector3(4.8f, 0.8f, 4.8f));
            GrapplePad(floatingGroup, "PlataformaFlotante_SalidaDer", new Vector3(14.5f, 4.3f, 51.0f), new Vector3(4.8f, 0.8f, 4.8f));

            // Las dos plataformas flotantes estáticas posteriores a las islas (azules con enganche).
            GrapplePad(floatingGroup, "PlataformaFlotante_Izq", new Vector3(-6.8f, 5.8f, 56.5f), new Vector3(5.4f, 0.9f, 5.4f));
            GrapplePad(floatingGroup, "PlataformaFlotante_Der", new Vector3(6.8f, 5.8f, 56.5f), new Vector3(5.4f, 0.9f, 5.4f));

            // Las dos plataformas que se desplazan (contorno morado en el diagrama): requieren salto
            // preciso o engancharse a las esferas suspendidas arriba.
            BuildTestMovingPlatform(floatingGroup, "TEST_PlataformaMovil_Izq",
                startPos: new Vector3(-9.5f, 7.4f, 63.5f),
                travelOffset: new Vector3(3.5f, 2.4f, 10.5f),
                size: new Vector3(5.0f, 0.85f, 6.8f),
                duration: 3.1f,
                phaseOffset: 0f);

            GameObject movDer = BuildTestMovingPlatform(floatingGroup, "TEST_PlataformaMovil_Der",
                startPos: new Vector3(9.5f, 7.4f, 63.5f),
                travelOffset: new Vector3(-3.5f, 2.4f, 10.5f),
                size: new Vector3(5.0f, 0.85f, 6.8f),
                duration: 3.1f,
                phaseOffset: 0.5f);
            // Orbe absorbible sobre la plataforma móvil derecha (punto verde en el diagrama).
            AddAbsorbableOrb(floatingGroup, "Objeto_PlataformaMovil_Der", movDer.transform.position + Vector3.up * 1.0f, 0.44f);

            // Esferas de enganche sobre y junto a las plataformas móviles (< 30 m desde cualquier punto previo).
            AddGrappleSphere(floatingGroup, "EsferaEnganche_Movil_Izq_1", new Vector3(-9.5f, 13.2f, 65.0f), 1.85f);
            AddGrappleSphere(floatingGroup, "EsferaEnganche_Movil_Izq_2", new Vector3(-6.0f, 14.8f, 74.0f), 1.85f);
            AddGrappleSphere(floatingGroup, "EsferaEnganche_Movil_Der_1", new Vector3(9.5f, 13.2f, 65.0f), 1.85f);
            AddGrappleSphere(floatingGroup, "EsferaEnganche_Movil_Der_2", new Vector3(6.0f, 14.8f, 74.0f), 1.85f);
            AddGrappleSphere(floatingGroup, "EsferaEnganche_Movil_BajaCentro", new Vector3(0f, 10.8f, 68.5f), 1.65f);

            // ------------------------------------------------------------------
            // ZONA 3: LOTE DE EDIFICIOS FRUTIGER AERO (TODOS CON ENGANCHE / CAPA GRAPPLE)
            // El jugador aterriza en los techos y va subiendo hacia el edificio central amplio.
            // ------------------------------------------------------------------
            Transform buildingsGroup = ChildGroup(route, "03_Lote_Edificios");

            // 1) Primer edificio frontal de llegada (aterrizaje desde plataformas móviles o esferas).
            GameObject edif01 = BuildTestFrutigerBuilding(buildingsGroup, "TEST_EdificioFrutiger_01_EntradaA",
                baseCenterXZ: new Vector2(0f, 83.5f), widthX: 11f, lengthZ: 12f, roofHeight: 11.2f, yaw: 0f);
            AddCheckpoint(buildingsGroup, new Vector3(0f, 11.2f, 80.5f), 0f, "Entrada Edificios");
            // Objeto absorbible en la primera azotea (punto verde del boceto).
            AddAbsorbableOrb(buildingsGroup, "Objeto_Edificio01", new Vector3(-2.2f, 11.8f, 85.0f), 0.44f);

            // 2) Segundo edificio frontal escalonado (sube a 13,2 m; tiene enemigo terrestre en el techo).
            GameObject edif02 = BuildTestFrutigerBuilding(buildingsGroup, "TEST_EdificioFrutiger_02_EntradaB",
                baseCenterXZ: new Vector2(0f, 97.5f), widthX: 11f, lengthZ: 11f, roofHeight: 13.2f, yaw: 0f);
            GameObject bugEdif02 = SpawnEnemy(buildingsGroup, HexbugPrefabPath, "Enemigo_Bug_Edificio02",
                new Vector3(1.5f, 13.6f, 98.0f), Quaternion.Euler(0f, 180f, 0f));
            ConstrainToSurface(bugEdif02, edif02.transform, new Vector2(4.5f, 4.5f));

            // 3) Edificio lateral izquierdo inclinado (con objeto absorbible y barra giratoria de obstáculo).
            BuildTestFrutigerBuilding(buildingsGroup, "TEST_EdificioFrutiger_03_FlancoIzqInclinado",
                baseCenterXZ: new Vector2(-17.5f, 90.0f), widthX: 14f, lengthZ: 9.5f, roofHeight: 12.4f, yaw: -24f);
            AddAbsorbableOrb(buildingsGroup, "Objeto_Edificio03_Izq", new Vector3(-17.5f, 13.0f, 90.0f), 0.44f);
            BuildTestRotatingSpinner(buildingsGroup, "TEST_AspasTecho_Edificio03", new Vector3(-17.5f, 12.4f, 90.0f),
                bladeRadius: 4.1f, speedDegPerSec: -78f, fourBlades: false);

            // 4) Torre chica superior-izquierda del boceto (con objeto absorbible).
            BuildTestFrutigerBuilding(buildingsGroup, "TEST_EdificioFrutiger_04_TorreIzqChica",
                baseCenterXZ: new Vector2(-21.0f, 108.0f), widthX: 8f, lengthZ: 8f, roofHeight: 14.4f, yaw: 0f);
            AddAbsorbableOrb(buildingsGroup, "Objeto_Edificio04_TorreChica", new Vector3(-21.0f, 15.0f, 108.0f), 0.44f);

            // 5) Edificio lateral derecho inclinado (con enemigo terrestre y objeto absorbible).
            GameObject edif05 = BuildTestFrutigerBuilding(buildingsGroup, "TEST_EdificioFrutiger_05_FlancoDerInclinado",
                baseCenterXZ: new Vector2(17.5f, 92.5f), widthX: 10.5f, lengthZ: 10.5f, roofHeight: 12.4f, yaw: 18f);
            GameObject bugEdif05 = SpawnEnemy(buildingsGroup, HexbugPrefabPath, "Enemigo_Bug_Edificio05",
                new Vector3(17.5f, 12.8f, 92.5f), Quaternion.Euler(0f, -150f, 0f));
            ConstrainToSurface(bugEdif05, edif05.transform, new Vector2(4.0f, 4.0f));
            AddAbsorbableOrb(buildingsGroup, "Objeto_Edificio05_Der", new Vector3(15.5f, 13.0f, 90.5f), 0.44f);

            // 6) EDIFICIO CENTRAL AMPLIO: enemigo de Spawn (Horse/Troyano) + Estrella + Muro bloqueador.
            BuildTestFrutigerBuilding(buildingsGroup, "TEST_EdificioFrutiger_06_CentralAmplio",
                baseCenterXZ: new Vector2(0f, 117.5f), widthX: 24f, lengthZ: 24f, roofHeight: 15.5f, yaw: 0f);
            AddCheckpoint(buildingsGroup, new Vector3(0f, 15.5f, 107.0f), 0f, "Azotea Central");

            GameObject centralSpawner = SpawnEnemy(buildingsGroup, HorsePrefabPath, "Enemigo_Spawner_Central",
                new Vector3(0f, 15.5f, 117.5f), Quaternion.Euler(0f, 180f, 0f));
            ConfigureEnemyHealth(centralSpawner, tierIndex: 1, baseHp: 3); // Resistente (5 golpes) para pelea fluida sin esponja.

            // Surtidores recargables en las esquinas de la azotea central para que nunca falte munición.
            AddRespawningAmmoPad(buildingsGroup, "SurtidorMunicion_Central_Izq", new Vector3(-9.2f, 15.5f, 111.0f));
            AddRespawningAmmoPad(buildingsGroup, "SurtidorMunicion_Central_Der", new Vector3(9.2f, 15.5f, 111.0f));
            AddRespawningAmmoPad(buildingsGroup, "SurtidorMunicion_Central_Fondo", new Vector3(-9.2f, 15.5f, 123.5f));

            // Estrella del edificio central: empieza oculta y el enemigo la suelta en su posición al morir
            // para que el jugador tenga que acercarse a recogerla (no se entrega sola).
            Vector3 starPos = new Vector3(0f, 16.75f, 117.5f);
            GameObject centralStar = CreateStarInstance(buildingsGroup, "Star_Verano_Central", starPos, starIndex: 0);
            if (centralStar != null) centralStar.SetActive(false);

            // Muro arquitectónico que impide avanzar hacia la última plataforma hasta derrotar al Spawner central.
            GameObject centralWall = BuildTestBlockingWall(buildingsGroup, "TEST_MuroBloqueoCentral",
                bottomCenter: new Vector3(0f, 15.5f, 128.6f), size: new Vector3(24f, 7.2f, 1.2f));

            CreateDefeatUnlock(buildingsGroup, "ControlDesbloqueo_ArenaCentral",
                targets: new[] { centralSpawner },
                barrier: centralWall.transform,
                openOffset: new Vector3(0f, -7.8f, 0f),
                duration: 1.3f,
                activateOnUnlock: centralStar != null ? new[] { centralStar } : new GameObject[0],
                deactivateOnUnlock: new GameObject[0],
                toast: "» Spawner derrotado: ¡Soltó la Estrella y el muro se abrió!");

            // 7) y 8) Edificios laterales posteriores (arriba-derecha y abajo-derecha en el plano),
            // cada uno custodiado por un enemigo volador (Spam Mail) como indican los sobres alados rojos.
            BuildTestFrutigerBuilding(buildingsGroup, "TEST_EdificioFrutiger_07_TorreSpamIzq",
                baseCenterXZ: new Vector2(-19.5f, 128.0f), widthX: 12.5f, lengthZ: 12.5f, roofHeight: 17.0f, yaw: 0f);
            SpawnEnemy(buildingsGroup, FlyingEmailPrefabPath, "Enemigo_SpamMail_EdificioIzq",
                new Vector3(-19.5f, 20.6f, 128.0f), Quaternion.Euler(0f, 135f, 0f));
            AddAbsorbableOrb(buildingsGroup, "Objeto_Edificio07_Izq", new Vector3(-17.0f, 17.6f, 125.0f), 0.44f);

            BuildTestFrutigerBuilding(buildingsGroup, "TEST_EdificioFrutiger_08_TorreSpamDer",
                baseCenterXZ: new Vector2(19.5f, 126.5f), widthX: 13.5f, lengthZ: 13.5f, roofHeight: 17.0f, yaw: 0f);
            SpawnEnemy(buildingsGroup, FlyingEmailPrefabPath, "Enemigo_SpamMail_EdificioDer",
                new Vector3(19.5f, 20.6f, 126.5f), Quaternion.Euler(0f, -135f, 0f));
            BuildTestRotatingSpinner(buildingsGroup, "TEST_AspasTecho_Edificio08", new Vector3(19.5f, 17.0f, 126.5f),
                bladeRadius: 5.2f, speedDegPerSec: 72f, fourBlades: false);

            // 9) Edificio puente tras el muro desbloqueado: última azotea azul antes de la plataforma final.
            BuildTestFrutigerBuilding(buildingsGroup, "TEST_EdificioFrutiger_09_AntesalaFinal",
                baseCenterXZ: new Vector2(0f, 141.5f), widthX: 12.5f, lengthZ: 12.0f, roofHeight: 18.2f, yaw: 0f);
            AddCheckpoint(buildingsGroup, new Vector3(0f, 18.2f, 138.0f), 0f, "Antesala Final");
            AddRespawningAmmoPad(buildingsGroup, "SurtidorMunicion_Antesala_Izq", new Vector3(-3.8f, 18.2f, 144.0f));
            AddRespawningAmmoPad(buildingsGroup, "SurtidorMunicion_Antesala_Der", new Vector3(3.8f, 18.2f, 144.0f));

            // Cornisas y esferas azules de enganche en las fachadas (visibles en el corte lateral del diagrama)
            // para escalar los edificios con el gancho desde el agua o entre alturas.
            GrapplePad(buildingsGroup, "CornisaEnganche_FachadaFrontal", new Vector3(0f, 6.2f, 76.2f), new Vector3(6.5f, 0.7f, 2.4f));
            GrapplePad(buildingsGroup, "CornisaEnganche_CentralIzq", new Vector3(-13.2f, 9.2f, 117.5f), new Vector3(2.6f, 0.7f, 7.0f));
            GrapplePad(buildingsGroup, "CornisaEnganche_CentralDer", new Vector3(13.2f, 9.2f, 117.5f), new Vector3(2.6f, 0.7f, 7.0f));
            AddGrappleSphere(buildingsGroup, "EsferaEnganche_Fachada_1", new Vector3(-8.5f, 11.5f, 102.0f), 1.7f);
            AddGrappleSphere(buildingsGroup, "EsferaEnganche_Fachada_2", new Vector3(8.5f, 11.5f, 102.0f), 1.7f);
            AddGrappleSphere(buildingsGroup, "EsferaEnganche_AscensoCentral", new Vector3(0f, 19.5f, 132.5f), 1.75f);

            // ------------------------------------------------------------------
            // ZONA 4: ÚLTIMA PLATAFORMA ALTA (NO ENGANCHE), WINRAR FINAL Y PUERTA DE META MIRANDO ARRIBA
            // ------------------------------------------------------------------
            Transform finaleGroup = ChildGroup(route, "04_PlataformaFinal_y_PuertaMeta");

            // Dos esferas de enganche aéreas en el arco superior hacia la última plataforma (corte lateral).
            AddGrappleSphere(finaleGroup, "EsferaEnganche_ArcoFinal_1", new Vector3(0f, 22.8f, 150.0f), 1.85f);
            AddGrappleSphere(finaleGroup, "EsferaEnganche_ArcoFinal_2", new Vector3(0f, 24.4f, 158.0f), 1.85f);

            // Última plataforma elevada: contorno blanco No enganche en el diagrama (capa Default).
            GameObject platFinal = Renderer("Plataforma_Final_WinRar", finaleGroup, Builtin(PrimitiveType.Cube),
                Materials["M_Frutiger_Platform"], new Vector3(0f, 21.0f, 165.0f), new Vector3(12.0f, 1.4f, 18.0f), false);
            platFinal.layer = Layers.Default;
            platFinal.AddComponent<BoxCollider>();
            MarkStaticBaked(platFinal);

            // Surtidores de respaldo a la entrada de la última plataforma para pelear contra el WinRAR.
            AddRespawningAmmoPad(finaleGroup, "SurtidorMunicion_Final_Izq", new Vector3(-4.2f, 21.7f, 158.5f));
            AddRespawningAmmoPad(finaleGroup, "SurtidorMunicion_Final_Der", new Vector3(4.2f, 21.7f, 158.5f));

            // WinRAR al final de la última plataforma.
            GameObject finalWinRar = SpawnEnemy(finaleGroup, WinRarPrefabPath, "Enemigo_WinRar_PlataformaFinal",
                new Vector3(0f, 21.75f, 169.8f), Quaternion.Euler(0f, 180f, 0f));
            ConfigureBugEnemy(finalWinRar, detectionRange: 13.5f, lungeOvershoot: 0.8f);
            ConstrainToSurface(finalWinRar, platFinal.transform, new Vector2(4.8f, 7.2f));

            // PUERTA DE META: abajo de la última plataforma, mirando hacia ARRIBA (forward = +Y) y
            // conectada a OtherDimension mientras se define el Nivel 3.
            // La coloco en y = 14,5 m y z = 173,6 m (se extiende de z = 173,6 a z = 178,0 justo bajo el
            // filo z = 174,0 de la última plataforma) para que al saltar hacia adelante caigas directo dentro.
            BuildUpwardGoalGate(finaleGroup, new Vector3(0f, 14.5f, 173.6f));

            // ------------------------------------------------------------------
            // ZONA 5: PELIGROS EN EL AGUA POCO PROFUNDA Y BURBUJAS TRAMPOLÍN DE RECUPERACIÓN
            // ------------------------------------------------------------------
            Transform waterFloorGroup = ChildGroup(route, "05_PeligrosAgua_y_BurbujasTrampolin");

            // Spawner cerca de la base de los edificios y bichos terrestres al ras del agua (corte lateral).
            GameObject waterSpawner = SpawnEnemy(waterFloorGroup, HorsePrefabPath, "Enemigo_Spawner_AguaEdificios",
                new Vector3(9.5f, -0.35f, 134.0f), Quaternion.Euler(0f, -90f, 0f));
            ConfigureEnemyHealth(waterSpawner, tierIndex: 1, baseHp: 3);

            SpawnEnemy(waterFloorGroup, HexbugPrefabPath, "Enemigo_Bug_AguaEdificios_1",
                new Vector3(-7.5f, 0.05f, 79.0f), Quaternion.identity);
            SpawnEnemy(waterFloorGroup, HexbugPrefabPath, "Enemigo_Bug_AguaEdificios_2",
                new Vector3(4.5f, 0.05f, 114.0f), Quaternion.Euler(0f, 90f, 0f));

            // WinRAR abajo en el agua cerca de la puerta de meta (corte lateral: "win" rojo al nivel del agua).
            GameObject waterWinRar = SpawnEnemy(waterFloorGroup, WinRarPrefabPath, "Enemigo_WinRar_AguaMeta",
                new Vector3(0f, -0.35f, 180.5f), Quaternion.Euler(0f, 180f, 0f));
            ConfigureBugEnemy(waterWinRar, detectionRange: 16f, lungeOvershoot: 1.2f);

            // Burbujas trampolín repartidas en el agua poco profunda para volver a subir desde cualquier caída.
            AddBounceBubble(waterFloorGroup, "BurbujaTrampolin_IslaInicio", new Vector3(-9.5f, 0.95f, 7.5f), 1.45f, 4.5f, 3.5f, new Vector3(0f, 2f, 5f));
            AddBounceBubble(waterFloorGroup, "BurbujaTrampolin_IslaIzq", new Vector3(-25.5f, 0.95f, 36.5f), 1.55f, 6.0f, 4.5f, new Vector3(-16.5f, 3.5f, 36.5f));
            AddBounceBubble(waterFloorGroup, "BurbujaTrampolin_IslaDer", new Vector3(26.0f, 0.95f, 36.5f), 1.55f, 6.0f, 4.5f, new Vector3(16.0f, 3.5f, 36.5f));
            AddBounceBubble(waterFloorGroup, "BurbujaTrampolin_Moviles", new Vector3(0f, 0.95f, 60.0f), 1.65f, 8.8f, 4.0f, new Vector3(0f, 7f, 56.5f));
            AddBounceBubble(waterFloorGroup, "BurbujaTrampolin_EdificiosFrente", new Vector3(-8.5f, 1.05f, 81.0f), 1.75f, 13.8f, 4.5f, new Vector3(0f, 11.5f, 83.5f));
            AddBounceBubble(waterFloorGroup, "BurbujaTrampolin_EdificioCentralIzq", new Vector3(-15.5f, 1.05f, 114.0f), 1.8f, 17.8f, 5.0f, new Vector3(-4f, 15.5f, 116f));
            AddBounceBubble(waterFloorGroup, "BurbujaTrampolin_EdificioCentralDer", new Vector3(15.5f, 1.05f, 114.0f), 1.8f, 17.8f, 5.0f, new Vector3(4f, 15.5f, 116f));
            AddBounceBubble(waterFloorGroup, "BurbujaTrampolin_RetornoMeta_Izq", new Vector3(-9.5f, 1.05f, 152.0f), 1.85f, 20.5f, 5.5f, new Vector3(0f, 18.5f, 142f));
            AddBounceBubble(waterFloorGroup, "BurbujaTrampolin_RetornoMeta_Der", new Vector3(9.5f, 1.05f, 152.0f), 1.85f, 20.5f, 5.5f, new Vector3(0f, 18.5f, 142f));

            // Numeración consecutiva 0..N-1 exigida por RespawnService y las pruebas de escena.
            for (int i = 0; i < Checkpoints.Count; i++)
            {
                Checkpoints[i].Configure(i, "Punto de control: " + Checkpoints[i].name.Replace("Checkpoint_", string.Empty));
            }
        }

        // ================================================================== piezas constructivas del Nivel 2
        private static GameObject Island(Transform parent, string name, Vector3 center, float radius, float top, int seed,
                                         Mesh foamRing, Vector2 scaleXZ, float yaw)
        {
            Mesh mesh = SaveMesh(FrutigerMeshes.Island("FR_" + name, radius, top, 4.5f, seed), true);
            GameObject go = Renderer(name, parent, mesh, Materials["M_Frutiger_Island"], center,
                new Vector3(scaleXZ.x, 1f, scaleXZ.y), true);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.layer = Layers.Default; // No enganche según leyenda del diagrama.
            MarkStaticBaked(go);

            GameObject foam = Renderer(name + "_Espuma", go.transform, foamRing, Materials["M_Frutiger_Foam"],
                center + Vector3.up * 0.08f, new Vector3(radius * scaleXZ.x, 1f, radius * scaleXZ.y), false);
            foam.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            foam.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Physics.SyncTransforms();
            return go;
        }

        /// <summary>Plataforma azul con enganche (capa Grapple) y esquinas limpias.</summary>
        private static GameObject GrapplePad(Transform parent, string name, Vector3 topCenter, Vector3 size)
        {
            GameObject go = Renderer(name, parent, Builtin(PrimitiveType.Cube), Materials["M_Frutiger_GrapplePad"],
                topCenter - Vector3.up * (size.y * 0.5f), size, false);
            go.layer = Layers.Grapple;
            go.AddComponent<BoxCollider>();
            MarkStaticBaked(go);
            Physics.SyncTransforms();
            return go;
        }

        /// <summary>
        /// Edificio temporal <c>TEST_EdificioFrutiger_*</c> naciendo desde el lecho marino hasta su azotea.
        /// Toda la torre está en <see cref="Layers.Grapple"/> ("Gancho sirve en edificios") y lleva franjas
        /// de vidrio Aero decorativas sin alterar el collider limpio de caja para que el equipo reemplace
        /// el bloque por sus edificios definitivos.
        /// </summary>
        private static GameObject BuildTestFrutigerBuilding(Transform parent, string name, Vector2 baseCenterXZ,
                                                            float widthX, float lengthZ, float roofHeight, float yaw)
        {
            const float seabedBaseY = -0.5f;
            float totalHeight = roofHeight - seabedBaseY;
            Vector3 center = new Vector3(baseCenterXZ.x, seabedBaseY + totalHeight * 0.5f, baseCenterXZ.y);

            GameObject tower = Renderer(name, parent, Builtin(PrimitiveType.Cube), Materials["M_Frutiger_BuildingTest"],
                center, new Vector3(widthX, totalHeight, lengthZ), false);
            tower.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            tower.layer = Layers.Grapple;
            tower.AddComponent<BoxCollider>();
            MarkStaticBaked(tower);

            // Remate superior de vidrio Aero en la cornisa para que la silueta se lea desde abajo y desde arriba.
            GameObject crown = Renderer(name + "_CornisaAero", parent, Builtin(PrimitiveType.Cube), Materials["M_Frutiger_Glass"],
                new Vector3(baseCenterXZ.x, roofHeight - 0.35f, baseCenterXZ.y),
                new Vector3(widthX + 0.35f, 0.55f, lengthZ + 0.35f), false);
            crown.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            crown.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            Physics.SyncTransforms();
            return tower;
        }

        /// <summary>
        /// Plataforma móvil temporal <c>TEST_PlataformaMovil_*</c> con borde morado, transporte de jugador
        /// y ranura de audio en bucle lista para asignar clip.
        /// </summary>
        private static GameObject BuildTestMovingPlatform(Transform parent, string name, Vector3 startPos,
                                                          Vector3 travelOffset, Vector3 size, float duration, float phaseOffset)
        {
            GameObject go = Renderer(name, parent, Builtin(PrimitiveType.Cube), Materials["M_Frutiger_MovingPlatform"],
                startPos, size, false);
            go.layer = Layers.Default;
            go.AddComponent<BoxCollider>();

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            MovingPlatform mp = go.AddComponent<MovingPlatform>();
            mp.Configure(
                offsetFromStart: travelOffset,
                duration: duration,
                offset01: phaseOffset,
                halfExtents: new Vector3(size.x * 0.48f, 0.75f, size.z * 0.48f),
                centerY: size.y * 0.5f + 0.55f);

            AddAudioSlotOn(go, $"Zumbido suave de plataforma móvil ({name})", AudioSlotTrigger.AmbientOrManual, true, 1f, 28f, 0.55f);
            Physics.SyncTransforms();
            return go;
        }

        /// <summary>
        /// Obstáculo temporal <c>TEST_AspasGiratorias_*</c> con 4 brazos en cruz (o 2 en barra simple),
        /// daño por contacto, empuje tangencial y ranura de audio para giro + golpe.
        /// </summary>
        private static GameObject BuildTestRotatingSpinner(Transform parent, string name, Vector3 groundPos,
                                                           float bladeRadius, float speedDegPerSec, bool fourBlades)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.position = groundPos;
            pivot.layer = Layers.Hazard;

            Mesh cylinder = Builtin(PrimitiveType.Cylinder);
            Mesh cube = Builtin(PrimitiveType.Cube);

            // Poste central cromado de sujeción.
            GameObject hub = Renderer(name + "_EjeCentral", pivot.transform, cylinder, Materials["M_Frutiger_Chrome"],
                groundPos + Vector3.up * 0.75f, new Vector3(1.1f, 0.75f, 1.1f), false);
            hub.layer = Layers.Default;
            CapsuleCollider hubCol = hub.AddComponent<CapsuleCollider>();
            hubCol.radius = 0.5f;
            hubCol.height = 2f;

            // Brazo A completo de -R a +R sobre Z local.
            float bladeHeight = 0.65f;
            GameObject armA = Renderer(name + "_Aspa_A", pivot.transform, cube, Materials["M_Frutiger_HazardRed"],
                groundPos + Vector3.up * bladeHeight, new Vector3(0.55f, 0.45f, bladeRadius * 2f), false);
            armA.layer = Layers.Hazard;
            armA.AddComponent<BoxCollider>();

            if (fourBlades)
            {
                // Brazo B perpendicular para completar la hélice de 4 aspas del boceto.
                GameObject armB = Renderer(name + "_Aspa_B", pivot.transform, cube, Materials["M_Frutiger_HazardRed"],
                    groundPos + Vector3.up * bladeHeight, new Vector3(bladeRadius * 2f, 0.45f, 0.55f), false);
                armB.layer = Layers.Hazard;
                armB.AddComponent<BoxCollider>();
            }

            RotatingHazardObstacle spinner = pivot.AddComponent<RotatingHazardObstacle>();
            spinner.Configure(
                speedDegPerSec: speedDegPerSec,
                crossFourBlades: fourBlades,
                radius: bladeRadius,
                crossHalf: new Vector2(0.38f, 0.34f),
                localHeight: bladeHeight);

            AddAudioSlotOn(pivot, $"Giro en bucle y golpe de aspas ({name})", AudioSlotTrigger.HazardHit, true, 1f, 34f, 0.85f);
            Physics.SyncTransforms();
            return pivot;
        }

        /// <summary>
        /// Muro bloqueador arquitectónico temporal <c>TEST_MuroBloqueoCentral</c> en capa <see cref="Layers.Wall"/> (No enganche).
        /// Uso una raíz con escala <c>(1, 1, 1)</c> para que ninguna pieza hija se deforme al multiplicar escalas,
        /// dividiendo la compuerta en tres bahías con pilares cromados, dintel blanco y paneles de vidrio Aero.
        /// </summary>
        private static GameObject BuildTestBlockingWall(Transform parent, string name, Vector3 bottomCenter, Vector3 size)
        {
            var wallRoot = new GameObject(name);
            wallRoot.transform.SetParent(parent, false);
            wallRoot.transform.position = bottomCenter;
            wallRoot.transform.localScale = Vector3.one;
            wallRoot.layer = Layers.Wall;

            BoxCollider wallCol = wallRoot.AddComponent<BoxCollider>();
            wallCol.center = new Vector3(0f, size.y * 0.5f, 0f);
            wallCol.size = size;

            Mesh cube = Builtin(PrimitiveType.Cube);

            // Cuerpo base blanco brillante (No enganche).
            GameObject core = Renderer("Muro_Base_Aero", wallRoot.transform, cube, Materials["M_Frutiger_Platform"],
                bottomCenter + new Vector3(0f, size.y * 0.5f, 0f),
                new Vector3(size.x - 1.6f, size.y - 0.8f, size.z * 0.72f), false);
            core.layer = Layers.Wall;

            // Zócalo inferior y dintel superior que enmarcan el muro sobre la azotea.
            GameObject plinth = Renderer("Zocalo_Inferior", wallRoot.transform, cube, Materials["M_Frutiger_Chrome"],
                bottomCenter + new Vector3(0f, 0.35f, 0f),
                new Vector3(size.x, 0.7f, size.z + 0.25f), false);
            plinth.layer = Layers.Wall;

            GameObject lintel = Renderer("Dintel_Superior", wallRoot.transform, cube, Materials["M_Frutiger_Chrome"],
                bottomCenter + new Vector3(0f, size.y - 0.4f, 0f),
                new Vector3(size.x + 0.3f, 0.8f, size.z + 0.3f), false);
            lintel.layer = Layers.Wall;

            // Dos pilares laterales cromados y dos columnas divisorias internas.
            float endX = size.x * 0.5f - 0.65f;
            float midX = size.x * 0.18f;
            float[] pillarXs = { -endX, -midX, midX, endX };
            for (int i = 0; i < pillarXs.Length; i++)
            {
                bool outer = i == 0 || i == pillarXs.Length - 1;
                GameObject pillar = Renderer($"Pilar_{i + 1}", wallRoot.transform, cube,
                    outer ? Materials["M_Frutiger_Chrome"] : Materials["M_Frutiger_Platform"],
                    bottomCenter + new Vector3(pillarXs[i], size.y * 0.5f, 0f),
                    new Vector3(outer ? 1.3f : 0.85f, size.y + 0.1f, size.z + (outer ? 0.35f : 0.18f)), false);
                pillar.layer = Layers.Wall;
            }

            // Tres ventanales translúcidos de vidrio Aero entre las columnas.
            float baySpan = (endX - midX) - 0.9f;
            float centerBaySpan = (midX * 2f) - 0.9f;
            (float x, float w)[] bays =
            {
                (-(midX + endX) * 0.5f, baySpan),
                (0f, centerBaySpan),
                ((midX + endX) * 0.5f, baySpan)
            };
            for (int i = 0; i < bays.Length; i++)
            {
                GameObject glassPanel = Renderer($"Ventanal_Aero_{i + 1}", wallRoot.transform, cube, Materials["M_Frutiger_Glass"],
                    bottomCenter + new Vector3(bays[i].x, size.y * 0.5f, 0f),
                    new Vector3(Mathf.Max(1.5f, bays[i].w), size.y - 1.8f, size.z * 0.92f), false);
                glassPanel.layer = Layers.Wall;
                glassPanel.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            // Emblema central compacto de bloqueo (solo en la bahía central, sin desbordar jamás el muro).
            GameObject lockEmblem = Renderer("Emblema_Bloqueo", wallRoot.transform, cube, Materials["M_Frutiger_HazardRed"],
                bottomCenter + new Vector3(0f, size.y * 0.5f, 0f),
                new Vector3(Mathf.Min(5.2f, centerBaySpan - 0.6f), 0.75f, size.z + 0.14f), false);
            lockEmblem.layer = Layers.Wall;

            Physics.SyncTransforms();
            return wallRoot;
        }

        private static void CreateDefeatUnlock(Transform parent, string name, GameObject[] targets, Transform barrier,
                                               Vector3 openOffset, float duration, GameObject[] activateOnUnlock,
                                               GameObject[] deactivateOnUnlock, string toast)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = barrier != null ? barrier.position : Vector3.zero;

            DefeatUnlockController unlock = go.AddComponent<DefeatUnlockController>();
            unlock.Configure(targets, barrier, openOffset, duration, activateOnUnlock, deactivateOnUnlock, toast);
            AddAudioSlotOn(go, $"Sonido de desbloqueo y apertura de muro ({name})", AudioSlotTrigger.ArenaGateOpen, true, 1f, 45f, 0.9f);
        }

        /// <summary>
        /// Instancia el prefab oficial <c>LevelGate.prefab</c> debajo de la última plataforma,
        /// orientado mirando hacia ARRIBA (<c>forward = Vector3.up</c>) y conectado con
        /// <see cref="SceneNames.OtherDimension"/> para que al saltar desde la plataforma final
        /// caigas dentro de la puerta y pases al siguiente nivel.
        /// </summary>
        private static void BuildUpwardGoalGate(Transform parent, Vector3 thresholdPosition)
        {
            GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelGateBuilder.PrefabPath);
            if (gatePrefab == null)
            {
                gatePrefab = LevelGateBuilder.BuildPrefab();
            }
            if (gatePrefab == null) return;

            var gateGo = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab, scene);
            gateGo.name = "LevelGate_Meta_Arriba";
            gateGo.transform.SetParent(parent, false);

            // Con forward = +Y y upwards = +Z, el plano de abertura (3,2 m ancho × 4,4 m largo) queda
            // horizontal extendiéndose desde thresholdPosition hasta +4,4 m en +Z, mirando directo al cielo.
            Quaternion upwardRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            gateGo.transform.SetPositionAndRotation(thresholdPosition, upwardRotation);

            LevelGate gate = gateGo.GetComponent<LevelGate>();
            if (gate != null)
            {
                gate.Configure(SceneNames.OtherDimension, SceneNames.Paths.OtherDimension, LevelGateBuilder.Aperture);
            }

            // Marco exterior flotante alrededor de la puerta horizontal para que se distinga con claridad
            // al asomarse desde el borde de la última plataforma.
            Mesh cube = Builtin(PrimitiveType.Cube);
            float w = LevelGateBuilder.Aperture.x;
            float h = LevelGateBuilder.Aperture.y;
            Vector3 ringCenter = thresholdPosition + new Vector3(0f, -0.25f, h * 0.5f);
            Transform cradle = ChildGroup(parent, "MarcoExterior_PuertaMeta");
            cradle.position = ringCenter;

            GameObject leftRim = Renderer("Borde_Izq", cradle, cube, Materials["M_Frutiger_Platform"],
                ringCenter + new Vector3(-(w * 0.5f + 0.55f), 0f, 0f), new Vector3(0.7f, 0.45f, h + 1.4f), false);
            MarkStaticBaked(leftRim);
            GameObject rightRim = Renderer("Borde_Der", cradle, cube, Materials["M_Frutiger_Platform"],
                ringCenter + new Vector3(w * 0.5f + 0.55f, 0f, 0f), new Vector3(0.7f, 0.45f, h + 1.4f), false);
            MarkStaticBaked(rightRim);
            GameObject backRim = Renderer("Borde_Norte", cradle, cube, Materials["M_Frutiger_Platform"],
                ringCenter + new Vector3(0f, 0f, h * 0.5f + 0.45f), new Vector3(w + 1.8f, 0.45f, 0.7f), false);
            MarkStaticBaked(backRim);
            GameObject frontRim = Renderer("Borde_Sur", cradle, cube, Materials["M_Frutiger_Platform"],
                ringCenter + new Vector3(0f, 0f, -(h * 0.5f + 0.45f)), new Vector3(w + 1.8f, 0.45f, 0.7f), false);
            MarkStaticBaked(frontRim);

            LevelGateVisuals visuals = gateGo.GetComponent<LevelGateVisuals>();
            if (visuals != null)
            {
                var so = new SerializedObject(visuals);
                so.FindProperty("colorTop").colorValue = new Color(0.55f, 0.96f, 1.0f, 1f);
                so.FindProperty("colorBottom").colorValue = new Color(0.28f, 1.0f, 0.68f, 1f);
                so.FindProperty("glowIntensity").floatValue = 3.1f;
                so.FindProperty("idleGlow").floatValue = 0.9f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AddAudioSlotOn(gateGo, "Zumbido de portal de meta y sonido al cruzar al siguiente nivel",
                AudioSlotTrigger.LevelGateOrCompletion, false, 1f, 45f, 0.9f);
        }

        /// <summary>Pórtico decorativo alrededor de <c>LevelEntrance</c> en la Isla 1 sin tapar la abertura.</summary>
        private static void BuildDecorativeStartFrame(Transform parent, Vector3 groundPos, Quaternion rotation)
        {
            Transform frame = ChildGroup(parent, "Marco_PuertaInicio");
            frame.SetPositionAndRotation(groundPos, rotation);
            Mesh cube = Builtin(PrimitiveType.Cube);
            float w = LevelGateBuilder.Aperture.x;
            float h = LevelGateBuilder.Aperture.y;

            GameObject left = Renderer("Poste_Izq", frame, cube, Materials["M_Frutiger_Chrome"],
                groundPos + rotation * new Vector3(-(w * 0.5f + 0.22f), (h + 0.4f) * 0.5f, 0f),
                new Vector3(0.38f, h + 0.4f, 0.42f), false);
            left.transform.rotation = rotation;
            left.AddComponent<BoxCollider>();
            MarkStaticBaked(left);

            GameObject right = Renderer("Poste_Der", frame, cube, Materials["M_Frutiger_Chrome"],
                groundPos + rotation * new Vector3(w * 0.5f + 0.22f, (h + 0.4f) * 0.5f, 0f),
                new Vector3(0.38f, h + 0.4f, 0.42f), false);
            right.transform.rotation = rotation;
            right.AddComponent<BoxCollider>();
            MarkStaticBaked(right);

            GameObject header = Renderer("Dintel", frame, cube, Materials["M_Frutiger_GrapplePad"],
                groundPos + rotation * new Vector3(0f, h + 0.22f, 0f),
                new Vector3(w + 0.85f, 0.42f, 0.48f), false);
            header.transform.rotation = rotation;
            header.layer = Layers.Default;
            header.AddComponent<BoxCollider>();
            MarkStaticBaked(header);
        }

        private static void AddGrappleSphere(Transform parent, string name, Vector3 position, float diameter)
        {
            GameObject go = Renderer(name, parent, Builtin(PrimitiveType.Sphere),
                Materials["M_Frutiger_Anchor"], position, Vector3.one * diameter, false);
            go.layer = Layers.Grapple;
            go.AddComponent<SphereCollider>();
            MarkStaticBaked(go);
        }

        private static void AddAbsorbableOrb(Transform parent, string name, Vector3 position, float radius)
        {
            GameObject go = Renderer(name, parent, Builtin(PrimitiveType.Sphere),
                Materials["M_Frutiger_Absorbable"], position, Vector3.one * (radius * 2f), false);
            go.layer = Layers.Absorbable;
            go.AddComponent<SphereCollider>();
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = 0.85f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<Absorbable>();
        }

        private static void AddRespawningAmmoPad(Transform parent, string name, Vector3 floorPosition)
        {
            GameObject pad = Renderer(name, parent, Builtin(PrimitiveType.Cylinder),
                Materials["M_Frutiger_GrapplePad"], floorPosition + Vector3.up * 0.05f, new Vector3(1.35f, 0.05f, 1.35f), false);
            pad.layer = Layers.Default;
            RespawningAbsorbablePad dispenser = pad.AddComponent<RespawningAbsorbablePad>();
            dispenser.Configure(Builtin(PrimitiveType.Sphere), Materials["M_Frutiger_Absorbable"], 0.44f, 3.2f);
        }

        private static void AddBounceBubble(Transform parent, string name, Vector3 center, float radius,
                                            float height, float boost, Vector3 target)
        {
            GameObject go = Renderer(name, parent, Builtin(PrimitiveType.Sphere),
                Materials["M_Frutiger_BounceBubble"], center, Vector3.one * (radius * 2f), false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            Vector3 toTarget = target - center;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(toTarget.normalized);

            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.56f;

            GameObject baseRing = Renderer(name + "_Base", parent, Builtin(PrimitiveType.Cylinder), Materials["M_Frutiger_BouncePad"],
                new Vector3(center.x, -0.32f, center.z), new Vector3(radius * 2.3f, 0.05f, radius * 2.3f), false);
            MarkStaticBaked(baseRing);

            BounceBubble bounce = go.AddComponent<BounceBubble>();
            bounce.Configure(height, boost);
            go.layer = Layers.Trigger;
            go.AddComponent<BubbleWobble>();

            AddAudioSlotOn(go, $"Sonido elástico de burbuja trampolín ({name})", AudioSlotTrigger.BubbleBounce, true, 1f, 30f, 0.8f);
        }

        private static void AddCheckpoint(Transform parent, Vector3 groundPoint, float yaw, string label)
        {
            var go = new GameObject("Checkpoint_" + label);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(groundPoint, Quaternion.Euler(0f, yaw, 0f));
            go.layer = Layers.Trigger;

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(6f, 4f, 6f);
            box.center = new Vector3(0f, 2f, 0f);

            Checkpoint cp = go.AddComponent<Checkpoint>();
            Checkpoints.Add(cp);

            GameObject ring = Renderer("Aro", go.transform, Builtin(PrimitiveType.Cylinder),
                Materials["M_Frutiger_Checkpoint"], groundPoint + Vector3.up * 0.05f, new Vector3(2.6f, 0.04f, 2.6f), false);
            MarkStaticBaked(ring);

            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = Vector3.up * 1.5f;
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 1.4f;
            light.color = new Color(0.7f, 0.9f, 1f);
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Baked;
        }

        private static GameObject SpawnEnemy(Transform parent, string prefabPath, string instanceName, Vector3 position, Quaternion rotation)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Frutiger] No se encontró el prefab de enemigo en {prefabPath}.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = instanceName;
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        private static void ConfigureEnemyHealth(GameObject enemy, int tierIndex, int baseHp)
        {
            if (enemy == null) return;
            MonoBehaviour health = enemy.GetComponents<MonoBehaviour>()
                .FirstOrDefault(c => c != null && c.GetType().Name == "EnemyHealth");
            if (health == null) return;

            var so = new SerializedObject(health);
            SerializedProperty tierProp = so.FindProperty("healthTier");
            SerializedProperty baseProp = so.FindProperty("baseMaxHealth");
            if (tierProp != null) tierProp.enumValueIndex = tierIndex;
            if (baseProp != null) baseProp.intValue = baseHp;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBugEnemy(GameObject enemy, float detectionRange, float lungeOvershoot)
        {
            if (enemy == null) return;
            MonoBehaviour bug = enemy.GetComponents<MonoBehaviour>()
                .FirstOrDefault(c => c != null && c.GetType().Name == "BugEnemyController");
            if (bug == null) return;

            var so = new SerializedObject(bug);
            SerializedProperty detProp = so.FindProperty("detectionRange");
            SerializedProperty overProp = so.FindProperty("lungeOvershoot");
            if (detProp != null) detProp.floatValue = detectionRange;
            if (overProp != null) overProp.floatValue = lungeOvershoot;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConstrainToSurface(GameObject enemy, Transform surface, Vector2 halfExtentsXZ)
        {
            if (enemy == null || surface == null) return;
            PlatformBoundsConstraint leash = enemy.AddComponent<PlatformBoundsConstraint>();
            leash.Configure(surface, halfExtentsXZ);
        }

        private static GameObject CreateStarInstance(Transform parent, string name, Vector3 position, int starIndex)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StarPrefabPath);
            if (prefab == null) return null;

            var star = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            star.name = name;
            star.transform.SetParent(parent, true);
            star.transform.position = position;

            MonoBehaviour ctrl = star.GetComponents<MonoBehaviour>()
                .FirstOrDefault(c => c != null && c.GetType().Name == "StarController");
            if (ctrl != null)
            {
                var so = new SerializedObject(ctrl);
                SerializedProperty idx = so.FindProperty("starIndex");
                if (idx != null) idx.intValue = starIndex;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (Light l in star.GetComponentsInChildren<Light>(true))
            {
                l.shadows = LightShadows.None;
            }
            return star;
        }

        // ================================================================== decoración Frutiger Aero
        private static void BuildDecor()
        {
            Transform group = Group("Decoracion");
            Mesh sphere = Builtin(PrimitiveType.Sphere);

            // Dos campos instanciados de burbujas (impostores cercanos + esferas altas iridiscentes).
            BubbleField(group, "BurbujasCercanas", new Vector3(0f, 11f, 88f), new Vector3(160f, 22f, 210f),
                440, 0.1f, 1.2f, "M_Frutiger_Bubble", 11, Builtin(PrimitiveType.Quad));
            BubbleField(group, "BurbujasAltas", new Vector3(0f, 28f, 92f), new Vector3(230f, 24f, 250f),
                64, 1.5f, 5.2f, "M_Frutiger_BubbleHero", 23, Builtin(PrimitiveType.Sphere));

            // Columnas de burbujas ascendentes desde el océano poco profundo.
            Vector3[] streams =
            {
                new Vector3(-18f, 0f, 14f), new Vector3(18f, 0f, 20f),
                new Vector3(-28f, 0f, 52f), new Vector3(26f, 0f, 58f),
                new Vector3(-30f, 0f, 104f), new Vector3(30f, 0f, 108f),
                new Vector3(0f, 0f, 160f)
            };
            for (int i = 0; i < streams.Length; i++) BubbleStream(group, "ColumnaBurbujas_" + (i + 1), streams[i]);

            // Árboles estilizados solo en las orillas de las islas para no estorbar el combate ni las aspas.
            Vector2[] trees =
            {
                new Vector2(-6.5f, -3.5f), new Vector2(8.5f, 1.5f), new Vector2(-4.0f, 11.0f),
                new Vector2(-23.5f, 38.0f), new Vector2(-13.5f, 44.5f)
            };
            for (int i = 0; i < trees.Length; i++) Tree(group, Ground(trees[i].x, trees[i].y), i);

            // Nubes 3D brillantes alrededor del archipiélago y los edificios.
            Vector3[] clouds =
            {
                new Vector3(-55f, 36f, 45f), new Vector3(65f, 40f, 55f),
                new Vector3(-60f, 44f, 135f), new Vector3(68f, 38f, 145f),
                new Vector3(0f, 48f, 215f), new Vector3(35f, 42f, -25f)
            };
            for (int i = 0; i < clouds.Length; i++) CloudCluster(group, clouds[i], i);

            // Canicas de vidrio flotantes fuera de las trayectorias de salto.
            for (int i = 0; i < 10; i++)
            {
                float a = i * 2.39996f;
                Vector3 p = new Vector3(Mathf.Cos(a) * (34f + (i % 3) * 8f), 7f + (i % 4) * 3.5f, 88f + Mathf.Sin(a) * (36f + i * 4f));
                GameObject marble = Renderer("CanicaVidrio_" + (i + 1), group, sphere, Materials["M_Frutiger_GlassMarble"],
                    p, Vector3.one * (1.2f + (i % 3) * 0.7f), false);
                marble.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            // Cardúmenes de peces voladores rodeando islas y edificios.
            Mesh fish = SaveMesh(FrutigerMeshes.Fish("FR_Fish"), false);
            FishSchoolAt(group, "Cardumen_Inicio", new Vector3(2f, 6.5f, 4f), 11f, 9, fish, 16f);
            FishSchoolAt(group, "Cardumen_Islas", new Vector3(0f, 8.5f, 36f), 14f, 10, fish, -18f);
            FishSchoolAt(group, "Cardumen_Edificios", new Vector3(0f, 23f, 118f), 20f, 12, fish, 13f);
            FishSchoolAt(group, "Cardumen_Meta", new Vector3(0f, 16f, 176f), 12f, 9, fish, -15f);

            // Cintas de luz tipo aurora en el cielo.
            RibbonAt(group, "CintaAurora_A", new Vector3(0f, 58f, 235f), 0f, 280f, 34f, 20f, 40f, 0.3f, 1.2f, "M_Frutiger_RibbonA");
            RibbonAt(group, "CintaAurora_B", new Vector3(135f, 50f, 105f), -82f, 240f, 26f, 16f, 30f, 1.7f, 2.2f, "M_Frutiger_RibbonB");
            RibbonAt(group, "CintaAurora_C", new Vector3(-135f, 48f, 105f), 82f, 220f, 24f, 14f, 25f, 2.6f, 1.6f, "M_Frutiger_RibbonC");

            // Haces de sol apoyados sobre el océano y el edificio central.
            Mesh cone = SaveMesh(FrutigerMeshes.LightCone("FR_LightCone", 5f, 9f), false);
            Vector3[] rayTargets =
            {
                new Vector3(-22f, 0f, 12f), new Vector3(24f, 0f, 42f),
                new Vector3(-28f, 0f, 98f), new Vector3(0f, 15.5f, 118f), new Vector3(18f, 0f, 168f)
            };
            for (int i = 0; i < rayTargets.Length; i++)
            {
                GameObject ray = Renderer("HazSol_" + (i + 1), group, cone, Materials["M_Frutiger_GodRay"],
                    rayTargets[i], new Vector3(1f, 70f, 1f), false);
                ray.transform.rotation = Quaternion.FromToRotation(Vector3.up, TowardSun);
                ray.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            // Arcoíris lejano enmarcando el horizonte detrás de la meta.
            Mesh quad = Builtin(PrimitiveType.Quad);
            GameObject rainbow = Renderer("Arcoiris", group, quad, Materials["M_Frutiger_Rainbow"],
                new Vector3(0f, 57f, 285f), new Vector3(260f, 130f, 1f), false);
            rainbow.transform.rotation = Quaternion.identity;
            rainbow.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ================================================================== ranuras globales de audio (sin crear clips)
        private static void BuildAudioSystem()
        {
            Transform audioGroup = Group("Audio_RanurasPreparadas");

            var oceanAmbience = new GameObject("Audio_AmbienteOceano_y_MusicaVerano");
            oceanAmbience.transform.SetParent(audioGroup, false);
            AddAudioSlotOn(oceanAmbience,
                "Música/ambiente general 2D de Estación Verano (olas suaves y brisa Frutiger Aero)",
                AudioSlotTrigger.AmbientOrManual, false, 0f, 100f, 0.65f);

            var waterSplash = new GameObject("Audio_ChapoteoAguaPocoProfunda");
            waterSplash.transform.SetParent(audioGroup, false);
            AddAudioSlotOn(waterSplash,
                "Chapoteo 3D al caer desde plataformas o edificios al agua poco profunda",
                AudioSlotTrigger.ShallowWaterSplash, false, 1f, 40f, 0.85f);
        }

        private static void AddAudioSlotOn(GameObject target, string description, AudioSlotTrigger trigger,
                                           bool matchSelf, float spatialBlend, float maxDistance, float volume)
        {
            if (target.GetComponent<AudioSource>() == null) target.AddComponent<AudioSource>();
            LevelAudioSlot slot = target.GetComponent<LevelAudioSlot>();
            if (slot == null) slot = target.AddComponent<LevelAudioSlot>();
            slot.Configure(description, trigger, matchSelf, spatialBlend, maxDistance, volume);
        }

        // ================================================================== helpers de decoración
        private static void BubbleField(Transform parent, string name, Vector3 center, Vector3 size, int count,
                                        float minR, float maxR, string material, int seed, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            BubbleFieldRenderer field = go.AddComponent<BubbleFieldRenderer>();
            var so = new SerializedObject(field);
            so.FindProperty("mesh").objectReferenceValue = mesh;
            so.FindProperty("material").objectReferenceValue = Materials[material];
            so.FindProperty("size").vector3Value = size;
            so.FindProperty("count").intValue = count;
            so.FindProperty("minRadius").floatValue = minR;
            so.FindProperty("maxRadius").floatValue = maxR;
            so.FindProperty("seed").intValue = seed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BubbleStream(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position + Vector3.up * 0.2f;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.8f);
            main.startColor = new Color(0.9f, 0.98f, 1f, 1f);
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 7f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14f, 0.5f, 14f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.2f;
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Materials["M_Frutiger_BubbleSprite"];
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV, ParticleSystemVertexStream.StableRandomX
            });
            go.AddComponent<ParticleBudget>();
        }

        private static void Tree(Transform parent, Vector3 ground, int index)
        {
            Transform group = new GameObject("Arbol_" + (index + 1)).transform;
            group.SetParent(parent, false);
            group.position = ground;
            float height = 3.2f + (index % 3) * 0.9f;
            Mesh cylinder = Builtin(PrimitiveType.Cylinder);
            GameObject trunk = Renderer("Tronco", group, cylinder, Materials["M_Frutiger_Trunk"],
                ground + Vector3.up * (height * 0.5f), new Vector3(0.35f, height * 0.5f, 0.35f), false);
            CapsuleCollider capsule = trunk.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.5f;
            MarkStaticBaked(trunk);

            Mesh sphere = Builtin(PrimitiveType.Sphere);
            string canopy = index % 2 == 0 ? "M_Frutiger_Canopy" : "M_Frutiger_CanopyAqua";
            MarkStaticBaked(Renderer("Copa", group, sphere, Materials[canopy], ground + Vector3.up * (height + 0.6f), Vector3.one * 3.2f, false));
            MarkStaticBaked(Renderer("Copa_2", group, sphere, Materials[canopy], ground + new Vector3(0.9f, height - 0.2f, 0.4f), Vector3.one * 2.2f, false));
            MarkStaticBaked(Renderer("Copa_3", group, sphere, Materials[canopy], ground + new Vector3(-0.8f, height + 0.1f, -0.5f), Vector3.one * 2f, false));
        }

        private static void CloudCluster(Transform parent, Vector3 center, int index)
        {
            Transform group = new GameObject("Nube_" + (index + 1)).transform;
            group.SetParent(parent, false);
            group.position = center;
            Mesh sphere = Builtin(PrimitiveType.Sphere);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + index;
                var offset = new Vector3(Mathf.Cos(a) * (5f + i % 2 * 3f), (i % 3) * 1.6f, Mathf.Sin(a) * 3f);
                float size = 7f + (i % 3) * 3f;
                GameObject puff = Renderer("Bocanada_" + (i + 1), group, sphere, Materials["M_Frutiger_Cloud"],
                    center + offset, new Vector3(size * 1.3f, size, size), false);
                puff.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void FishSchoolAt(Transform parent, string name, Vector3 center, float radius, int count, Mesh fish, float angularSpeed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                float r = radius * (0.8f + 0.4f * ((i * 7) % 5) / 5f);
                var local = new Vector3(Mathf.Cos(a) * r, ((i * 3) % 4 - 1.5f) * 0.7f, Mathf.Sin(a) * r);
                var travel = new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)) * Mathf.Sign(angularSpeed);
                GameObject f = Renderer("Pez_" + (i + 1), go.transform, fish, Materials["M_Frutiger_Fish"],
                    center + local, Vector3.one * (1.4f + (i % 3) * 0.3f), false);
                f.transform.rotation = Quaternion.LookRotation(travel);
                f.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            FishSchool school = go.AddComponent<FishSchool>();
            var so = new SerializedObject(school);
            so.FindProperty("angularSpeed").floatValue = angularSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RibbonAt(Transform parent, string name, Vector3 center, float yaw, float length, float width,
                                     float amplitude, float depth, float phase, float twist, string material)
        {
            Mesh mesh = SaveMesh(FrutigerMeshes.Ribbon("FR_" + name, length, width, amplitude, depth, phase, twist), false);
            GameObject go = Renderer(name, parent, mesh, Materials[material], center, Vector3.one, false);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ================================================================== jugador independiente y puerta de entrada
        private static void BuildPlayerAndSystems()
        {
            // Se activan únicamente cuando alguien abre 20_Verano_Frutiger.unity directo en el editor;
            // al venir desde el Tutorial se conservan el jugador y el HUD que cruzan la puerta.
            var standalone = new GameObject("--- STANDALONE ---");
            var bootstrap = new GameObject("LevelBootstrap");
            bootstrap.AddComponent<StandaloneLevelContent>().Configure(standalone);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1Builder.Paths.Player);
            if (prefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Vector3 spawn = Ground(2.8f, -4.0f) + Vector3.up * 0.15f;
                player.transform.SetPositionAndRotation(spawn, Quaternion.identity);
                player.transform.SetParent(standalone.transform, true);
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
            eventSystem.transform.SetParent(standalone.transform, false);
            standalone.SetActive(false);
        }

        /// <summary>
        /// Puerta de inicio en la Isla 1: punto exacto donde desemboca <c>LevelGate_Verano</c> del Tutorial,
        /// mirando hacia los 3 objetos absorbibles y la bifurcación de las islas (+Z).
        /// </summary>
        private static void BuildEntrance()
        {
            var entrance = new GameObject("LevelEntrance");
            entrance.transform.SetParent(root, false);
            entrance.transform.SetPositionAndRotation(Ground(2.8f, -6.2f), Quaternion.identity);
            entrance.AddComponent<LevelEntrance>().Configure(string.Empty, LevelGateBuilder.Aperture);
        }

        // ================================================================== utilidades
        private static Transform Group(string name) => ChildGroup(root, name);

        private static Transform ChildGroup(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void MarkStaticBaked(GameObject go)
        {
            if (go == null) return;
            const StaticEditorFlags flags = StaticEditorFlags.ContributeGI |
                                            StaticEditorFlags.BatchingStatic |
                                            StaticEditorFlags.OccluderStatic |
                                            StaticEditorFlags.OccludeeStatic |
                                            StaticEditorFlags.ReflectionProbeStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
        }

        private static GameObject Renderer(string name, Transform parent, Mesh mesh, Material material,
                                           Vector3 position, Vector3 scale, bool meshCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (meshCollider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        private static readonly Dictionary<PrimitiveType, Mesh> BuiltinMeshes = new Dictionary<PrimitiveType, Mesh>();

        private static Mesh Builtin(PrimitiveType type)
        {
            if (BuiltinMeshes.TryGetValue(type, out Mesh cached) && cached != null) return cached;
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            BuiltinMeshes[type] = mesh;
            return mesh;
        }

        private static Vector3 Ground(float x, float z)
        {
            Physics.SyncTransforms();
            var origin = new Vector3(x, 200f, z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(x, 0f, z);
        }

        private static Mesh SaveMesh(Mesh mesh, bool generateLightmapUV)
        {
            if (generateLightmapUV)
            {
                Unwrapping.GenerateSecondaryUVSet(mesh);
            }

            string path = $"{MeshesRoot}/{mesh.name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            if (mesh.uv != null && mesh.uv.Length == mesh.vertexCount) existing.SetUVs(0, mesh.uv);
            if (mesh.uv2 != null && mesh.uv2.Length == mesh.vertexCount) existing.SetUVs(1, mesh.uv2);
            existing.SetTriangles(mesh.triangles, 0);
            existing.bounds = mesh.bounds;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
