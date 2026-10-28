// -----------------------------------------------------------------------------
// NostalgIA — Editor (NostalgIA.Editor)
// Archivo: LevelGateBuilder.cs
// Propósito: construir el prefab de la puerta entre niveles (mallas, materiales,
// perfil de destello) y conectarla en el Tutorial con el nivel de Verano.
// -----------------------------------------------------------------------------

using System.Linq;
using NostalgIA.Core;
using NostalgIA.Core.Levels;
using NostalgIA.EditorTools.Setup;
using NostalgIA.Gameplay.Level;
using NostalgIA.Gameplay.Player;
using NostalgIA.Presentation.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NostalgIA.EditorTools.Levels
{
    /// <summary>
    /// Constructor de la puerta de nivel. Todos los materiales y mallas se guardan como assets
    /// (ninguno se crea en tiempo de ejecución) y el prefab queda listo para colocarse en
    /// cualquier nivel: basta con indicar la escena destino en <see cref="LevelGate"/>.
    /// </summary>
    public static class LevelGateBuilder
    {
        private const string MenuRoot = "NostalgIA/Niveles/";
        public const string PrefabPath = "Assets/_Project/Prefabs/Level/LevelGate.prefab";
        private const string MaterialsFolder = "Assets/_Project/Art/Materials/Levels";
        private const string MeshesFolder = "Assets/_Project/Art/Meshes/Levels";
        private const string FlashProfilePath = "Assets/_Project/Rendering/Volumes/VP_LevelGateFlash.asset";

        /// <summary>Abertura estándar de la puerta: ancho × alto en metros.</summary>
        public static readonly Vector2 Aperture = new Vector2(3.2f, 4.4f);

        /// <summary>Distancia de la fuente de luz virtual detrás de la puerta, en m.</summary>
        private const float SourceDistance = 6f;

        /// <summary>Longitud de la mancha de luz sobre el suelo, en m.</summary>
        private const float PoolLength = 7.5f;

        /// <summary>Nombre de la instancia en el Tutorial.</summary>
        public const string TutorialGateName = "LevelGate_Verano";

        // ================================================================== prefab
        /// <summary>Crea o reemplaza el prefab de la puerta y sus assets.</summary>
        [MenuItem(MenuRoot + "Construir prefab de puerta de nivel", priority = 300)]
        public static GameObject BuildPrefab()
        {
            Phase1Builder.EnsureFolder(MaterialsFolder);
            Phase1Builder.EnsureFolder(MeshesFolder);
            Phase1Builder.EnsureFolder("Assets/_Project/Prefabs/Level");

            Material portal = Mat("M_LevelGate_Portal", "NOS/LevelGatePortal", m => { });
            Material trim = Mat("M_LevelGate_Trim", "NOS/LevelGateGlow", m =>
            {
                m.DisableKeyword("_SHAFT");
                m.SetFloat("_Shaft", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.SetFloat("_Cull", (float)CullMode.Back);
                m.renderQueue = (int)RenderQueue.Geometry;
            });
            Material shaftMaterial = Mat("M_LevelGate_Shaft", "NOS/LevelGateGlow", m =>
            {
                m.EnableKeyword("_SHAFT");
                m.SetFloat("_Shaft", 1f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            });
            Material frame = Mat("M_LevelGate_Frame", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.12f, 0.14f, 0.19f));
                m.SetFloat("_Metallic", 0.75f);
                m.SetFloat("_Smoothness", 0.72f);
            });
            Material dustMaterial = Mat("M_LevelGate_Dust", "NOS/DataParticle", m =>
            {
                m.SetColor("_TintColor", Color.white);
                m.SetFloat("_Intensity", 1.6f);
                m.SetFloat("_GlyphMix", 0f);
                m.SetFloat("_FlickerSpeed", 3f);
            });
            Mesh shaftMesh = SaveMesh(BuildShaftMesh(Aperture, SourceDistance, PoolLength));
            VolumeProfile flash = BuildFlashProfile();
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            var root = new GameObject("LevelGate");
            LevelGate gate = root.AddComponent<LevelGate>();
            gate.Configure(SceneNames.VeranoFrutiger, SceneNames.Paths.VeranoFrutiger, Aperture);
            LevelGateVisuals visuals = root.AddComponent<LevelGateVisuals>();

            float w = Aperture.x;
            float h = Aperture.y;

            // Marco: postes y dintel con colisión; umbral sin colisión para no tropezar.
            Transform frameGroup = Group(root.transform, "Frame");
            Piece(frameGroup, "Post_Left", cube, frame, new Vector3(-(w * 0.5f + 0.2f), (h + 0.4f) * 0.5f, 0f), new Vector3(0.4f, h + 0.4f, 0.45f), true);
            Piece(frameGroup, "Post_Right", cube, frame, new Vector3(w * 0.5f + 0.2f, (h + 0.4f) * 0.5f, 0f), new Vector3(0.4f, h + 0.4f, 0.45f), true);
            Piece(frameGroup, "Header", cube, frame, new Vector3(0f, h + 0.2f, 0f), new Vector3(w + 0.8f, 0.4f, 0.5f), true);
            Piece(frameGroup, "Threshold", cube, frame, new Vector3(0f, 0.03f, 0f), new Vector3(w + 0.8f, 0.06f, 0.5f), false);

            Renderer[] trims = BuildTrims(root.transform, cube, trim, w, h);

            GameObject surface = Piece(root.transform, "Surface", cube, portal, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, 0.02f), false);

            GameObject shaft = new GameObject("LightShaft");
            shaft.transform.SetParent(root.transform, false);
            shaft.AddComponent<MeshFilter>().sharedMesh = shaftMesh;
            var shaftRenderer = shaft.AddComponent<MeshRenderer>();
            shaftRenderer.sharedMaterial = shaftMaterial;
            shaftRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shaftRenderer.receiveShadows = false;

            Light spill = BuildSpillLight(root.transform, w, h);
            ParticleSystem dust = BuildDust(root.transform, dustMaterial, w, h);

            var flashObject = new GameObject("FlashVolume");
            flashObject.transform.SetParent(root.transform, false);
            Volume flashVolume = flashObject.AddComponent<Volume>();
            flashVolume.isGlobal = true;
            flashVolume.priority = 60f;
            flashVolume.weight = 0f;
            flashVolume.sharedProfile = flash;
            flashVolume.enabled = false;

            // Abertura de regreso: solo superficie y bordes (sin marco), para que al disolverse no
            // quede ninguna pieza flotando en el nivel nuevo.
            Transform arrival = Group(root.transform, "Arrival");
            GameObject arrivalSurface = Piece(arrival, "Surface", cube, portal, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, 0.02f), false);
            Renderer[] arrivalTrims = BuildTrims(arrival, cube, trim, w, h);
            arrival.gameObject.SetActive(false);

            var so = new SerializedObject(visuals);
            so.FindProperty("spillLight").objectReferenceValue = spill;
            so.FindProperty("shaft").objectReferenceValue = shaftRenderer;
            so.FindProperty("dust").objectReferenceValue = dust;
            so.FindProperty("aperture").vector2Value = Aperture;
            so.FindProperty("surface").objectReferenceValue = surface.GetComponent<Renderer>();
            SetArray(so.FindProperty("trims"), trims);
            so.FindProperty("arrival").objectReferenceValue = arrival;
            so.FindProperty("arrivalSurface").objectReferenceValue = arrivalSurface.GetComponent<Renderer>();
            SetArray(so.FindProperty("arrivalTrims"), arrivalTrims);
            so.FindProperty("flashVolume").objectReferenceValue = flashVolume;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[Niveles] Prefab de puerta construido en " + PrefabPath);
            return prefab;
        }

        /// <summary>Tres bordes luminosos (izquierda, derecha, arriba) junto a la abertura.</summary>
        private static Renderer[] BuildTrims(Transform parent, Mesh cube, Material material, float w, float h)
        {
            Transform group = Group(parent, "Trims");
            return new[]
            {
                Piece(group, "Trim_Left", cube, material, new Vector3(-(w * 0.5f + 0.02f), h * 0.5f, 0f), new Vector3(0.05f, h, 0.47f), false).GetComponent<Renderer>(),
                Piece(group, "Trim_Right", cube, material, new Vector3(w * 0.5f + 0.02f, h * 0.5f, 0f), new Vector3(0.05f, h, 0.47f), false).GetComponent<Renderer>(),
                Piece(group, "Trim_Top", cube, material, new Vector3(0f, h + 0.02f, 0f), new Vector3(w + 0.09f, 0.05f, 0.47f), false).GetComponent<Renderer>()
            };
        }

        /// <summary>
        /// Foco sin sombras detrás de la abertura. Su vértice está en la fuente virtual
        /// S = (0, k·h, −d), con k tal que el rayo que pasa por el borde superior de la abertura
        /// toque el suelo a la distancia L: la pendiente del rayo es (h − k·h)/d y corta y = 0 en
        /// z = h·d / (k·h − h), de donde k = 1 + d / L. El ángulo del cono es el doble del mayor
        /// ángulo entre el eje del foco y las esquinas de la abertura, más un margen.
        /// </summary>
        private static Light BuildSpillLight(Transform parent, float w, float h)
        {
            float k = 1f + SourceDistance / PoolLength;
            var source = new Vector3(0f, k * h, -SourceDistance);
            var poolCenter = new Vector3(0f, 0f, PoolLength * 0.45f);
            Vector3 axis = (poolCenter - source).normalized;

            float maxAngle = 0f;
            Vector3[] corners =
            {
                new Vector3(-w * 0.5f, 0f, 0f), new Vector3(w * 0.5f, 0f, 0f),
                new Vector3(-w * 0.5f, h, 0f), new Vector3(w * 0.5f, h, 0f)
            };
            foreach (Vector3 corner in corners) maxAngle = Mathf.Max(maxAngle, Vector3.Angle(axis, corner - source));

            var lightObject = new GameObject("SpillLight");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = source;
            lightObject.transform.localRotation = Quaternion.LookRotation(axis, Vector3.up);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = Mathf.Min(170f, maxAngle * 2f + 8f);
            light.innerSpotAngle = light.spotAngle * 0.9f;
            light.range = Vector3.Distance(source, new Vector3(w * 2f, 0f, PoolLength)) + 6f;
            light.intensity = 7f;
            light.shadows = LightShadows.None;
            light.bounceIntensity = 0f;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            return light;
        }

        /// <summary>Polvo luminoso que flota dentro del haz (sistema fijo del prefab, no se instancia).</summary>
        private static ParticleSystem BuildDust(Transform parent, Material material, float w, float h)
        {
            var dustObject = new GameObject("Dust");
            dustObject.transform.SetParent(parent, false);
            dustObject.transform.localPosition = new Vector3(0f, h * 0.45f, PoolLength * 0.35f);
            var ps = dustObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.maxParticles = 70;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 12f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(w * 1.3f, h * 0.9f, PoolLength * 0.7f);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.06f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.25f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = dustObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>
        /// Malla del haz: cuña entre la abertura (z = 0) y la mancha del suelo. Los rayos salen de
        /// la fuente S y pasan por las esquinas superiores de la abertura hasta tocar el suelo en
        /// z = L, donde la anchura crece en proporción (L + d)/d. Se forman tres caras (superior y
        /// laterales); u recorre la anchura y v la longitud (0 en la puerta, 1 en el suelo).
        /// </summary>
        public static Mesh BuildShaftMesh(Vector2 aperture, float sourceDistance, float poolLength)
        {
            float w = aperture.x * 0.5f;
            float h = aperture.y;
            float spread = (poolLength + sourceDistance) / sourceDistance;
            var topLeft = new Vector3(-w, h, 0f);
            var topRight = new Vector3(w, h, 0f);
            var bottomLeft = new Vector3(-w, 0.02f, 0f);
            var bottomRight = new Vector3(w, 0.02f, 0f);
            var farLeft = new Vector3(-w * spread, 0.02f, poolLength);
            var farRight = new Vector3(w * spread, 0.02f, poolLength);

            var vertices = new[]
            {
                // Cara superior (cuadrilátero inclinado).
                topLeft, topRight, farRight, farLeft,
                // Cara izquierda (triángulo).
                bottomLeft, topLeft, farLeft,
                // Cara derecha (triángulo).
                topRight, bottomRight, farRight
            };
            var uvs = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f)
            };
            var triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 5, 6, 7, 8, 9 };
            var mesh = new Mesh { name = "LevelGate_Shaft", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Perfil del destello al cruzar: exposición +1,1 EV (×2,1 de luminancia, 2^1,1), Bloom
        /// más intenso con umbral bajo y un poco menos de saturación, como al salir a plena luz.
        /// </summary>
        private static VolumeProfile BuildFlashProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(FlashProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, FlashProfilePath);
            }
            Bloom bloom = Override<Bloom>(profile);
            bloom.intensity.Override(2.4f);
            bloom.threshold.Override(0.6f);
            bloom.scatter.Override(0.8f);
            ColorAdjustments color = Override<ColorAdjustments>(profile);
            color.postExposure.Override(1.1f);
            color.saturation.Override(-12f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T Override<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            return component;
        }

        // ================================================================== Tutorial → Verano
        /// <summary>
        /// Sustituye la puerta dimensional del Tutorial por una puerta de nivel hacia Verano
        /// (Frutiger Aero), marca el <c>EventSystem</c> para que acompañe al jugador y agrega la
        /// escena de Verano a la lista del build.
        /// </summary>
        [MenuItem(MenuRoot + "Conectar Tutorial con Verano", priority = 301)]
        public static void ConnectTutorial()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? BuildPrefab();
            Scene scene = EditorSceneManager.OpenScene(SceneNames.Paths.Tutorial, OpenSceneMode.Single);

            GameObject door = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(c => c != null && c.GetType().Name == "TutorialDoor")
                .Select(c => c.gameObject)
                .FirstOrDefault();
            if (door == null)
            {
                Debug.LogError("[Niveles] No se encontró la puerta del Tutorial (TutorialDoor).");
                return;
            }

            foreach (GameObject existing in scene.GetRootGameObjects().Where(r => r.name == TutorialGateName).ToArray())
                Object.DestroyImmediate(existing);

            // La puerta original puede ser un cubo escalado o una forma de ProBuilder con escala 1, así
            // que su base no se deduce de la escala: se toman los límites de su malla y se busca el suelo
            // a ambos lados con rayos. El lado de aproximación es el que tiene suelo a la altura de la
            // base de la puerta (la escalera); el otro lado puede ser un vacío.
            Physics.SyncTransforms();
            Transform doorTransform = door.transform;
            Bounds bounds = WorldMeshBounds(door);
            Vector3 normal = Vector3.ProjectOnPlane(doorTransform.forward, Vector3.up).normalized;
            float frontFloor = FloorHeight(bounds, normal, scene);
            float backFloor = FloorHeight(bounds, -normal, scene);
            float baseY = bounds.min.y;
            if (Mathf.Abs(backFloor - baseY) < Mathf.Abs(frontFloor - baseY)) normal = -normal;
            float floorY = Mathf.Max(frontFloor, backFloor);
            if (float.IsNegativeInfinity(floorY)) floorY = baseY;
            Vector3 floor = new Vector3(bounds.center.x, floorY, bounds.center.z);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = TutorialGateName;
            instance.transform.SetPositionAndRotation(floor, Quaternion.LookRotation(normal, Vector3.up));
            instance.GetComponent<LevelGate>().Configure(SceneNames.VeranoFrutiger, SceneNames.Paths.VeranoFrutiger, Aperture);
            door.SetActive(false);

            // Acompañan al jugador al nivel siguiente: el EventSystem y el HUD (raíces con el presentador
            // del HUD o con lienzos en pantalla). Los lienzos en el mundo pertenecen al nivel.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool travels = root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null ||
                               root.GetComponentInChildren<NostalgIA.Presentation.UI.TutorialHudPresenter>(true) != null ||
                               root.GetComponentsInChildren<Canvas>(true).Any(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace);
                if (travels && root.GetComponent<PersistAcrossLevels>() == null && root.GetComponentInChildren<PlayerMotor>(true) == null)
                    root.AddComponent<PersistAcrossLevels>();
            }

            EnsureInBuild(SceneNames.Paths.VeranoFrutiger);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Niveles] Puerta {TutorialGateName} colocada en {floor} mirando a {normal}.");
        }

        /// <summary>Límites en el mundo de todas las mallas de un objeto (aunque esté inactivo).</summary>
        private static Bounds WorldMeshBounds(GameObject go)
        {
            bool any = false;
            Bounds result = new Bounds(go.transform.position, Vector3.zero);
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds local = filter.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                    Vector3 world = filter.transform.TransformPoint(corner);
                    if (!any) { result = new Bounds(world, Vector3.zero); any = true; }
                    else result.Encapsulate(world);
                }
            }
            return result;
        }

        /// <summary>
        /// Altura del suelo a 1,5 m del borde de la puerta en la dirección indicada, o −∞ si no hay
        /// suelo en los 60 m inferiores. Se usa la física de la escena del editor.
        /// </summary>
        private static float FloorHeight(Bounds bounds, Vector3 direction, Scene scene)
        {
            float reach = Mathf.Abs(Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(direction.x), 0f, Mathf.Abs(direction.z)))) + 1.5f;
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + 2f, bounds.center.z) + direction * reach;
            PhysicsScene physics = scene.GetPhysicsScene();
            return physics.Raycast(origin, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : float.NegativeInfinity;
        }

        /// <summary>Agrega una escena a la lista del build si aún no está.</summary>
        public static void EnsureInBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ================================================================== utilidades
        private static Material Mat(string name, string shaderName, System.Action<Material> setup)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Shader shader = Shader.Find(shaderName);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            setup(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh SaveMesh(Mesh mesh)
        {
            string path = $"{MeshesFolder}/{mesh.name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.SetVertices(mesh.vertices);
            existing.SetUVs(0, mesh.uv);
            existing.SetTriangles(mesh.triangles, 0);
            existing.SetNormals(mesh.normals);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Piece(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (material.shader.name.StartsWith("NOS/LevelGate"))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        private static void SetArray(SerializedProperty property, Renderer[] items)
        {
            property.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }
}
