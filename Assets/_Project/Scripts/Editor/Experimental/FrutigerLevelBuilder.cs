// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: FrutigerLevelBuilder.cs
// Propósito: construye desde cero el nivel experimental Frutiger Aero
// (rama experimental/frutiger-aero): materiales, mallas, iluminación,
// postprocesado, decoración y recorrido jugable.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using NostalgIA.Core;
using NostalgIA.EditorTools.Setup;
using NostalgIA.Gameplay.Level;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
using NostalgIA.Presentation.Environment;
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
    /// Constructor del nivel Frutiger Aero (menú <c>NostalgIA › Experimental</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Concepto.</b> Un archipiélago en una laguna turquesa bajo un cielo cian: islas low poly
    /// con playa, plataformas de plástico brillante, un pabellón de vidrio Aero, islas flotantes,
    /// burbujas iridiscentes por todas partes, cardúmenes de peces voladores, cintas de luz tipo
    /// aurora, haces de sol y un arcoíris. Paleta de Verano de la Guía de estilo §5.2; los colores
    /// semánticos (naranja de anclaje, cian de absorbible, verde de checkpoint) se reservan para
    /// jugabilidad.
    /// </para>
    /// <para>
    /// <b>Recorrido.</b> Isla Inicio → saltos por plataformas → Isla Pabellón (objetos
    /// absorbibles) → burbujas trampolín a una plataforma aérea y a la Isla Flotante → anclajes
    /// de gancho sobre el vacío → Isla Arcoíris (meta y estrella). Cuatro checkpoints y un
    /// volumen letal bajo la superficie del agua.
    /// </para>
    /// <para>
    /// <b>Regeneración.</b> La escena se reconstruye completa en cada ejecución (es un nivel
    /// generado); las mallas y los materiales se actualizan en sus assets conservando el GUID.
    /// </para>
    /// </remarks>
    public static class FrutigerLevelBuilder
    {
        private const string MenuRoot = "NostalgIA/Experimental/";
        public const string ScenePath = "Assets/_Project/Scenes/Stations/20_Verano_Frutiger.unity";
        private const string MaterialsRoot = "Assets/_Project/Art/Materials/Verano/Frutiger";
        private const string MeshesRoot = "Assets/_Project/Art/Meshes/Frutiger";
        public const string VolumePath = "Assets/_Project/Rendering/Volumes/VP_Verano.asset";
        public const string VolumePcPath = "Assets/_Project/Rendering/Volumes/VP_Verano_PC.asset";

        /// <summary>Shaders del nivel (los usa también la prueba de compilación).</summary>
        public static readonly string[] ShaderNames =
        {
            "NOS/AeroSky", "NOS/AeroGloss", "NOS/AeroBubble", "NOS/AeroBubbleSprite", "NOS/AeroWater",
            "NOS/AeroGlass", "NOS/AeroRibbon", "NOS/AeroGodRay", "NOS/AeroRainbow", "NOS/AeroFoam", "NOS/AeroFish"
        };

        // Paleta de Verano (Guía de estilo §5.2) y colores semánticos (§3).
        private static readonly Color SkyCyan = Hex(0x2EC5FF);
        private static readonly Color DeepBlue = Hex(0x0077D9);
        private static readonly Color Lime = Hex(0x7ED957);
        private static readonly Color Turquoise = Hex(0x3EE6D2);
        private static readonly Color Cloud = Hex(0xF4FBFF);
        private static readonly Color SunAccent = Hex(0xFFE27A);
        private static readonly Color AnchorOrange = Hex(0xFF8C1F);
        private static readonly Color AbsorbableCyan = Hex(0x3FE0FF);
        private static readonly Color CheckpointGreen = Hex(0x38E54D);

        /// <summary>Dirección hacia el sol: 60° a la derecha del avance inicial y 42° de elevación.</summary>
        private static readonly Vector3 TowardSun = new Vector3(0.644f, 0.669f, 0.372f).normalized;

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static Scene scene;
        private static Transform root;

        // ================================================================== menú
        [MenuItem(MenuRoot + "Construir nivel Frutiger Aero", priority = 200)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
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

            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            root = new GameObject("Frutiger").transform;

            BuildAtmosphere();
            BuildWater();
            BuildIslandsAndRoute();
            BuildDecor();
            BuildPlayerAndSystems();

            Physics.SyncTransforms();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Frutiger] Nivel construido en " + ScenePath);
        }

        /// <summary>
        /// Escribe en <c>Temp/frutiger_shaders.txt</c> (y en consola) los errores y advertencias de
        /// compilación de los shaders del nivel en la plataforma activa.
        /// </summary>
        [MenuItem(MenuRoot + "Diagnóstico de shaders", priority = 201)]
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

        /// <summary>Puntos de vista para revisar la composición: (posición, punto observado).</summary>
        private static readonly (string name, Vector3 eye, Vector3 target)[] Views =
        {
            ("01_inicio", new Vector3(0f, 5f, -9f), new Vector3(0f, 3f, 30f)),
            ("02_plataformas", new Vector3(3f, 6.5f, 26f), new Vector3(0f, 3f, 64f)),
            ("03_pabellon", new Vector3(7f, 4.6f, 55f), new Vector3(-1f, 3f, 66f)),
            ("04_hacia_isla_flotante", new Vector3(4f, 6f, 60f), new Vector3(28f, 16f, 86f)),
            ("05_hacia_meta", new Vector3(27f, 21f, 82f), new Vector3(72f, 4f, 118f)),
            ("06_meta", new Vector3(58f, 9f, 100f), new Vector3(74f, 5f, 121f)),
            ("07_panorama", new Vector3(-45f, 55f, -35f), new Vector3(35f, 0f, 70f)),
            ("08_cenital", new Vector3(35f, 380f, 59f), new Vector3(35f, 0f, 60f)),
            ("09_trampolines", new Vector3(1f, 6f, 57f), new Vector3(14f, 7f, 71f))
        };

        /// <summary>
        /// Renderiza la escena abierta desde puntos de vista fijos y guarda PNG en
        /// <c>Temp/frutiger_view_*.png</c> (revisión visual sin entrar en modo de juego).
        /// </summary>
        [MenuItem(MenuRoot + "Capturar vistas del nivel", priority = 202)]
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
                    go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
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

        // ================================================================== materiales
        private static void CreateMaterials()
        {
            Materials.Clear();

            Material sky = Mat("M_Frutiger_Sky", "NOS/AeroSky");
            sky.SetColor("_ZenithColor", new Color(0.11f, 0.56f, 0.95f));
            sky.SetColor("_HorizonColor", new Color(0.74f, 0.94f, 1f));
            sky.SetColor("_GroundColor", new Color(0.22f, 0.66f, 0.84f));

            // Islas: arena junto al agua → pasto lima arriba; motas blancas (flores).
            Gloss("M_Frutiger_Island", Lime, Hex(0xFFE9A8), new Vector2(0.2f, 1.8f), Hex(0xEBD9A6), 0.55f, 0.35f, 0.35f, true, true,
                speckle: new Color(1f, 0.97f, 0.78f, 1f), speckleAmount: 0.18f).SetFloat("_SpeckleDensity", 3.5f);
            Material skyIsland = Gloss("M_Frutiger_SkyIsland", Lime, Hex(0xF2E3BE), new Vector2(15f, 18.5f), Hex(0xF4E6C4), 0.85f, 0.45f, 0.5f, true, false,
                speckle: new Color(1f, 0.95f, 0.6f, 1f), speckleAmount: 0.18f);
            skyIsland.SetFloat("_SpeckleDensity", 3.5f);
            skyIsland.SetFloat("_Wrap", 0.45f);   // La base, siempre en sombra, recibe luz envuelta y no se ve sucia.
            Gloss("M_Frutiger_Seabed", Hex(0xF7EBC4), Hex(0xBFE6D4), new Vector2(-6f, -2.5f), Hex(0xF7EBC4), 0f, 0.1f, 0.1f, false, true);
            // Plataformas: plástico blanco con canto cian, brillo muy nítido.
            Material platform = Gloss("M_Frutiger_Platform", Cloud, Cloud, new Vector2(0f, 1f), SkyCyan, 1f, 0.8f, 1.5f, false, false);
            platform.SetFloat("_SlopeSharpness", 9f);
            platform.SetFloat("_ClearcoatPower", 900f);
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

            // Semánticos (Guía §3): anclaje naranja con pulso a 0,5 Hz, absorbible cian, checkpoint verde.
            Material anchor = Gloss("M_Frutiger_Anchor", AnchorOrange, AnchorOrange, new Vector2(0f, 1f), AnchorOrange, 0f, 0.8f, 1.5f, false, false);
            anchor.SetColor("_EmissionColor", AnchorOrange * 0.9f);
            anchor.SetFloat("_EmissionPulse", 0.5f);
            Material absorbable = Gloss("M_Frutiger_Absorbable", AbsorbableCyan, AbsorbableCyan, new Vector2(0f, 1f), AbsorbableCyan, 0f, 0.9f, 1.6f, false, false);
            absorbable.SetColor("_EmissionColor", AbsorbableCyan * 0.25f);
            Material checkpoint = Gloss("M_Frutiger_Checkpoint", CheckpointGreen, CheckpointGreen, new Vector2(0f, 1f), CheckpointGreen, 0f, 0.8f, 1.2f, false, false);
            checkpoint.SetColor("_EmissionColor", CheckpointGreen * 0.6f);
            checkpoint.SetFloat("_EmissionPulse", 0.8f);

            Material water = Mat("M_Frutiger_Water", "NOS/AeroWater");
            water.SetColor("_ShallowColor", Turquoise);
            water.SetColor("_DeepColor", DeepBlue);
            water.SetVector("_WaveA", new Vector4(1f, 0.3f, 0.08f, 18f));
            water.SetVector("_WaveB", new Vector4(-0.4f, 1f, 0.07f, 12f));
            water.SetVector("_WaveC", new Vector4(0.7f, -0.6f, 0.05f, 9f));

            // Campo de burbujas en modo impostor (malla Quad, 2 triángulos por burbuja).
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
            bounce.SetFloat("_DriftAmplitude", 0f);          // La burbuja trampolín no se aleja de su collider.
            bounce.SetFloat("_WobbleAmount", 0.04f);
            bounce.SetFloat("_EdgeAlpha", 0.9f);
            bounce.SetFloat("_BaseAlpha", 0.08f);
            bounce.SetColor("_Tint", new Color(1f, 0.92f, 1f));
            bounce.SetFloat("_Iridescence", 2f);
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

        /// <summary>Material NOS/AeroGloss con los parámetros más usados.</summary>
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
            m.SetFloat("_Intensity", 0.75f);   // Sin saturar a blanco con el Bloom: se conserva el color.
            m.SetFloat("_Filaments", 0.9f);
        }

        /// <summary>Carga o crea un material como asset (conserva el GUID si ya existe).</summary>
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
            // VP_Verano: luminoso y saturado, Bloom suave sobre los brillos del plástico y el agua.
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

            // VP_Verano_PC: destello de lente en espacio de pantalla, solo en PC (Guía §5.2).
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

        // ================================================================== ambiente
        private static void BuildAtmosphere()
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
            sun.shadowStrength = 0.75f;   // Sombras suaves: el cielo cian rellena las zonas en sombra.
            RenderSettings.sun = sun;

            RenderSettings.skybox = Materials["M_Frutiger_Sky"];
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.82f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.86f, 0.92f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.56f, 0.48f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.94f, 1f);
            RenderSettings.fogStartDistance = 90f;
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
        }

        private static void BuildWater()
        {
            Transform group = Group("Laguna");
            Mesh grid = SaveMesh(FrutigerMeshes.WaterGrid("FR_WaterGrid", 520f, 160));
            GameObject water = Renderer("Agua", group, grid, Materials["M_Frutiger_Water"], new Vector3(35f, 0f, 60f), Vector3.one, false);
            water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Lecho marino: plano de arena con cáusticas a 6 m de profundidad.
            Mesh plane = Builtin(PrimitiveType.Plane);
            Renderer("LechoMarino", group, plane, Materials["M_Frutiger_Seabed"], new Vector3(35f, -6f, 60f), new Vector3(52f, 1f, 52f), false);

            // Volumen letal bajo la superficie: el jugador se hunde un poco y reaparece.
            var kill = new GameObject("KillVolume_Agua");
            kill.transform.SetParent(group, false);
            kill.transform.position = new Vector3(35f, -4.5f, 60f);
            BoxCollider box = kill.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(600f, 4f, 600f);
            kill.AddComponent<KillVolume>();
            kill.layer = Layers.Trigger;
        }

        // ================================================================== recorrido
        private static readonly List<Checkpoint> Checkpoints = new List<Checkpoint>();

        private static void BuildIslandsAndRoute()
        {
            Checkpoints.Clear();
            Transform group = Group("Recorrido");
            Mesh foamRing = SaveMesh(FrutigerMeshes.FoamRing("FR_FoamRing"));

            // Isla Inicio.
            Island(group, "IslaInicio", new Vector3(0f, 0f, 0f), 16f, 3f, 101, foamRing);
            Vector3 spawn = Ground(0f, -6f);
            AddCheckpoint(group, spawn, 0f, "Isla Inicio");

            // Plataformas de plástico sobre el agua (saltos de 2–3 m con desnivel ≤ 0,9 m).
            Vector3[] path =
            {
                new Vector3(0f, 1.6f, 20f), new Vector3(3.5f, 2.3f, 26f), new Vector3(-1.5f, 3.1f, 32f),
                new Vector3(2.5f, 3.6f, 38f), new Vector3(-0.5f, 2.9f, 44f), new Vector3(1f, 2.2f, 49.5f)
            };
            for (int i = 0; i < path.Length; i++) Disc(group, $"Plataforma_{i + 1}", path[i], 2.6f, 0.6f);

            // Isla Pabellón: vidrio Aero y absorbibles.
            Island(group, "IslaPabellon", new Vector3(0f, 0f, 64f), 13f, 3f, 202, foamRing);
            AddCheckpoint(group, Ground(-2f, 58f), 0f, "Isla Pabellón");
            Pavilion(group, Ground(-1f, 66f));
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                Vector3 p = Ground(-1f + Mathf.Cos(a) * 5.5f, 66f + Mathf.Sin(a) * 5.5f) + Vector3.up * 0.6f;
                AddAbsorbable(group, p, 0.42f + 0.06f * (i % 3));
            }

            // Burbujas trampolín: isla → plataforma aérea → Isla Flotante.
            Vector3 bounce1 = Ground(9f, 68f) + Vector3.up * 1.4f;
            AddBounceBubble(group, "BurbujaTrampolin_1", bounce1, 1.5f, 10f, 6f, new Vector3(18f, 0f, 74f));
            Vector3 airPlatform = new Vector3(18f, 10.5f, 74f);
            Disc(group, "PlataformaAerea", airPlatform, 3.4f, 0.7f);
            AddBounceBubble(group, "BurbujaTrampolin_2", airPlatform + new Vector3(1.8f, 1.45f, 1.2f), 1.4f, 11f, 6f, new Vector3(27f, 0f, 84f));

            Vector3 skyIsland = new Vector3(28f, 16f, 86f);
            FloatingIsland(group, "IslaFlotante", skyIsland, 9.5f, 2.2f, 9f, 303);
            AddCheckpoint(group, Ground(27f, 85f), 45f, "Isla Flotante");

            // Gancho: dos anclajes semánticos sobre el vacío, con discos de descanso debajo.
            Vector3[] anchors = { new Vector3(44f, 21f, 96f), new Vector3(57f, 19f, 106f) };
            for (int i = 0; i < anchors.Length; i++)
            {
                AddAnchor(group, $"Anclaje_{i + 1}", anchors[i]);
                Disc(group, $"Descanso_{i + 1}", anchors[i] + new Vector3(0f, -7.5f, -1f), 2.4f, 0.5f);
            }

            // Isla Arcoíris: meta, estrella y arco de vidrio.
            Vector3 goalCenter = new Vector3(72f, 0f, 118f);
            Island(group, "IslaArcoiris", goalCenter, 17f, 4f, 404, foamRing);
            AddCheckpoint(group, Ground(64f, 110f), 45f, "Isla Arcoíris");
            GoalRing(group, Ground(74f, 121f));
            AddStar(group, Ground(74f, 121f) + Vector3.up * 2.2f);

            for (int i = 0; i < Checkpoints.Count; i++)
                Checkpoints[i].Configure(i, "Punto de control: " + Checkpoints[i].name.Replace("Checkpoint_", string.Empty));
        }

        private static void Island(Transform parent, string name, Vector3 center, float radius, float top, int seed, Mesh foamRing)
        {
            Mesh mesh = SaveMesh(FrutigerMeshes.Island("FR_" + name, radius, top, 6f, seed));
            GameObject go = Renderer(name, parent, mesh, Materials["M_Frutiger_Island"], center, Vector3.one, true);
            go.isStatic = true;
            GameObject foam = Renderer(name + "_Espuma", go.transform, foamRing, Materials["M_Frutiger_Foam"], center + Vector3.up * 0.08f, new Vector3(radius, 1f, radius), false);
            foam.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Physics.SyncTransforms();
        }

        private static void FloatingIsland(Transform parent, string name, Vector3 center, float radius, float top, float depth, int seed)
        {
            Mesh mesh = SaveMesh(FrutigerMeshes.FloatingIsland("FR_" + name, radius, top, depth, seed));
            GameObject go = Renderer(name, parent, mesh, Materials["M_Frutiger_SkyIsland"], center, Vector3.one, true);
            go.isStatic = true;
            Physics.SyncTransforms();
        }

        /// <summary>Disco de plástico (cilindro achatado) con collider convexo.</summary>
        private static GameObject Disc(Transform parent, string name, Vector3 topCenter, float radius, float thickness)
        {
            Mesh cylinder = Builtin(PrimitiveType.Cylinder);
            // El cilindro del motor mide 2 m de alto: escala y = grosor / 2; se coloca para que la cara
            // superior quede en topCenter.
            GameObject go = Renderer(name, parent, cylinder, Materials["M_Frutiger_Platform"],
                topCenter - Vector3.up * (thickness * 0.5f), new Vector3(radius * 2f, thickness * 0.5f, radius * 2f), false);
            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.convex = true;
            go.isStatic = true;
            Physics.SyncTransforms();
            return go;
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

            // Señal visual: aro verde emisivo en el suelo y luz que cambia al activarse.
            Mesh cylinder = Builtin(PrimitiveType.Cylinder);
            Renderer("Aro", go.transform, cylinder, Materials["M_Frutiger_Checkpoint"], groundPoint + Vector3.up * 0.05f, new Vector3(2.6f, 0.04f, 2.6f), false);
            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = Vector3.up * 1.5f;
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 1.5f;
            light.color = new Color(0.7f, 0.9f, 1f);
            light.shadows = LightShadows.None;
        }

        private static void AddAbsorbable(Transform parent, Vector3 position, float radius)
        {
            GameObject go = Renderer("Absorbible_Orbe", parent, Builtin(PrimitiveType.Sphere),
                Materials["M_Frutiger_Absorbable"], position, Vector3.one * (radius * 2f), false);
            go.layer = Layers.Absorbable;
            go.AddComponent<SphereCollider>();
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = 0.6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<Absorbable>();
        }

        private static void AddBounceBubble(Transform parent, string name, Vector3 center, float radius, float height, float boost, Vector3 target)
        {
            GameObject go = Renderer(name, parent, Builtin(PrimitiveType.Sphere),
                Materials["M_Frutiger_BounceBubble"], center, Vector3.one * (radius * 2f), false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Vector3 toTarget = target - center;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(toTarget.normalized);
            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.55f;   // Un poco mayor que la malla (0,5): se activa al rozarla.
            // Anillo amarillo de acento solar bajo la burbuja: señala el trampolín a distancia.
            Renderer(name + "_Base", parent, Builtin(PrimitiveType.Cylinder), Materials["M_Frutiger_BouncePad"],
                center + Vector3.down * (radius - 0.02f), new Vector3(radius * 2.4f, 0.04f, radius * 2.4f), false);
            BounceBubble bounce = go.AddComponent<BounceBubble>();
            bounce.Configure(height, boost);
            go.layer = Layers.Trigger;
            go.AddComponent<BubbleWobble>();
        }

        private static void AddAnchor(Transform parent, string name, Vector3 position)
        {
            GameObject go = Renderer(name, parent, Builtin(PrimitiveType.Sphere),
                Materials["M_Frutiger_Anchor"], position, Vector3.one * 2.2f, false);
            go.layer = Layers.Grapple;
            go.AddComponent<SphereCollider>();
            // Columna decorativa de cromo que "sostiene" el anclaje (sin collider).
            Mesh cylinder = Builtin(PrimitiveType.Cylinder);
            Renderer(name + "_Pilar", go.transform, cylinder, Materials["M_Frutiger_Chrome"], position + Vector3.down * 3.2f, new Vector3(0.35f, 2.2f, 0.35f), false);
        }

        private static void AddStar(Transform parent, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Level/Star.prefab");
            if (prefab == null) return;
            var star = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            star.name = "Star_1";
            star.transform.SetParent(parent, true);
            star.transform.position = position;
        }

        private static void Pavilion(Transform parent, Vector3 ground)
        {
            Transform group = new GameObject("PabellonAero").transform;
            group.SetParent(parent, false);
            group.position = ground;
            Mesh cube = Builtin(PrimitiveType.Cube);
            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) / 4f * Mathf.PI * 2f;
                var offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3.2f;
                GameObject panel = Renderer("Vidrio_" + (i + 1), group, cube, Materials["M_Frutiger_Glass"],
                    ground + offset + Vector3.up * 1.8f, new Vector3(2.6f, 3.6f, 0.08f), false);
                panel.transform.rotation = Quaternion.LookRotation(-offset.normalized);
                panel.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Mesh cylinder = Builtin(PrimitiveType.Cylinder);
            Renderer("Techo", group, cylinder, Materials["M_Frutiger_Platform"], ground + Vector3.up * 3.75f, new Vector3(8.4f, 0.12f, 8.4f), false);
            Renderer("OrbeCromado", group, Builtin(PrimitiveType.Sphere), Materials["M_Frutiger_Chrome"], ground + Vector3.up * 4.6f, Vector3.one * 1.3f, false);
        }

        private static void GoalRing(Transform parent, Vector3 ground)
        {
            Mesh torus = SaveMesh(FrutigerMeshes.Torus("FR_Torus", 0.09f));
            GameObject ring = Renderer("ArcoMeta", parent, torus, Materials["M_Frutiger_Glass"], ground + Vector3.up * 4.2f, Vector3.one * 4f, false);
            ring.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            ring.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Mesh cylinder = Builtin(PrimitiveType.Cylinder);
            Renderer("BaseMeta", parent, cylinder, Materials["M_Frutiger_Chrome"], ground + Vector3.up * 0.15f, new Vector3(5f, 0.15f, 5f), false);
        }

        // ================================================================== decoración
        private static void BuildDecor()
        {
            Transform group = Group("Decoracion");
            Mesh sphere = Builtin(PrimitiveType.Sphere);

            // Campos de burbujas instanciadas (cientos, una llamada de dibujo cada uno).
            BubbleField(group, "BurbujasCercanas", new Vector3(35f, 9f, 62f), new Vector3(150f, 18f, 170f), 420, 0.1f, 1.2f, "M_Frutiger_Bubble", 11, Builtin(PrimitiveType.Quad));
            BubbleField(group, "BurbujasAltas", new Vector3(35f, 26f, 62f), new Vector3(220f, 22f, 240f), 60, 1.5f, 5.5f, "M_Frutiger_BubbleHero", 23, Builtin(PrimitiveType.Sphere));

            // Columnas de burbujas que suben del agua (partículas).
            Vector3[] streams = { new Vector3(-12f, 0f, 10f), new Vector3(14f, 0f, 30f), new Vector3(-14f, 0f, 48f), new Vector3(20f, 0f, 60f), new Vector3(45f, 0f, 92f), new Vector3(55f, 0f, 120f), new Vector3(88f, 0f, 104f) };
            for (int i = 0; i < streams.Length; i++) BubbleStream(group, "ColumnaBurbujas_" + (i + 1), streams[i]);

            // Árboles de copa brillante.
            Vector2[] trees = { new Vector2(-7f, 4f), new Vector2(6f, -5f), new Vector2(-4f, -9f), new Vector2(-6f, 58f), new Vector2(-8f, 70f), new Vector2(66f, 124f), new Vector2(80f, 112f), new Vector2(78f, 126f), new Vector2(62f, 116f), new Vector2(31f, 89f) };
            for (int i = 0; i < trees.Length; i++) Tree(group, Ground(trees[i].x, trees[i].y), i);

            // Nubes 3D de esferas brillantes.
            Vector3[] clouds = { new Vector3(-40f, 34f, 90f), new Vector3(95f, 38f, 60f), new Vector3(20f, 42f, 170f), new Vector3(110f, 32f, 150f), new Vector3(-30f, 30f, 20f), new Vector3(60f, 45f, -20f) };
            for (int i = 0; i < clouds.Length; i++) CloudCluster(group, clouds[i], i);

            // Canicas de vidrio flotantes.
            for (int i = 0; i < 10; i++)
            {
                float a = i * 2.39996f;   // Ángulo dorado: reparto uniforme sin patrones.
                Vector3 p = new Vector3(35f + Mathf.Cos(a) * (14f + i * 4f), 5f + (i % 4) * 2.5f, 64f + Mathf.Sin(a) * (14f + i * 4f));
                GameObject marble = Renderer("CanicaVidrio_" + (i + 1), group, sphere, Materials["M_Frutiger_GlassMarble"], p, Vector3.one * (1.2f + (i % 3) * 0.7f), false);
                marble.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            // Cardúmenes de peces voladores.
            Mesh fish = SaveMesh(FrutigerMeshes.Fish("FR_Fish"));
            FishSchoolAt(group, "Cardumen_Inicio", new Vector3(0f, 6f, 2f), 9f, 9, fish, 16f);
            FishSchoolAt(group, "Cardumen_Pabellon", new Vector3(2f, 7.5f, 64f), 8f, 8, fish, -20f);
            FishSchoolAt(group, "Cardumen_Arcoiris", new Vector3(72f, 8f, 118f), 11f, 10, fish, 14f);
            FishSchoolAt(group, "Cardumen_Cielo", new Vector3(40f, 24f, 90f), 14f, 12, fish, -10f);

            // Cintas de luz tipo aurora en el cielo, detrás del recorrido.
            RibbonAt(group, "CintaAurora_A", new Vector3(30f, 55f, 210f), 0f, 280f, 34f, 20f, 40f, 0.3f, 1.2f, "M_Frutiger_RibbonA");
            RibbonAt(group, "CintaAurora_B", new Vector3(150f, 48f, 90f), -80f, 240f, 26f, 16f, 30f, 1.7f, 2.2f, "M_Frutiger_RibbonB");
            RibbonAt(group, "CintaAurora_C", new Vector3(-70f, 45f, 120f), 75f, 220f, 24f, 14f, 25f, 2.6f, 1.6f, "M_Frutiger_RibbonC");

            // Haces de sol: conos alineados con la dirección de la luz, apoyados en las islas.
            Mesh cone = SaveMesh(FrutigerMeshes.LightCone("FR_LightCone", 5f, 9f));
            Vector3[] rayTargets = { new Vector3(-16f, 0f, 4f), new Vector3(0f, 0f, 64f), new Vector3(70f, 0f, 116f), new Vector3(28f, 12f, 86f), new Vector3(18f, 0f, 36f) };
            for (int i = 0; i < rayTargets.Length; i++)
            {
                GameObject ray = Renderer("HazSol_" + (i + 1), group, cone, Materials["M_Frutiger_GodRay"], rayTargets[i],
                    new Vector3(1f, 70f, 1f), false);
                ray.transform.rotation = Quaternion.FromToRotation(Vector3.up, TowardSun);
                ray.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            // Arcoíris lejano detrás de la meta.
            Mesh quad = Builtin(PrimitiveType.Quad);
            GameObject rainbow = Renderer("Arcoiris", group, quad, Materials["M_Frutiger_Rainbow"], new Vector3(80f, -8f, 260f), new Vector3(260f, 130f, 1f), false);
            rainbow.transform.position = new Vector3(80f, 57f, 260f);   // El centro del arco (uv.y = 0) queda a la altura del agua.
            rainbow.transform.rotation = Quaternion.LookRotation(new Vector3(-0.2f, 0f, 1f));
            rainbow.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void BubbleField(Transform parent, string name, Vector3 center, Vector3 size, int count, float minR, float maxR, string material, int seed, Mesh mesh)
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

        /// <summary>Columna de burbujas: emisor de caja sobre el agua, subida lenta y bamboleo.</summary>
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
            shape.rotation = new Vector3(-90f, 0f, 0f);   // La caja emite hacia arriba (+Y del mundo).
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
            GameObject trunk = Renderer("Tronco", group, cylinder, Materials["M_Frutiger_Trunk"], ground + Vector3.up * (height * 0.5f), new Vector3(0.35f, height * 0.5f, 0.35f), false);
            CapsuleCollider capsule = trunk.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.5f;
            Mesh sphere = Builtin(PrimitiveType.Sphere);
            string canopy = index % 2 == 0 ? "M_Frutiger_Canopy" : "M_Frutiger_CanopyAqua";
            Renderer("Copa", group, sphere, Materials[canopy], ground + Vector3.up * (height + 0.6f), Vector3.one * 3.2f, false);
            Renderer("Copa_2", group, sphere, Materials[canopy], ground + new Vector3(0.9f, height - 0.2f, 0.4f), Vector3.one * 2.2f, false);
            Renderer("Copa_3", group, sphere, Materials[canopy], ground + new Vector3(-0.8f, height + 0.1f, -0.5f), Vector3.one * 2f, false);
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
                GameObject puff = Renderer("Bocanada_" + (i + 1), group, sphere, Materials["M_Frutiger_Cloud"], center + offset, new Vector3(size * 1.3f, size, size), false);
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
                // Un giro positivo en Y (Unity, mano izquierda) es horario visto desde arriba: el punto
                // (cos a, sin a) avanza en la dirección (sin a, −cos a). El pez mira hacia donde viaja.
                var travel = new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)) * Mathf.Sign(angularSpeed);
                GameObject f = Renderer("Pez_" + (i + 1), go.transform, fish, Materials["M_Frutiger_Fish"], center + local, Vector3.one * (1.4f + (i % 3) * 0.3f), false);
                f.transform.rotation = Quaternion.LookRotation(travel);
                f.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            FishSchool school = go.AddComponent<FishSchool>();
            var so = new SerializedObject(school);
            so.FindProperty("angularSpeed").floatValue = angularSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RibbonAt(Transform parent, string name, Vector3 center, float yaw, float length, float width, float amplitude, float depth, float phase, float twist, string material)
        {
            Mesh mesh = SaveMesh(FrutigerMeshes.Ribbon("FR_" + name, length, width, amplitude, depth, phase, twist));
            GameObject go = Renderer(name, parent, mesh, Materials[material], center, Vector3.one, false);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ================================================================== jugador y sistemas
        private static void BuildPlayerAndSystems()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1Builder.Paths.Player);
            if (prefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Vector3 spawn = Ground(0f, -6f) + Vector3.up * 0.15f;
                player.transform.SetPositionAndRotation(spawn, Quaternion.identity);
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        // ================================================================== utilidades
        private static Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            return go.transform;
        }

        /// <summary>Crea un objeto con malla y material; opcionalmente con MeshCollider.</summary>
        private static GameObject Renderer(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale, bool meshCollider)
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

        /// <summary>Malla primitiva del motor (se toma de un primitivo temporal y se cachea).</summary>
        private static Mesh Builtin(PrimitiveType type)
        {
            if (BuiltinMeshes.TryGetValue(type, out Mesh cached) && cached != null) return cached;
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            BuiltinMeshes[type] = mesh;
            return mesh;
        }

        /// <summary>Altura del suelo en (x, z) con un rayo hacia abajo sobre los colliders ya creados.</summary>
        private static Vector3 Ground(float x, float z)
        {
            Physics.SyncTransforms();
            var origin = new Vector3(x, 200f, z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(x, 0f, z);
        }

        /// <summary>Guarda la malla como asset (reemplaza el contenido conservando el GUID).</summary>
        private static Mesh SaveMesh(Mesh mesh)
        {
            string path = $"{MeshesRoot}/{mesh.name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            // Copia explícita de los datos: EditorUtility.CopySerialized deja en memoria el búfer de
            // vértices anterior cuando cambia la cantidad de vértices (la malla se dibujaba recortada).
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            if (mesh.uv != null && mesh.uv.Length == mesh.vertexCount) existing.SetUVs(0, mesh.uv);
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
