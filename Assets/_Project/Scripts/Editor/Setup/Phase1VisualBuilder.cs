// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: Phase1VisualBuilder.cs
// Propósito: constructor idempotente del acabado visual de la Fase 1: materiales
// de Estación, partículas reactivas, perfiles de postprocesado, iluminación de
// Primavera, iconos táctiles, glifos y capa de pausa (tareas F1-B02, F1-B03).
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using NostalgIA.Input;
using NostalgIA.Input.TouchControls;
using NostalgIA.Presentation.CameraFX;
using NostalgIA.Presentation.UI;
using NostalgIA.Presentation.VFX;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NostalgIA.EditorTools.Setup
{
    /// <summary>
    /// Constructor del acabado visual de la Fase 1 (menú <c>NostalgIA › Fase 1 › Acabado visual</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Igual que <see cref="Phase1Builder"/>, en este módulo se decidió producir los assets
    /// visuales con código de editor: materiales, sistemas de partículas y perfiles de
    /// postprocesado quedan descritos con sus valores y su justificación, y repetir el montaje no
    /// duplica nada (cada paso busca antes de crear). Todo se crea como <b>asset</b> en tiempo de
    /// edición; en ejecución no se instancia ningún material (regla de rendimiento del proyecto).
    /// </para>
    /// <para>Pasos de <see cref="BuildAll"/>:</para>
    /// <list type="number">
    /// <item>Importación de los iconos táctiles como sprites.</item>
    /// <item>Materiales: kit de Primavera y Verano (<c>NOS/StationKit</c>), partículas
    /// (<c>NOS/DataParticle</c>) y cielo de Primavera.</item>
    /// <item>Perfiles <c>VP_Primavera</c> y <c>VP_Feedback</c> (TDD §8.4).</item>
    /// <item>Juego de glifos de entrada (<c>InputGlyphSet</c>).</item>
    /// <item><c>GameRoot</c>: director de ráfagas, volumen de retroalimentación y capa de pausa.</item>
    /// <item>Controles táctiles con iconos.</item>
    /// <item><c>Player.prefab</c>: postprocesado en la cámara y vórtice de succión.</item>
    /// <item>Calidad móvil: HDR desactivado en <c>Mobile_RPAsset</c>.</item>
    /// <item>Tutorial: sustitución del material de ProBuilder, volumen global e iluminación.</item>
    /// </list>
    /// </remarks>
    public static class Phase1VisualBuilder
    {
        // ------------------------------------------------------------------ rutas
        private const string MenuRoot = "NostalgIA/Fase 1/Acabado visual/";
        private const string MaterialsRoot = "Assets/_Project/Art/Materials";
        private const string VolumesRoot = "Assets/_Project/Rendering/Volumes";

        /// <summary>Rutas de los assets visuales (las usan también las pruebas).</summary>
        public static class Paths
        {
            public const string KitPrimavera = MaterialsRoot + "/Primavera/M_Primavera_Kit.mat";
            public const string KitVerano = MaterialsRoot + "/Verano/M_Verano_Kit.mat";
            public const string SkyPrimavera = MaterialsRoot + "/Primavera/SKY_Primavera.mat";
            public const string VfxDataSquare = MaterialsRoot + "/VFX/M_VFX_DataSquare.mat";
            public const string VfxDataSpark = MaterialsRoot + "/VFX/M_VFX_DataSpark.mat";
            public const string VfxDataBits = MaterialsRoot + "/VFX/M_VFX_DataBits.mat";
            public const string VolumePrimavera = VolumesRoot + "/VP_Primavera.asset";
            public const string VolumeFeedback = VolumesRoot + "/VP_Feedback.asset";
            public const string GlyphSet = "Assets/_Project/Data/Input/InputGlyphSet_Default.asset";
            public const string TouchIcons = "Assets/_Project/Art/UI/Sprites/Touch";
            public const string MobilePipeline = "Assets/_Project/Rendering/URP/Mobile_RPAsset.asset";
            public const string StationKitShader = "NOS/StationKit";
            public const string DataParticleShader = "NOS/DataParticle";
        }

        /// <summary>Nombres de los iconos táctiles (sin extensión).</summary>
        public static readonly string[] TouchIconNames =
        {
            "Icon_Jump", "Icon_Crouch", "Icon_SwitchTool", "Icon_Pause", "Icon_Vacuum", "Icon_Grapple"
        };

        /// <summary>
        /// Canales de vértice de las partículas: posición, color, UV (TEXCOORD0.xy) y
        /// StableRandom.x, que Unity empaqueta en TEXCOORD0.z y el shader usa para el dígito y la
        /// fase del parpadeo.
        /// </summary>
        private static readonly List<ParticleSystemVertexStream> ParticleStreams = new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.StableRandomX
        };

        // Paleta de la Guía de estilo (§3 y §5).
        private static readonly Color NightBlue = Hex(0x06102B);
        private static readonly Color ScreenBlue = Hex(0x0A2A9E);
        private static readonly Color CodeBlue = Hex(0x3A6BFF);
        private static readonly Color Phosphor = Hex(0xE8F4FF);
        private static readonly Color ColumnGray = Hex(0x8C8F96);
        private static readonly Color AbsorbableCyan = Hex(0x3FE0FF);
        private static readonly Color AnchorOrange = Hex(0xFF8C1F);
        private static readonly Color EnemyRed = Hex(0xFF3B5C);

        // ================================================================== menús
        /// <summary>Ejecuta todos los pasos visuales en orden.</summary>
        [MenuItem(MenuRoot + "Montar acabado visual", priority = 130)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAllWithoutPrompt();
        }

        /// <summary>Versión sin diálogo (la invoca <see cref="Phase1Builder.BuildAll"/>).</summary>
        public static void BuildAllWithoutPrompt()
        {
            ConfigureTouchIconImporters();
            CreateMaterials();
            CreateVolumeProfiles();
            CreateGlyphSet();
            ConfigureGameRootVisuals();
            ConfigureTouchIcons();
            ConfigurePlayerVisuals();
            ConfigureMobilePipeline();
            ConfigureTutorialVisuals();
            AssetDatabase.SaveAssets();
            Debug.Log("[Fase 1] Acabado visual montado.");
        }

        // ================================================================== 1. iconos
        /// <summary>Importa los iconos táctiles como sprites sin mipmaps (siempre se ven a tamaño fijo).</summary>
        [MenuItem(MenuRoot + "1. Importar iconos táctiles", priority = 131)]
        public static void ConfigureTouchIconImporters()
        {
            foreach (string name in TouchIconNames)
            {
                string path = $"{Paths.TouchIcons}/{name}.png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    Debug.LogWarning($"[Fase 1] Falta el icono {path}.");
                    continue;
                }
                bool changed = importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled
                               || importer.maxTextureSize != 256 || !importer.alphaIsTransparency;
                if (!changed) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;          // Tamaño en pantalla constante: los mipmaps solo gastarían memoria.
                importer.alphaIsTransparency = true;     // Evita halos oscuros al filtrar los bordes.
                importer.maxTextureSize = 256;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
        }

        /// <summary>Carga el sprite de un icono táctil.</summary>
        public static Sprite LoadTouchIcon(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{Paths.TouchIcons}/{name}.png");

        // ================================================================== 2. materiales
        /// <summary>Crea (o actualiza) los materiales de la fase.</summary>
        [MenuItem(MenuRoot + "2. Materiales", priority = 132)]
        public static void CreateMaterials()
        {
            Shader kit = Shader.Find(Paths.StationKitShader);
            Shader particle = Shader.Find(Paths.DataParticleShader);
            if (kit == null || particle == null)
            {
                Debug.LogError("[Fase 1] No se encontraron los shaders NOS/StationKit o NOS/DataParticle (¿errores de compilación?).");
                return;
            }

            // Primavera: los valores por defecto del shader ya son la paleta de la Estación
            // (piso #0A2A9E/#06102B, muros #8C8F96 → #0A2A9E, rejilla #3A6BFF); se escriben
            // explícitamente para que el material no dependa de cambios futuros del shader.
            Material primavera = LoadOrCreateMaterial(Paths.KitPrimavera, kit);
            primavera.SetColor("_FloorColorA", ScreenBlue);
            primavera.SetColor("_FloorColorB", NightBlue);
            primavera.SetColor("_CeilingColor", NightBlue);
            primavera.SetColor("_WallColorBottom", ColumnGray);
            primavera.SetColor("_WallColorTop", ScreenBlue);
            primavera.SetColor("_LineColor", CodeBlue);
            primavera.SetFloat("_WallHeight", 6f);
            primavera.SetFloat("_GridScale", 1f);
            primavera.SetFloat("_LineWidth", 0.03f);
            primavera.SetFloat("_LineEmission", 0.6f);
            primavera.SetFloat("_CodeDensity", 0.35f);
            primavera.SetFloat("_CodeEmission", 0.5f);
            primavera.SetFloat("_ScanSpeed", 1.5f);
            primavera.SetFloat("_ScanEmission", 0.25f);
            primavera.SetFloat("_PixelsPerMeter", 32f);   // 32 px por metro (Guía de estilo §5.1).
            primavera.SetFloat("_GrainStrength", 0.04f);
            primavera.SetFloat("_Smoothness", 0.3f);
            primavera.SetFloat("_DamagePulseTint", 0.35f);
            primavera.enableInstancing = true;
            EditorUtility.SetDirty(primavera);

            // Verano: exterior luminoso. Piso lima/turquesa, muros de nube a cielo y rejilla de
            // acento solar con poca emisión (la luz clave domina en esta Estación).
            Material verano = LoadOrCreateMaterial(Paths.KitVerano, kit);
            verano.SetColor("_FloorColorA", Hex(0x7ED957));
            verano.SetColor("_FloorColorB", Hex(0x3EE6D2));
            verano.SetColor("_CeilingColor", Hex(0xF4FBFF));
            verano.SetColor("_WallColorBottom", Hex(0xF4FBFF));
            verano.SetColor("_WallColorTop", Hex(0x2EC5FF));
            verano.SetColor("_LineColor", Hex(0xFFE27A));
            verano.SetFloat("_WallHeight", 8f);
            verano.SetFloat("_GridScale", 0.5f);
            verano.SetFloat("_LineWidth", 0.02f);
            verano.SetFloat("_LineEmission", 0.15f);
            verano.SetFloat("_CodeDensity", 0f);          // Sin "código" en Verano.
            verano.SetFloat("_CodeEmission", 0f);
            verano.SetFloat("_ScanSpeed", 0f);
            verano.SetFloat("_ScanEmission", 0f);
            verano.SetFloat("_PixelsPerMeter", 16f);
            verano.SetFloat("_GrainStrength", 0.03f);
            verano.SetFloat("_Smoothness", 0.5f);
            verano.SetFloat("_DamagePulseTint", 0.35f);
            verano.enableInstancing = true;
            EditorUtility.SetDirty(verano);

            // Partículas: el color lo pone cada sistema (startColor); el material fija la forma.
            ConfigureParticleMaterial(LoadOrCreateMaterial(Paths.VfxDataSquare, particle), 1.6f, 0f, 2.5f, 22f);
            ConfigureParticleMaterial(LoadOrCreateMaterial(Paths.VfxDataSpark, particle), 2.6f, 0f, 5f, 30f);
            ConfigureParticleMaterial(LoadOrCreateMaterial(Paths.VfxDataBits, particle), 2.2f, 1f, 2.5f, 12f);

            // Cielo de Primavera: procedural del motor con tonos de noche azul; sin sol visible
            // (la luz clave es cenital y la Estación es un interior simulado).
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                Material sky = LoadOrCreateMaterial(Paths.SkyPrimavera, skyShader);
                sky.SetFloat("_SunDisk", 0f);                              // 0 = sin disco solar.
                sky.SetColor("_SkyTint", new Color(0.16f, 0.24f, 0.62f));
                sky.SetColor("_GroundColor", NightBlue);
                sky.SetFloat("_AtmosphereThickness", 0.55f);
                sky.SetFloat("_Exposure", 0.55f);
                EditorUtility.SetDirty(sky);
            }
        }

        /// <summary>Parámetros del shader de partículas.</summary>
        private static void ConfigureParticleMaterial(Material material, float intensity, float glyphMix, float sharpness, float flicker)
        {
            material.SetColor("_TintColor", Color.white);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_GlyphMix", glyphMix);
            material.SetFloat("_HaloSharpness", sharpness);
            material.SetFloat("_FlickerSpeed", flicker);
            material.enableInstancing = false;   // Las partículas se agrupan por el propio sistema.
            EditorUtility.SetDirty(material);
        }

        /// <summary>Carga un material o lo crea como asset con el shader indicado.</summary>
        private static Material LoadOrCreateMaterial(string path, Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                if (material.shader != shader) material.shader = shader;
                return material;
            }
            Phase1Builder.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ================================================================== 3. postprocesado
        /// <summary>Crea los perfiles de Volume de la fase (TDD §8.4).</summary>
        [MenuItem(MenuRoot + "3. Perfiles de postprocesado", priority = 133)]
        public static void CreateVolumeProfiles()
        {
            // VP_Primavera: tonemapping neutro, contraste alto, Bloom solo en emisivos (umbral 0,9)
            // y viñeta leve que enmarca la vista sin oscurecer las señales de jugabilidad.
            VolumeProfile primavera = LoadOrCreateProfile(Paths.VolumePrimavera);
            Tonemapping tonemap = GetOrAddOverride<Tonemapping>(primavera);
            tonemap.mode.Override(TonemappingMode.Neutral);
            ColorAdjustments grading = GetOrAddOverride<ColorAdjustments>(primavera);
            grading.contrast.Override(20f);
            grading.saturation.Override(-10f);
            Bloom bloom = GetOrAddOverride<Bloom>(primavera);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(false);   // El filtrado bicúbico duplica el costo en móvil.
            Vignette vignette = GetOrAddOverride<Vignette>(primavera);
            vignette.color.Override(Color.black);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.4f);
            EditorUtility.SetDirty(primavera);

            // VP_Feedback: aspecto del golpe con peso 1. Solo sobrescribe lo que cambia; el resto
            // (contraste, Bloom) se hereda del perfil de la Estación al mezclar por peso.
            VolumeProfile feedback = LoadOrCreateProfile(Paths.VolumeFeedback);
            Vignette hurtVignette = GetOrAddOverride<Vignette>(feedback);
            hurtVignette.color.Override(EnemyRed);
            hurtVignette.intensity.Override(0.45f);
            hurtVignette.smoothness.Override(0.6f);
            ChromaticAberration aberration = GetOrAddOverride<ChromaticAberration>(feedback);
            aberration.intensity.Override(0.6f);
            ColorAdjustments hurtGrading = GetOrAddOverride<ColorAdjustments>(feedback);
            hurtGrading.saturation.Override(-35f);
            EditorUtility.SetDirty(feedback);
        }

        /// <summary>Carga o crea un perfil de Volume.</summary>
        private static VolumeProfile LoadOrCreateProfile(string path)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;
            Phase1Builder.EnsureFolder(VolumesRoot);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        /// <summary>
        /// Devuelve el override de tipo <typeparamref name="T"/> del perfil, creándolo como
        /// sub-asset si falta (mismo procedimiento que el inspector de Volume).
        /// </summary>
        private static T GetOrAddOverride<T>(VolumeProfile profile) where T : VolumeComponent
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

        // ================================================================== 4. glifos
        /// <summary>Crea el juego de glifos con las asignaciones de <c>InputSystem_Actions</c>.</summary>
        [MenuItem(MenuRoot + "4. Juego de glifos", priority = 134)]
        public static void CreateGlyphSet()
        {
            Phase1Builder.EnsureFolder("Assets/_Project/Data/Input");
            InputGlyphSet set = AssetDatabase.LoadAssetAtPath<InputGlyphSet>(Paths.GlyphSet);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<InputGlyphSet>();
                AssetDatabase.CreateAsset(set, Paths.GlyphSet);
            }

            // (acción, nombre, teclado y ratón, mando Xbox, táctil, icono)
            (string action, string display, string kbm, string pad, string touch, string icon)[] rows =
            {
                (InputActionNames.Move, "Moverse", "W A S D", "Stick izq.", "Pad izquierdo", null),
                (InputActionNames.LookDelta, "Mirar", "Ratón", "Stick der.", "Arrastra a la derecha", null),
                (InputActionNames.LookStick, "Mirar", "Ratón", "Stick der.", "Arrastra a la derecha", null),
                (InputActionNames.Jump, "Saltar", "Espacio", "A", "Saltar", "Icon_Jump"),
                (InputActionNames.Sprint, "Correr", "Shift", "L3", "Pad al máximo", null),
                (InputActionNames.Crouch, "Agacharse / deslizar", "C", "R3", "Agachar", "Icon_Crouch"),
                (InputActionNames.SwitchTool, "Cambiar herramienta", "F", "Y", "Cambiar", "Icon_SwitchTool"),
                (InputActionNames.Absorb, "Absorber", "Clic der.", "LT", "Mantén el pad", "Icon_Vacuum"),
                (InputActionNames.Fire, "Disparar", "Clic izq.", "RT", "Toca el pad", "Icon_Vacuum"),
                (InputActionNames.Grapple, "Enganchar (modo gancho)", "Clic der.", "RT", "Toca el pad", "Icon_Grapple"),
                (InputActionNames.Pause, "Pausa", "Esc", "Menú", "Pausa", "Icon_Pause")
            };

            var so = new SerializedObject(set);
            SerializedProperty entries = so.FindProperty("entries");
            entries.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                SerializedProperty e = entries.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("action").stringValue = rows[i].action;
                e.FindPropertyRelative("displayName").stringValue = rows[i].display;
                e.FindPropertyRelative("keyboardMouse").stringValue = rows[i].kbm;
                e.FindPropertyRelative("gamepad").stringValue = rows[i].pad;
                e.FindPropertyRelative("touch").stringValue = rows[i].touch;
                e.FindPropertyRelative("touchIcon").objectReferenceValue = rows[i].icon != null ? LoadTouchIcon(rows[i].icon) : null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
        }

        // ================================================================== 5. GameRoot
        /// <summary>Agrega a <c>GameRoot</c> las ráfagas, el volumen de retroalimentación y la pausa.</summary>
        [MenuItem(MenuRoot + "5. GameRoot", priority = 135)]
        public static void ConfigureGameRootVisuals()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Phase1Builder.Paths.GameRoot);
            try
            {
                Transform vfx = Phase1Builder.GetOrCreateChild(root.transform, "VFX");
                var square = AssetDatabase.LoadAssetAtPath<Material>(Paths.VfxDataSquare);
                var spark = AssetDatabase.LoadAssetAtPath<Material>(Paths.VfxDataSpark);
                var bits = AssetDatabase.LoadAssetAtPath<Material>(Paths.VfxDataBits);

                ParticleSystem launch = ConfigureLaunchBurst(GetOrCreateSystem(vfx, "LaunchBurst", square));
                ParticleSystem anchor = ConfigureAnchorSparks(GetOrCreateSystem(vfx, "AnchorSparks", spark));
                ParticleSystem collect = ConfigureCollectBits(GetOrCreateSystem(vfx, "CollectBits", bits));

                GameplayVfxDirector director = Phase1Builder.GetOrAdd<GameplayVfxDirector>(vfx.gameObject);
                var directorSo = new SerializedObject(director);
                directorSo.FindProperty("launchBurst").objectReferenceValue = launch;
                directorSo.FindProperty("anchorSparks").objectReferenceValue = anchor;
                directorSo.FindProperty("collectBits").objectReferenceValue = collect;
                directorSo.ApplyModifiedPropertiesWithoutUndo();

                // Volumen global de retroalimentación: prioridad 100 para imponerse al perfil de la
                // Estación; peso 0 hasta que PostFXController lo anime.
                Transform postFx = Phase1Builder.GetOrCreateChild(root.transform, "PostFX");
                Volume volume = Phase1Builder.GetOrAdd<Volume>(postFx.gameObject);
                volume.isGlobal = true;
                volume.priority = 100f;
                volume.weight = 0f;
                volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Paths.VolumeFeedback);
                PostFXController controller = Phase1Builder.GetOrAdd<PostFXController>(postFx.gameObject);
                Phase1Builder.SetObject(controller, "feedbackVolume", volume);

                BuildPauseOverlay(root.transform);

                PrefabUtility.SaveAsPrefabAsset(root, Phase1Builder.Paths.GameRoot);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Destello de salida: chorro corto y estrecho en la dirección del disparo.</summary>
        private static ParticleSystem ConfigureLaunchBurst(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ConfigureBurstMain(ps, 64, new Vector2(0.2f, 0.4f), new Vector2(3f, 9f), new Vector2(0.05f, 0.12f));
            main.startColor = new ParticleSystem.MinMaxGradient(AbsorbableCyan, Phosphor);
            main.gravityModifier = 0f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16f;
            shape.radius = 0.05f;
            SetFadeAndShrink(ps);
            SetDrag(ps, 4f);
            return ps;
        }

        /// <summary>Chispas de anclaje: abanico amplio sobre la normal, con gravedad.</summary>
        private static ParticleSystem ConfigureAnchorSparks(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ConfigureBurstMain(ps, 64, new Vector2(0.25f, 0.55f), new Vector2(2f, 6f), new Vector2(0.03f, 0.07f));
            main.startColor = new ParticleSystem.MinMaxGradient(AnchorOrange, Hex(0xFFD9A0));
            main.gravityModifier = 1.2f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.08f;
            SetFadeAndShrink(ps);
            SetDrag(ps, 1.5f);
            // Estiradas según la velocidad: se leen como chispas en lugar de puntos.
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
            return ps;
        }

        /// <summary>Bits ascendentes «0/1» de estrellas y checkpoints (el color lo fija el director).</summary>
        private static ParticleSystem ConfigureCollectBits(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ConfigureBurstMain(ps, 96, new Vector2(0.8f, 1.4f), new Vector2(1.2f, 3f), new Vector2(0.16f, 0.28f));
            main.startColor = Color.white;
            main.gravityModifier = -0.1f;   // Flotan hacia arriba al frenar.
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.6f;
            SetFadeAndShrink(ps);
            SetDrag(ps, 1f);
            return ps;
        }

        /// <summary>
        /// Ajustes comunes de un sistema de ráfagas: en bucle sin emisión propia (solo
        /// <see cref="ParticleSystem.Emit(int)"/>), simulación en mundo y tope de partículas.
        /// </summary>
        private static ParticleSystem.MainModule ConfigureBurstMain(ParticleSystem ps, int maxParticles, Vector2 lifetime, Vector2 speed, Vector2 size)
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // Cada ráfaga queda donde nació.
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = maxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.useUnscaledTime = false;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            return main;
        }

        /// <summary>Atenuación y encogimiento al final de la vida (sin cortes bruscos).</summary>
        private static void SetFadeAndShrink(ParticleSystem ps)
        {
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
        }

        /// <summary>Arrastre lineal: la ráfaga se abre rápido y se frena, en lugar de viajar sin fin.</summary>
        private static void SetDrag(ParticleSystem ps, float drag)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(100f);  // Sin tope efectivo: solo nos interesa el arrastre.
            limit.drag = new ParticleSystem.MinMaxCurve(drag);
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        /// <summary>Obtiene o crea un sistema de partículas con su renderer configurado.</summary>
        private static ParticleSystem GetOrCreateSystem(Transform parent, string name, Material material)
        {
            Transform t = Phase1Builder.GetOrCreateChild(parent, name);
            ParticleSystem ps = Phase1Builder.GetOrAdd<ParticleSystem>(t.gameObject);
            var renderer = t.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortMode = ParticleSystemSortMode.None;  // Aditivo: el orden no altera el resultado.
            renderer.SetActiveVertexStreams(ParticleStreams);
            return ps;
        }

        /// <summary>Capa de pausa dentro de GameRoot (persistente entre escenas).</summary>
        private static void BuildPauseOverlay(Transform gameRoot)
        {
            Transform overlay = Phase1Builder.GetOrCreateUiChild(gameRoot, "PauseOverlay");
            Phase1Builder.ConfigureOverlayCanvas(overlay.gameObject, 200, true);
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;

            Transform backdrop = Phase1Builder.GetOrCreateUiChild(overlay, "Backdrop");
            Phase1Builder.Stretch((RectTransform)backdrop, Vector2.zero, Vector2.one);
            Image backdropImage = Phase1Builder.GetOrAdd<Image>(backdrop.gameObject);
            backdropImage.color = new Color(NightBlue.r, NightBlue.g, NightBlue.b, 0.78f);
            backdropImage.raycastTarget = true;   // Bloquea toques al juego mientras está en pausa.

            // Ventana: borde de 2 px en azul código (iconografía con contorno, Guía §7).
            Image border = Phase1Builder.GetOrCreateImage(overlay, "WindowBorder", Vector2.zero, new Vector2(664f, 544f), CodeBlue);
            border.raycastTarget = false;
            Image window = Phase1Builder.GetOrCreateImage(border.transform, "Window", Vector2.zero, new Vector2(660f, 540f), new Color(0.03f, 0.08f, 0.24f, 0.96f));
            window.raycastTarget = false;

            TextMeshProUGUI title = GetOrCreateText(window.transform, "Title", new Vector2(0f, 212f), new Vector2(600f, 70f), font, 46f, TextAlignmentOptions.Center);
            title.text = "PAUSA";
            title.characterSpacing = 18f;
            title.fontStyle = FontStyles.Bold;
            title.color = Phosphor;

            TextMeshProUGUI controls = GetOrCreateText(window.transform, "Controls", new Vector2(0f, 40f), new Vector2(560f, 250f), font, 24f, TextAlignmentOptions.TopLeft);
            controls.text = string.Empty;
            controls.lineSpacing = 14f;
            controls.color = Phosphor;

            Image buttonImage = Phase1Builder.GetOrCreateImage(window.transform, "ResumeButton", new Vector2(0f, -135f), new Vector2(360f, 60f), CodeBlue);
            buttonImage.raycastTarget = true;
            Button button = Phase1Builder.GetOrAdd<Button>(buttonImage.gameObject);
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.92f, 1f);
            colors.selectedColor = new Color(0.85f, 0.92f, 1f);
            colors.pressedColor = new Color(0.65f, 0.75f, 0.95f);
            button.colors = colors;
            TextMeshProUGUI hint = GetOrCreateText(buttonImage.transform, "Label", Vector2.zero, new Vector2(340f, 52f), font, 26f, TextAlignmentOptions.Center);
            hint.text = "Continuar";
            hint.color = Phosphor;

            Image nextButtonImage = Phase1Builder.GetOrCreateImage(window.transform, "NextLevelTestButton", new Vector2(0f, -206f), new Vector2(440f, 54f), new Color(0.11f, 0.36f, 0.68f, 0.96f));
            nextButtonImage.raycastTarget = true;
            Button nextButton = Phase1Builder.GetOrAdd<Button>(nextButtonImage.gameObject);
            nextButton.targetGraphic = nextButtonImage;
            nextButton.colors = colors;
            TextMeshProUGUI nextHint = GetOrCreateText(nextButtonImage.transform, "Label", Vector2.zero, new Vector2(420f, 48f), font, 21f, TextAlignmentOptions.Center);
            nextHint.text = "Siguiente nivel (Test)";
            nextHint.color = Phosphor;

            PauseOverlayPresenter presenter = Phase1Builder.GetOrAdd<PauseOverlayPresenter>(overlay.gameObject);
            var so = new SerializedObject(presenter);
            so.FindProperty("glyphs").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputGlyphSet>(Paths.GlyphSet);
            so.FindProperty("controlsText").objectReferenceValue = controls;
            so.FindProperty("resumeHintText").objectReferenceValue = hint;
            so.FindProperty("resumeButton").objectReferenceValue = button;
            so.FindProperty("nextLevelButton").objectReferenceValue = nextButton;
            so.FindProperty("nextLevelLabel").objectReferenceValue = nextHint;
            so.ApplyModifiedPropertiesWithoutUndo();

            // El lienzo empieza apagado: el presentador lo enciende solo en pausa.
            overlay.GetComponent<Canvas>().enabled = false;
        }

        /// <summary>Crea (o reutiliza) un texto TMP centrado en su padre.</summary>
        private static TextMeshProUGUI GetOrCreateText(Transform parent, string name, Vector2 position, Vector2 size, TMP_FontAsset font, float fontSize, TextAlignmentOptions alignment)
        {
            Transform t = Phase1Builder.GetOrCreateUiChild(parent, name);
            TextMeshProUGUI text = Phase1Builder.GetOrAdd<TextMeshProUGUI>(t.gameObject);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            if (font != null) text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        // ================================================================== 6. táctil
        /// <summary>Coloca los iconos en los botones táctiles y oculta las etiquetas de texto.</summary>
        [MenuItem(MenuRoot + "6. Iconos táctiles", priority = 136)]
        public static void ConfigureTouchIcons()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Phase1Builder.Paths.TouchControls);
            try
            {
                Transform safeArea = root.transform.Find("SafeArea");
                if (safeArea == null)
                {
                    Debug.LogError("[Fase 1] TouchControls.prefab no tiene SafeArea; ejecute antes el paso 4 de la Fase 1.");
                    return;
                }

                AssignIcon(safeArea, "JumpButton", "Icon_Jump");
                AssignIcon(safeArea, "CrouchButton", "Icon_Crouch");
                AssignIcon(safeArea, "SwitchToolButton", "Icon_SwitchTool");
                AssignIcon(safeArea, "PauseButton", "Icon_Pause");
                Image padIcon = AssignIcon(safeArea, "ToolPad", "Icon_Vacuum");

                Transform pad = safeArea.Find("ToolPad");
                if (pad != null && pad.TryGetComponent(out TouchToolPad toolPad))
                {
                    var so = new SerializedObject(toolPad);
                    so.FindProperty("icon").objectReferenceValue = padIcon;
                    so.FindProperty("vacuumIcon").objectReferenceValue = LoadTouchIcon("Icon_Vacuum");
                    so.FindProperty("grappleIcon").objectReferenceValue = LoadTouchIcon("Icon_Grapple");
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, Phase1Builder.Paths.TouchControls);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Agrega la imagen «Icon» (58 % del botón) y desactiva la etiqueta: los iconos son
        /// universales y legibles sin idioma (Guía de estilo §7).
        /// </summary>
        private static Image AssignIcon(Transform safeArea, string buttonName, string iconName)
        {
            Transform button = safeArea.Find(buttonName);
            if (button == null) return null;
            var buttonRect = (RectTransform)button;
            float side = Mathf.Min(buttonRect.sizeDelta.x, buttonRect.sizeDelta.y) * 0.58f;
            Image icon = Phase1Builder.GetOrCreateImage(button, "Icon", Vector2.zero, new Vector2(side, side), Color.white);
            icon.sprite = LoadTouchIcon(iconName);
            icon.preserveAspect = true;
            icon.raycastTarget = false;   // El toque lo recibe el botón, no el icono.
            Transform label = button.Find("Label");
            if (label != null) label.gameObject.SetActive(false);
            return icon;
        }

        // ================================================================== 7. jugador
        /// <summary>Activa el postprocesado en la cámara y agrega el vórtice de succión a la herramienta.</summary>
        [MenuItem(MenuRoot + "7. Player.prefab", priority = 137)]
        public static void ConfigurePlayerVisuals()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Phase1Builder.Paths.Player);
            try
            {
                Camera camera = root.GetComponentInChildren<Camera>(true);
                if (camera != null)
                {
                    UniversalAdditionalCameraData data = Phase1Builder.GetOrAdd<UniversalAdditionalCameraData>(camera.gameObject);
                    data.renderPostProcessing = true;     // Sin esto, ningún Volume tiene efecto.
                    data.volumeLayerMask |= 1;            // Los volúmenes viven en la capa Default.
                    data.stopNaN = false;
                    data.dithering = true;                // Rompe las bandas del gradiente oscuro de Primavera (costo mínimo).
                }

                Transform tool = FindDeep(root.transform, "Tool");
                if (tool != null)
                {
                    // Emisor 3,3 m delante de la boquilla mirando hacia ella: las partículas viajan
                    // hacia la boquilla, se cierran con velocidad radial negativa y giran con
                    // velocidad orbital (espiral). La boquilla está en (0,05; −0,1; 0,75) local.
                    Transform vortexT = Phase1Builder.GetOrCreateChild(tool, "SuctionVortex");
                    vortexT.localPosition = new Vector3(0.05f, -0.1f, 0.75f + 3.3f);
                    vortexT.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    vortexT.gameObject.layer = tool.gameObject.layer;
                    ParticleSystem ps = GetOrCreateSystem(tool, "SuctionVortex", AssetDatabase.LoadAssetAtPath<Material>(Paths.VfxDataSquare));
                    ConfigureVortex(ps);
                    Phase1Builder.GetOrAdd<SuctionVortexFx>(vortexT.gameObject);
                }

                PrefabUtility.SaveAsPrefabAsset(root, Phase1Builder.Paths.Player);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Vórtice de succión (pool de 1 sistema, ≤ 60 partículas; TDD §8.2).</summary>
        private static void ConfigureVortex(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;  // Sigue a la cámara.
            main.maxParticles = 60;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(13f, 17f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(Phosphor, new Color(0.7f, 0.85f, 1f, 1f));

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 95f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 1.15f;
            shape.radiusThickness = 0.35f;   // Nacen en el anillo exterior: el centro queda despejado.

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // Unity exige que los tres ejes orbitales usen el mismo modo de curva (dos constantes).
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(5f, 10f);   // Giro alrededor del eje (espiral).
            velocity.radial = new ParticleSystem.MinMaxCurve(-4.2f, -2.2f); // Convergen hacia el eje.

            SetFadeAndShrink(ps);
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.25f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);   // Aparece y se desvanece: sin «pop».

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;  // Rayas de aire en la dirección del flujo.
            renderer.velocityScale = 0.035f;
            renderer.lengthScale = 1f;
        }

        // ================================================================== 8. calidad móvil
        /// <summary>Desactiva HDR en el asset de URP móvil (TDD §8.4: HDR desactivado en móvil).</summary>
        [MenuItem(MenuRoot + "8. Calidad móvil", priority = 138)]
        public static void ConfigureMobilePipeline()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Paths.MobilePipeline);
            if (asset == null) return;
            // HDR obliga a un búfer de color de 64 bits por píxel; en GPUs móviles de memoria
            // compartida duplica el ancho de banda del pase principal. Con el umbral de Bloom en
            // 0,9 el resplandor de los emisivos se conserva en LDR.
            if (asset.supportsHDR)
            {
                asset.supportsHDR = false;
                EditorUtility.SetDirty(asset);
            }
        }

        // ================================================================== 9. Tutorial
        /// <summary>Material de Estación, volumen global e iluminación de Primavera en el Tutorial.</summary>
        [MenuItem(MenuRoot + "9. Escena del Tutorial", priority = 139)]
        public static void ConfigureTutorialVisuals()
        {
            string previous = SceneManager.GetActiveScene().path;
            Scene scene = EditorSceneManager.OpenScene(NostalgIA.Core.SceneNames.Paths.Tutorial, OpenSceneMode.Single);

            int replaced = ReplaceProBuilderDefaultMaterial(AssetDatabase.LoadAssetAtPath<Material>(Paths.KitPrimavera));
            EnsureStationVolume(AssetDatabase.LoadAssetAtPath<VolumeProfile>(Paths.VolumePrimavera));
            ApplyPrimaveraLighting();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Fase 1] Tutorial: {replaced} ranura(s) de material de ProBuilder sustituidas por M_Primavera_Kit.");

            if (!string.IsNullOrEmpty(previous) && previous != NostalgIA.Core.SceneNames.Paths.Tutorial)
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }

        /// <summary>
        /// Materiales de prototipo que el kit de la Estación sustituye: el gris por defecto de
        /// ProBuilder y la cuadrícula de pruebas «Test 2» del prototipo (pisos y muros PolyShape).
        /// </summary>
        public static readonly string[] PrototypeMaterialNames = { "ProBuilderDefault", "Test 2" };

        /// <summary>
        /// Sustituye, en todas las ranuras de material de la escena, los materiales de prototipo
        /// por el material de la Estación y limpia la referencia heredada que ProBuilder guarda en
        /// cada cara. Devuelve las ranuras de renderer cambiadas.
        /// </summary>
        public static int ReplaceProBuilderDefaultMaterial(Material replacement)
        {
            if (replacement == null) return 0;
            int count = 0;
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || System.Array.IndexOf(PrototypeMaterialNames, materials[i].name) < 0) continue;
                    materials[i] = replacement;
                    changed = true;
                    count++;
                }
                if (!changed) continue;
                Undo.RecordObject(renderer, "Material de Estación");
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
            ClearLegacyFaceMaterials();
            return count;
        }

        /// <summary>
        /// ProBuilder 4+ dibuja con los materiales del <see cref="MeshRenderer"/>, pero conserva en
        /// cada cara un campo obsoleto <c>m_Material</c> (de la época en que el material era por
        /// cara) que sigue apuntando al gris por defecto. No afecta al render, pero mantiene una
        /// dependencia innecesaria del material del paquete y hace saltar la regla del validador;
        /// lo dejamos vacío. Se accede por serialización porque este ensamblado no referencia
        /// ProBuilder.
        /// </summary>
        public static int ClearLegacyFaceMaterials()
        {
            int modifiedCount = 0;
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour.GetType().Name != "ProBuilderMesh") continue;
                var so = new SerializedObject(behaviour);
                SerializedProperty faces = so.FindProperty("m_Faces");
                if (faces == null || !faces.isArray) continue;
                bool changed = false;
                for (int i = 0; i < faces.arraySize; i++)
                {
                    SerializedProperty legacy = faces.GetArrayElementAtIndex(i).FindPropertyRelative("m_Material");
                    if (legacy == null || legacy.objectReferenceValue == null) continue;
                    legacy.objectReferenceValue = null;
                    changed = true;
                }
                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    modifiedCount++;
                }
            }
            return modifiedCount;
        }

        /// <summary>
        /// Limpia las caras residuales de ProBuilder en la escena activa sin alterar los materiales asignados.
        /// </summary>
        [MenuItem(MenuRoot + "Limpiar caras residuales de ProBuilder (escena activa)", priority = 140)]
        public static void ClearLegacyFaceMaterialsInActiveScene()
        {
            int modifiedCount = ClearLegacyFaceMaterials();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Fase 1] Caras residuales de ProBuilder limpiadas en {modifiedCount} objeto(s) de la escena activa.");
        }

        /// <summary>Volumen global de la Estación (prioridad 0; un solo perfil por Estación, Guía §8).</summary>
        private static void EnsureStationVolume(VolumeProfile profile)
        {
            if (profile == null) return;
            GameObject go = GameObject.Find("Volume_Primavera") ?? new GameObject("Volume_Primavera");
            go.layer = 0;
            Volume volume = Phase1Builder.GetOrAdd<Volume>(go);
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
        }

        /// <summary>
        /// Luz clave fría (≈ 7 500 K) con sombras suaves, ambiente azul oscuro en tres tonos y sin
        /// niebla (Guía de estilo §5.1).
        /// </summary>
        private static void ApplyPrimaveraLighting()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                Undo.RecordObject(light, "Luz de Primavera");
                light.useColorTemperature = true;
                light.colorTemperature = 7500f;
                light.color = new Color(0.92f, 0.95f, 1f);   // Filtro levemente azul sobre la temperatura.
                light.intensity = 1.15f;
                light.shadows = LightShadows.Soft;
                // Cenital con leve inclinación: las sombras de las columnas se leen sin alargarse.
                light.transform.rotation = Quaternion.Euler(68f, -35f, 0f);
                EditorUtility.SetDirty(light);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.24f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.11f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.03f, 0.09f);
            RenderSettings.fog = false;
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(Paths.SkyPrimavera);
            if (sky != null) RenderSettings.skybox = sky;
        }

        // ================================================================== utilidades
        /// <summary>Color desde un entero 0xRRGGBB (sRGB, como los hex de la Guía de estilo).</summary>
        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        /// <summary>Busca un descendiente por nombre.</summary>
        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
