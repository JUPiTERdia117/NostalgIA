using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Genera la escena y los recursos artísticos del capítulo inicial.</summary>
public static class Level1SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Level1.unity";
    private const string SourceScenePath = "Assets/Scenes/Testing/Prototype.unity";
    private const string ArtPath = "Assets/Art/Level1";
    private const string MeshPath = ArtPath + "/Meshes";
    private const string MaterialPath = ArtPath + "/Materials";
    private const string TexturePath = ArtPath + "/Textures";

    private static Material basalt;
    private static Material moss;
    private static Material brass;
    private static Material aqua;
    private static Material paleCrystal;
    private static Material coral;
    private static Material obsidian;
    private static Material skyGlass;
    private static Mesh islandMesh;
    private static Mesh steppingMesh;
    private static Mesh columnMesh;
    private static Mesh spireMesh;
    private static Mesh gemMesh;
    private static Mesh hoopMesh;
    private static Mesh spikeClusterMesh;
    private static Mesh laserBladeMesh;
    private static Mesh archMesh;
    private static Mesh pathRibbonMesh;
    private static Mesh toolBarrelMesh;

    [MenuItem("JuegoOMGEPICO/Construir Nivel 1")]
    public static void BuildFromMenu() => Build();

    public static void BuildAndExit()
    {
        try
        {
            Build();
            Debug.Log("Nivel 1 listo: Assets/Scenes/Level1.unity");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void Build()
    {
        EnsureFolders();
        CreateArtAssets();

        Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        GameObject[] originalRoots = scene.GetRootGameObjects();
        GameObject player = FindRoot(originalRoots, root => root.CompareTag("Player"));
        GameObject canvas = FindRoot(originalRoots, root => root.GetComponent<Canvas>() != null);
        GameObject eventSystem = FindRoot(originalRoots, root => root.name == "EventSystem");
        GameObject gameControllerObject = FindRoot(originalRoots, root => root.GetComponent<GameController>() != null);
        GameObject lightObject = FindRoot(originalRoots, root => root.GetComponent<Light>() != null);

        if (player == null || canvas == null || eventSystem == null || gameControllerObject == null)
            throw new InvalidOperationException("La escena Prototype ya no contiene el jugador, el Canvas, el EventSystem o el GameController esperados.");

        if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
        EditorSceneManager.SaveScene(scene, ScenePath);

        foreach (GameObject root in originalRoots)
        {
            if (root == player || root == canvas || root == eventSystem || root == gameControllerObject || root == lightObject)
                continue;
            UnityEngine.Object.DestroyImmediate(root);
        }

        RemoveMissingBehaviours(scene);

        ConfigurePlayer(player);
        HidePrototypeScore(canvas);
        ConfigureLight(lightObject);
        ConfigureRenderSettings();
        CreateVolume();
        BuildWorld(player.transform.position);
        SetupBuildSettings();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Art");
        EnsureFolder("Assets/Art", "Level1");
        EnsureFolder(ArtPath, "Meshes");
        EnsureFolder(ArtPath, "Materials");
        EnsureFolder(ArtPath, "Textures");
        EnsureFolder("Assets/Scripts", "Editor");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static GameObject FindRoot(GameObject[] roots, Predicate<GameObject> predicate)
    {
        foreach (GameObject root in roots)
            if (predicate(root)) return root;
        return null;
    }

    private static void RemoveMissingBehaviours(Scene scene)
    {
        int removed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        }
        if (removed > 0) Debug.Log($"Se retiraron {removed} componentes sin script del nivel copiado.");
    }

    private static void ConfigurePlayer(GameObject player)
    {
        player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Transform selfCapsule = player.transform.Find("Capsule");
        if (selfCapsule != null)
        {
            MeshRenderer bodyRenderer = selfCapsule.GetComponent<MeshRenderer>();
            if (bodyRenderer != null) bodyRenderer.enabled = false;
        }
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.dead = false;
            controller.health = 3;
        }

        Camera camera = player.GetComponentInChildren<Camera>(true);
        if (camera != null)
        {
            UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
            }
        }

        // Conserva los componentes y su comportamiento; sustituye solo la esfera verde de prueba
        // por una boquilla de gancho/aspiradora facetada que no tapa la retícula.
        ObjectVacum vacuum = player.GetComponentInChildren<ObjectVacum>(true);
        if (vacuum != null)
        {
            GameObject tool = vacuum.gameObject;
            MeshFilter filter = tool.GetComponent<MeshFilter>();
            MeshRenderer renderer = tool.GetComponent<MeshRenderer>();
            if (filter != null) filter.sharedMesh = toolBarrelMesh;
            if (renderer != null)
            {
                renderer.sharedMaterials = new[] { obsidian, brass, aqua };
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            // Mantiene visible la herramienta en la esquina inferior derecha sin tapar el camino.
            tool.transform.localPosition = new Vector3(0.25f, 1.17f, 0.62f);
            tool.transform.localScale = new Vector3(0.14f, 0.13f, 0.20f);

            Transform collar = CreateMeshPart("Aro del emisor", tool.transform, hoopMesh, brass,
                new Vector3(0f, 0f, 0.52f), Vector3.one * 0.30f);
            collar.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Transform lens = CreateMeshPart("Lente de aurora", tool.transform, gemMesh, aqua,
                new Vector3(0f, 0f, 0.54f), new Vector3(0.085f, 0.085f, 0.04f));
            lens.localRotation = Quaternion.Euler(0f, 16f, 0f);
        }
    }

    private static void HidePrototypeScore(GameObject canvas)
    {
        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name == "StarScore")
            {
                text.gameObject.SetActive(false);
                return;
            }
        }
    }

    private static void ConfigureLight(GameObject lightObject)
    {
        if (lightObject == null) lightObject = new GameObject("Luz de la aurora");
        Light light = lightObject.GetComponent<Light>();
        if (light == null) light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.79f, 0.62f);
        light.intensity = 1.55f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.86f;
        lightObject.transform.rotation = Quaternion.Euler(47f, -34f, 0f);
        lightObject.name = "Luz cálida · aurora";
    }

    private static void ConfigureRenderSettings()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.40f, 0.53f, 0.64f);
        RenderSettings.ambientEquatorColor = new Color(0.31f, 0.43f, 0.47f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.18f, 0.23f);
        RenderSettings.ambientIntensity = 1.1f;
        RenderSettings.reflectionIntensity = 0.8f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.30f, 0.42f, 0.51f);
        RenderSettings.fogDensity = 0.0065f;
    }

    private static void CreateVolume()
    {
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Level 1 · Aurora grading";
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.92f);
        bloom.intensity.Override(0.36f);
        bloom.scatter.Override(0.62f);

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.12f);
        color.contrast.Override(9f);
        color.saturation.Override(7f);
        color.colorFilter.Override(new Color(1f, 0.985f, 0.94f));

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.18f);
        vignette.smoothness.Override(0.44f);
        vignette.color.Override(new Color(0.025f, 0.06f, 0.09f));

        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

        string profilePath = MaterialPath + "/Level1_VolumeProfile.asset";
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath) != null) AssetDatabase.DeleteAsset(profilePath);
        AssetDatabase.CreateAsset(profile, profilePath);

        GameObject volumeObject = new GameObject("Color de aurora · postproceso", typeof(Volume));
        Volume volume = volumeObject.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 5f;
        volume.sharedProfile = profile;
    }

    private static void SetupBuildSettings()
    {
        var settings = new List<EditorBuildSettingsScene>();
        settings.Add(new EditorBuildSettingsScene(ScenePath, true));
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == ScenePath) continue;
            settings.Add(new EditorBuildSettingsScene(scene.path, false));
        }
        EditorBuildSettings.scenes = settings.ToArray();
    }

    private static void CreateArtAssets()
    {
        Texture2D basaltAlbedo = CreateStoneTexture("Basalto · albedo", new Color(0.09f, 0.16f, 0.22f), new Color(0.30f, 0.40f, 0.43f), 1531);
        Texture2D basaltNormal = CreateNormalTexture("Basalto · relieve", 1531, 1.9f);
        Texture2D mossAlbedo = CreateStoneTexture("Musgo · albedo", new Color(0.12f, 0.25f, 0.25f), new Color(0.37f, 0.53f, 0.43f), 9317);
        Texture2D brassAlbedo = CreateStoneTexture("Latón · albedo", new Color(0.28f, 0.14f, 0.055f), new Color(0.78f, 0.49f, 0.20f), 4421);

        basalt = CreateMaterial("Basalto · obsidiana azul", new Color(0.76f, 0.82f, 0.85f), 0.18f, 0.42f, basaltAlbedo, basaltNormal, Color.black);
        moss = CreateMaterial("Pátina · jade", new Color(0.83f, 0.90f, 0.82f), 0.08f, 0.34f, mossAlbedo, null, Color.black);
        brass = CreateMaterial("Latón antiguo", new Color(0.91f, 0.77f, 0.54f), 0.72f, 0.68f, brassAlbedo, null, new Color(0.25f, 0.095f, 0.025f));
        aqua = CreateMaterial("Aurora · turquesa", new Color(0.62f, 0.97f, 1f), 0.28f, 0.76f, null, null, new Color(0.12f, 0.78f, 0.97f));
        paleCrystal = CreateMaterial("Cristal · celeste", new Color(0.78f, 0.96f, 1f), 0.18f, 0.75f, null, null, new Color(0.35f, 0.77f, 1f));
        coral = CreateMaterial("Peligro · coral incandescente", new Color(1f, 0.40f, 0.22f), 0.2f, 0.54f, null, null, new Color(1f, 0.16f, 0.045f));
        obsidian = CreateMaterial("Obsidiana · guardián", new Color(0.21f, 0.32f, 0.38f), 0.38f, 0.72f, null, null, new Color(0.02f, 0.24f, 0.34f));
        skyGlass = CreateMaterial("Portal · vidrio de aurora", new Color(0.25f, 0.82f, 0.88f, 0.24f), 0f, 0.5f, null, null, new Color(0.10f, 0.8f, 0.92f), true);

        islandMesh = SaveMesh("Malla_Plataforma_Isla", CreateIslandMesh(10, 1f, 1f, 1f, 5f));
        steppingMesh = SaveMesh("Malla_Peldaño_Fracturado", CreateIslandMesh(9, 1f, 1f, 1f, 3f));
        columnMesh = SaveMesh("Malla_Columna_Cincelada", CreateSpireMesh(8, 1f, 1f, 1f));
        spireMesh = SaveMesh("Malla_Cristal_Bifaz", CreateShardMesh());
        gemMesh = SaveMesh("Malla_Gema_Icono", CreateGemMesh());
        hoopMesh = SaveMesh("Malla_Aro_Grabado", CreateTorusMesh(1f, 0.055f, 40, 8));
        spikeClusterMesh = SaveMesh("Malla_Dientes_de_Piedra", CreateSpikeClusterMesh());
        laserBladeMesh = SaveMesh("Malla_Hoja_Laser", CreateBeamMesh(5.2f, 0.23f, 0.18f, 8));
        archMesh = SaveMesh("Malla_Arco_del_Templo", CreateArchMesh(2.85f, 2.65f, 0.19f, 40, 10));
        pathRibbonMesh = SaveMesh("Malla_Incrustacion_Ruta", CreateRibbonMesh(new[] { new Vector3(-0.5f, 0f, -1f), new Vector3(-0.1f, 0f, 0f), new Vector3(0.25f, 0f, 1f) }, 0.08f));
        toolBarrelMesh = SaveMesh("Malla_Emisor_de_Herramienta", CreateToolBarrelMesh());
    }

    private static Material CreateMaterial(string name, Color baseColor, float metallic, float smoothness, Texture2D baseMap, Texture2D normalMap, Color emission, bool transparent = false)
    {
        Shader shader = Shader.Find(transparent ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = name };
        string baseColorProperty = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
        material.SetColor(baseColorProperty, baseColor);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (baseMap != null && material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", baseMap);
            material.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
        }
        if (normalMap != null && material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", 0.28f);
            material.EnableKeyword("_NORMALMAP");
        }
        if (emission.maxColorComponent > 0.001f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        string assetPath = MaterialPath + "/" + SafeName(name) + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(material, assetPath);
        return material;
    }

    private static Texture2D CreateStoneTexture(string name, Color dark, Color light, int seed)
    {
        const int size = 512;
        var texture = new Texture2D(size, size, TextureFormat.RGB24, true, false) { name = name, wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color[size * size];
        var random = new System.Random(seed);
        float offset = (float)random.NextDouble() * 100f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;
                float broad = PeriodicNoise(u, v, 5f, 7f, offset);
                float medium = PeriodicNoise(u, v, 19f, 23f, offset + 12f);
                float grain = PeriodicNoise(u, v, 71f, 83f, offset + 42f);
                float seam = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin((u * 17f + v * 3f + broad * 0.28f) * Mathf.PI))), 18f);
                float value = Mathf.Clamp01(0.58f * broad + 0.27f * medium + 0.15f * grain + seam * 0.12f - 0.06f);
                Color c = Color.Lerp(dark, light, value);
                float fleck = HashNoise(x / 2, y / 2, seed + 177);
                c *= 0.91f + fleck * 0.15f;
                pixels[y * size + x] = c;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(true, false);
        string path = TexturePath + "/" + SafeName(name) + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = size;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D CreateNormalTexture(string name, int seed, float strength)
    {
        const int size = 512;
        var texture = new Texture2D(size, size, TextureFormat.RGB24, true, true) { name = name, wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float left = Relief(x - 1, y, seed);
                float right = Relief(x + 1, y, seed);
                float down = Relief(x, y - 1, seed);
                float up = Relief(x, y + 1, seed);
                Vector3 n = new Vector3((left - right) * strength, (down - up) * strength, 1f).normalized;
                pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(true, false);
        string path = TexturePath + "/" + SafeName(name) + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.NormalMap;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = size;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static float Relief(int x, int y, int seed)
    {
        float u = Mathf.Repeat(x / 512f, 1f);
        float v = Mathf.Repeat(y / 512f, 1f);
        return 0.60f * PeriodicNoise(u, v, 5f, 7f, seed * 0.002f) +
               0.28f * PeriodicNoise(u, v, 19f, 23f, seed * 0.002f + 12f) +
               0.12f * PeriodicNoise(u, v, 71f, 83f, seed * 0.002f + 42f);
    }

    private static float PeriodicNoise(float u, float v, float fx, float fy, float offset)
    {
        float x = u * fx + offset;
        float y = v * fy + offset * 0.713f;
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float tx = Mathf.SmoothStep(0f, 1f, x - x0);
        float ty = Mathf.SmoothStep(0f, 1f, y - y0);
        float a = HashNoise(x0, y0, Mathf.RoundToInt(offset * 1000f));
        float b = HashNoise(x0 + 1, y0, Mathf.RoundToInt(offset * 1000f));
        float c = HashNoise(x0, y0 + 1, Mathf.RoundToInt(offset * 1000f));
        float d = HashNoise(x0 + 1, y0 + 1, Mathf.RoundToInt(offset * 1000f));
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private static float HashNoise(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x00ffffff) / 16777215f;
        }
    }

    private static Mesh SaveMesh(string name, Mesh mesh)
    {
        mesh.name = name;
        string path = MeshPath + "/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static string SafeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c.ToString(), string.Empty);
        return name.Replace(" ", "_").Replace("·", "-");
    }

    private static void BuildWorld(Vector3 startPosition)
    {
        var world = new GameObject("CAPÍTULO 01 · TEMPLO DE LA BÓVEDA");
        var route = Group("Ruta y arquitectura", world.transform);
        var hazards = Group("Trampas", world.transform);
        var guardians = Group("Centinelas", world.transform);
        var pickups = Group("Núcleos de aurora", world.transform);
        var dressing = Group("Islas y luminarias", world.transform);

        GameObject flowObject = new GameObject("Flujo del capítulo", typeof(Level1WorldFlow), typeof(Level1HUD));
        flowObject.transform.SetParent(world.transform, false);
        SerializedObject flowSerialized = new SerializedObject(flowObject.GetComponent<Level1WorldFlow>());
        flowSerialized.FindProperty("hud").objectReferenceValue = flowObject.GetComponent<Level1HUD>();
        flowSerialized.ApplyModifiedPropertiesWithoutUndo();

        PlatformSpec[] platforms =
        {
            new PlatformSpec("Umbral de la Aurora", 0f, 0f, 0f, 8.7f, 12f, 3.0f, 154),
            new PlatformSpec("Peldaño quebrado I", 0f, 0.0f, 8.1f, 4.8f, 3.4f, 2.45f, 62),
            new PlatformSpec("Peldaño quebrado II", 0f, 0.36f, 12.5f, 4.8f, 3.8f, 2.55f, 77),
            new PlatformSpec("Peldaño quebrado III", 0f, 0.72f, 17.2f, 5.0f, 4.0f, 2.7f, 83),
            new PlatformSpec("Santuario del eco", 0f, 0.78f, 23.4f, 9.0f, 8.7f, 3.2f, 120),
            new PlatformSpec("Jardín de las veletas", 0.45f, 1.04f, 31.3f, 7.2f, 8.0f, 3.1f, 89),
            new PlatformSpec("Patio de las cuchillas", 0f, 1.28f, 39.5f, 8.2f, 8.4f, 3.5f, 111),
            new PlatformSpec("Atrio de la bóveda", 0f, 1.28f, 48.1f, 10.0f, 10.0f, 3.8f, 203)
        };

        for (int i = 0; i < platforms.Length; i++)
            CreatePlatform(platforms[i], route, i < 4 ? steppingMesh : islandMesh, i * 83 + 11);

        AddPathInlays(route);
        CreateGate("Arco del primer umbral", new Vector3(0f, 0f, 5.25f), route, 1f);
        CreateGate("Arco del santuario", new Vector3(0f, 0.78f, 24.8f), route, 0.9f);
        CreateGate("Arco de la bóveda", new Vector3(0f, 1.28f, 47.2f), route, 1.14f);

        CreateAnchorPylon("Ancla · umbral izquierdo", new Vector3(-3.05f, 0f, 3.9f), route, 2.55f, aqua);
        CreateAnchorPylon("Ancla · umbral derecho", new Vector3(3.05f, 0f, 4.65f), route, 2.95f, paleCrystal);
        CreateAnchorPylon("Ancla · jardín", new Vector3(-2.7f, 1.04f, 32.9f), route, 2.8f, aqua);
        CreateAnchorPylon("Ancla · patio", new Vector3(3.0f, 1.28f, 41.5f), route, 3.0f, paleCrystal);

        CreateCheckpoint(new Vector3(0f, 0.78f, 24.2f), route);
        CreateCore(new Vector3(-1.85f, 1.38f, 4.1f), pickups, 0.85f);
        CreateCore(new Vector3(0.9f, 2.17f, 23.3f), pickups, 1.0f);
        CreateCore(new Vector3(-0.45f, 2.1f, 45.6f), pickups, 1.0f);
        CreateThrowable(new Vector3(-2.2f, 0.74f, 8.9f), pickups, "Reliquia cinética · primera carga");
        CreateThrowable(new Vector3(2.15f, 1.82f, 33.6f), pickups, "Reliquia cinética · carga del jardín");

        CreateWarden("Centinela · explorador", new Vector3(2.3f, 0f, 5.3f), guardians, 0.92f);
        CreateWarden("Centinela · guardián del jardín", new Vector3(-2.05f, 1.04f, 32.7f), guardians, 1.0f);
        CreateWarden("Centinela · guardián del portal", new Vector3(2.0f, 1.28f, 41.7f), guardians, 1.05f);

        CreateSweeper(new Vector3(0.1f, 1.04f, 30.2f), hazards);
        CreateSpikeLane(new Vector3(0f, 1.28f, 38.25f), hazards);
        CreateGoal(new Vector3(0f, 1.28f, 48.5f), route);
        CreateWorldSetDressing(platforms, dressing);
        CreateAmbientLights(dressing);

        SceneManager.MoveGameObjectToScene(world, SceneManager.GetActiveScene());
    }

    private readonly struct PlatformSpec
    {
        public readonly string name;
        public readonly float x, topY, z, width, length, depth;
        public readonly int seed;

        public PlatformSpec(string name, float x, float topY, float z, float width, float length, float depth, int seed)
        {
            this.name = name; this.x = x; this.topY = topY; this.z = z;
            this.width = width; this.length = length; this.depth = depth; this.seed = seed;
        }
    }

    private static Transform Group(string name, Transform parent)
    {
        var group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private static void CreatePlatform(PlatformSpec spec, Transform parent, Mesh mesh, int seed)
    {
        float bodyHeight = spec.depth;
        var platform = new GameObject(spec.name);
        platform.transform.SetParent(parent, false);
        platform.transform.position = new Vector3(spec.x, spec.topY - bodyHeight * 0.9f, spec.z);

        MeshFilter filter = platform.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = platform.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { basalt, moss, brass };
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        MeshCollider collider = platform.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        platform.AddComponent<Level1Walkable>();

        platform.transform.localScale = new Vector3(spec.width, bodyHeight, spec.length);

        // Irregular stone facets continue beneath the edge as a tapering island keel.
        CreateKeel(platform.transform, spec.width, spec.length, bodyHeight, seed);
    }

    private static void CreateKeel(Transform parent, float width, float length, float depth, int seed)
    {
        var keel = new GameObject("Quilla de roca flotante");
        keel.transform.SetParent(parent, false);
        keel.transform.localPosition = new Vector3(0f, -0.43f, 0f);
        keel.transform.localRotation = Quaternion.identity;
        keel.transform.localScale = new Vector3(width * 0.57f, Mathf.Min(1.1f, depth * 0.45f), length * 0.6f);
        keel.AddComponent<MeshFilter>().sharedMesh = steppingMesh;
        MeshRenderer renderer = keel.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = basalt;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        // A decorative mesh below the deck has no collision; the top remains the only safe surface.
    }

    private static void AddPathInlays(Transform parent)
    {
        CreateRibbon("Filete guía · izquierdo", new[]
        {
            new Vector3(-0.55f, 0.036f, -4.7f), new Vector3(-0.55f, 0.036f, 5.2f),
            new Vector3(-0.5f, 0.036f, 8.05f), new Vector3(-0.42f, 0.396f, 12.4f),
            new Vector3(-0.35f, 0.756f, 17.1f), new Vector3(-0.35f, 0.816f, 21.2f)
        }, 0.035f, brass, parent);
        CreateRibbon("Filete guía · derecho", new[]
        {
            new Vector3(0.55f, 0.036f, -4.7f), new Vector3(0.55f, 0.036f, 5.2f),
            new Vector3(0.55f, 0.036f, 8.05f), new Vector3(0.5f, 0.396f, 12.4f),
            new Vector3(0.4f, 0.756f, 17.1f), new Vector3(0.35f, 0.816f, 21.2f)
        }, 0.035f, brass, parent);
        CreateRibbon("Trazado · jardín", new[]
        {
            new Vector3(-2.35f, 1.076f, 28.1f), new Vector3(-2.35f, 1.076f, 34.3f),
            new Vector3(-2.8f, 1.316f, 37.2f)
        }, 0.045f, brass, parent);
        CreateRibbon("Trazado · atrio", new[]
        {
            new Vector3(-2.65f, 1.316f, 44.1f), new Vector3(-2.65f, 1.316f, 51.9f)
        }, 0.045f, brass, parent);
        CreateRibbon("Trazado · atrio espejo", new[]
        {
            new Vector3(2.65f, 1.316f, 44.1f), new Vector3(2.65f, 1.316f, 51.9f)
        }, 0.045f, brass, parent);
    }

    private static void CreateRibbon(string name, Vector3[] points, float width, Material material, Transform parent)
    {
        Mesh mesh = CreateRibbonMesh(points, width);
        string assetPath = MeshPath + "/" + SafeName(name) + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(mesh, assetPath);
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private static void CreateGate(string name, Vector3 ground, Transform parent, float scale)
    {
        var gate = new GameObject(name);
        gate.transform.SetParent(parent, false);
        gate.transform.position = ground;
        gate.transform.localScale = Vector3.one * scale;
        gate.AddComponent<MeshFilter>().sharedMesh = archMesh;
        MeshRenderer archRenderer = gate.AddComponent<MeshRenderer>();
        archRenderer.sharedMaterial = brass;
        archRenderer.shadowCastingMode = ShadowCastingMode.On;
        archRenderer.receiveShadows = true;
        gate.AddComponent<MeshCollider>().sharedMesh = archMesh;

        CreateObelisk(name + " · columna norte", new Vector3(-2.85f, 0.05f, 0f) * scale + ground, parent, 2.35f * scale, brass, 0.34f * scale);
        CreateObelisk(name + " · columna sur", new Vector3(2.85f, 0.05f, 0f) * scale + ground, parent, 2.35f * scale, brass, 0.34f * scale);

        Transform crown = CreateMeshPart(name + " · piedra clave", gate.transform, gemMesh, paleCrystal, new Vector3(0f, 2.63f, 0f), new Vector3(0.30f, 0.44f, 0.30f));
        crown.localRotation = Quaternion.Euler(0f, 0f, 18f);
        AddPointLight(gate.transform, "Luz · arco", new Vector3(0f, 2.32f, -0.15f), new Color(0.33f, 0.82f, 1f), 5.5f, 4.7f, false);
    }

    private static void CreateAnchorPylon(string name, Vector3 position, Transform parent, float height, Material glow)
    {
        CreateObelisk(name, position, parent, height, brass, 0.50f);
        var pylon = new GameObject(name + " · ancla facetada");
        pylon.transform.SetParent(parent, false);
        pylon.transform.position = position + Vector3.up * (height * 0.64f);
        pylon.transform.rotation = Quaternion.Euler(0f, 0f, 12f);
        pylon.transform.localScale = new Vector3(0.34f, 0.8f, 0.34f);
        pylon.AddComponent<MeshFilter>().sharedMesh = spireMesh;
        MeshRenderer renderer = pylon.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = glow;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        CapsuleCollider collider = pylon.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.2f, 0f);
        collider.height = 1.8f;
        collider.radius = 0.65f;
        collider.direction = 1;
        AddPointLight(pylon.transform, "Faro de gancho", Vector3.up * 0.55f, new Color(0.24f, 0.80f, 1f), 3.8f, 3.8f, false);
    }

    private static void CreateObelisk(string name, Vector3 position, Transform parent, float height, Material material, float width)
    {
        var column = new GameObject(name);
        column.transform.SetParent(parent, false);
        column.transform.position = position;
        column.transform.localScale = new Vector3(width, height, width);
        column.AddComponent<MeshFilter>().sharedMesh = columnMesh;
        MeshRenderer renderer = column.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        column.AddComponent<MeshCollider>().sharedMesh = columnMesh;
    }

    private static void CreateCheckpoint(Vector3 position, Transform parent)
    {
        var checkpoint = new GameObject("Baliza de retorno", typeof(SphereCollider), typeof(Level1Checkpoint));
        checkpoint.transform.SetParent(parent, false);
        checkpoint.transform.position = position;
        SphereCollider trigger = checkpoint.GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.55f;
        trigger.center = Vector3.up * 0.55f;

        CreateObelisk("Baliza · pedestal facetado", position + Vector3.down * 0.05f, parent, 1.18f, basalt, 0.7f);
        Transform beacon = CreateMeshPart("Baliza · fuego celeste", parent, spireMesh, aqua, position + Vector3.up * 1.10f, new Vector3(0.42f, 0.75f, 0.42f));
        beacon.localRotation = Quaternion.Euler(0f, 0f, 9f);
        CreateMeshPart("Baliza · aro suspendido", parent, hoopMesh, brass, position + Vector3.up * 1.08f, new Vector3(0.82f, 0.82f, 0.82f)).localRotation = Quaternion.Euler(90f, 0f, 0f);
        AddPointLight(checkpoint.transform, "Resplandor de checkpoint", Vector3.up * 1.35f, new Color(0.16f, 0.85f, 0.94f), 5f, 6f, false);
    }

    private static void CreateCore(Vector3 position, Transform parent, float scale)
    {
        var core = new GameObject("Núcleo de aurora", typeof(SphereCollider), typeof(Level1Pickup));
        core.transform.SetParent(parent, false);
        core.transform.position = position;
        SphereCollider trigger = core.GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.84f * scale;

        Transform gem = CreateMeshPart("Gema partida", core.transform, gemMesh, aqua, Vector3.zero, new Vector3(0.40f, 0.73f, 0.36f) * scale);
        gem.localRotation = Quaternion.Euler(0f, 14f, 10f);
        Transform ringA = CreateMeshPart("Jaula de latón", core.transform, hoopMesh, brass, Vector3.zero, Vector3.one * 0.79f * scale);
        ringA.localRotation = Quaternion.Euler(90f, 12f, 0f);
        Transform ringB = CreateMeshPart("Halo de aurora", core.transform, hoopMesh, paleCrystal, Vector3.zero, Vector3.one * 0.88f * scale);
        ringB.localRotation = Quaternion.Euler(90f, -18f, 46f);
        AddPointLight(core.transform, "Luz de fragmento", Vector3.zero, new Color(0.16f, 0.78f, 0.95f), 2.3f, 3.5f, false);
    }

    private static void CreateThrowable(Vector3 position, Transform parent, string name)
    {
        var relic = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(Rigidbody), typeof(PoolableController), typeof(Level1ThrownRelic));
        relic.layer = LayerMask.NameToLayer("Poolable");
        relic.transform.SetParent(parent, false);
        relic.transform.position = position;
        relic.transform.localScale = Vector3.one * 0.50f;
        relic.GetComponent<MeshFilter>().sharedMesh = gemMesh;
        MeshRenderer renderer = relic.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = paleCrystal;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        MeshCollider collider = relic.GetComponent<MeshCollider>();
        collider.sharedMesh = gemMesh;
        collider.convex = true;
        Rigidbody body = relic.GetComponent<Rigidbody>();
        body.mass = 0.65f;
        body.useGravity = true;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Transform halo = CreateMeshPart("Aro de sujeción", relic.transform, hoopMesh, brass, Vector3.zero, Vector3.one * 0.78f);
        halo.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Transform splinter = CreateMeshPart("Arista luminosa", relic.transform, spireMesh, aqua, new Vector3(0f, 0.30f, 0f), new Vector3(0.27f, 0.52f, 0.25f));
        splinter.localRotation = Quaternion.Euler(0f, 11f, 13f);
    }

    private static void CreateWarden(string name, Vector3 floorPosition, Transform parent, float scale)
    {
        var enemy = new GameObject(name, typeof(CharacterController), typeof(Level1Warden));
        enemy.transform.SetParent(parent, false);
        enemy.transform.position = floorPosition;
        enemy.transform.localScale = Vector3.one * scale;
        CharacterController body = enemy.GetComponent<CharacterController>();
        body.height = 1.52f;
        body.radius = 0.42f;
        body.center = new Vector3(0f, 0.76f, 0f);
        body.stepOffset = 0.15f;
        body.skinWidth = 0.04f;

        Transform form = Group("Forma", enemy.transform);
        MeshRenderer coreRenderer = CreateMeshPart("Corazón facetado", form, gemMesh, obsidian, new Vector3(0f, 0.85f, 0f), new Vector3(0.68f, 0.72f, 0.62f)).gameObject.GetComponent<MeshRenderer>();
        coreRenderer.material = obsidian;

        Transform halo = CreateMeshPart("Aro de contención", form, hoopMesh, brass, new Vector3(0f, 0.85f, 0f), new Vector3(0.92f, 0.92f, 0.92f));
        halo.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Transform eye = CreateMeshPart("Visor luminoso", form, gemMesh, coral, new Vector3(0f, 0.86f, 0.53f), new Vector3(0.22f, 0.13f, 0.08f));
        eye.localRotation = Quaternion.Euler(0f, 0f, 45f);

        for (int side = -1; side <= 1; side += 2)
        {
            Transform fin = CreateMeshPart("Aleta cortante", form, spireMesh, side < 0 ? paleCrystal : aqua,
                new Vector3(side * 0.67f, 0.79f, -0.02f), new Vector3(0.34f, 0.70f, 0.30f));
            fin.localRotation = Quaternion.Euler(0f, side * 19f, side * -28f);
        }

        Transform crown = CreateMeshPart("Corona de cristal", form, spireMesh, paleCrystal, new Vector3(0f, 1.52f, -0.02f), new Vector3(0.28f, 0.55f, 0.27f));
        crown.localRotation = Quaternion.Euler(0f, 0f, 180f);
        AddPointLight(form, "Luz del visor", new Vector3(0f, 0.86f, 0.75f), new Color(0.16f, 0.72f, 1f), 2.2f, 2.5f, false);

        SerializedObject serialized = new SerializedObject(enemy.GetComponent<Level1Warden>());
        serialized.FindProperty("visualRoot").objectReferenceValue = form;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateSweeper(Vector3 floorPosition, Transform parent)
    {
        var pivot = new GameObject("Molinete de energía · trampa mortal", typeof(Level1Sweeper));
        pivot.transform.SetParent(parent, false);
        pivot.transform.position = floorPosition;
        CreateMeshPart("Núcleo del molinete", pivot.transform, gemMesh, coral, new Vector3(0f, 1.12f, 0f), new Vector3(0.38f, 0.38f, 0.38f));

        var blade = new GameObject("Cuchilla giratoria", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(Level1Hazard));
        blade.transform.SetParent(pivot.transform, false);
        blade.transform.localPosition = new Vector3(0f, 1.10f, 0f);
        blade.GetComponent<MeshFilter>().sharedMesh = laserBladeMesh;
        MeshRenderer renderer = blade.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = coral;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        BoxCollider trigger = blade.GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(5.1f, 0.42f, 0.46f);
        trigger.center = Vector3.zero;
        SerializedObject hazard = new SerializedObject(blade.GetComponent<Level1Hazard>());
        hazard.FindProperty("damage").intValue = 3;
        hazard.FindProperty("lethal").boolValue = true;
        hazard.ApplyModifiedPropertiesWithoutUndo();

        Transform counterBlade = CreateMeshPart("Cuchilla opuesta", pivot.transform, laserBladeMesh, brass, new Vector3(0f, 1.10f, 0f), new Vector3(0.72f, 1f, 0.72f));
        counterBlade.localRotation = Quaternion.Euler(0f, 180f, 0f);
        AddPointLight(pivot.transform, "Luz roja · trampa", new Vector3(0f, 1.1f, 0f), new Color(1f, 0.16f, 0.07f), 4f, 2.8f, false);
    }

    private static void CreateSpikeLane(Vector3 floorPosition, Transform parent)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            var bed = new GameObject(side < 0 ? "Campo de espinas · oeste" : "Campo de espinas · este", typeof(BoxCollider), typeof(Level1Hazard));
            bed.transform.SetParent(parent, false);
            bed.transform.position = floorPosition + new Vector3(side * 2.28f, 0.16f, 0f);
            BoxCollider trigger = bed.GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.65f, 0.85f, 3.0f);
            SerializedObject hazard = new SerializedObject(bed.GetComponent<Level1Hazard>());
            hazard.FindProperty("damage").intValue = 3;
            hazard.FindProperty("lethal").boolValue = true;
            hazard.ApplyModifiedPropertiesWithoutUndo();

            Transform spikes = CreateMeshPart("Dientes de basalto", bed.transform, spikeClusterMesh, coral,
                new Vector3(0f, -0.08f, 0f), new Vector3(2.7f, 0.78f, 3.05f));
            spikes.localRotation = Quaternion.Euler(0f, side * 5f, 0f);
            CreateMeshPart("Placa de resguardo", bed.transform, steppingMesh, brass, new Vector3(0f, -0.18f, 0f), new Vector3(2.8f, 0.18f, 3.2f));
        }
    }

    private static void CreateGoal(Vector3 position, Transform parent)
    {
        var goal = new GameObject("Portal de la bóveda", typeof(BoxCollider), typeof(Level1Goal));
        goal.transform.SetParent(parent, false);
        goal.transform.position = position;
        BoxCollider trigger = goal.GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 2.15f, 0f);
        trigger.size = new Vector3(4.5f, 4.8f, 1.6f);

        Transform grandHoop = CreateMeshPart("Halo monumental", goal.transform, hoopMesh, brass,
            new Vector3(0f, 2.7f, 0f), new Vector3(2.25f, 2.25f, 2.25f));
        grandHoop.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Transform innerHoop = CreateMeshPart("Borde de aurora", goal.transform, hoopMesh, aqua,
            new Vector3(0f, 2.7f, 0.02f), new Vector3(2.02f, 2.02f, 2.02f));
        innerHoop.localRotation = Quaternion.Euler(90f, 4f, 0f);

        var field = new GameObject("Membrana celeste", typeof(MeshFilter), typeof(MeshRenderer));
        field.transform.SetParent(goal.transform, false);
        field.transform.localPosition = new Vector3(0f, 2.7f, 0.06f);
        field.GetComponent<MeshFilter>().sharedMesh = CreatePortalFieldMesh(3.7f, 3.7f);
        field.GetComponent<MeshRenderer>().sharedMaterial = skyGlass;

        CreateObelisk("Pilar de la victoria · oeste", position + new Vector3(-3.15f, 0f, 0f), parent, 3.4f, brass, 0.52f);
        CreateObelisk("Pilar de la victoria · este", position + new Vector3(3.15f, 0f, 0f), parent, 3.4f, brass, 0.52f);
        AddPointLight(goal.transform, "Luz del portal", new Vector3(0f, 2.8f, 0f), new Color(0.12f, 0.80f, 1f), 8f, 6.4f, false);
    }

    private static void CreateWorldSetDressing(PlatformSpec[] platforms, Transform parent)
    {
        for (int i = 0; i < platforms.Length; i++)
        {
            PlatformSpec p = platforms[i];
            int crystalCount = i == 0 || i == platforms.Length - 1 ? 5 : 3;
            for (int j = 0; j < crystalCount; j++)
            {
                float side = j % 2 == 0 ? -1f : 1f;
                float zOffset = -p.length * 0.31f + j * (p.length * 0.62f / Mathf.Max(1, crystalCount - 1));
                Vector3 position = new Vector3(p.x + side * (p.width * 0.39f), p.topY - 0.06f, p.z + zOffset);
                if (Mathf.Abs(position.z - 38.25f) < 2.6f && Mathf.Abs(position.x) > 1.15f) continue;
                float crystalScale = (j % 2 == 0 ? 0.68f : 0.48f) * (i == 0 || i == platforms.Length - 1 ? 1.13f : 1f);
                Transform crystal = CreateMeshPart("Cristal de borde", parent, spireMesh, j % 2 == 0 ? paleCrystal : aqua,
                    position + Vector3.up * (0.45f * crystalScale), new Vector3(0.43f, 0.98f, 0.40f) * crystalScale);
                crystal.localRotation = Quaternion.Euler(0f, j * 29f, (j % 2 == 0 ? 7f : -8f));
            }
        }

        var chunks = new System.Random(5321);
        for (int i = 0; i < 23; i++)
        {
            float z = -3f + i * 2.4f;
            float side = i % 2 == 0 ? -1f : 1f;
            float x = side * (8f + (float)chunks.NextDouble() * 7f);
            float y = -5.5f - (float)chunks.NextDouble() * 9f;
            var chunk = new GameObject("Asteroide distante · " + (i + 1));
            chunk.transform.SetParent(parent, false);
            chunk.transform.position = new Vector3(x, y, z);
            float size = 0.8f + (float)chunks.NextDouble() * 1.8f;
            chunk.transform.localScale = new Vector3(size, size * 0.82f, size * 1.1f);
            chunk.transform.rotation = Quaternion.Euler(chunks.Next(0, 45), chunks.Next(0, 360), chunks.Next(-20, 20));
            chunk.AddComponent<MeshFilter>().sharedMesh = steppingMesh;
            MeshRenderer renderer = chunk.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = basalt;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }
    }

    private static void CreateAmbientLights(Transform parent)
    {
        AddPointLight(parent, "Luz · estación inicial", new Vector3(0f, 3.1f, 2.9f), new Color(0.20f, 0.73f, 1f), 5f, 6f, false);
        AddPointLight(parent, "Luz · santuario", new Vector3(0f, 3.8f, 24.2f), new Color(0.22f, 0.85f, 0.86f), 6f, 7f, false);
        AddPointLight(parent, "Luz · salida", new Vector3(0f, 4.4f, 48.6f), new Color(0.2f, 0.78f, 1f), 9f, 8f, false);
    }

    private static void AddPointLight(Transform parent, string name, Vector3 localPosition, Color color, float range, float intensity, bool shadows)
    {
        var lightObject = new GameObject(name, typeof(Light));
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = localPosition;
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
    }

    private static Transform CreateMeshPart(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return go.transform;
    }

    private static Mesh CreateIslandMesh(int sides, float width, float length, float relief, float uvScale)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var stone = new List<int>();
        var metal = new List<int>();
        var top = new List<int>();
        int[] ring0 = AddPlatformRing(vertices, uv, sides, width * 0.72f, length * 0.72f, 0f, relief, uvScale, 11f, 0.10f);
        int[] ring1 = AddPlatformRing(vertices, uv, sides, width * 1.00f, length * 1.00f, 0.20f, relief, uvScale, 27f, 0.04f);
        int[] ring2 = AddPlatformRing(vertices, uv, sides, width * 0.98f, length * 0.98f, 0.70f, relief, uvScale, 49f, 0.025f);
        int[] ring3 = AddPlatformRing(vertices, uv, sides, width * 1.025f, length * 1.025f, 0.82f, relief, uvScale, 67f, 0.018f);
        int[] ring4 = AddPlatformRing(vertices, uv, sides, width * 0.91f, length * 0.91f, 0.90f, relief, uvScale, 67f, 0.015f);
        AddSideBand(stone, ring0, ring1, sides);
        AddSideBand(stone, ring1, ring2, sides);
        AddSideBand(metal, ring2, ring3, sides);
        AddSideBand(stone, ring3, ring4, sides);
        int center = vertices.Count;
        vertices.Add(new Vector3(0f, 0.90f, 0f));
        uv.Add(new Vector2(0.5f * uvScale, 0.5f * uvScale));
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            top.Add(center); top.Add(ring4[next]); top.Add(ring4[i]);
        }
        return BuildFlatMesh("Malla-Isla", vertices, uv, new[] { stone, top, metal });
    }

    private static int[] AddPlatformRing(List<Vector3> vertices, List<Vector2> uv, int sides, float width, float length, float y, float relief, float uvScale, float phase, float jitter)
    {
        int[] indices = new int[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = (i / (float)sides) * Mathf.PI * 2f + 0.12f;
            float noise = Mathf.Sin(i * 17.73f + phase) * jitter;
            float x = Mathf.Cos(angle) * width * 0.5f * (1f + noise);
            float z = Mathf.Sin(angle) * length * 0.5f * (1f + noise * 0.72f);
            float heightNoise = y >= 0.89f ? 0f : Mathf.Sin(i * 8.3f + phase * 0.73f) * 0.022f * relief;
            indices[i] = vertices.Count;
            vertices.Add(new Vector3(x, y * relief + heightNoise, z));
            uv.Add(new Vector2((x / width + 0.5f) * uvScale, (z / length + 0.5f) * uvScale));
        }
        return indices;
    }

    private static void AddSideBand(List<int> triangles, int[] lower, int[] upper, int sides)
    {
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            triangles.Add(lower[i]); triangles.Add(upper[i]); triangles.Add(upper[next]);
            triangles.Add(lower[i]); triangles.Add(upper[next]); triangles.Add(lower[next]);
        }
    }

    private static Mesh CreateSpireMesh(int sides, float topWidth, float topLength, float height)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        int[] baseRing = AddSimpleRing(vertices, uv, sides, topWidth, topLength, 0f);
        int[] shoulder = AddSimpleRing(vertices, uv, sides, topWidth * 0.66f, topLength * 0.66f, height * 0.53f);
        int[] crown = AddSimpleRing(vertices, uv, sides, topWidth * 0.42f, topLength * 0.42f, height * 0.76f);
        AddSideBand(triangles, baseRing, shoulder, sides);
        AddSideBand(triangles, shoulder, crown, sides);
        int tip = vertices.Count;
        vertices.Add(new Vector3(0f, height, 0f));
        uv.Add(new Vector2(0.5f, 1f));
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            triangles.Add(crown[i]); triangles.Add(tip); triangles.Add(crown[next]);
        }
        return BuildFlatMesh("Malla-Columna", vertices, uv, new[] { triangles });
    }

    private static int[] AddSimpleRing(List<Vector3> vertices, List<Vector2> uv, int sides, float width, float length, float y)
    {
        int[] indices = new int[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = i / (float)sides * Mathf.PI * 2f + 0.2f;
            float x = Mathf.Cos(angle) * width * 0.5f;
            float z = Mathf.Sin(angle) * length * 0.5f;
            indices[i] = vertices.Count;
            vertices.Add(new Vector3(x, y, z));
            uv.Add(new Vector2(x / width + 0.5f, y));
        }
        return indices;
    }

    private static Mesh CreateShardMesh()
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        const int sides = 5;
        int[] lower = AddSimpleRing(vertices, uv, sides, 0.66f, 0.58f, 0f);
        int[] wide = AddSimpleRing(vertices, uv, sides, 0.92f, 0.76f, 0.48f);
        int[] upper = AddSimpleRing(vertices, uv, sides, 0.56f, 0.51f, 0.82f);
        AddSideBand(triangles, lower, wide, sides);
        AddSideBand(triangles, wide, upper, sides);
        int tip = vertices.Count;
        vertices.Add(new Vector3(0.11f, 1.45f, -0.06f));
        uv.Add(new Vector2(0.5f, 1f));
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            triangles.Add(upper[i]); triangles.Add(tip); triangles.Add(upper[next]);
        }
        return BuildFlatMesh("Malla-Cristal", vertices, uv, new[] { triangles });
    }

    private static Mesh CreateGemMesh()
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var vertices = new List<Vector3>
        {
            new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),
            new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),
            new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)
        };
        int[] faces =
        {
            0,11,5,0,5,1,0,1,7,0,7,10,0,10,11, 1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,
            3,9,4,3,4,2,3,2,6,3,6,8,3,8,9, 4,9,5,2,4,11,6,2,10,8,6,7,9,8,1
        };
        for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;
        var triangles = new List<int>();
        for (int i = 0; i < faces.Length; i += 3)
        {
            Vector3 a = vertices[faces[i]].normalized;
            Vector3 b = vertices[faces[i + 1]].normalized;
            Vector3 c = vertices[faces[i + 2]].normalized;
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, a + b + c) < 0f)
            {
                triangles.Add(faces[i]); triangles.Add(faces[i + 2]); triangles.Add(faces[i + 1]);
            }
            else
            {
                triangles.Add(faces[i]); triangles.Add(faces[i + 1]); triangles.Add(faces[i + 2]);
            }
        }
        // One level of triangular cuts keeps the mineral facets crisp at gameplay distance.
        SubdivideIcosphere(vertices, triangles);
        var uv = new List<Vector2>();
        foreach (Vector3 v in vertices)
        {
            Vector3 n = v.normalized;
            uv.Add(new Vector2(Mathf.Atan2(n.z, n.x) / (Mathf.PI * 2f) + 0.5f, Mathf.Asin(n.y) / Mathf.PI + 0.5f));
        }
        return BuildFlatMesh("Malla-Gema", vertices, uv, new[] { triangles });
    }

    private static void SubdivideIcosphere(List<Vector3> vertices, List<int> triangles)
    {
        var midpointCache = new Dictionary<long, int>();
        int GetMidpoint(int a, int b)
        {
            int min = Mathf.Min(a, b), max = Mathf.Max(a, b);
            long key = ((long)min << 32) | (uint)max;
            if (midpointCache.TryGetValue(key, out int cached)) return cached;
            int index = vertices.Count;
            vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
            midpointCache.Add(key, index);
            return index;
        }

        var refined = new List<int>(triangles.Count * 4);
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            int ab = GetMidpoint(a, b), bc = GetMidpoint(b, c), ca = GetMidpoint(c, a);
            refined.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        triangles.Clear();
        triangles.AddRange(refined);
    }

    private static Mesh CreateTorusMesh(float majorRadius, float tubeRadius, int segments, int tubeSegments)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float phi = i / (float)segments * Mathf.PI * 2f;
            Vector3 radial = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
            Vector3 center = radial * majorRadius;
            for (int j = 0; j <= tubeSegments; j++)
            {
                float theta = j / (float)tubeSegments * Mathf.PI * 2f;
                Vector3 p = center + radial * (Mathf.Cos(theta) * tubeRadius) + Vector3.up * (Mathf.Sin(theta) * tubeRadius);
                vertices.Add(p);
                uv.Add(new Vector2(i / (float)segments, j / (float)tubeSegments));
            }
        }
        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < tubeSegments; j++)
            {
                int a = i * (tubeSegments + 1) + j;
                int b = a + tubeSegments + 1;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
            }
        }
        return BuildSmoothMesh("Malla-Aro", vertices, uv, new[] { triangles });
    }

    private static Mesh CreateArchMesh(float halfWidth, float rise, float tube, int segments, int sides)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.PI * i / segments;
            Vector3 center = new Vector3(Mathf.Cos(a) * halfWidth, 0.25f + Mathf.Sin(a) * rise, 0f);
            Vector3 normal = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            for (int j = 0; j <= sides; j++)
            {
                float b = Mathf.PI * 2f * j / sides;
                Vector3 p = center + normal * (Mathf.Cos(b) * tube) + Vector3.forward * (Mathf.Sin(b) * tube);
                vertices.Add(p);
                uv.Add(new Vector2(i / (float)segments, j / (float)sides));
            }
        }
        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j;
                int b = a + sides + 1;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
            }
        }
        return BuildSmoothMesh("Malla-Arco", vertices, uv, new[] { triangles });
    }

    private static Mesh CreateSpikeClusterMesh()
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                float x = (col - 2) * 0.48f + (row % 2 == 0 ? 0f : 0.18f);
                float z = (row - 1.5f) * 0.57f;
                float height = 0.54f + Mathf.Abs(Mathf.Sin(row * 11f + col * 7f)) * 0.42f;
                AddPyramid(vertices, uv, triangles, new Vector3(x, 0f, z), 0.42f, height);
            }
        }
        return BuildFlatMesh("Malla-Espinas", vertices, uv, new[] { triangles });
    }

    private static void AddPyramid(List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Vector3 center, float width, float height)
    {
        int start = vertices.Count;
        vertices.Add(center + new Vector3(-width * 0.5f, 0f, -width * 0.5f));
        vertices.Add(center + new Vector3(width * 0.5f, 0f, -width * 0.5f));
        vertices.Add(center + new Vector3(width * 0.5f, 0f, width * 0.5f));
        vertices.Add(center + new Vector3(-width * 0.5f, 0f, width * 0.5f));
        vertices.Add(center + new Vector3(0.04f, height, 0.02f));
        uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up, new Vector2(0.5f, 1f) });
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            triangles.Add(start + i); triangles.Add(start + 4); triangles.Add(start + next);
        }
    }

    private static Mesh CreateBeamMesh(float length, float width, float height, int sides)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        float[] angles = new float[sides];
        for (int i = 0; i < sides; i++) angles[i] = i * Mathf.PI * 2f / sides + Mathf.PI / 8f;
        for (int end = 0; end < 2; end++)
        {
            float x = end == 0 ? -length * 0.5f : length * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                vertices.Add(new Vector3(x, Mathf.Sin(angles[i]) * height * 0.5f, Mathf.Cos(angles[i]) * width * 0.5f));
                uv.Add(new Vector2(end, i / (float)sides));
            }
        }
        for (int i = 0; i < sides; i++)
        {
            int n = (i + 1) % sides;
            triangles.Add(i); triangles.Add(sides + i); triangles.Add(sides + n);
            triangles.Add(i); triangles.Add(sides + n); triangles.Add(n);
        }
        return BuildFlatMesh("Malla-Hoja", vertices, uv, new[] { triangles });
    }

    private static Mesh CreateRibbonMesh(Vector3[] points, float width)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 tangent = i == 0 ? points[1] - points[0] : i == points.Length - 1 ? points[i] - points[i - 1] : points[i + 1] - points[i - 1];
            Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized * width * 0.5f;
            Vector3 a = points[i] - side + Vector3.up * 0.025f;
            Vector3 b = points[i] + side + Vector3.up * 0.025f;
            vertices.Add(a); vertices.Add(b);
            uv.Add(new Vector2(0f, i)); uv.Add(new Vector2(1f, i));
        }
        for (int i = 0; i < points.Length - 1; i++)
        {
            int a = i * 2, b = a + 1, nextA = a + 2, nextB = a + 3;
            triangles.Add(a); triangles.Add(nextA); triangles.Add(b);
            triangles.Add(b); triangles.Add(nextA); triangles.Add(nextB);
        }
        return BuildFlatMesh("Malla-Trazado", vertices, uv, new[] { triangles });
    }

    private static Mesh CreatePortalFieldMesh(float width, float height)
    {
        var mesh = new Mesh { name = "Malla-Membrana" };
        mesh.vertices = new[]
        {
            new Vector3(-width * 0.5f, -height * 0.5f, 0f), new Vector3(width * 0.5f, -height * 0.5f, 0f),
            new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(-width * 0.5f, height * 0.5f, 0f)
        };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh CreateToolBarrelMesh()
    {
        const int sides = 10;
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var body = new List<int>();
        var collar = new List<int>();
        var muzzle = new List<int>();
        int[] ring0 = AddToolRing(vertices, uv, sides, -0.82f, 0.28f);
        int[] ring1 = AddToolRing(vertices, uv, sides, -0.42f, 0.42f);
        int[] ring2 = AddToolRing(vertices, uv, sides, 0.16f, 0.35f);
        int[] ring3 = AddToolRing(vertices, uv, sides, 0.41f, 0.40f);
        int[] ring4 = AddToolRing(vertices, uv, sides, 0.55f, 0.31f);
        int[] ring5 = AddToolRing(vertices, uv, sides, 0.69f, 0.245f);
        AddToolTubeBand(body, ring0, ring1, sides);
        AddToolTubeBand(body, ring1, ring2, sides);
        AddToolTubeBand(body, ring2, ring3, sides);
        AddToolTubeBand(collar, ring3, ring4, sides);
        AddToolTubeBand(muzzle, ring4, ring5, sides);
        return BuildFlatMesh("Malla-Emisor", vertices, uv, new[] { body, collar, muzzle });
    }

    private static int[] AddToolRing(List<Vector3> vertices, List<Vector2> uv, int sides, float z, float radius)
    {
        int[] result = new int[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = i / (float)sides * Mathf.PI * 2f + Mathf.PI / 10f;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            result[i] = vertices.Count;
            vertices.Add(new Vector3(x, y, z));
            uv.Add(new Vector2(i / (float)sides, z));
        }
        return result;
    }

    private static void AddToolTubeBand(List<int> triangles, int[] rear, int[] front, int sides)
    {
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            triangles.Add(rear[i]); triangles.Add(rear[next]); triangles.Add(front[next]);
            triangles.Add(rear[i]); triangles.Add(front[next]); triangles.Add(front[i]);
        }
    }

    private static Mesh BuildFlatMesh(string name, List<Vector3> vertices, List<Vector2> uv, List<int>[] submeshes)
    {
        var outVertices = new List<Vector3>();
        var outUv = new List<Vector2>();
        var outNormals = new List<Vector3>();
        var outTriangles = new List<int>[submeshes.Length];
        for (int s = 0; s < submeshes.Length; s++)
        {
            outTriangles[s] = new List<int>();
            List<int> input = submeshes[s];
            for (int i = 0; i + 2 < input.Count; i += 3)
            {
                Vector3 a = vertices[input[i]], b = vertices[input[i + 1]], c = vertices[input[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                int start = outVertices.Count;
                outVertices.Add(a); outVertices.Add(b); outVertices.Add(c);
                outUv.Add(uv[input[i]]); outUv.Add(uv[input[i + 1]]); outUv.Add(uv[input[i + 2]]);
                outNormals.Add(normal); outNormals.Add(normal); outNormals.Add(normal);
                outTriangles[s].Add(start); outTriangles[s].Add(start + 1); outTriangles[s].Add(start + 2);
            }
        }
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(outVertices);
        mesh.SetUVs(0, outUv);
        mesh.SetNormals(outNormals);
        mesh.subMeshCount = outTriangles.Length;
        for (int i = 0; i < outTriangles.Length; i++) mesh.SetTriangles(outTriangles[i], i, false);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    private static Mesh BuildSmoothMesh(string name, List<Vector3> vertices, List<Vector2> uv, List<int>[] submeshes)
    {
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.subMeshCount = submeshes.Length;
        for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i, false);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }
}
