using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Genera la escena editable del primer nivel y sus mallas/materiales estilizados.</summary>
public static class Level1SceneBuilder
{
    private const string GeneratedFolder = "Assets/Level1/Generated";
    private const string ScenePath = "Assets/Scenes/Level1.unity";
    private static Transform worldRoot;
    private static Material rockMaterial;
    private static Material capMaterial;
    private static Material deepCapMaterial;
    private static Material goldMaterial;
    private static Material tealGlowMaterial;
    private static Material amberGlowMaterial;
    private static Material leafMaterial;
    private static Material trunkMaterial;
    private static Material enemyMaterial;
    private static Material enemyEyeMaterial;
    private static Material starMaterial;
    private static TMP_FontAsset font;
    private static GameObject victoryPanelObject;
    private static TextMeshProUGUI statusLabel;
    private static Level1Hud levelHud;

    [MenuItem("Tools/Juego OMG/Build Level 1 - Santuario del Viento")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Level1");
        Directory.CreateDirectory(GeneratedFolder);
        AssetDatabase.Refresh();
        CreateMaterials();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        Time.timeScale = 1f;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = new GameObject("SANTUARIO DEL VIENTO · NIVEL 01");
        worldRoot = root.transform;

        ConfigureAtmosphere();
        BuildPlayerAndGame();
        BuildInterface();
        BuildCourse();
        BuildDistantIslands();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/Testing/Prototype.unity", false),
            new EditorBuildSettingsScene("Assets/Scenes/Testing/Aspiradora.unity", false),
            new EditorBuildSettingsScene("Assets/Scenes/Testing/Gancho.unity", false)
        };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Nivel 1 creado en Assets/Scenes/Level1.unity");
    }

    [MenuItem("Tools/Juego OMG/Refresh Level 1 HUD")]
    public static void RefreshLevel1Hud()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;
        TextMeshProUGUI health = GameObject.Find("Vitalidad")?.GetComponent<TextMeshProUGUI>();
        if (health != null) health.text = "VITALIDAD   3 / 3";
        TextMeshProUGUI status = GameObject.Find("Texto de estado")?.GetComponent<TextMeshProUGUI>();
        if (status != null) status.text = "OBJETIVO   Reúne los 3 prismas y alcanza el portal.";
        GameObject firstPrism = GameObject.Find("Prisma 01 · Atrio");
        if (firstPrism != null) firstPrism.transform.position = new Vector3(4f, 1.65f, 5.2f);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureAtmosphere()
    {
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Customizable Skybox/Stylized Sky/Materials/Sunset2.mat");
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.05f;
        RenderSettings.reflectionIntensity = 0.8f;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.36f, 0.47f, 0.62f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 65f;
        RenderSettings.fogEndDistance = 145f;

        GameObject sunObject = new GameObject("Luz solar · hora dorada");
        sunObject.transform.SetParent(worldRoot);
        sunObject.transform.rotation = Quaternion.Euler(43f, -28f, 0f);
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.79f, 0.62f);
        sun.intensity = 1.35f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
    }

    private static void BuildCourse()
    {
        // La ruta va de sur a norte; los cambios de altura y los anclajes enseñan el gancho.
        BuildPlatform("01 Atrio de llegada", 0f, 0f, 0f, 16f, 14f, 2.8f, 11, true);
        BuildPlatform("02 Puente de las brasas", 0f, 0.8f, 14f, 10f, 8f, 2.25f, 18, false);
        BuildPlatform("03 Jardín de basalto", -5f, 1.7f, 24.2f, 8f, 7f, 2.35f, 23, true);
        BuildPlatform("04 Patio de cuchillas", -7f, 2.6f, 31.2f, 7f, 7f, 2.45f, 31, false);
        BuildPlatform("05 Balcón del núcleo", -2.5f, 3.2f, 39.2f, 13f, 12f, 3.1f, 39, true);
        BuildPlatform("06 Travesía celeste", 5f, 4f, 49.7f, 9f, 8f, 2.75f, 49, false);
        BuildPlatform("07 Jardín de vigías", 10f, 5f, 58.5f, 8f, 8f, 2.65f, 58, true);
        BuildPlatform("08 Umbral de Lumen", 5.5f, 5.8f, 68f, 15f, 13f, 3.45f, 68, true);

        BuildRidge(0f, 0f, -5.1f, 5.7f, 0.42f);
        BuildCheckpoint(new Vector3(-2.5f, 3.2f, 39.2f));
        BuildLethalBeam(new Vector3(0f, 0.8f, 14.4f), 8.2f, 0.55f, "Barrera térmica");
        BuildSpinningBlade(new Vector3(-7f, 2.6f, 31.2f));
        BuildHazardSpikes(new Vector3(6.2f, 4f, 49.7f));
        BuildGrapplePylon(new Vector3(2.6f, 0f, 8.8f), "Ancla 01 · Arranque");
        BuildGrapplePylon(new Vector3(-2.8f, 0.8f, 18.2f), "Ancla 02 · Jardín");
        BuildGrapplePylon(new Vector3(-9.1f, 2.6f, 30.7f), "Ancla 03 · Patio");
        BuildGrapplePylon(new Vector3(1.8f, 4f, 46.1f), "Ancla 04 · Travesía");
        BuildGrapplePylon(new Vector3(10f, 5f, 55.1f), "Ancla 05 · Vigías");

        BuildStar(new Vector3(4f, 1.65f, 5.2f), "Prisma 01 · Atrio");
        BuildStar(new Vector3(-5.8f, 3.35f, 24.3f), "Prisma 02 · Jardín");
        BuildStar(new Vector3(10f, 6.65f, 58.5f), "Prisma 03 · Vigías");

        BuildPoolableCrystal(new Vector3(-4.7f, 3.95f, 36.8f), "Núcleo lanzable 01");
        BuildPoolableCrystal(new Vector3(0f, 3.95f, 39.2f), "Núcleo lanzable 02");
        BuildPoolableCrystal(new Vector3(4.5f, 4.75f, 49.7f), "Núcleo lanzable 03");

        BuildSentinel(new Vector3(-5.7f, 3.2f, 38.8f), 1);
        BuildSentinel(new Vector3(1.1f, 3.2f, 41.2f), 2);
        BuildSentinel(new Vector3(11.1f, 5f, 57.8f), 3);
        BuildGoal(new Vector3(5.5f, 5.8f, 68f));
        BuildFallVolume();
        BuildDecorativeTrees();
    }

    private static void BuildPlatform(string name, float x, float top, float z,
        float width, float depth, float thickness, int seed, bool garden)
    {
        GameObject platform = new GameObject(name);
        platform.transform.SetParent(worldRoot);
        platform.transform.position = new Vector3(x, top, z);

        Mesh baseMesh = SaveMesh("Malla_" + SafeName(name) + "_basalto",
            MakePlatformMesh("Base " + name, width, depth, thickness, seed));
        MeshFilter baseFilter = platform.AddComponent<MeshFilter>();
        baseFilter.sharedMesh = baseMesh;
        MeshRenderer baseRenderer = platform.AddComponent<MeshRenderer>();
        baseRenderer.sharedMaterial = rockMaterial;
        baseRenderer.shadowCastingMode = ShadowCastingMode.On;
        baseRenderer.receiveShadows = true;
        MeshCollider solid = platform.AddComponent<MeshCollider>();
        solid.sharedMesh = baseMesh;

        GameObject surface = new GameObject("Superficie facetada y grabados");
        surface.transform.SetParent(platform.transform, false);
        surface.transform.localPosition = new Vector3(0f, 0.045f, 0f);
        Mesh overlay = SaveMesh("Malla_" + SafeName(name) + "_superficie",
            MakePlatformMesh("Superficie " + name, width * 0.86f, depth * 0.86f, 0.09f, seed + 97));
        surface.AddComponent<MeshFilter>().sharedMesh = overlay;
        surface.AddComponent<MeshRenderer>().sharedMaterial = garden ? capMaterial : deepCapMaterial;

        float ringRadius = Mathf.Min(width, depth) * 0.31f;
        Mesh ringMesh = GetTorusMesh("Malla_Aro_Platforma", 1f, 0.025f, false);
        GameObject runeRing = AddMeshObject(platform.transform, "Aro rúnico de latón", ringMesh,
            goldMaterial, new Vector3(0f, 0.16f, 0f), Quaternion.identity,
            new Vector3(ringRadius, 1f, ringRadius));
        runeRing.isStatic = true;

        // Pequeñas vetas de luz marcan las esquinas y dan lectura de ruta desde lejos.
        Mesh shard = GetSpikeMesh("Malla_Cristal_Brasa", 1f, 0.18f, 6);
        for (int i = 0; i < 4; i++)
        {
            float angle = Mathf.PI * 0.25f + i * Mathf.PI * 0.5f;
            Vector3 local = new Vector3(Mathf.Cos(angle) * width * 0.38f, 0.06f,
                Mathf.Sin(angle) * depth * 0.38f);
            AddMeshObject(platform.transform, "Veta cristalina", shard,
                (i % 2 == 0) ? tealGlowMaterial : goldMaterial,
                local, Quaternion.Euler(0f, i * 90f, 0f), new Vector3(0.52f, 0.7f, 0.52f));
        }
    }

    private static void BuildRidge(float x, float y, float z, float length, float width)
    {
        GameObject ridge = AddMeshObject(worldRoot, "Dintel de bienvenida · relieve tallado",
            GetBarMesh("Malla_Dintel", length, 0.65f, width), goldMaterial,
            new Vector3(x, y, z), Quaternion.identity, Vector3.one);
        ridge.isStatic = true;
        AddMeshObject(worldRoot, "Sello del atrio", GetTorusMesh("Malla_Aro_Platforma", 1f, 0.025f, true),
            tealGlowMaterial, new Vector3(x, y + 2.35f, z + 0.05f), Quaternion.identity,
            new Vector3(1.35f, 1.35f, 1.35f));
        CreatePointLight("Luz del atrio", new Vector3(x, y + 1.8f, z),
            new Color(0.13f, 0.85f, 0.77f), 4f, 8f);
    }

    private static void BuildCheckpoint(Vector3 position)
    {
        GameObject landmark = new GameObject("Checkpoint · Balcón del núcleo");
        landmark.transform.SetParent(worldRoot);
        landmark.transform.position = position;
        Mesh column = GetColumnMesh("Malla_Columnas_Santuario", 0.34f, 0.28f, 3.1f, 7);
        AddMeshObject(landmark.transform, "Pilar oeste", column, rockMaterial,
            new Vector3(-3.2f, 0f, -0.4f), Quaternion.identity, Vector3.one);
        AddMeshObject(landmark.transform, "Pilar este", column, rockMaterial,
            new Vector3(3.2f, 0f, -0.4f), Quaternion.identity, Vector3.one);
        AddMeshObject(landmark.transform, "Travesaño de bronce", GetBarMesh("Malla_Arco_Ancho", 6.8f, 0.55f, 0.4f),
            goldMaterial, new Vector3(0f, 3.0f, -0.4f), Quaternion.identity, Vector3.one);
        AddMeshObject(landmark.transform, "Halo de reaparición", GetTorusMesh("Malla_Aro_Platforma", 1f, 0.055f, true),
            tealGlowMaterial, new Vector3(0f, 1.95f, -0.12f), Quaternion.identity,
            new Vector3(1.15f, 1.15f, 1.15f));

        GameObject trigger = new GameObject("Activador del punto de reaparición");
        trigger.transform.SetParent(landmark.transform, false);
        trigger.transform.localPosition = Vector3.zero;
        BoxCollider checkpoint = trigger.AddComponent<BoxCollider>();
        checkpoint.isTrigger = true;
        checkpoint.center = new Vector3(0f, 1.05f, 0f);
        checkpoint.size = new Vector3(8f, 2.1f, 7f);
        trigger.AddComponent<RespawnController>();
        CreatePointLight("Luz del checkpoint", position + new Vector3(0f, 2.4f, 0f),
            new Color(0.12f, 0.9f, 0.78f), 5f, 10f);
    }

    private static void BuildLethalBeam(Vector3 topPosition, float length, float height, string label)
    {
        GameObject supports = new GameObject(label + " · emisores");
        supports.transform.SetParent(worldRoot);
        supports.transform.position = topPosition;
        Mesh supportMesh = GetColumnMesh("Malla_Emisor_Barrera", 0.26f, 0.18f, 1.15f, 6);
        AddMeshObject(supports.transform, "Emisor izquierdo", supportMesh, goldMaterial,
            new Vector3(-length * 0.43f, 0f, 0f), Quaternion.identity, Vector3.one);
        AddMeshObject(supports.transform, "Emisor derecho", supportMesh, goldMaterial,
            new Vector3(length * 0.43f, 0f, 0f), Quaternion.identity, Vector3.one);
        GameObject laser = AddMeshObject(supports.transform, "Línea de energía", GetBarMesh("Malla_Barrera_Energia", length, height, 0.22f),
            amberGlowMaterial, new Vector3(0f, 0.57f, 0f), Quaternion.identity, Vector3.one);
        BoxCollider trigger = laser.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(length, height * 1.4f, 0.4f);
        laser.AddComponent<LethalHazard>();
        CreatePointLight(label + " · resplandor", topPosition + new Vector3(0f, 0.9f, 0f),
            new Color(1f, 0.24f, 0.08f), 2.2f, 5.5f);
    }

    private static void BuildSpinningBlade(Vector3 topPosition)
    {
        GameObject pivot = new GameObject("Cuchilla orbital · trampa mortal");
        pivot.transform.SetParent(worldRoot);
        pivot.transform.position = topPosition + new Vector3(0f, 0.88f, 0f);
        pivot.AddComponent<SpinningHazard>();
        Mesh bladeMesh = GetBarMesh("Malla_Cuchilla_Orbital", 5.4f, 0.34f, 0.42f);
        GameObject blade = AddMeshObject(pivot.transform, "Hoja facetada", bladeMesh, amberGlowMaterial,
            Vector3.zero, Quaternion.identity, Vector3.one);
        BoxCollider collider = blade.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(5.4f, 0.55f, 0.6f);
        blade.AddComponent<LethalHazard>();
        AddMeshObject(pivot.transform, "Núcleo de giro", GetSpikeMesh("Malla_Nucleo_Cuchilla", 0.9f, 0.42f, 8),
            goldMaterial, Vector3.zero, Quaternion.identity, new Vector3(1f, 0.45f, 1f));
        CreatePointLight("Brillo de la cuchilla", topPosition + new Vector3(0f, 1.4f, 0f),
            new Color(1f, 0.18f, 0.055f), 3f, 5f);
    }

    private static void BuildHazardSpikes(Vector3 topPosition)
    {
        Mesh spike = GetSpikeMesh("Malla_Espina_Mortal", 1.65f, 0.42f, 7);
        Vector3[] offsets =
        {
            new Vector3(2.6f, 0.02f, -1.7f), new Vector3(3.3f, 0.02f, -0.4f),
            new Vector3(2.2f, 0.02f, 0.9f), new Vector3(3.4f, 0.02f, 1.7f)
        };
        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject hazard = AddMeshObject(worldRoot, "Espina de cuarzo · letal", spike, amberGlowMaterial,
                topPosition + offsets[i], Quaternion.Euler(0f, i * 37f, 0f), Vector3.one);
            MeshCollider trigger = hazard.AddComponent<MeshCollider>();
            trigger.sharedMesh = spike;
            trigger.convex = true;
            trigger.isTrigger = true;
            hazard.AddComponent<LethalHazard>();
        }
    }

    private static void BuildGrapplePylon(Vector3 basePosition, string label)
    {
        GameObject pylon = new GameObject(label);
        pylon.transform.SetParent(worldRoot);
        pylon.transform.position = basePosition;
        pylon.layer = LayerMask.NameToLayer("Graple");
        Mesh columnMesh = SaveMesh("Malla_" + SafeName(label) + "_ancla",
            MakeColumnMesh("Columna de ancla", 0.5f, 0.33f, 4.25f, 7));
        pylon.AddComponent<MeshFilter>().sharedMesh = columnMesh;
        pylon.AddComponent<MeshRenderer>().sharedMaterial = rockMaterial;
        MeshCollider collider = pylon.AddComponent<MeshCollider>();
        collider.sharedMesh = columnMesh;
        GameObject rings = AddMeshObject(pylon.transform, "Aros de orientación",
            GetTorusMesh("Malla_Aro_Platforma", 1f, 0.04f, false), tealGlowMaterial,
            new Vector3(0f, 3.3f, 0f), Quaternion.identity, new Vector3(0.72f, 1f, 0.72f));
        rings.layer = pylon.layer;
        GameObject crystal = AddMeshObject(pylon.transform, "Faro prismático", GetSpikeMesh("Malla_Cristal_Brasa", 1f, 0.3f, 6),
            tealGlowMaterial, new Vector3(0f, 4.05f, 0f), Quaternion.Euler(0f, 0f, 90f),
            new Vector3(0.68f, 0.85f, 0.68f));
        crystal.layer = pylon.layer;
        CreatePointLight(label + " · baliza", basePosition + new Vector3(0f, 3.5f, 0f),
            new Color(0.12f, 0.93f, 0.8f), 2.5f, 7f);
    }

    private static void BuildStar(Vector3 position, string label)
    {
        GameObject star = new GameObject(label);
        star.transform.SetParent(worldRoot);
        star.transform.position = position;
        Mesh starMesh = GetStarMesh("Malla_Prisma_Cinco_Puntas");
        AddMeshObject(star.transform, "Prisma facetado", starMesh, starMaterial,
            Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, 0.34f));
        AddMeshObject(star.transform, "Aro de captura", GetTorusMesh("Malla_Aro_Platforma", 1f, 0.035f, true),
            goldMaterial, Vector3.zero, Quaternion.identity, new Vector3(0.9f, 0.9f, 0.9f));
        SphereCollider trigger = star.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.85f;
        star.AddComponent<StarController>();
        star.AddComponent<Level1CollectibleMotion>();
        CreatePointLight("Luz de prisma", position, new Color(1f, 0.72f, 0.12f), 2f, 4f);
    }

    private static void BuildPoolableCrystal(Vector3 position, string label)
    {
        GameObject core = new GameObject(label);
        core.transform.SetParent(worldRoot);
        core.transform.position = position;
        core.layer = LayerMask.NameToLayer("Poolable");
        AddMeshObject(core.transform, "Cristal lanzable", GetSpikeMesh("Malla_Nucleo_Lanzable", 1f, 0.45f, 6),
            amberGlowMaterial, Vector3.zero, Quaternion.Euler(0f, 0f, 180f),
            new Vector3(0.72f, 1.05f, 0.72f)).layer = core.layer;
        SphereCollider collider = core.AddComponent<SphereCollider>();
        collider.radius = 0.42f;
        Rigidbody body = core.AddComponent<Rigidbody>();
        body.mass = 1.2f;
        body.isKinematic = true;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        core.AddComponent<PoolableController>();
    }

    private static void BuildSentinel(Vector3 position, int variant)
    {
        GameObject root = new GameObject("Centinela de cuarzo " + variant.ToString("00"));
        root.transform.SetParent(worldRoot);
        root.transform.position = position;
        SphereCollider attackZone = root.AddComponent<SphereCollider>();
        attackZone.isTrigger = true;
        attackZone.center = new Vector3(0f, 0.92f, 0f);
        attackZone.radius = 0.92f;
        root.AddComponent<EnemyController>();

        Transform model = new GameObject("Armadura articulada").transform;
        model.SetParent(root.transform, false);
        model.localPosition = new Vector3(0f, 0.85f, 0f);
        Mesh shell = GetIcosphereMesh("Malla_Armadura_Centinela", 1);
        AddMeshObject(model, "Caparazón facetado", shell, enemyMaterial,
            new Vector3(0f, 0.12f, 0f), Quaternion.identity, new Vector3(0.58f, 0.72f, 0.48f));
        AddMeshObject(model, "Visor luminoso", GetSpikeMesh("Malla_Visor_Centinela", 0.34f, 0.22f, 5), enemyEyeMaterial,
            new Vector3(0f, 0.26f, 0.43f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.9f, 0.9f, 0.45f));
        Mesh limb = GetColumnMesh("Malla_Patas_Centinela", 0.12f, 0.07f, 0.58f, 5);
        for (int i = 0; i < 4; i++)
        {
            float angle = Mathf.PI * 0.25f + i * Mathf.PI * 0.5f;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * 0.45f, -0.48f, Mathf.Sin(angle) * 0.36f);
            GameObject leg = AddMeshObject(model, "Pata blindada", limb, goldMaterial,
                offset, Quaternion.Euler(0f, -i * 90f, 16f), Vector3.one);
            AddMeshObject(leg.transform, "Punta de pata", GetSpikeMesh("Malla_Punta_Centinela", 0.3f, 0.12f, 5),
                enemyEyeMaterial, new Vector3(0f, -0.52f, 0f), Quaternion.identity, Vector3.one);
        }
        AddMeshObject(model, "Aro de energía", GetTorusMesh("Malla_Aro_Platforma", 1f, 0.04f, false),
            tealGlowMaterial, new Vector3(0f, -0.1f, 0f), Quaternion.identity,
            new Vector3(0.62f, 1f, 0.62f));
        CreatePointLight("Baliza de centinela", position + Vector3.up * 1.6f,
            new Color(0.2f, 0.9f, 0.84f), 1.2f, 2.8f);
    }

    private static void BuildGoal(Vector3 platformCenter)
    {
        float gateZ = platformCenter.z + 3.4f;
        GameObject gate = new GameObject("Portal de salida · Umbral de Lumen");
        gate.transform.SetParent(worldRoot);
        gate.transform.position = new Vector3(platformCenter.x, platformCenter.y, gateZ);
        Mesh portal = SaveMesh("Malla_Portal_Final", MakeTorusMesh("Portal vertical", 1.78f, 0.3f, true, 64, 12));
        AddMeshObject(gate.transform, "Aro de salida", portal, tealGlowMaterial,
            new Vector3(0f, 2.25f, 0f), Quaternion.identity, Vector3.one);
        AddMeshObject(gate.transform, "Umbral inferior", GetBarMesh("Malla_Umbral_Portal", 4.6f, 0.36f, 0.5f),
            goldMaterial, new Vector3(0f, 0.2f, 0f), Quaternion.identity, Vector3.one);
        AddMeshObject(gate.transform, "Pilar portal izquierdo", GetColumnMesh("Malla_Columnas_Santuario", 0.23f, 0.28f, 4.15f, 7),
            goldMaterial, new Vector3(-2.05f, 0.05f, 0f), Quaternion.identity, Vector3.one);
        AddMeshObject(gate.transform, "Pilar portal derecho", GetColumnMesh("Malla_Columnas_Santuario", 0.23f, 0.28f, 4.15f, 7),
            goldMaterial, new Vector3(2.05f, 0.05f, 0f), Quaternion.identity, Vector3.one);

        GameObject triggerObject = new GameObject("Zona de victoria");
        triggerObject.transform.SetParent(gate.transform, false);
        triggerObject.transform.localPosition = new Vector3(0f, 2.1f, 0f);
        BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(3.1f, 4.2f, 1.5f);
        Level1Goal goal = triggerObject.AddComponent<Level1Goal>();
        SerializedObject serialized = new SerializedObject(goal);
        serialized.FindProperty("requiredPrisms").intValue = 3;
        serialized.FindProperty("victoryPanel").objectReferenceValue = victoryPanelObject;
        serialized.FindProperty("statusText").objectReferenceValue = statusLabel;
        serialized.FindProperty("hud").objectReferenceValue = levelHud;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        AddWorldLabel("EL UMBRAL DE LUMEN", gate.transform.position + new Vector3(0f, 5.05f, 0f));
        CreatePointLight("Luz del portal", gate.transform.position + new Vector3(0f, 2.25f, -0.5f),
            new Color(0.06f, 0.9f, 0.82f), 6f, 12f);
    }

    private static void BuildFallVolume()
    {
        GameObject volume = new GameObject("Vacío bajo las islas · muerte y reaparición");
        volume.transform.SetParent(worldRoot);
        volume.transform.position = new Vector3(0f, -11f, 38f);
        BoxCollider collider = volume.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(220f, 2f, 210f);
        volume.AddComponent<LethalHazard>();
    }

    private static void BuildDecorativeTrees()
    {
        Mesh trunk = GetColumnMesh("Malla_Tronco_Aurora", 0.13f, 0.075f, 1.25f, 6);
        Mesh leaves = GetSpikeMesh("Malla_Copa_Aurora", 1.8f, 0.72f, 7);
        Vector3[] trees =
        {
            new Vector3(-5.7f, 0f, -4.3f), new Vector3(5.8f, 0f, -4.6f),
            new Vector3(-9.7f, 0f, 3.2f), new Vector3(9.3f, 0f, 1.2f),
            new Vector3(-6.4f, 3.2f, 36.2f), new Vector3(-1.2f, 3.2f, 43.1f),
            new Vector3(6.6f, 5f, 57.1f), new Vector3(12.5f, 5f, 60.2f),
            new Vector3(10.4f, 5.8f, 64.8f), new Vector3(0.5f, 5.8f, 66.4f)
        };
        for (int i = 0; i < trees.Length; i++)
        {
            float scale = (i % 3 == 0) ? 1f : 0.78f;
            GameObject plant = new GameObject("Abeto facetado " + (i + 1).ToString("00"));
            plant.transform.SetParent(worldRoot);
            plant.transform.position = trees[i];
            plant.transform.localScale = Vector3.one * scale;
            AddMeshObject(plant.transform, "Tronco", trunk, trunkMaterial,
                new Vector3(0f, 0f, 0f), Quaternion.identity, Vector3.one);
            AddMeshObject(plant.transform, "Copa inferior", leaves, leafMaterial,
                new Vector3(0f, 1.15f, 0f), Quaternion.identity, new Vector3(1f, 0.68f, 1f));
            AddMeshObject(plant.transform, "Copa superior", leaves, leafMaterial,
                new Vector3(0f, 1.95f, 0f), Quaternion.identity, new Vector3(0.68f, 0.5f, 0.68f));
        }
    }

    private static void BuildPlayerAndGame()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/EasyPeasyFirstPersonController/Prefabs/FirstPersonController.prefab");
        if (prefab == null) throw new FileNotFoundException("No se encontró el prefab de FirstPersonController.");
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.name = "Jugador · Explorador del viento";
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 0.08f, -2.1f);
        player.transform.rotation = Quaternion.identity;
        player.transform.SetParent(worldRoot, true);
        EasyPeasyFirstPersonController.FirstPersonController controller =
            player.GetComponent<EasyPeasyFirstPersonController.FirstPersonController>();
        SerializedObject controllerSettings = new SerializedObject(controller);
        SerializedProperty groundMask = controllerSettings.FindProperty("groundMask");
        if (groundMask != null) groundMask.intValue = 1;
        SerializedProperty ledgeLayer = controllerSettings.FindProperty("ledgeLayer");
        if (ledgeLayer != null) ledgeLayer.intValue = 1;
        controllerSettings.ApplyModifiedPropertiesWithoutUndo();

        GameObject tool = new GameObject("Aspiradora y gancho");
        tool.transform.SetParent(controller.playerCamera.transform, false);
        tool.transform.localPosition = new Vector3(0.24f, -0.18f, 0.45f);
        tool.AddComponent<ObjectVacum>();
        tool.AddComponent<GrapleController>();
        player.AddComponent<PlayerController>();

        GameObject gameObject = new GameObject("Game Controller");
        gameObject.transform.SetParent(worldRoot);
        GameController game = gameObject.AddComponent<GameController>();
        game.UpdateSpawnPoint(new Vector3(0f, 0.08f, -2.1f));
    }

    private static void BuildInterface()
    {
        GameObject canvasObject = new GameObject("HUD del Santuario", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Color panelColor = new Color(0.025f, 0.055f, 0.085f, 0.84f);
        RectTransform titlePanel = CreatePanel(canvasObject.transform, "Panel de misión",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(430f, 128f), new Vector2(30f, -28f), panelColor);
        AddUIText(titlePanel, "Nombre del nivel", "SANTUARIO DEL VIENTO",
            new Vector2(20f, -14f), new Vector2(390f, 32f), 23f,
            new Color(0.9f, 0.96f, 1f), TextAlignmentOptions.Left, FontStyles.Bold);
        AddUIText(titlePanel, "Subtítulo del nivel", "SECTOR 01   /   UMBRAL DE LUMEN",
            new Vector2(20f, -47f), new Vector2(390f, 24f), 13f,
            new Color(0.37f, 0.83f, 0.79f), TextAlignmentOptions.Left, FontStyles.Bold);
        TextMeshProUGUI score = AddUIText(titlePanel, "Contador de prismas", "PRISMAS   0 / 3",
            new Vector2(20f, -82f), new Vector2(390f, 28f), 17f,
            new Color(1f, 0.77f, 0.29f), TextAlignmentOptions.Left, FontStyles.Bold);

        RectTransform healthPanel = CreatePanel(canvasObject.transform, "Panel de vitalidad",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(330f, 64f), new Vector2(-30f, -28f), panelColor);
        TextMeshProUGUI health = AddUIText(healthPanel, "Vitalidad", "VITALIDAD   3 / 3",
            new Vector2(18f, -7f), new Vector2(300f, 48f), 18f,
            new Color(0.86f, 0.98f, 0.97f), TextAlignmentOptions.Center, FontStyles.Bold);

        RectTransform controls = CreatePanel(canvasObject.transform, "Panel de controles",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(490f, 108f), new Vector2(30f, 28f), panelColor);
        AddUIText(controls, "Controles", "WASD  MOVER     ·     ESPACIO  SALTAR     ·     SHIFT  CORRER\nX  GANCHO     ·     Q  CAMBIAR HERRAMIENTA\nCLIC IZQ.  ASPIRAR     ·     CLIC DER.  LANZAR",
            new Vector2(16f, -10f), new Vector2(458f, 90f), 13f,
            new Color(0.78f, 0.88f, 0.94f), TextAlignmentOptions.Left, FontStyles.Normal);

        RectTransform statusPanel = CreatePanel(canvasObject.transform, "Panel de estado",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(760f, 54f), new Vector2(0f, 36f), new Color(0.025f, 0.055f, 0.085f, 0.74f));
        statusLabel = AddUIText(statusPanel, "Texto de estado", "OBJETIVO   Reúne los 3 prismas y alcanza el portal.",
            new Vector2(14f, -5f), new Vector2(732f, 44f), 16f,
            new Color(0.57f, 0.96f, 0.87f), TextAlignmentOptions.Center, FontStyles.Bold);

        RectTransform victoryPanel = CreatePanel(canvasObject.transform, "Panel de victoria",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(760f, 310f), Vector2.zero, new Color(0.015f, 0.04f, 0.07f, 0.96f));
        AddUIText(victoryPanel, "Título de victoria", "PORTAL ACTIVADO",
            new Vector2(36f, -46f), new Vector2(688f, 70f), 38f,
            new Color(0.39f, 1f, 0.85f), TextAlignmentOptions.Center, FontStyles.Bold);
        AddUIText(victoryPanel, "Mensaje de victoria", "¡NIVEL 01 COMPLETADO!\nEl Santuario del Viento vuelve a brillar.",
            new Vector2(45f, -126f), new Vector2(670f, 112f), 23f,
            new Color(0.9f, 0.95f, 1f), TextAlignmentOptions.Center, FontStyles.Normal);
        victoryPanelObject = victoryPanel.gameObject;
        victoryPanelObject.SetActive(false);

        levelHud = canvasObject.AddComponent<Level1Hud>();
        SerializedObject hudSerialized = new SerializedObject(levelHud);
        hudSerialized.FindProperty("healthText").objectReferenceValue = health;
        hudSerialized.FindProperty("statusText").objectReferenceValue = statusLabel;
        hudSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameController game = Object.FindAnyObjectByType<GameController>();
        SerializedObject gameSerialized = new SerializedObject(game);
        gameSerialized.FindProperty("scoreText").objectReferenceValue = score;
        gameSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildDistantIslands()
    {
        GameObject islandAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Customizable Skybox/Model/Low Poly Island.fbx");
        if (islandAsset == null) return;
        Vector3[] positions =
        {
            new Vector3(-35f, -11f, 18f), new Vector3(31f, -13f, 39f),
            new Vector3(-27f, -15f, 65f), new Vector3(34f, -18f, 76f)
        };
        float[] scales = { 1.45f, 1.1f, 1.2f, 1.55f };
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject island = (GameObject)PrefabUtility.InstantiatePrefab(islandAsset);
            island.name = "Isla lejana · silueta " + (i + 1).ToString("00");
            island.transform.SetParent(worldRoot, true);
            island.transform.position = positions[i];
            island.transform.rotation = Quaternion.Euler(0f, i * 73f, 0f);
            island.transform.localScale = Vector3.one * scales[i];
        }
    }

    private static void AddWorldLabel(string value, Vector3 position)
    {
        GameObject labelObject = new GameObject("Rótulo del portal");
        labelObject.transform.SetParent(worldRoot);
        labelObject.transform.position = position;
        labelObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = value;
        label.font = font;
        label.fontSize = 3.2f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.8f, 1f, 0.94f);
        label.outlineWidth = 0.16f;
        label.outlineColor = new Color(0.02f, 0.1f, 0.12f);
    }

    private static RectTransform CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position, Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TextMeshProUGUI AddUIText(Transform parent, string name, string value,
        Vector2 position, Vector2 size, float fontSize, Color color,
        TextAlignmentOptions alignment, FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject AddMeshObject(Transform parent, string name, Mesh mesh,
        Material material, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        GameObject instance = new GameObject(name);
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = localRotation;
        instance.transform.localScale = localScale;
        instance.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = instance.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return instance;
    }

    private static void CreatePointLight(string name, Vector3 position, Color color, float intensity, float range)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(worldRoot);
        lightObject.transform.position = position;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    private static void CreateMaterials()
    {
        rockMaterial = MakeMaterial("Basalto · azul profundo", new Color(0.13f, 0.19f, 0.28f), 0.28f, 0.68f);
        capMaterial = MakeMaterial("Piedra tallada · verdiazul", new Color(0.27f, 0.43f, 0.43f), 0.18f, 0.72f);
        deepCapMaterial = MakeMaterial("Piedra tallada · noche", new Color(0.2f, 0.29f, 0.39f), 0.2f, 0.72f);
        goldMaterial = MakeMaterial("Latón de aurora", new Color(0.74f, 0.42f, 0.14f), 0.62f, 0.78f);
        tealGlowMaterial = MakeMaterial("Luz rúnica · turquesa", new Color(0.12f, 0.75f, 0.7f), 0.18f, 0.72f, new Color(0.04f, 0.82f, 0.7f));
        amberGlowMaterial = MakeMaterial("Peligro · ámbar vivo", new Color(1f, 0.25f, 0.07f), 0.05f, 0.52f, new Color(1f, 0.15f, 0.025f));
        leafMaterial = MakeMaterial("Follaje · jade oscuro", new Color(0.1f, 0.38f, 0.3f), 0.04f, 0.53f);
        trunkMaterial = MakeMaterial("Madera · cedro", new Color(0.34f, 0.19f, 0.13f), 0.08f, 0.46f);
        enemyMaterial = MakeMaterial("Centinela · obsidiana", new Color(0.11f, 0.16f, 0.23f), 0.6f, 0.82f);
        enemyEyeMaterial = MakeMaterial("Visor · menta", new Color(0.25f, 1f, 0.82f), 0.22f, 0.75f, new Color(0.18f, 1f, 0.75f));
        starMaterial = MakeMaterial("Prisma · oro solar", new Color(1f, 0.69f, 0.14f), 0.24f, 0.8f, new Color(1f, 0.58f, 0.08f));
    }

    private static Material MakeMaterial(string name, Color color, float metallic, float smoothness, Color? emission = null)
    {
        string path = GeneratedFolder + "/MAT_" + SafeName(name) + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission.Value * 1.8f);
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Mesh SaveMesh(string name, Mesh mesh)
    {
        string path = GeneratedFolder + "/" + SafeName(name) + ".asset";
        Mesh previous = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (previous != null) AssetDatabase.DeleteAsset(path);
        mesh.name = name;
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh GetTorusMesh(string name, float major, float minor, bool vertical, int radial = 40, int tube = 8)
    {
        string path = GeneratedFolder + "/" + SafeName(name + "_" + major + "_" + minor + "_" + vertical + "_" + radial + "_" + tube) + ".asset";
        Mesh cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (cached != null) return cached;
        Mesh mesh = new Mesh { name = name };
        Vector3[] vertices = new Vector3[radial * tube];
        int[] triangles = new int[radial * tube * 6];
        for (int i = 0; i < radial; i++)
        {
            float u = i * Mathf.PI * 2f / radial;
            for (int j = 0; j < tube; j++)
            {
                float v = j * Mathf.PI * 2f / tube;
                if (vertical)
                    vertices[i * tube + j] = new Vector3((major + minor * Mathf.Cos(v)) * Mathf.Cos(u),
                        (major + minor * Mathf.Cos(v)) * Mathf.Sin(u), minor * Mathf.Sin(v));
                else
                    vertices[i * tube + j] = new Vector3((major + minor * Mathf.Cos(v)) * Mathf.Cos(u),
                        minor * Mathf.Sin(v), (major + minor * Mathf.Cos(v)) * Mathf.Sin(u));
            }
        }
        int t = 0;
        for (int i = 0; i < radial; i++)
        for (int j = 0; j < tube; j++)
        {
            int a = i * tube + j;
            int b = ((i + 1) % radial) * tube + j;
            int c = i * tube + (j + 1) % tube;
            int d = ((i + 1) % radial) * tube + (j + 1) % tube;
            if (vertical)
            {
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
            }
            else
            {
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh GetSpikeMesh(string name, float height, float radius, int sides)
    {
        string path = GeneratedFolder + "/" + SafeName(name + "_" + height + "_" + radius + "_" + sides) + ".asset";
        Mesh cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (cached != null) return cached;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            vertices.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        int tip = vertices.Count;
        vertices.Add(new Vector3(0f, height, 0f));
        int bottom = vertices.Count;
        vertices.Add(Vector3.zero);
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            triangles.Add(i); triangles.Add(tip); triangles.Add(next);
            triangles.Add(bottom); triangles.Add(next); triangles.Add(i);
        }
        return SaveMesh(name, BuildMesh(vertices, triangles));
    }

    private static Mesh GetColumnMesh(string name, float bottomRadius, float topRadius, float height, int sides)
    {
        string path = GeneratedFolder + "/" + SafeName(name + "_" + bottomRadius + "_" + topRadius + "_" + height + "_" + sides) + ".asset";
        Mesh cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (cached != null) return cached;
        return SaveMesh(name, MakeColumnMesh(name, bottomRadius, topRadius, height, sides));
    }

    private static Mesh MakeColumnMesh(string name, float bottomRadius, float topRadius, float height, int sides)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            vertices.Add(new Vector3(Mathf.Cos(angle) * bottomRadius, 0f, Mathf.Sin(angle) * bottomRadius));
        }
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            vertices.Add(new Vector3(Mathf.Cos(angle) * topRadius, height, Mathf.Sin(angle) * topRadius));
        }
        int topCenter = vertices.Count;
        vertices.Add(new Vector3(0f, height, 0f));
        int bottomCenter = vertices.Count;
        vertices.Add(Vector3.zero);
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            triangles.Add(i); triangles.Add(i + sides); triangles.Add(next);
            triangles.Add(next); triangles.Add(i + sides); triangles.Add(next + sides);
            triangles.Add(topCenter); triangles.Add(i + sides); triangles.Add(next + sides);
            triangles.Add(bottomCenter); triangles.Add(next); triangles.Add(i);
        }
        return BuildMesh(vertices, triangles, name);
    }

    private static Mesh GetBarMesh(string name, float length, float height, float depth)
    {
        string path = GeneratedFolder + "/" + SafeName(name + "_" + length + "_" + height + "_" + depth) + ".asset";
        Mesh cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (cached != null) return cached;
        float x = length * 0.5f;
        float y = height * 0.5f;
        float z = depth * 0.5f;
        float bevel = Mathf.Min(height, depth) * 0.22f;
        Vector2[] section =
        {
            new Vector2(-y + bevel, -z), new Vector2(y - bevel, -z),
            new Vector2(y, -z + bevel), new Vector2(y, z - bevel),
            new Vector2(y - bevel, z), new Vector2(-y + bevel, z),
            new Vector2(-y, z - bevel), new Vector2(-y, -z + bevel)
        };
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        for (int i = 0; i < section.Length; i++) vertices.Add(new Vector3(-x, section[i].x, section[i].y));
        for (int i = 0; i < section.Length; i++) vertices.Add(new Vector3(x, section[i].x, section[i].y));
        int leftCenter = vertices.Count; vertices.Add(new Vector3(-x, 0f, 0f));
        int rightCenter = vertices.Count; vertices.Add(new Vector3(x, 0f, 0f));
        for (int i = 0; i < section.Length; i++)
        {
            int next = (i + 1) % section.Length;
            triangles.Add(i); triangles.Add(next); triangles.Add(i + section.Length);
            triangles.Add(next); triangles.Add(next + section.Length); triangles.Add(i + section.Length);
            triangles.Add(leftCenter); triangles.Add(next); triangles.Add(i);
            triangles.Add(rightCenter); triangles.Add(i + section.Length); triangles.Add(next + section.Length);
        }
        return SaveMesh(name, BuildMesh(vertices, triangles, name));
    }

    private static Mesh GetStarMesh(string name)
    {
        string path = GeneratedFolder + "/" + name + ".asset";
        Mesh cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (cached != null) return cached;
        const int points = 10;
        const float depth = 0.22f;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        vertices.Add(Vector3.zero);
        vertices.Add(new Vector3(0f, 0f, depth));
        for (int i = 0; i < points; i++)
        {
            float angle = Mathf.PI * 0.5f + i * Mathf.PI * 2f / points;
            float radius = i % 2 == 0 ? 1f : 0.44f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -depth));
        }
        int backStart = vertices.Count;
        for (int i = 0; i < points; i++)
        {
            float angle = Mathf.PI * 0.5f + i * Mathf.PI * 2f / points;
            float radius = i % 2 == 0 ? 1f : 0.44f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, depth));
        }
        for (int i = 0; i < points; i++)
        {
            int next = (i + 1) % points;
            triangles.Add(0); triangles.Add(2 + next); triangles.Add(2 + i);
            triangles.Add(1); triangles.Add(backStart + i); triangles.Add(backStart + next);
            triangles.Add(2 + i); triangles.Add(2 + next); triangles.Add(backStart + i);
            triangles.Add(2 + next); triangles.Add(backStart + next); triangles.Add(backStart + i);
        }
        return SaveMesh(name, BuildMesh(vertices, triangles, name));
    }

    private static Mesh GetIcosphereMesh(string name, int subdivisions)
    {
        string path = GeneratedFolder + "/" + name + "_" + subdivisions + ".asset";
        Mesh cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (cached != null) return cached;
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        List<Vector3> vertices = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;
        List<int> faces = new List<int>
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        Dictionary<long, int> midpointCache = new Dictionary<long, int>();
        for (int pass = 0; pass < subdivisions; pass++)
        {
            List<int> nextFaces = new List<int>();
            midpointCache.Clear();
            for (int i = 0; i < faces.Count; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                int ab = Midpoint(vertices, midpointCache, a, b);
                int bc = Midpoint(vertices, midpointCache, b, c);
                int ca = Midpoint(vertices, midpointCache, c, a);
                nextFaces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            faces = nextFaces;
        }
        for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;
        return SaveMesh(name, BuildMesh(vertices, faces, name));
    }

    private static int Midpoint(List<Vector3> vertices, Dictionary<long, int> cache, int a, int b)
    {
        int min = Mathf.Min(a, b), max = Mathf.Max(a, b);
        long key = ((long)min << 32) + max;
        if (cache.TryGetValue(key, out int index)) return index;
        index = vertices.Count;
        vertices.Add((vertices[a] + vertices[b]).normalized);
        cache[key] = index;
        return index;
    }

    private static Mesh MakePlatformMesh(string name, float width, float depth, float thickness, int seed)
    {
        const int count = 12;
        System.Random random = new System.Random(seed);
        float[] variation = new float[count];
        for (int i = 0; i < count; i++) variation[i] = 0.92f + (float)random.NextDouble() * 0.15f;
        List<Vector3> vertices = new List<Vector3> { Vector3.zero };
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            float r = variation[i];
            vertices.Add(new Vector3(Mathf.Cos(angle) * width * 0.5f * r, 0f,
                Mathf.Sin(angle) * depth * 0.5f * r));
        }
        int shoulder = vertices.Count;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            float r = variation[i] * 1.08f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * width * 0.5f * r, -0.34f,
                Mathf.Sin(angle) * depth * 0.5f * r));
        }
        int lower = vertices.Count;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            float r = variation[i] * 0.69f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * width * 0.5f * r, -thickness,
                Mathf.Sin(angle) * depth * 0.5f * r));
        }
        int bottomCenter = vertices.Count;
        vertices.Add(new Vector3(0f, -thickness, 0f));
        List<int> triangles = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            triangles.Add(0); triangles.Add(1 + next); triangles.Add(1 + i);
            triangles.Add(1 + i); triangles.Add(1 + next); triangles.Add(shoulder + i);
            triangles.Add(1 + next); triangles.Add(shoulder + next); triangles.Add(shoulder + i);
            triangles.Add(shoulder + i); triangles.Add(shoulder + next); triangles.Add(lower + i);
            triangles.Add(shoulder + next); triangles.Add(lower + next); triangles.Add(lower + i);
            triangles.Add(bottomCenter); triangles.Add(lower + i); triangles.Add(lower + next);
        }
        return BuildMesh(vertices, triangles, name);
    }

    private static Mesh MakeTorusMesh(string name, float major, float minor, bool vertical, int radial, int tube)
    {
        Mesh mesh = new Mesh { name = name };
        Vector3[] vertices = new Vector3[radial * tube];
        int[] triangles = new int[radial * tube * 6];
        for (int i = 0; i < radial; i++)
        {
            float u = i * Mathf.PI * 2f / radial;
            for (int j = 0; j < tube; j++)
            {
                float v = j * Mathf.PI * 2f / tube;
                vertices[i * tube + j] = new Vector3((major + minor * Mathf.Cos(v)) * Mathf.Cos(u),
                    (major + minor * Mathf.Cos(v)) * Mathf.Sin(u), minor * Mathf.Sin(v));
            }
        }
        int t = 0;
        for (int i = 0; i < radial; i++)
        for (int j = 0; j < tube; j++)
        {
            int a = i * tube + j;
            int b = ((i + 1) % radial) * tube + j;
            int c = i * tube + (j + 1) % tube;
            int d = ((i + 1) % radial) * tube + (j + 1) % tube;
            triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
            triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildMesh(List<Vector3> vertices, List<int> triangles, string name = "")
    {
        Mesh mesh = new Mesh { name = name };
        if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static string SafeName(string value)
    {
        string safe = value.Replace(" ", "_").Replace("·", "_").Replace("/", "_");
        foreach (char invalid in Path.GetInvalidFileNameChars()) safe = safe.Replace(invalid, '_');
        return safe;
    }
}
