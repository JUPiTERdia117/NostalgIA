// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: InviernoLevelBuilder.cs
// Propósito: construye el Nivel 4 final (40_Invierno_IA — Estación Invierno,
// laberinto circular de televisores CRT y combate final contra la IA).
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NostalgIA.Core;
using NostalgIA.EditorTools.Levels;
using NostalgIA.Gameplay.Level;
using NostalgIA.Gameplay.Player;
using NostalgIA.Gameplay.Tools;
using NostalgIA.Presentation.Audio;
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
    /// Constructor del Nivel 4 (Estación Invierno — Laberinto Circular de TVs + Jefe IA).
    /// Genera la escena completa en su propia región del mundo (<c>x = 9000 m</c>) siguiendo el
    /// boceto y la especificación de diseño.
    /// </summary>
    public static class InviernoLevelBuilder
    {
        private const string LevelsMenuRoot = "NostalgIA/Niveles/";
        private const string ExperimentalMenuRoot = "NostalgIA/Experimental/";
        public const string ScenePath = SceneNames.Paths.InviernoIA;

        /// <summary>
        /// Región propia del Nivel 4 a 9 km del origen para que nunca se encime con Tutorial (0 km),
        /// Verano (3 km) ni Otoño/Dreamcore (6 km).
        /// </summary>
        public static readonly Vector3 RegionOrigin = new Vector3(9000f, 0f, 0f);

        private const string MaterialsRoot = "Assets/_Project/Art/Materials/Invierno";
        private const string MeshesRoot = "Assets/_Project/Art/Meshes/Invierno";
        public const string VolumePath = "Assets/_Project/Rendering/Volumes/VP_Invierno.asset";

        // Prefabs oficiales reutilizados del proyecto.
        private const string StarPrefabPath = "Assets/_Project/Prefabs/Level/Star.prefab";
        private const string FlyingEmailPrefabPath = "Assets/_Project/Prefabs/Enemies/FlyingEmail.prefab";
        private const string HexbugPrefabPath = "Assets/_Project/Prefabs/Enemies/Hexbug.prefab";
        private const string HorsePrefabPath = "Assets/_Project/Prefabs/Enemies/Horse.prefab";
        private const string WinRarPrefabPath = "Assets/_Project/Prefabs/Enemies/WinRar.prefab";

        // Radios del laberinto circular (en metros).
        private const float ArenaRadius = 24f;      // R1: cámara central de la IA (diámetro 48 m)
        private const float InnerMidRadius = 31f;   // Centro del pasillo del anillo interior
        private const float Ring2Radius = 38f;      // R2: muro entre anillo interior y anillo exterior
        private const float OuterMidRadius = 45f;   // Centro del pasillo del anillo exterior
        private const float Ring3Radius = 52f;      // R3: muro perimetral exterior
        private const float WallHeight = 6.2f;      // Altura de las paredes de televisores
        private const float WallThickness = 1.65f;  // Espesor físico de los muros

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly List<Checkpoint> Checkpoints = new List<Checkpoint>();
        private static Scene scene;
        private static Transform root;
        private static Mesh tvWallCaseMesh;
        private static Mesh tvWallScreenMeshA;
        private static Mesh tvWallScreenMeshB;

        // ================================================================== menús
        [MenuItem(LevelsMenuRoot + "Construir Nivel 4 — Invierno (Laberinto TV + IA)", priority = 306)]
        public static void BuildMenu() => Build();

        [MenuItem(LevelsMenuRoot + "Construir Nivel 4 y Conectar Cadena de Niveles", priority = 307)]
        public static void BuildAndConnectChain()
        {
            Build();
            ConnectPreviousLevelsToInvierno();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[Niveles] Nivel 4 (40_Invierno_IA) construido y conectado en la cadena de estaciones.");
        }

        [MenuItem(ExperimentalMenuRoot + "Construir nivel Invierno (Laberinto IA)", priority = 204)]
        public static void Build()
        {
            if (SceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            LevelGateBuilder.EnsureFolder(MaterialsRoot);
            LevelGateBuilder.EnsureFolder(MeshesRoot);
            LevelGateBuilder.EnsureFolder("Assets/_Project/Rendering/Volumes");

            CreateMaterials();
            CreateVolumeProfile();
            CreateProceduralTVWallMeshes();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(LevelGateBuilder.PrefabPath) == null)
            {
                LevelGateBuilder.BuildPrefab();
            }

            Checkpoints.Clear();
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            root = new GameObject("Invierno_Laberinto_IA").transform;

            BuildAtmosphereAndLighting();
            BuildCircularFloorAndLavaBasins();
            BuildCircularTVMazeWalls();
            BuildEntryAndInitialCorridor();
            BuildRightBranchCrusherAndMeetingPlatforms();
            BuildForwardBranchHordeAndSuspendedLava();
            BuildCentralAIBossArena();
            BuildAudioSystem();
            BuildPlayerAndSystems();
            BuildEntrance();

            LevelRegionTools.ShiftSceneRoots(scene, RegionOrigin);

            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            LevelGateBuilder.EnsureInBuild(ScenePath);
            Debug.Log("[Invierno] Nivel 4 (40_Invierno_IA) construido en " + ScenePath);
        }

        // ================================================================== conexión con niveles previos
        private static void ConnectPreviousLevelsToInvierno()
        {
            // Si existe dreamcore.unity (Nivel 3), conectamos Verano -> dreamcore -> Invierno.
            // Si aún no está dreamcore.unity, conectamos Verano -> Invierno directamente para poder probar el salto.
            bool hasDreamcore = File.Exists(SceneNames.Paths.Dreamcore);

            if (File.Exists(SceneNames.Paths.VeranoFrutiger))
            {
                Scene veranoScene = EditorSceneManager.OpenScene(SceneNames.Paths.VeranoFrutiger, OpenSceneMode.Single);
                LevelGate[] gates = Object.FindObjectsByType<LevelGate>(FindObjectsSortMode.None);
                foreach (LevelGate gate in gates)
                {
                    if (gate == null) continue;
                    string targetName = hasDreamcore ? SceneNames.Dreamcore : SceneNames.InviernoIA;
                    string targetPath = hasDreamcore ? SceneNames.Paths.Dreamcore : SceneNames.Paths.InviernoIA;
                    gate.Configure(targetName, targetPath, gate.Aperture);
                    EditorUtility.SetDirty(gate);
                }
                EditorSceneManager.MarkSceneDirty(veranoScene);
                EditorSceneManager.SaveScene(veranoScene);
                LevelGateBuilder.EnsureInBuild(SceneNames.Paths.VeranoFrutiger);
            }

            if (hasDreamcore)
            {
                LevelGateBuilder.EnsureInBuild(SceneNames.Paths.Dreamcore);
            }
        }

        // ================================================================== materiales y mallas de TVs
        private static void CreateMaterials()
        {
            Materials.Clear();
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");

            Shader blueLava = Shader.Find("Tutorial/BlueCodeLava");
            if (blueLava == null) blueLava = lit;

            // Paleta de Invierno / Motherboard corrupta e instalación de televisores CRT.
            MakeLit("M_Invierno_Floor", lit, Hex(0x141821), 0.25f, 0.35f, Color.black);
            MakeLit("M_Invierno_FloorTrim", lit, Hex(0x1F2736), 0.4f, 0.5f, Hex(0x091624));
            MakeLit("M_Invierno_ArenaFloor", lit, Hex(0x10141D), 0.35f, 0.45f, Hex(0x06101A));
            MakeLit("M_Invierno_TVCase", lit, Hex(0x1C1E24), 0.3f, 0.28f, Color.black);
            MakeLit("M_Invierno_TVScreenCyan", lit, Hex(0x28B8D8), 0.1f, 0.88f, Hex(0x0E5E78) * 1.4f);
            MakeLit("M_Invierno_TVScreenStatic", lit, Hex(0x526478), 0.1f, 0.82f, Hex(0x223042) * 1.1f);
            MakeLit("M_Invierno_PlatformGrapple", lit, Hex(0x1FD678), 0.2f, 0.65f, Hex(0x0B5E32));
            MakeLit("M_Invierno_PlatformMoving", lit, Hex(0x2CE692), 0.25f, 0.72f, Hex(0x0F7A46));
            MakeLit("M_Invierno_GrappleSphere", lit, Hex(0x38F2FF), 0.15f, 0.85f, Hex(0x14889E) * 1.5f);
            MakeLit("M_Invierno_Absorbable", lit, Hex(0x52FF78), 0.2f, 0.75f, Hex(0x188C34));
            MakeLit("M_Invierno_Crusher", lit, Hex(0x8C2230), 0.45f, 0.42f, Hex(0x3D0A12));
            MakeLit("M_Invierno_Blade", lit, Hex(0xFF384C), 0.5f, 0.65f, Hex(0x7A101C));
            MakeLit("M_Invierno_AICoreBody", lit, Hex(0xDCE2EC), 0.55f, 0.68f, Hex(0x121822));
            MakeLit("M_Invierno_AICable", lit, Hex(0x15181F), 0.4f, 0.3f, Color.black);
            MakeLit("M_Invierno_AIMonitorFrame", lit, Hex(0x262C3A), 0.5f, 0.55f, Color.black);
            MakeLit("M_Invierno_AIMonitorScreen", lit, Hex(0x00E5FF), 0.1f, 0.9f, Hex(0x006680) * 1.4f);
            MakeLit("M_Invierno_Checkpoint", lit, Hex(0x38E54D), 0.2f, 0.75f, Hex(0x147022));

            // Material compartido para los lagos de lava con el shader del proyecto.
            string lavaMatPath = $"{MaterialsRoot}/M_Invierno_Lava.mat";
            Material lavaMat = AssetDatabase.LoadAssetAtPath<Material>(lavaMatPath);
            if (lavaMat == null)
            {
                lavaMat = new Material(blueLava) { name = "M_Invierno_Lava" };
                AssetDatabase.CreateAsset(lavaMat, lavaMatPath);
            }
            else if (lavaMat.shader != blueLava)
            {
                lavaMat.shader = blueLava;
            }
            lavaMat.enableInstancing = true;
            EditorUtility.SetDirty(lavaMat);
            Materials["M_Invierno_Lava"] = lavaMat;
        }

        private static void MakeLit(string name, Shader shader, Color albedo, float metallic, float smoothness, Color emission)
        {
            string path = $"{MaterialsRoot}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", albedo);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", albedo);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0.01f)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            Materials[name] = mat;
        }

        private static void CreateVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }

            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.65f);
            bloom.scatter.Override(0.6f);

            if (!profile.TryGet(out ColorAdjustments color)) color = profile.Add<ColorAdjustments>(true);
            color.active = true;
            color.postExposure.Override(0.15f);
            color.contrast.Override(14f);
            color.saturation.Override(8f);

            if (!profile.TryGet(out Vignette vignette)) vignette = profile.Add<Vignette>(true);
            vignette.active = true;
            vignette.intensity.Override(0.24f);

            EditorUtility.SetDirty(profile);
        }

        /// <summary>
        /// Construye un bloque modular de 3×3 televisores CRT apilados en una sola malla compartida
        /// para que cada segmento de pared del laberinto se vea como la pila de TVs de referencia
        /// manteniendo un costo mínimo de draw calls.
        /// </summary>
        private static void CreateProceduralTVWallMeshes()
        {
            var caseVerts = new List<Vector3>();
            var caseNormals = new List<Vector3>();
            var caseUvs = new List<Vector2>();
            var caseTris = new List<int>();

            var screenAVerts = new List<Vector3>();
            var screenANormals = new List<Vector3>();
            var screenAUvs = new List<Vector2>();
            var screenATris = new List<int>();

            var screenBVerts = new List<Vector3>();
            var screenBNormals = new List<Vector3>();
            var screenBUvs = new List<Vector2>();
            var screenBTris = new List<int>();

            // Módulo normalizado de 1 m de ancho × 1 m de alto × 1 m de grosor con 3 columnas × 3 filas de TVs CRT.
            int cols = 3;
            int rows = 3;
            float cellW = 1f / cols;
            float cellH = 1f / rows;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int idx = r * cols + c;
                    float cx = -0.5f + (c + 0.5f) * cellW;
                    float cy = -0.5f + (r + 0.5f) * cellH;

                    // Ligera variación por televisor para que se sienta como una pila apilada de distintos monitores.
                    float wScale = 0.90f + ((idx * 5) % 3) * 0.03f;
                    float hScale = 0.90f + ((idx * 7) % 3) * 0.03f;
                    float depthScale = 0.82f + ((idx * 3) % 3) * 0.07f;
                    float yawJitter = ((idx % 3) - 1) * 3.2f;

                    Vector3 tvCenter = new Vector3(cx, cy, 0f);
                    Vector3 tvSize = new Vector3(cellW * wScale, cellH * hScale, depthScale);
                    Quaternion rot = Quaternion.Euler(0f, yawJitter, 0f);

                    // Carcasa plástica del televisor CRT
                    AppendRotatedBox(caseVerts, caseNormals, caseUvs, caseTris, tvCenter, tvSize, rot);

                    // Bisel trasero (tubo catódico)
                    Vector3 tubeSize = new Vector3(tvSize.x * 0.58f, tvSize.y * 0.58f, 0.98f);
                    AppendRotatedBox(caseVerts, caseNormals, caseUvs, caseTris, tvCenter, tubeSize, rot);

                    // Pantalla en ambos frentes (+Z y -Z del muro) para que el pasillo muestre pantallas de ambos lados
                    Vector3 screenSize = new Vector3(tvSize.x * 0.74f, tvSize.y * 0.68f, depthScale + 0.04f);
                    Vector3 screenCenter = tvCenter + new Vector3(-tvSize.x * 0.05f, 0f, 0f);

                    if ((idx % 2) == 0)
                    {
                        AppendRotatedBox(screenAVerts, screenANormals, screenAUvs, screenATris, screenCenter, screenSize, rot);
                    }
                    else
                    {
                        AppendRotatedBox(screenBVerts, screenBNormals, screenBUvs, screenBTris, screenCenter, screenSize, rot);
                    }
                }
            }

            tvWallCaseMesh = SaveMeshAsset("INV_TVStack_Cases", caseVerts, caseNormals, caseUvs, caseTris);
            tvWallScreenMeshA = SaveMeshAsset("INV_TVStack_ScreensA", screenAVerts, screenANormals, screenAUvs, screenATris);
            tvWallScreenMeshB = SaveMeshAsset("INV_TVStack_ScreensB", screenBVerts, screenBNormals, screenBUvs, screenBTris);
        }

        private static void AppendRotatedBox(
            List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris,
            Vector3 center, Vector3 size, Quaternion rot)
        {
            Vector3 h = size * 0.5f;
            Vector3[] faceNormals =
            {
                Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down
            };
            Vector3[,] faceCorners =
            {
                { new Vector3(-h.x, -h.y,  h.z), new Vector3( h.x, -h.y,  h.z), new Vector3( h.x,  h.y,  h.z), new Vector3(-h.x,  h.y,  h.z) },
                { new Vector3( h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x,  h.y, -h.z), new Vector3( h.x,  h.y, -h.z) },
                { new Vector3( h.x, -h.y,  h.z), new Vector3( h.x, -h.y, -h.z), new Vector3( h.x,  h.y, -h.z), new Vector3( h.x,  h.y,  h.z) },
                { new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, -h.y,  h.z), new Vector3(-h.x,  h.y,  h.z), new Vector3(-h.x,  h.y, -h.z) },
                { new Vector3(-h.x,  h.y,  h.z), new Vector3( h.x,  h.y,  h.z), new Vector3( h.x,  h.y, -h.z), new Vector3(-h.x,  h.y, -h.z) },
                { new Vector3(-h.x, -h.y, -h.z), new Vector3( h.x, -h.y, -h.z), new Vector3( h.x, -h.y,  h.z), new Vector3(-h.x, -h.y,  h.z) }
            };

            for (int f = 0; f < 6; f++)
            {
                int baseIdx = verts.Count;
                Vector3 n = rot * faceNormals[f];
                for (int c = 0; c < 4; c++)
                {
                    verts.Add(center + rot * faceCorners[f, c]);
                    normals.Add(n);
                }
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(0f, 1f));

                tris.Add(baseIdx + 0);
                tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 0);
                tris.Add(baseIdx + 3);
                tris.Add(baseIdx + 2);
            }
        }

        private static Mesh SaveMeshAsset(string name, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh);

            string path = $"{MeshesRoot}/{name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            existing.SetUVs(0, mesh.uv);
            existing.SetUVs(1, mesh.uv2);
            existing.SetTriangles(mesh.triangles, 0);
            existing.bounds = mesh.bounds;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ================================================================== atmósfera e iluminación (Baked-ready)
        private static void BuildAtmosphereAndLighting()
        {
            Transform group = Group("Atmosfera_Invierno");

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex(0x283852);
            RenderSettings.ambientEquatorColor = Hex(0x1C2536);
            RenderSettings.ambientGroundColor = Hex(0x0E121B);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex(0x0B1018);
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 165f;

            var dirLightGo = new GameObject("Directional Light");
            dirLightGo.transform.SetParent(group, false);
            dirLightGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            Light dirLight = dirLightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = Hex(0xB8D8FF);
            dirLight.intensity = 1.15f;
            dirLight.lightmapBakeType = LightmapBakeType.Mixed;
            dirLight.shadows = LightShadows.Soft;

            var volumeGo = new GameObject("Global Volume");
            volumeGo.transform.SetParent(group, false);
            Volume vol = volumeGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);

            RenderSettings.sun = dirLight;

            // Sondas de reflexión y luz para iluminación pre-cocinada (Etapa 3).
            var refProbeGo = new GameObject("ReflectionProbe_Central");
            refProbeGo.transform.SetParent(group, false);
            refProbeGo.transform.position = new Vector3(0f, 6f, 0f);
            ReflectionProbe rp = refProbeGo.AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Baked;
            rp.size = new Vector3(130f, 30f, 130f);

            var lpgGo = new GameObject("LightProbeGroup_Laberinto");
            lpgGo.transform.SetParent(group, false);
            LightProbeGroup lpg = lpgGo.AddComponent<LightProbeGroup>();
            var probePositions = new List<Vector3>();
            for (int a = 0; a < 12; a++)
            {
                float ang = a * 30f;
                probePositions.Add(Polar(12f, ang, 2.2f));
                probePositions.Add(Polar(InnerMidRadius, ang, 2.2f));
                probePositions.Add(Polar(OuterMidRadius, ang, 2.2f));
            }
            lpg.probePositions = probePositions.ToArray();
        }

        // ================================================================== pisos circulares y fosas de lava
        private static void BuildCircularFloorAndLavaBasins()
        {
            Transform floorGroup = Group("Pisos_y_LagosLava");

            // 1. Piso central firme de la arena de la IA (R = 0..24 m).
            GameObject arenaDisk = CreateBox("TEST_Piso_ArenaCentral_IA", floorGroup,
                new Vector3(0f, -0.5f, 0f), new Vector3(ArenaRadius * 2.05f, 1f, ArenaRadius * 2.05f),
                Quaternion.identity, "M_Invierno_ArenaFloor", Layers.Default, true);

            // Anillo decorativo central bajo el núcleo de la IA.
            CreateBox("TEST_Tarima_CentroIA", floorGroup,
                new Vector3(0f, 0.04f, 0f), new Vector3(11f, 0.08f, 11f),
                Quaternion.Euler(0f, 45f, 0f), "M_Invierno_FloorTrim", Layers.Default, true);

            // 2. Anillo Interior (R = 24..38 m):
            //    - Firme en el arco superior e izquierdo (θ = -20° hasta 236°): pasillo de paredes que aplastan,
            //      callejón de la plataforma móvil y cámara de vuelta en U con el Spawner.
            //    - Lago de lava en el arco inferior-derecho (θ = 236° hasta 340° / -124° a -20°):
            //      "Lava con muchas plataformas y enemigos (como el tutorial)".
            BuildFloorArc(floorGroup, "TEST_Piso_AnilloInterior_Firme", InnerMidRadius, 14.6f, -20f, 236f, 28);
            BuildLavaArc(floorGroup, "Lava_AnilloInterior_MultiPlataformas", InnerMidRadius, 13.6f, 236f, 340f, 14);

            // 3. Anillo Exterior (R = 38..52 m):
            //    Dividido en tramos firmes y los dos lagos de lava del anillo exterior:
            //    - Tramo firme de entrada y callejón derecho (θ = 28° a 128°).
            //    - Lago de lava 1 ("Pasillo simple con lava", camino izquierdo inicial): θ = -4° a 28°.
            //    - Tramo firme de bifurcación, "Un montón de enemigos", WinRar y callejón sur: θ = -134° (226°) a -4° (356°).
            //    - Tramo firme de conexión con el pasillo aplastador: θ = 128° a 148°.
            //    - Lago de lava 2 ("Lava con 2 plataformas que se mueven y aspas"): θ = 148° a 214°.
            //    - Orilla firme tras el lago de las 2 plataformas (Estrella 3 y vuelta en U): θ = 214° a 226°.
            BuildFloorArc(floorGroup, "TEST_Piso_Exterior_EntradaYDer", OuterMidRadius, 14.6f, 28f, 128f, 14);
            BuildLavaArc(floorGroup, "Lava_Exterior_PasilloSimple", OuterMidRadius, 13.6f, -4f, 28f, 6);
            BuildFloorArc(floorGroup, "TEST_Piso_Exterior_RutaAdelante", OuterMidRadius, 14.6f, -134f, -4f, 18);
            BuildFloorArc(floorGroup, "TEST_Piso_Exterior_CruceOeste", OuterMidRadius, 14.6f, 128f, 148f, 4);
            BuildLavaArc(floorGroup, "Lava_Exterior_DosPlataformasMoviles", OuterMidRadius, 13.6f, 148f, 214f, 10);
            BuildFloorArc(floorGroup, "TEST_Piso_Exterior_OrillaVueltaU", OuterMidRadius, 14.6f, 214f, 226f, 3);

            // 4. Vestíbulo radial de "Puerta inicio" (θ = 52°, R = 51..64 m).
            Vector3 entryCenter = Polar(57.5f, 52f, -0.5f);
            Quaternion entryRot = RadialLookInward(52f);
            CreateBox("TEST_Piso_VestibuloInicio", floorGroup,
                entryCenter, new Vector3(11.5f, 1f, 14f), entryRot, "M_Invierno_Floor", Layers.Default, true);

            // 5. Volumen de muerte de respaldo bajo todo el nivel por si el jugador cae al vacío.
            var killGo = new GameObject("KillVolume_Respaldo");
            killGo.transform.SetParent(floorGroup, false);
            killGo.transform.position = new Vector3(0f, -14f, 0f);
            killGo.layer = Layers.Hazard;
            BoxCollider killCol = killGo.AddComponent<BoxCollider>();
            killCol.isTrigger = true;
            killCol.size = new Vector3(260f, 4f, 260f);
            killGo.AddComponent<KillVolume>();
        }

        private static void BuildFloorArc(Transform parent, string prefix, float radius, float width,
                                          float startDeg, float endDeg, int segments)
        {
            Transform group = ChildGroup(parent, prefix);
            float totalAngle = endDeg - startDeg;
            float step = totalAngle / segments;
            for (int i = 0; i < segments; i++)
            {
                float a0 = startDeg + i * step;
                float a1 = a0 + step;
                float mid = (a0 + a1) * 0.5f;
                float chord = 2f * (radius + width * 0.5f) * Mathf.Sin(Mathf.Abs(step) * 0.5f * Mathf.Deg2Rad) + 1.2f;

                Vector3 pos = Polar(radius, mid, -0.5f);
                Quaternion rot = TangentRotation(mid);
                CreateBox($"{prefix}_{i + 1:00}", group, pos, new Vector3(width, 1f, chord),
                    rot, "M_Invierno_Floor", Layers.Default, true);
            }
        }

        /// <summary>
        /// Construye una fosa de lava curva: el fondo visual y el volumen letal quedan hundidos a
        /// y = -0,85 m para que el jugador vea el foso de código azul y muera al caer sin que el
        /// borde seco del pasillo lo mate por error.
        /// </summary>
        private static void BuildLavaArc(Transform parent, string name, float radius, float width,
                                         float startDeg, float endDeg, int segments)
        {
            Transform group = ChildGroup(parent, name);
            float step = (endDeg - startDeg) / segments;
            for (int i = 0; i < segments; i++)
            {
                float a0 = startDeg + i * step;
                float a1 = a0 + step;
                float mid = (a0 + a1) * 0.5f;
                float chord = 2f * (radius + width * 0.45f) * Mathf.Sin(Mathf.Abs(step) * 0.5f * Mathf.Deg2Rad) + 0.9f;

                Vector3 lavaPos = Polar(radius, mid, -0.85f);
                Quaternion rot = TangentRotation(mid);

                GameObject lavaSeg = CreateBox($"TEST_Lava_{name}_{i + 1:00}", group,
                    lavaPos, new Vector3(width, 0.35f, chord), rot, "M_Invierno_Lava", Layers.Hazard, false);

                BoxCollider trigger = lavaSeg.GetComponent<BoxCollider>();
                trigger.isTrigger = true;
                ContactHazard hazard = lavaSeg.AddComponent<ContactHazard>();
                hazard.Configure(ContactHazard.HazardEffect.Instakill, 0f, 0f, 99, false);

                // Fondo oscuro debajo de la lava por estética.
                CreateBox($"TEST_FosoFondo_{i + 1:00}", group,
                    Polar(radius, mid, -1.45f), new Vector3(width + 1f, 0.5f, chord + 0.8f),
                    rot, "M_Invierno_Floor", Layers.Default, true);
            }
        }

        // ================================================================== paredes circulares de televisores CRT (no enganchables)
        private static void BuildCircularTVMazeWalls()
        {
            Transform wallGroup = Group("TEST_Paredes_Laberinto_TVs");

            // 1. Muro exterior perimetral (R3 = 52 m), abierto únicamente en "Puerta inicio" (θ = 46°..58°).
            BuildTVWallArc(wallGroup, "TEST_MuroTV_Exterior", Ring3Radius, 58f, 406f, 44);

            // Paredes laterales del vestíbulo de entrada (θ = 46° y θ = 58°, R = 52..64 m).
            BuildTVWallRadial(wallGroup, "TEST_MuroTV_Entrada_Izq", 46f, Ring3Radius, 64f, 2);
            BuildTVWallRadial(wallGroup, "TEST_MuroTV_Entrada_Der", 58f, Ring3Radius, 64f, 2);
            BuildTVWallArc(wallGroup, "TEST_MuroTV_Entrada_Fondo", 64f, 45f, 59f, 2);

            // 2. Muro intermedio (R2 = 38 m) entre el Anillo Exterior y el Anillo Interior:
            //    Aberturas exactas según el diagrama:
            //    - Abertura 1: θ = -16°.. -2° (344°..358°), desvío a la derecha tras el primer lago de lava.
            //    - Abertura 2: θ = 132°..146°, salida derecha tras el pasillo de paredes que aplastan.
            //    - Abertura 3: θ = 201°..213°, primera vuelta a la izquierda tras el lago de las 2 plataformas.
            //    - Abertura 4: θ = 262°..276°, vuelta a la derecha hacia el lago de lava con plataformas.
            BuildTVWallArc(wallGroup, "TEST_MuroTV_Intermedio_ArcoNorte", Ring2Radius, -2f, 132f, 16);
            BuildTVWallArc(wallGroup, "TEST_MuroTV_Intermedio_ArcoOeste", Ring2Radius, 146f, 201f, 7);
            BuildTVWallArc(wallGroup, "TEST_MuroTV_Intermedio_ArcoSurOeste", Ring2Radius, 213f, 262f, 6);
            BuildTVWallArc(wallGroup, "TEST_MuroTV_Intermedio_ArcoSurEste", Ring2Radius, 276f, 344f, 9);

            // 3. Muro interior (R1 = 24 m) que rodea la cámara central de la IA ("meta / IA"):
            //    Dos entradas hacia la IA:
            //    - Entrada Ruta Derecha (tras la vuelta en U y el Spawner + 2 arañas): θ = 186°..202°.
            //    - Entrada Ruta Adelante (tras cruzar el lago de plataformas hacia la izquierda y girar a la derecha): θ = 219°..233°.
            BuildTVWallArc(wallGroup, "TEST_MuroTV_AnilloIA_Principal", ArenaRadius, 233f, 546f, 28);
            BuildTVWallArc(wallGroup, "TEST_MuroTV_AnilloIA_SeparadorOeste", ArenaRadius, 202f, 219f, 2);

            // 4. Muros divisorios radiales del laberinto (callejones sin salida y vueltas en U):
            //    - Tope del callejón sin salida derecho inicial (WinRar + Estrella 1): θ = 128° en el Anillo Exterior.
            BuildTVWallRadial(wallGroup, "TEST_MuroTV_Tope_CallejonDerechoInicio", 128f, Ring2Radius, Ring3Radius, 2);

            //    - Tope del callejón de la plataforma móvil (Estrella 2) y pared de la vuelta en U: θ = 175° en el Anillo Interior.
            BuildTVWallRadial(wallGroup, "TEST_MuroTV_Tope_CallejonPlataformaMovil", 175f, ArenaRadius, Ring2Radius, 2);

            //    - Muro radial en θ = 226° que cierra el otro lado del lago de las 2 plataformas (Estrella 3) y también
            //      cierra el callejón sin salida del segundo WinRar (Estrella 4) en el Anillo Exterior.
            BuildTVWallRadial(wallGroup, "TEST_MuroTV_Divisor_OesteSur", 226f, Ring2Radius, Ring3Radius, 2);

            //    - Muro radial en θ = 214° en el Anillo Interior: forma la vuelta en U hacia el Spawner + 2 arañas por un lado,
            //      y por el otro lado detiene al jugador al final del lago de plataformas obligándolo a dar vuelta a la derecha hacia la IA.
            BuildTVWallRadial(wallGroup, "TEST_MuroTV_Divisor_VueltaU_y_MetaSur", 214f, ArenaRadius, Ring2Radius, 2);

            //    - Muro radial en θ = 340° (-20°) en el Anillo Interior: cierra el callejón sin salida derecho del lago
            //      de múltiples plataformas (Estrella 5) y lo separa del inicio del pasillo de paredes que aplastan.
            BuildTVWallRadial(wallGroup, "TEST_MuroTV_Tope_Estrella5_LavaInterior", 340f, ArenaRadius, Ring2Radius, 2);
        }

        private static void BuildTVWallArc(Transform parent, string name, float radius, float startDeg, float endDeg, int segments)
        {
            Transform group = ChildGroup(parent, name);
            float step = (endDeg - startDeg) / segments;
            for (int i = 0; i < segments; i++)
            {
                float a0 = startDeg + i * step;
                float a1 = a0 + step;
                float mid = (a0 + a1) * 0.5f;
                float length = 2f * radius * Mathf.Sin(Mathf.Abs(step) * 0.5f * Mathf.Deg2Rad) + 0.35f;

                Vector3 center = Polar(radius, mid, WallHeight * 0.5f);
                // Oriento el eje X del módulo a lo largo de la tangente del arco y el eje Z hacia el centro.
                Quaternion rot = RadialLookInward(mid);
                CreateTVWallSegment($"{name}_{i + 1:00}", group, center, new Vector3(length, WallHeight, WallThickness), rot);
            }
        }

        private static void BuildTVWallRadial(Transform parent, string name, float angleDeg, float rInner, float rOuter, int segments)
        {
            Transform group = ChildGroup(parent, name);
            float totalLen = rOuter - rInner;
            float segLen = totalLen / segments;
            for (int i = 0; i < segments; i++)
            {
                float rMid = rInner + (i + 0.5f) * segLen;
                Vector3 center = Polar(rMid, angleDeg, WallHeight * 0.5f);
                Quaternion rot = TangentRotation(angleDeg);
                CreateTVWallSegment($"{name}_{i + 1:00}", group, center, new Vector3(segLen + 0.25f, WallHeight, WallThickness), rot);
            }
        }

        /// <summary>
        /// Crea un segmento de pared de televisores CRT con colisionador liso en <c>Layers.Default</c>
        /// (no enganchable con el gancho) y mallas compartidas de carcasas y pantallas CRT.
        /// </summary>
        private static GameObject CreateTVWallSegment(string name, Transform parent, Vector3 center, Vector3 size, Quaternion rotation)
        {
            GameObject wallGo = Renderer(name, parent, tvWallCaseMesh,
                Materials["M_Invierno_TVCase"], center, size, false);
            wallGo.transform.rotation = rotation;
            wallGo.layer = Layers.Default;
            wallGo.AddComponent<BoxCollider>();
            MarkStaticBaked(wallGo);

            GameObject screensA = new GameObject("Pantallas_Cian");
            screensA.transform.SetParent(wallGo.transform, false);
            screensA.layer = Layers.Default;
            screensA.AddComponent<MeshFilter>().sharedMesh = tvWallScreenMeshA;
            screensA.AddComponent<MeshRenderer>().sharedMaterial = Materials["M_Invierno_TVScreenCyan"];
            MarkStaticBaked(screensA);

            GameObject screensB = new GameObject("Pantallas_Estatica");
            screensB.transform.SetParent(wallGo.transform, false);
            screensB.layer = Layers.Default;
            screensB.AddComponent<MeshFilter>().sharedMesh = tvWallScreenMeshB;
            screensB.AddComponent<MeshRenderer>().sharedMaterial = Materials["M_Invierno_TVScreenStatic"];
            MarkStaticBaked(screensB);

            return wallGo;
        }

        // ================================================================== Zona 1: Entrada, Callejón Derecho (Estrella 1) y Pasillo Simple con Lava
        private static void BuildEntryAndInitialCorridor()
        {
            Transform zone = Group("Zona1_Inicio_y_PrimerasRutas");

            // Checkpoint 1 en la entrada (θ = 52°, R = 54 m mirando hacia el centro del laberinto).
            AddCheckpoint(zone, Polar(53.5f, 52f, 0f), 52f + 180f, "01_PuertaInicio");

            // Objetos absorbibles iniciales en el pasillo de llegada y las 2 arañas enemigos.
            AddAbsorbableOrb(zone, "Absorbible_Inicio_01", Polar(48.5f, 50f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_Inicio_02", Polar(48.5f, 54f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_Inicio_03", Polar(45.5f, 48f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_Inicio_04", Polar(45.5f, 56f, 0.55f), 0.45f);
            AddRespawningAmmoPad(zone, "Surtidor_Inicio", Polar(50.5f, 52f, 0f));

            SpawnEnemy(zone, HexbugPrefabPath, "Arana_Inicio_01", Polar(44.5f, 44f, 0.1f), TangentRotation(44f));
            SpawnEnemy(zone, HexbugPrefabPath, "Arana_Inicio_02", Polar(44.5f, 60f, 0.1f), TangentRotation(60f + 180f));

            // --- CAMINO DE LA DERECHA INICIAL (θ = 65°..124° en el Anillo Exterior): Callejón sin salida + WinRar + Estrella 1 ---
            AddAbsorbableOrb(zone, "Absorbible_CallejonDer_01", Polar(43.5f, 78f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_CallejonDer_02", Polar(46.5f, 84f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_CallejonDer_03", Polar(44.0f, 92f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_CallejonDer_04", Polar(46.0f, 98f, 0.55f), 0.45f);
            AddRespawningAmmoPad(zone, "Surtidor_CallejonDer", Polar(45f, 88f, 0f));

            GameObject winRar1 = SpawnEnemy(zone, WinRarPrefabPath, "WinRar_CallejonDerecho",
                Polar(45f, 110f, 0.1f), TangentRotation(110f + 180f));
            ConfigureBugEnemy(winRar1, 18f, 1.4f);

            GameObject star1 = CreateStarInstance(zone, "Star_01_CallejonWinRar", Polar(45f, 120f, 1.35f), 0);
            AttachDefeatUnlock(zone, "Unlock_Estrella1_WinRar",
                new[] { winRar1 }, null, Vector3.zero, 0.5f,
                new[] { star1 }, null,
                "» ¡WinRar derrotado! Recoge la Estrella del callejón.", true);

            // --- CAMINO DE LA IZQUIERDA INICIAL (θ = 28°..-4° en el Anillo Exterior): Pasillo simple con lava + plataforma + esfera ---
            Vector3 lavaPlatPos = Polar(45f, 12f, 0.35f);
            CreateBox("TEST_Plataforma_PasilloLavaSimple", zone,
                lavaPlatPos, new Vector3(5.8f, 0.6f, 7.2f), TangentRotation(12f),
                "M_Invierno_PlatformGrapple", Layers.Grapple, true);

            // Esfera de enganche suspendida sobre el lago para cruzar con el gancho.
            AddGrappleSphere(zone, "TEST_EsferaEnganche_LavaSimple", Polar(45f, 12f, 5.2f), 1.65f);

            // Checkpoint en la bifurcación tras cruzar el primer lago de lava (θ = -9°).
            AddCheckpoint(zone, Polar(45f, -9f, 0f), -9f - 90f, "02_BifurcacionPostLava");
        }

        // ================================================================== Zona 2: CAMINO DE LA DERECHA (Paredes que aplastan, Estrella 2 y Lago con 2 plataformas móviles + aspas + Estrella 3 + Vuelta en U)
        private static void BuildRightBranchCrusherAndMeetingPlatforms()
        {
            Transform zone = Group("Zona2_CaminoDerecha_Aplastadores_y_DosPlataformas");

            // 1. Pasillo con paredes que aplastan (Anillo Interior, θ = 8°..108°):
            //    Alterno aplastadores que se cierran de lado a lado y de arriba a abajo (referencia al Nivel 3).
            float[] crusherAngles = { 14f, 30f, 46f, 64f, 82f, 100f };
            for (int i = 0; i < crusherAngles.Length; i++)
            {
                float ang = crusherAngles[i];
                bool vertical = (i % 2) == 1;
                BuildCrusherObstacle(zone, $"TEST_ParedAplastadora_{i + 1:00}", ang, vertical, i * 0.22f);
            }

            // 2. Final del pasillo de aplastadores (θ = 116°..136° en el Anillo Interior):
            //    2 enemigos voladores (Spam Mail) y varios objetos por recoger.
            AddCheckpoint(zone, Polar(InnerMidRadius, 114f, 0f), 114f + 90f, "03_FinPasilloAplastadores");
            AddAbsorbableOrb(zone, "Absorbible_PostCrusher_01", Polar(28.5f, 118f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_PostCrusher_02", Polar(33.5f, 121f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_PostCrusher_03", Polar(29.0f, 127f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_PostCrusher_04", Polar(33.0f, 131f, 0.55f), 0.45f);
            AddRespawningAmmoPad(zone, "Surtidor_PostCrusher", Polar(InnerMidRadius, 124f, 0f));

            SpawnEnemy(zone, FlyingEmailPrefabPath, "SpamMail_PostCrusher_01",
                Polar(29.5f, 128f, 2.8f), TangentRotation(128f + 180f));
            SpawnEnemy(zone, FlyingEmailPrefabPath, "SpamMail_PostCrusher_02",
                Polar(32.5f, 134f, 3.2f), TangentRotation(134f + 180f));

            // 3. Hacia delante desde el final del pasillo (θ = 148°..170° en el Anillo Interior):
            //    Callejón sin salida con una plataforma que se mueve sobre la cual hay una estrella (Estrella 2).
            Vector3 starPlatStart = Polar(28.5f, 152f, 2.2f);
            Vector3 starPlatEnd = Polar(33.5f, 168f, 2.2f);
            GameObject movingStarPlat = CreateMovingPlatformBox("TEST_PlataformaMovil_Estrella2", zone,
                starPlatStart, new Vector3(4.2f, 0.55f, 4.2f), TangentRotation(160f),
                "M_Invierno_PlatformMoving", Layers.Grapple,
                starPlatEnd - starPlatStart, 2.6f, 0f);

            // Esfera de enganche de apoyo para engancharse con precisión hacia la plataforma móvil.
            AddGrappleSphere(zone, "TEST_EsferaEnganche_PrecisionEstrella2", Polar(InnerMidRadius, 158f, 5.1f), 1.5f);

            CreateStarInstance(movingStarPlat.transform, "Star_02_PlataformaMovil",
                starPlatStart + Vector3.up * 1.35f, 1);

            // 4. Hacia la derecha desde el final del pasillo (saliendo por θ = 139° al Anillo Exterior, θ = 148°..214°):
            //    Pasillo cubierto de lava con DOS plataformas móviles que se encuentran a la mitad y regresan,
            //    y aspas giratorias que obligan al jugador a saltar.
            AddCheckpoint(zone, Polar(OuterMidRadius, 144f, 0f), 144f + 90f, "04_OrillaLavaDosPlataformas");

            Vector3 meetPoint = Polar(OuterMidRadius, 181f, 0.35f);
            Vector3 platAStart = Polar(OuterMidRadius, 152f, 0.35f);
            Vector3 platBStart = Polar(OuterMidRadius, 210f, 0.35f);

            // Plataforma 1: avanza desde θ = 152° hasta la mitad (θ = 181° - medio largo) y regresa.
            Vector3 halfGapOffset = (platBStart - platAStart).normalized * 2.8f;
            Vector3 platATarget = meetPoint - halfGapOffset;
            Vector3 platBTarget = meetPoint + halfGapOffset;

            CreateMovingPlatformBox("TEST_PlataformaMovil_Lava_01", zone,
                platAStart, new Vector3(5.2f, 0.6f, 5.4f), TangentRotation(166f),
                "M_Invierno_PlatformMoving", Layers.Grapple,
                platATarget - platAStart, 4.2f, 0f);

            // Plataforma 2: sale del otro lado (θ = 210°) con el mismo periodo y fase 0 para encontrarse al centro con la primera.
            CreateMovingPlatformBox("TEST_PlataformaMovil_Lava_02", zone,
                platBStart, new Vector3(5.2f, 0.6f, 5.4f), TangentRotation(196f),
                "M_Invierno_PlatformMoving", Layers.Grapple,
                platBTarget - platBStart, 4.2f, 0f);

            // Aspas giratorias sobre el recorrido del lago para obligar al jugador a saltar mientras viaja en las plataformas.
            float[] bladeAngles = { 164f, 181f, 198f };
            for (int i = 0; i < bladeAngles.Length; i++)
            {
                float bAng = bladeAngles[i];
                BuildRotatingSpinnerBlade(zone, $"TEST_AspasGiro_Lava_{i + 1:00}",
                    Polar(OuterMidRadius, bAng, 0.55f), (i % 2 == 0) ? 72f : -72f, 5.6f);
            }

            // 5. Al otro lado del lago (θ = 216°..224° en el Anillo Exterior):
            //    Estrella 3 frente a la pared divisoria, vuelta a la izquierda + otra vuelta a la izquierda (vuelta en U)
            //    hacia la cámara interior (θ = 180°..210° en el Anillo Interior) con 1 Spawn y 2 arañas antes de la IA.
            CreateStarInstance(zone, "Star_03_OrillaLavaAspas", Polar(OuterMidRadius, 220f, 1.35f), 2);
            AddCheckpoint(zone, Polar(OuterMidRadius, 216f, 0f), 216f + 180f, "05_VueltaEnU_PreIA");

            // Objetos absorbibles y surtidor en la sala de la vuelta en U.
            AddAbsorbableOrb(zone, "Absorbible_VueltaU_01", Polar(34f, 204f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_VueltaU_02", Polar(28f, 200f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_VueltaU_03", Polar(33f, 192f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_VueltaU_04", Polar(28f, 186f, 0.55f), 0.45f);
            AddRespawningAmmoPad(zone, "Surtidor_VueltaU", Polar(31f, 198f, 0f));

            GameObject spawnerU = SpawnEnemy(zone, HorsePrefabPath, "Spawn_VueltaU_PreIA",
                Polar(29.5f, 190f, 0.1f), TangentRotation(190f));
            GameObject spiderU1 = SpawnEnemy(zone, HexbugPrefabPath, "Arana_VueltaU_01",
                Polar(32.5f, 194f, 0.1f), TangentRotation(194f));
            GameObject spiderU2 = SpawnEnemy(zone, HexbugPrefabPath, "Arana_VueltaU_02",
                Polar(28.5f, 184f, 0.1f), TangentRotation(184f));

            // Compuerta que se abre hacia la IA al derrotar al Spawn y las 2 arañas de la vuelta en U.
            GameObject gateRightRoute = CreateBox("TEST_Compuerta_AccesoIA_RutaDerecha", zone,
                Polar(ArenaRadius, 194f, WallHeight * 0.5f), new Vector3(7.5f, WallHeight, WallThickness),
                RadialLookInward(194f), "M_Invierno_Crusher", Layers.Default, false);

            AttachDefeatUnlock(zone, "Unlock_AccesoIA_RutaDerecha",
                new[] { spawnerU, spiderU1, spiderU2 },
                gateRightRoute.transform, new Vector3(0f, -WallHeight - 0.5f, 0f), 1.2f,
                null, null,
                "» ¡Guardia de la vuelta en U derrotada! Se abrió el acceso al núcleo de la IA.", false);
        }

        private static void BuildCrusherObstacle(Transform parent, string name, float angleDeg, bool vertical, float phaseOffset)
        {
            var holder = new GameObject(name + "_Anclaje");
            holder.transform.SetParent(parent, false);

            if (vertical)
            {
                // Pared aplastadora que baja desde arriba hasta el suelo y vuelve a subir.
                holder.transform.SetPositionAndRotation(Polar(InnerMidRadius, angleDeg, 5.2f), TangentRotation(angleDeg));
                GameObject block = CreateBox(name, holder.transform,
                    holder.transform.position, new Vector3(12.2f, 3.6f, 2.2f), holder.transform.rotation,
                    "M_Invierno_Crusher", Layers.Default, false);

                Rigidbody rb = block.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                HazardMotion motion = block.AddComponent<HazardMotion>();
                motion.Configure(HazardMotion.MotionKind.Crusher, new Vector3(0f, -3.35f, 0f), 2.7f, phaseOffset);

                // Volumen inferior de aplastamiento letal mientras cae.
                var killZone = new GameObject("ZonaAplastamiento");
                killZone.transform.SetParent(block.transform, false);
                killZone.transform.localPosition = new Vector3(0f, -0.52f, 0f);
                killZone.layer = Layers.Hazard;
                BoxCollider kc = killZone.AddComponent<BoxCollider>();
                kc.isTrigger = true;
                kc.size = new Vector3(0.92f, 0.22f, 0.88f);
                ContactHazard ch = killZone.AddComponent<ContactHazard>();
                ch.Configure(ContactHazard.HazardEffect.Instakill, 0f, 0f, 99, true);
            }
            else
            {
                // Pared aplastadora que se mueve de lado a lado a través del pasillo.
                holder.transform.SetPositionAndRotation(Polar(ArenaRadius + 3.2f, angleDeg, 2.2f), RadialLookInward(angleDeg));
                GameObject block = CreateBox(name, holder.transform,
                    holder.transform.position, new Vector3(2.4f, 4.2f, 5.6f), holder.transform.rotation,
                    "M_Invierno_Crusher", Layers.Default, false);

                Rigidbody rb = block.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                HazardMotion motion = block.AddComponent<HazardMotion>();
                motion.Configure(HazardMotion.MotionKind.Oscillate, new Vector3(0f, 0f, -7.2f), 2.5f, phaseOffset);

                var pushZone = new GameObject("ZonaEmpuje");
                pushZone.transform.SetParent(block.transform, false);
                pushZone.layer = Layers.Hazard;
                BoxCollider pc = pushZone.AddComponent<BoxCollider>();
                pc.isTrigger = true;
                pc.size = new Vector3(1.08f, 0.95f, 1.08f);
                ContactHazard ch = pushZone.AddComponent<ContactHazard>();
                ch.Configure(ContactHazard.HazardEffect.Knockback, 13f, 4.2f, 1, false);
            }
        }

        private static void BuildRotatingSpinnerBlade(Transform parent, string name, Vector3 pivotPosition, float speedDeg, float radius)
        {
            var spinner = new GameObject(name);
            spinner.transform.SetParent(parent, false);
            spinner.transform.position = pivotPosition;
            spinner.layer = Layers.Hazard;

            // Eje central y dos brazos largos que barren sobre las plataformas móviles.
            CreateBox("EjeCentral", spinner.transform, pivotPosition, new Vector3(0.9f, 1.4f, 0.9f),
                Quaternion.identity, "M_Invierno_Crusher", Layers.Default, false);
            CreateBox("Aspa_Barra", spinner.transform, pivotPosition + Vector3.up * 0.45f,
                new Vector3(0.45f, 0.38f, radius * 2f), Quaternion.identity, "M_Invierno_Blade", Layers.Hazard, false);

            RotatingHazardObstacle rotHazard = spinner.AddComponent<RotatingHazardObstacle>();
            rotHazard.Configure(speedDeg, false, radius, new Vector2(0.35f, 0.32f), 0.45f, 1, 10.5f, 4.8f);
        }

        // ================================================================== Zona 3: CAMINO DE ADELANTE ("Un montón de enemigos", 2 WinRars, Estrella 4 y Lago de Plataformas + Estrella 5)
        private static void BuildForwardBranchHordeAndSuspendedLava()
        {
            Transform zone = Group("Zona3_CaminoAdelante_Horda_y_LavaPlataformas");

            // 1. "Un montón de enemigos" (Anillo Exterior, θ = -18°..-60°):
            //    1 Spawn, múltiples arañas y varios enemigos voladores (Spam Mail) + objetos alrededor.
            float[] ammoAngles = { -16f, -22f, -28f, -34f, -42f, -50f, -56f };
            for (int i = 0; i < ammoAngles.Length; i++)
            {
                float r = (i % 2 == 0) ? 41.5f : 48.5f;
                AddAbsorbableOrb(zone, $"Absorbible_Horda_{i + 1:00}", Polar(r, ammoAngles[i], 0.55f), 0.45f);
            }
            AddRespawningAmmoPad(zone, "Surtidor_Horda_01", Polar(42f, -25f, 0f));
            AddRespawningAmmoPad(zone, "Surtidor_Horda_02", Polar(48f, -46f, 0f));

            SpawnEnemy(zone, HorsePrefabPath, "Spawn_HordaExterior",
                Polar(OuterMidRadius, -38f, 0.1f), TangentRotation(-38f));
            SpawnEnemy(zone, HexbugPrefabPath, "Arana_Horda_01",
                Polar(42f, -26f, 0.1f), TangentRotation(-26f));
            SpawnEnemy(zone, HexbugPrefabPath, "Arana_Horda_02",
                Polar(48f, -32f, 0.1f), TangentRotation(-32f));
            SpawnEnemy(zone, HexbugPrefabPath, "Arana_Horda_03",
                Polar(42f, -44f, 0.1f), TangentRotation(-44f));
            SpawnEnemy(zone, HexbugPrefabPath, "Arana_Horda_04",
                Polar(48f, -52f, 0.1f), TangentRotation(-52f));

            SpawnEnemy(zone, FlyingEmailPrefabPath, "SpamMail_Horda_01",
                Polar(43f, -30f, 3.0f), TangentRotation(-30f));
            SpawnEnemy(zone, FlyingEmailPrefabPath, "SpamMail_Horda_02",
                Polar(47f, -40f, 3.4f), TangentRotation(-40f));
            SpawnEnemy(zone, FlyingEmailPrefabPath, "SpamMail_Horda_03",
                Polar(44f, -50f, 2.9f), TangentRotation(-50f));

            // 2. Primer WinRar tras la horda (θ = -74° en el Anillo Exterior):
            AddCheckpoint(zone, Polar(OuterMidRadius, -62f, 0f), -62f - 90f, "06_PostHordaExterior");
            AddAbsorbableOrb(zone, "Absorbible_WinRarRuta_01", Polar(42f, -66f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_WinRarRuta_02", Polar(48f, -68f, 0.55f), 0.45f);
            AddRespawningAmmoPad(zone, "Surtidor_WinRarRuta", Polar(OuterMidRadius, -65f, 0f));

            GameObject winRarMid = SpawnEnemy(zone, WinRarPrefabPath, "WinRar_PasoSur",
                Polar(OuterMidRadius, -75f, 0.1f), TangentRotation(-75f));
            ConfigureBugEnemy(winRarMid, 18f, 1.4f);

            // 3. Bifurcación en θ = -91° (269°):
            //    - Si avanza hacia delante (θ = -102°..-128°): segundo WinRar que protege la Estrella 4 en un camino sin salida.
            AddAbsorbableOrb(zone, "Absorbible_CallejonSur_01", Polar(42.5f, -100f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_CallejonSur_02", Polar(47.5f, -104f, 0.55f), 0.45f);
            AddAbsorbableOrb(zone, "Absorbible_CallejonSur_03", Polar(45.0f, -108f, 0.55f), 0.45f);
            AddRespawningAmmoPad(zone, "Surtidor_CallejonSur", Polar(OuterMidRadius, -102f, 0f));

            GameObject winRarSouth = SpawnEnemy(zone, WinRarPrefabPath, "WinRar_GuardaEstrella4",
                Polar(OuterMidRadius, -115f, 0.1f), TangentRotation(-115f));
            ConfigureBugEnemy(winRarSouth, 18f, 1.4f);

            GameObject star4 = CreateStarInstance(zone, "Star_04_CallejonSurWinRar", Polar(OuterMidRadius, -125f, 1.35f), 3);
            AttachDefeatUnlock(zone, "Unlock_Estrella4_WinRarSur",
                new[] { winRarSouth }, null, Vector3.zero, 0.5f,
                new[] { star4 }, null,
                "» ¡Segundo WinRar derrotado! Recoge la Estrella del callejón sur.", true);

            // 4. Si da vuelta a la derecha en la bifurcación (entrando por θ = 269° / -91° al Anillo Interior):
            //    Lago con lava con múltiples plataformas suspendidas, enemigos sobre ellas y enemigos voladores sobre la lava.
            AddCheckpoint(zone, Polar(36.5f, 269f, 0f), 269f + 180f, "07_EntradaLagoPlataformas");

            // Plataformas suspendidas sobre la lava (tanto hacia la derecha θ -> 334° donde está la Estrella 5,
            // como hacia la izquierda θ -> 236° donde está la superficie firme antes de la IA).
            float[] platAngles = { 269f, 284f, 300f, 316f, 331f, 254f, 240f };
            float[] platRadii =  { 31.0f, 29.5f, 32.5f, 29.5f, 31.0f, 32.0f, 30.0f };
            for (int i = 0; i < platAngles.Length; i++)
            {
                float pAng = platAngles[i];
                float pRad = platRadii[i];
                Vector3 pPos = Polar(pRad, pAng, 0.35f);
                GameObject plat = CreateBox($"TEST_PlataformaSuspendida_Lava_{i + 1:00}", zone,
                    pPos, new Vector3(5.4f, 0.65f, 6.2f), TangentRotation(pAng),
                    "M_Invierno_PlatformGrapple", Layers.Grapple, true);

                // Esfera de enganche elevada entre plataformas para facilitar el cruce estilo Tutorial.
                AddGrappleSphere(zone, $"TEST_EsferaEnganche_LavaMulti_{i + 1:00}",
                    Polar(InnerMidRadius, pAng, 4.8f), 1.45f);

                // Enemigos sobre las plataformas intermedias y objetos absorbibles de apoyo.
                if (i == 0 || i == 2 || i == 5)
                {
                    AddAbsorbableOrb(zone, $"Absorbible_PlatLava_{i + 1:00}_A", pPos + new Vector3(0.8f, 0.85f, 0f), 0.42f);
                    AddAbsorbableOrb(zone, $"Absorbible_PlatLava_{i + 1:00}_B", pPos + new Vector3(-0.8f, 0.85f, 0f), 0.42f);
                }
                if (i == 1 || i == 3 || i == 5)
                {
                    GameObject spiderOnPlat = SpawnEnemy(zone, HexbugPrefabPath, $"Arana_PlataformaLava_{i + 1:00}",
                        pPos + Vector3.up * 0.45f, TangentRotation(pAng));
                    PlatformBoundsConstraint leash = spiderOnPlat.AddComponent<PlatformBoundsConstraint>();
                    leash.Configure(plat.transform, new Vector2(2.2f, 2.6f));
                }
            }

            // Enemigos voladores (Spam Mail) flotando sobre el lago de lava.
            SpawnEnemy(zone, FlyingEmailPrefabPath, "SpamMail_LavaInterior_01",
                Polar(InnerMidRadius, 292f, 3.2f), TangentRotation(292f));
            SpawnEnemy(zone, FlyingEmailPrefabPath, "SpamMail_LavaInterior_02",
                Polar(InnerMidRadius, 322f, 3.4f), TangentRotation(322f));
            SpawnEnemy(zone, FlyingEmailPrefabPath, "SpamMail_LavaInterior_03",
                Polar(InnerMidRadius, 248f, 3.2f), TangentRotation(248f));

            // Extremo derecho sobre el lago (θ = 333°): Callejón sin salida con la Estrella 5.
            CreateStarInstance(zone, "Star_05_CallejonLavaDerecha", Polar(31f, 333f, 1.75f), 4);

            // Extremo izquierdo sobre el lago (θ = 234°..216°): superficie firme, pared divisoria en θ = 214°
            // y vuelta a la derecha (θ = 226°) directo hacia la IA.
            AddCheckpoint(zone, Polar(28.5f, 226f, 0f), 226f + 180f, "08_SuperficieFirme_EntradaIA");
        }

        // ================================================================== Zona 4: Centro del Laberinto — Arena de la IA (Boss Fight estilo Portal)
        private static void BuildCentralAIBossArena()
        {
            Transform arena = Group("Zona4_Centro_Jefe_IA");

            // 1. Chasis central colgante de la IA en (0, 6.2, 0) inspirado en GLaDOS de Portal + cables al techo.
            Transform coreRoot = ChildGroup(arena, "TEST_NucleoCentral_IA");
            coreRoot.position = new Vector3(0f, 5.8f, 0f);

            CreateBox("ColumnaSoporte_Techo", coreRoot, new Vector3(0f, 9.5f, 0f), new Vector3(2.4f, 4.2f, 2.4f),
                Quaternion.identity, "M_Invierno_AICable", Layers.Default, false);
            CreateBox("ChasisSuperior_IA", coreRoot, new Vector3(0f, 6.8f, 0f), new Vector3(3.6f, 2.2f, 3.6f),
                Quaternion.Euler(0f, 25f, 0f), "M_Invierno_AICoreBody", Layers.Default, false);
            CreateBox("TorsoCurvo_IA", coreRoot, new Vector3(0f, 4.9f, 0f), new Vector3(2.6f, 2.4f, 2.8f),
                Quaternion.Euler(12f, 0f, 0f), "M_Invierno_AICoreBody", Layers.Default, false);

            GameObject eyeSphere = Renderer("TEST_OjoOptico_IA", coreRoot, Builtin(PrimitiveType.Sphere),
                Materials["M_Invierno_AIMonitorScreen"], new Vector3(0f, 4.6f, 1.45f), new Vector3(1.5f, 1.5f, 0.9f), false);

            // Cables colgantes desde el núcleo hacia los 3 monitores de pared.
            float[] monitorAngles = { 30f, 120f, 300f };
            for (int i = 0; i < monitorAngles.Length; i++)
            {
                float mAng = monitorAngles[i];
                Vector3 cableMid = Polar(11.5f, mAng, 7.6f);
                CreateBox($"TEST_CableTecho_IA_{i + 1:00}", arena, cableMid,
                    new Vector3(0.45f, 0.45f, 21f), RadialLookInward(mAng),
                    "M_Invierno_AICable", Layers.Default, false);
            }

            // 2. Tres secciones de monitor en la pared perimetral de la arena (R = 22.6 m) que se activan
            //    una tras cada oleada derrotada para dispararles un objeto y dañar a la IA.
            var monitors = new AIMonitorTarget[3];
            for (int i = 0; i < 3; i++)
            {
                monitors[i] = BuildWallMonitorTarget(arena, i, monitorAngles[i]);
            }

            // 3. Objetos absorbibles tirados en el suelo de la arena + 4 surtidores automáticos para que el jugador
            //    jamás se quede sin munición durante las 3 oleadas ni al disparar a los monitores.
            for (int i = 0; i < 12; i++)
            {
                float ang = i * 30f;
                float rad = (i % 2 == 0) ? 10.5f : 16.5f;
                AddAbsorbableOrb(arena, $"Absorbible_ArenaIA_{i + 1:00}", Polar(rad, ang, 0.55f), 0.46f);
            }
            AddRespawningAmmoPad(arena, "Surtidor_ArenaIA_Norte", Polar(13.5f, 90f, 0f));
            AddRespawningAmmoPad(arena, "Surtidor_ArenaIA_Sur", Polar(13.5f, 270f, 0f));
            AddRespawningAmmoPad(arena, "Surtidor_ArenaIA_Este", Polar(13.5f, 0f, 0f));
            AddRespawningAmmoPad(arena, "Surtidor_ArenaIA_Oeste", Polar(13.5f, 180f, 0f));

            // 4. Las 3 oleadas de enemigos de la IA (empiezan desactivadas y AIBossController las despliega en orden):
            //    - Oleada 1: 3 arañas + 2 Spam Mail.
            Transform wave1 = ChildGroup(arena, "Oleada_01_IA");
            SpawnEnemy(wave1, HexbugPrefabPath, "Oleada1_Arana_01", Polar(14f, 20f, 0.1f), RadialLookInward(20f));
            SpawnEnemy(wave1, HexbugPrefabPath, "Oleada1_Arana_02", Polar(14f, 140f, 0.1f), RadialLookInward(140f));
            SpawnEnemy(wave1, HexbugPrefabPath, "Oleada1_Arana_03", Polar(14f, 280f, 0.1f), RadialLookInward(280f));
            SpawnEnemy(wave1, FlyingEmailPrefabPath, "Oleada1_SpamMail_01", Polar(15f, 75f, 3.0f), RadialLookInward(75f));
            SpawnEnemy(wave1, FlyingEmailPrefabPath, "Oleada1_SpamMail_02", Polar(15f, 330f, 3.0f), RadialLookInward(330f));

            //    - Oleada 2: 1 Spawn (Troyano) + 2 Spam Mail + 3 arañas.
            Transform wave2 = ChildGroup(arena, "Oleada_02_IA");
            SpawnEnemy(wave2, HorsePrefabPath, "Oleada2_Spawn_01", Polar(15f, 60f, 0.1f), RadialLookInward(60f));
            SpawnEnemy(wave2, HexbugPrefabPath, "Oleada2_Arana_01", Polar(13f, 110f, 0.1f), RadialLookInward(110f));
            SpawnEnemy(wave2, HexbugPrefabPath, "Oleada2_Arana_02", Polar(13f, 250f, 0.1f), RadialLookInward(250f));
            SpawnEnemy(wave2, HexbugPrefabPath, "Oleada2_Arana_03", Polar(13f, 340f, 0.1f), RadialLookInward(340f));
            SpawnEnemy(wave2, FlyingEmailPrefabPath, "Oleada2_SpamMail_01", Polar(16f, 15f, 3.2f), RadialLookInward(15f));
            SpawnEnemy(wave2, FlyingEmailPrefabPath, "Oleada2_SpamMail_02", Polar(16f, 165f, 3.2f), RadialLookInward(165f));

            //    - Oleada 3: 1 WinRar + 1 Spawn (Troyano) + 2 Spam Mail + 2 arañas.
            Transform wave3 = ChildGroup(arena, "Oleada_03_IA");
            GameObject bossWaveWinRar = SpawnEnemy(wave3, WinRarPrefabPath, "Oleada3_WinRar_01",
                Polar(15f, 45f, 0.1f), RadialLookInward(45f));
            ConfigureBugEnemy(bossWaveWinRar, 22f, 1.4f);
            SpawnEnemy(wave3, HorsePrefabPath, "Oleada3_Spawn_01", Polar(15f, 135f, 0.1f), RadialLookInward(135f));
            SpawnEnemy(wave3, HexbugPrefabPath, "Oleada3_Arana_01", Polar(13f, 90f, 0.1f), RadialLookInward(90f));
            SpawnEnemy(wave3, HexbugPrefabPath, "Oleada3_Arana_02", Polar(13f, 310f, 0.1f), RadialLookInward(310f));
            SpawnEnemy(wave3, FlyingEmailPrefabPath, "Oleada3_SpamMail_01", Polar(16f, 0f, 3.2f), RadialLookInward(0f));
            SpawnEnemy(wave3, FlyingEmailPrefabPath, "Oleada3_SpamMail_02", Polar(16f, 270f, 3.2f), RadialLookInward(270f));

            // Luz/baliza de victoria que se enciende al derrotar el tercer monitor de la IA.
            var victoryBeacon = new GameObject("Baliza_Victoria_IA");
            victoryBeacon.transform.SetParent(arena, false);
            victoryBeacon.transform.position = new Vector3(0f, 3.2f, 0f);
            Light vicLight = victoryBeacon.AddComponent<Light>();
            vicLight.type = LightType.Point;
            vicLight.range = 32f;
            vicLight.intensity = 3.5f;
            vicLight.color = Hex(0x38E54D);
            vicLight.shadows = LightShadows.None;
            vicLight.lightmapBakeType = LightmapBakeType.Baked;
            victoryBeacon.SetActive(false);

            // Controlador principal del encuentro contra la IA.
            var bossCtrlGo = new GameObject("AIBossController");
            bossCtrlGo.transform.SetParent(arena, false);
            bossCtrlGo.transform.position = Vector3.zero;
            AIBossController bossCtrl = bossCtrlGo.AddComponent<AIBossController>();
            bossCtrl.Configure(
                21.0f,
                wave1,
                wave2,
                wave3,
                monitors,
                coreRoot,
                eyeSphere.GetComponent<Renderer>(),
                new[] { victoryBeacon });
        }

        private static AIMonitorTarget BuildWallMonitorTarget(Transform parent, int index, float angleDeg)
        {
            Vector3 pos = Polar(ArenaRadius - 1.35f, angleDeg, 2.75f);
            Quaternion rot = RadialLookInward(angleDeg);

            var monitorRoot = new GameObject($"TEST_MonitorPared_IA_0{index + 1}");
            monitorRoot.transform.SetParent(parent, false);
            monitorRoot.transform.SetPositionAndRotation(pos, rot);
            monitorRoot.layer = Layers.Default;

            BoxCollider hitCollider = monitorRoot.AddComponent<BoxCollider>();
            hitCollider.size = new Vector3(4.6f, 3.4f, 1.2f);

            // Marco blindado exterior del monitor.
            CreateBox("Marco_Monitor", monitorRoot.transform,
                pos - rot * Vector3.forward * 0.25f, new Vector3(5.0f, 3.8f, 0.7f),
                rot, "M_Invierno_AIMonitorFrame", Layers.Default, false);

            // Pantalla cristalina que cambia de color al activarse y romperse.
            GameObject screenGo = CreateBox("Pantalla_Monitor", monitorRoot.transform,
                pos + rot * Vector3.forward * 0.12f, new Vector3(4.2f, 3.0f, 0.25f),
                rot, "M_Invierno_AIMonitorScreen", Layers.Default, false);

            // Compuerta blindada que cubre el monitor mientras la oleada sigue viva y sube al quedar vulnerable.
            GameObject shutterGo = CreateBox("Compuerta_Blindaje", monitorRoot.transform,
                pos + rot * Vector3.forward * 0.32f, new Vector3(4.5f, 3.2f, 0.22f),
                rot, "M_Invierno_Crusher", Layers.Default, false);

            // Baliza superior para que el jugador localice de inmediato qué monitor se activó en la pared.
            GameObject beaconGo = Renderer("Baliza_Vulnerable", monitorRoot.transform, Builtin(PrimitiveType.Sphere),
                Materials["M_Invierno_GrappleSphere"], pos + Vector3.up * 2.5f + rot * Vector3.forward * 0.4f,
                Vector3.one * 0.95f, false);
            beaconGo.SetActive(false);

            AIMonitorTarget target = monitorRoot.AddComponent<AIMonitorTarget>();
            target.Configure(index, screenGo.GetComponent<Renderer>(), shutterGo.transform, new Vector3(0f, 3.5f, 0f), beaconGo);

            AddAudioSlotOn(monitorRoot,
                $"Sonido de activación e impacto del Monitor {index + 1} de la IA",
                AudioSlotTrigger.ArenaGateOpen, true, 1f, 45f, 0.95f);

            return target;
        }

        // ================================================================== Audio preparado (sin crear archivos de audio)
        private static void BuildAudioSystem()
        {
            Transform audioGroup = Group("Audio_RanurasPreparadas");

            var ambient = new GameObject("Audio_AmbienteLaberintoTVs_y_Estática");
            ambient.transform.SetParent(audioGroup, false);
            AddAudioSlotOn(ambient,
                "Ambiente general 2D de Estación Invierno (zumbido de televisores CRT y pulsos digitales)",
                AudioSlotTrigger.AmbientOrManual, false, 0f, 120f, 0.65f);

            var bossVictory = new GameObject("Audio_VictoriaContraIA");
            bossVictory.transform.SetParent(audioGroup, false);
            AddAudioSlotOn(bossVictory,
                "Sonido de colapso final del núcleo de la IA y victoria del juego",
                AudioSlotTrigger.LevelGateOrCompletion, false, 0f, 120f, 0.95f);
        }

        private static void AddAudioSlotOn(GameObject target, string description, AudioSlotTrigger trigger,
                                           bool matchSelf, float spatialBlend, float maxDistance, float volume)
        {
            if (target.GetComponent<AudioSource>() == null) target.AddComponent<AudioSource>();
            LevelAudioSlot slot = target.GetComponent<LevelAudioSlot>();
            if (slot == null) slot = target.AddComponent<LevelAudioSlot>();
            slot.Configure(description, trigger, matchSelf, spatialBlend, maxDistance, volume);
        }

        // ================================================================== Jugador standalone y puerta de llegada
        private static void BuildPlayerAndSystems()
        {
            var standalone = new GameObject("--- STANDALONE ---");
            var bootstrap = new GameObject("LevelBootstrap");
            bootstrap.AddComponent<StandaloneLevelContent>().Configure(standalone);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
            if (prefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Vector3 spawn = Polar(58.5f, 52f, 0.15f);
                Quaternion rot = RadialLookInward(52f);
                player.transform.SetPositionAndRotation(spawn, rot);
                player.transform.SetParent(standalone.transform, true);
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
            eventSystem.transform.SetParent(standalone.transform, false);
            standalone.SetActive(false);
        }

        private static void BuildEntrance()
        {
            var entrance = new GameObject("LevelEntrance");
            entrance.transform.SetParent(root, false);
            Vector3 entrancePos = Polar(60.5f, 52f, 0f);
            Quaternion entranceRot = RadialLookInward(52f);
            entrance.transform.SetPositionAndRotation(entrancePos, entranceRot);
            entrance.AddComponent<LevelEntrance>().Configure(string.Empty, LevelGateBuilder.Aperture);
        }

        // ================================================================== Utilidades geométricas y de creación
        private static Vector3 Polar(float radius, float angleDeg, float y)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad) * radius, y, Mathf.Sin(rad) * radius);
        }

        /// <summary>Rotación cuyo frente (+Z) apunta hacia el centro del laberinto desde el ángulo indicado.</summary>
        private static Quaternion RadialLookInward(float angleDeg)
        {
            Vector3 inward = -Polar(1f, angleDeg, 0f).normalized;
            return Quaternion.LookRotation(inward, Vector3.up);
        }

        /// <summary>Rotación cuyo frente (+Z) sigue la tangente antihoraria en el ángulo indicado.</summary>
        private static Quaternion TangentRotation(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
            return Quaternion.LookRotation(tangent, Vector3.up);
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 size,
                                            Quaternion rotation, string materialName, int layer, bool markStatic)
        {
            GameObject go = Renderer(name, parent, Builtin(PrimitiveType.Cube),
                Materials[materialName], position, size, false);
            go.transform.rotation = rotation;
            go.layer = layer;
            go.AddComponent<BoxCollider>();
            if (markStatic) MarkStaticBaked(go);
            return go;
        }

        private static GameObject CreateMovingPlatformBox(
            string name, Transform parent, Vector3 position, Vector3 size,
            Quaternion rotation, string materialName, int layer,
            Vector3 travelOffset, float duration, float phaseOffset)
        {
            var rootGo = new GameObject(name);
            rootGo.transform.SetParent(parent, false);
            rootGo.transform.SetPositionAndRotation(position, rotation);
            rootGo.layer = layer;

            BoxCollider col = rootGo.AddComponent<BoxCollider>();
            col.size = size;

            var meshGo = new GameObject("Malla");
            meshGo.transform.SetParent(rootGo.transform, false);
            meshGo.transform.localScale = size;
            meshGo.layer = layer;
            meshGo.AddComponent<MeshFilter>().sharedMesh = Builtin(PrimitiveType.Cube);
            meshGo.AddComponent<MeshRenderer>().sharedMaterial = Materials[materialName];

            MovingPlatform mp = rootGo.AddComponent<MovingPlatform>();
            mp.Configure(travelOffset, duration, phaseOffset, new Vector3(size.x * 0.48f, 0.8f, size.z * 0.48f), size.y * 0.5f + 0.45f);
            return rootGo;
        }

        private static void AddGrappleSphere(Transform parent, string name, Vector3 position, float diameter)
        {
            GameObject go = Renderer(name, parent, Builtin(PrimitiveType.Sphere),
                Materials["M_Invierno_GrappleSphere"], position, Vector3.one * diameter, false);
            go.layer = Layers.Grapple;
            go.AddComponent<SphereCollider>();
            MarkStaticBaked(go);
        }

        private static void AddAbsorbableOrb(Transform parent, string name, Vector3 position, float radius)
        {
            GameObject go = Renderer(name, parent, Builtin(PrimitiveType.Sphere),
                Materials["M_Invierno_Absorbable"], position, Vector3.one * (radius * 2f), false);
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
                Materials["M_Invierno_FloorTrim"], floorPosition + Vector3.up * 0.05f, new Vector3(1.35f, 0.05f, 1.35f), false);
            pad.layer = Layers.Default;
            RespawningAbsorbablePad dispenser = pad.AddComponent<RespawningAbsorbablePad>();
            dispenser.Configure(Builtin(PrimitiveType.Sphere), Materials["M_Invierno_Absorbable"], 0.44f, 3.0f);
        }

        private static void AddCheckpoint(Transform parent, Vector3 groundPoint, float yaw, string label)
        {
            var go = new GameObject("Checkpoint_" + label);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(groundPoint, Quaternion.Euler(0f, yaw, 0f));
            go.layer = Layers.Trigger;

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(8f, 4f, 8f);
            box.center = new Vector3(0f, 2f, 0f);

            Checkpoint cp = go.AddComponent<Checkpoint>();
            Checkpoints.Add(cp);

            GameObject ring = Renderer("Aro", go.transform, Builtin(PrimitiveType.Cylinder),
                Materials["M_Invierno_Checkpoint"], groundPoint + Vector3.up * 0.05f, new Vector3(2.6f, 0.04f, 2.6f), false);
            MarkStaticBaked(ring);
        }

        private static void AttachDefeatUnlock(Transform parent, string name, GameObject[] targets, Transform barrier,
                                               Vector3 openOffset, float duration, GameObject[] activateOnUnlock,
                                               GameObject[] deactivateOnUnlock, string toast, bool dropAtEnemy)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = barrier != null ? barrier.position : Vector3.zero;

            DefeatUnlockController unlock = go.AddComponent<DefeatUnlockController>();
            unlock.Configure(targets, barrier, openOffset, duration, activateOnUnlock, deactivateOnUnlock, toast, dropAtEnemy);
            AddAudioSlotOn(go, $"Sonido de desbloqueo ({name})", AudioSlotTrigger.ArenaGateOpen, true, 1f, 45f, 0.9f);
        }

        private static GameObject SpawnEnemy(Transform parent, string prefabPath, string instanceName, Vector3 position, Quaternion rotation)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Invierno] No se encontró el prefab de enemigo en {prefabPath}.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = instanceName;
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
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
                l.lightmapBakeType = LightmapBakeType.Baked;
            }
            return star;
        }

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

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
