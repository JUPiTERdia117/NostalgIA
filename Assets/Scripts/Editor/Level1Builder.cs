using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Constructor de escena para Nivel 1.
/// Crea programáticamente la escena Level1.unity con iluminación URP estilizada,
/// modelos 3D profesionales, obstáculos mortales, enemigos inteligentes,
/// plataformas móviles, estrellas coleccionables, meta de victoria y UI completa.
/// </summary>
public static class Level1Builder
{
    [MenuItem("Tools/Generar Nivel 1")]
    public static void BuildLevel1Scene()
    {
        Debug.Log("--- Iniciando construcción de Nivel 1 ---");

        // 1. Crear nueva escena vacía
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Cargar Materiales con fallback garantizado
        Material matRock = GetMaterial("Assets/Art/Materials/Mat_CelestialRock.mat", new Color(0.16f, 0.18f, 0.25f, 1f));
        Material matGold = GetMaterial("Assets/Art/Materials/Mat_AncientGold.mat", new Color(0.95f, 0.78f, 0.28f, 1f), true);
        Material matCyanGlow = GetMaterial("Assets/Art/Materials/Mat_EnergyCyan_Glow.mat", new Color(0.15f, 0.9f, 1.0f, 1f), true);
        Material matRedGlow = GetMaterial("Assets/Art/Materials/Mat_DangerRed_Glow.mat", new Color(0.95f, 0.12f, 0.12f, 1f), true);
        Material matStarGlow = GetMaterial("Assets/Art/Materials/Mat_StarGold_Glow.mat", new Color(1.0f, 0.85f, 0.15f, 1f), true);
        Material matDroneArmor = GetMaterial("Assets/Art/Materials/Mat_DroneArmor.mat", new Color(0.12f, 0.13f, 0.15f, 1f));
        Material matVictoryGlow = GetMaterial("Assets/Art/Materials/Mat_VictoryPortal_Glow.mat", new Color(0.6f, 0.2f, 1.0f, 1f), true);
        Material matCheckpointInactive = GetMaterial("Assets/Art/Materials/Mat_CheckpointInactive.mat", new Color(0.85f, 0.55f, 0.1f, 1f), true);
        Material matCheckpointActive = GetMaterial("Assets/Art/Materials/Mat_CheckpointActive.mat", new Color(0.1f, 1.0f, 0.45f, 1f), true);

        // Skybox estilizado
        Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Customizable Skybox/Stylized Sky/Materials/Sunset2.mat");
        if (skyboxMat == null)
            skyboxMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Customizable Skybox/Stylized Sky/Materials/Day2.mat");

        if (skyboxMat != null)
        {
            RenderSettings.skybox = skyboxMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.005f;
            RenderSettings.fogColor = new Color(0.75f, 0.65f, 0.8f, 1f);
        }

        // 3. Generar y Cargar Mallas Nativas (.asset)
        Level1MeshGenerator.GenerateAllNativeMeshes();

        Mesh meshHexIsland = GetNativeMesh("Platform_HexIsland.asset", Level1MeshGenerator.CreateHexIslandMesh);
        Mesh meshHoverPlatform = GetNativeMesh("MovingPlatform_Hover.asset", Level1MeshGenerator.CreateHoverPlatformMesh);
        Mesh meshGrapplePylon = GetNativeMesh("Grapple_AnchorPylon.asset", Level1MeshGenerator.CreateGrapplePylonMesh);
        Mesh meshSpikeBlock = GetNativeMesh("Hazard_SpikeBlock.asset", Level1MeshGenerator.CreateSpikeTrapMesh);
        Mesh meshSawBlade = GetNativeMesh("Hazard_SawBlade.asset", Level1MeshGenerator.CreateSawBladeMesh);
        Mesh meshLaserPillar = GetNativeMesh("Hazard_LaserPillar.asset", Level1MeshGenerator.CreateLaserPillarMesh);
        Mesh meshDrone = GetNativeMesh("Enemy_SentinelDrone.asset", Level1MeshGenerator.CreateDroneMesh);
        Mesh meshStar = GetNativeMesh("Collectible_Star3D.asset", Level1MeshGenerator.CreateStarMesh);
        Mesh meshCheckpoint = GetNativeMesh("Checkpoint_Shrine.asset", Level1MeshGenerator.CreateCheckpointMesh);
        Mesh meshEnergyCube = GetNativeMesh("Energy_CubeBlock.asset", Level1MeshGenerator.CreateEnergyCubeMesh);
        Mesh meshVictoryArch = GetNativeMesh("Victory_PortalArch.asset", Level1MeshGenerator.CreateVictoryArchMesh);

        // 4. Luz Direccional (Sol)
        GameObject sunObj = new GameObject("Directional Light");
        Light sunLight = sunObj.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(1.0f, 0.93f, 0.85f, 1f);
        sunLight.intensity = 1.3f;
        sunLight.shadows = LightShadows.Soft;
        sunObj.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        RenderSettings.sun = sunLight;

        // 5. GameController y Gestores
        GameObject gmObj = new GameObject("GameController_Manager");
        gmObj.tag = "GameController";
        GameController gameController = gmObj.AddComponent<GameController>();
        SoundEffectsManager sfxManager = gmObj.AddComponent<SoundEffectsManager>();

        // 6. Kill Plane (Abismo Mortal)
        GameObject killPlane = new GameObject("DeathZone_KillPlane");
        killPlane.transform.position = new Vector3(0, -30f, 100f);
        BoxCollider killCol = killPlane.AddComponent<BoxCollider>();
        killCol.isTrigger = true;
        killCol.size = new Vector3(600f, 6f, 600f);
        HazardObstacle killHazard = killPlane.AddComponent<HazardObstacle>();
        SetSerializedPrivate(killHazard, "instantKill", true);

        // 7. Jugador
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EasyPeasyFirstPersonController/Prefabs/FirstPersonController.prefab");
        GameObject playerInstance;
        if (playerPrefab != null)
        {
            playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        }
        else
        {
            playerInstance = new GameObject("FirstPersonController");
            playerInstance.AddComponent<CharacterController>();
        }
        playerInstance.name = "Player";
        playerInstance.tag = "Player";
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0) playerInstance.layer = playerLayer;
        playerInstance.transform.position = new Vector3(0, 1.5f, 0);

        // Configurar o añadir PlayerController
        PlayerController playerController = playerInstance.GetComponent<PlayerController>();
        if (playerController == null)
            playerController = playerInstance.AddComponent<PlayerController>();

        // Obtener la cámara del jugador y asegurar UniversalAdditionalCameraData para URP
        Camera camComp = playerInstance.GetComponentInChildren<Camera>(true);
        Transform playerCam = camComp != null ? camComp.transform : playerInstance.transform;
        if (camComp != null && camComp.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() == null)
        {
            camComp.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        }

        GameObject toolObj = new GameObject("Tool");
        toolObj.transform.SetParent(playerCam);
        toolObj.transform.localPosition = new Vector3(0.2f, -0.2f, 0.5f);

        ObjectVacum vacuum = toolObj.AddComponent<ObjectVacum>();
        GrapleController graple = toolObj.AddComponent<GrapleController>();

        SetSerializedPrivate(playerController, "vacuum", vacuum);
        SetSerializedPrivate(playerController, "graple", graple);
        SetSerializedPrivate(playerController, "health", 3);

        // 8. Interfaz de Usuario (UI)
        GameObject canvasObj = CreateLevelUI(playerController, gameController);

        // 9. Construcción de Zonas del Nivel
        // --- ZONA 1: Santuario del Despertar (Inicio) ---
        GameObject island1 = CreateMeshObject("Island_Start", meshHexIsland, matRock, new Vector3(0, 0, 0), new Vector3(2.5f, 2.0f, 2.5f));
        AddConvexCollider(island1);

        // Cubo de energía para probar la aspiradora
        CreateEnergyCube(meshEnergyCube, matCyanGlow, new Vector3(3f, 1.2f, 3f));
        CreateEnergyCube(meshEnergyCube, matCyanGlow, new Vector3(-3f, 1.2f, 3f));

        // Estrella 1: En una plataforma elevada a la derecha
        GameObject starPillar1 = CreateMeshObject("Pillar_Star1", meshHexIsland, matRock, new Vector3(8f, 1.5f, 6f), new Vector3(0.5f, 1.2f, 0.5f));
        AddConvexCollider(starPillar1);
        CreateStar(meshStar, matStarGlow, new Vector3(8f, 4.2f, 6f));

        // --- ZONA 2: Plataformas Móviles y el Cañón de Peligros ---
        // Plataforma Móvil 1 que cruza el abismo hacia la Isla 2
        GameObject hoverPlat1 = CreateMeshObject("MovingPlat_1", meshHoverPlatform, matRock, new Vector3(0, 0.2f, 16f), new Vector3(1.2f, 1.0f, 1.2f), ProceduralModelView.ModelType.HoverPlatform);
        AddConvexCollider(hoverPlat1);
        MovingPlatform mp1 = hoverPlat1.AddComponent<MovingPlatform>();
        SetSerializedPrivate(mp1, "movementOffset", new Vector3(0, 0, 14f));
        SetSerializedPrivate(mp1, "speed", 3.2f);
        SetSerializedPrivate(mp1, "pauseDuration", 0.6f);

        // Isla 2: Peligros (Pinchos y Sierra Patrullando)
        GameObject island2 = CreateMeshObject("Island_Hazards", meshHexIsland, matRock, new Vector3(0, 0, 42f), new Vector3(2.6f, 2.0f, 2.6f));
        AddConvexCollider(island2);

        // Trampas de Pinchos en el camino
        GameObject spike1 = CreateMeshObject("SpikeTrap_A", meshSpikeBlock, matRedGlow, new Vector3(-1.8f, 0.1f, 38f), Vector3.one, ProceduralModelView.ModelType.SpikeTrap);
        BoxCollider sc1 = spike1.AddComponent<BoxCollider>();
        sc1.isTrigger = true;
        sc1.size = new Vector3(2f, 1.5f, 2f);
        sc1.center = new Vector3(0, 0.75f, 0);
        HazardObstacle ho1 = spike1.AddComponent<HazardObstacle>();
        SetSerializedPrivate(ho1, "instantKill", false);
        SetSerializedPrivate(ho1, "damageAmount", 1);

        GameObject spike2 = CreateMeshObject("SpikeTrap_B", meshSpikeBlock, matRedGlow, new Vector3(1.8f, 0.1f, 44f), Vector3.one, ProceduralModelView.ModelType.SpikeTrap);
        BoxCollider sc2 = spike2.AddComponent<BoxCollider>();
        sc2.isTrigger = true;
        sc2.size = new Vector3(2f, 1.5f, 2f);
        sc2.center = new Vector3(0, 0.75f, 0);
        HazardObstacle ho2 = spike2.AddComponent<HazardObstacle>();
        SetSerializedPrivate(ho2, "instantKill", false);
        SetSerializedPrivate(ho2, "damageAmount", 1);

        // Sierra Giratoria en riel transversal
        GameObject saw1 = CreateMeshObject("SawBlade_Patrol", meshSawBlade, matRedGlow, new Vector3(0, 1.0f, 41f), new Vector3(0.8f, 0.8f, 0.8f), ProceduralModelView.ModelType.SawBlade);
        SphereCollider sawCol = saw1.AddComponent<SphereCollider>();
        sawCol.isTrigger = true;
        sawCol.radius = 1.3f;
        RotatingHazard rotHazard = saw1.AddComponent<RotatingHazard>();
        SetSerializedPrivate(rotHazard, "rotationSpeed", 500f);
        SetSerializedPrivate(rotHazard, "enablePatrol", true);
        SetSerializedPrivate(rotHazard, "patrolOffset", new Vector3(4.5f, 0, 0));
        SetSerializedPrivate(rotHazard, "patrolSpeed", 3.0f);

        // Enemigo Dron 1 custodiando la zona
        CreateEnemyDrone(meshDrone, matDroneArmor, new Vector3(0, 2.0f, 48f));

        // Cubo de munición cerca
        CreateEnergyCube(meshEnergyCube, matCyanGlow, new Vector3(-3f, 0.8f, 45f));

        // Isla 3: Checkpoint 1
        GameObject island3 = CreateMeshObject("Island_Checkpoint1", meshHexIsland, matRock, new Vector3(0, 0, 60f), new Vector3(1.8f, 1.8f, 1.8f));
        AddConvexCollider(island3);
        CreateCheckpoint(meshCheckpoint, matCheckpointInactive, matCheckpointActive, new Vector3(0, 0.1f, 60f));

        // --- ZONA 3: El Gran Abismo del Gancho (Grapple Gorge) ---
        // Tres pilones en el abismo con layer Graple (Layer 8)
        int grappleLayer = LayerMask.NameToLayer("Graple");
        if (grappleLayer < 0) grappleLayer = 0;

        GameObject pylon1 = CreateMeshObject("GrapplePylon_1", meshGrapplePylon, matGold, new Vector3(4f, 2.0f, 75f), Vector3.one, ProceduralModelView.ModelType.GrapplePylon);
        pylon1.layer = grappleLayer;
        SphereCollider pc1 = pylon1.AddComponent<SphereCollider>();
        pc1.center = new Vector3(0, 4.7f, 0);
        pc1.radius = 2.0f;

        GameObject pylon2 = CreateMeshObject("GrapplePylon_2", meshGrapplePylon, matGold, new Vector3(-4f, 5.0f, 92f), Vector3.one, ProceduralModelView.ModelType.GrapplePylon);
        pylon2.layer = grappleLayer;
        SphereCollider pc2 = pylon2.AddComponent<SphereCollider>();
        pc2.center = new Vector3(0, 4.7f, 0);
        pc2.radius = 2.0f;

        GameObject pylon3 = CreateMeshObject("GrapplePylon_3", meshGrapplePylon, matGold, new Vector3(2f, 7.0f, 110f), Vector3.one, ProceduralModelView.ModelType.GrapplePylon);
        pylon3.layer = grappleLayer;
        SphereCollider pc3 = pylon3.AddComponent<SphereCollider>();
        pc3.center = new Vector3(0, 4.7f, 0);
        pc3.radius = 2.0f;

        // Estrella 2: Recompensa en pleno aire entre pylon 1 y pylon 2
        CreateStar(meshStar, matStarGlow, new Vector3(0f, 9.5f, 84f));

        // --- ZONA 4: El Bastión de los Láseres y Centinelas ---
        GameObject island4 = CreateMeshObject("Island_Fortress", meshHexIsland, matRock, new Vector3(0, 4.0f, 130f), new Vector3(2.8f, 2.2f, 2.8f));
        AddConvexCollider(island4);

        // Pilones Láser pulsantes
        CreateLaserHazard(meshLaserPillar, matRedGlow, new Vector3(-4f, 4.2f, 126f), new Vector3(4f, 4.2f, 126f));
        CreateLaserHazard(meshLaserPillar, matRedGlow, new Vector3(4f, 4.2f, 134f), new Vector3(-4f, 4.2f, 134f));

        // Dos Drones Centinelas en la fortaleza
        CreateEnemyDrone(meshDrone, matDroneArmor, new Vector3(-3f, 6.5f, 130f));
        CreateEnemyDrone(meshDrone, matDroneArmor, new Vector3(3f, 6.5f, 138f));

        // Cubos de energía para combate
        CreateEnergyCube(meshEnergyCube, matCyanGlow, new Vector3(-4f, 5.0f, 122f));
        CreateEnergyCube(meshEnergyCube, matCyanGlow, new Vector3(4f, 5.0f, 122f));

        // Checkpoint 2 antes de la ascensión
        CreateCheckpoint(meshCheckpoint, matCheckpointInactive, matCheckpointActive, new Vector3(0, 4.2f, 142f));

        // Estrella 3: En la torre de la fortaleza
        GameObject starTower = CreateMeshObject("Tower_Star3", meshHexIsland, matRock, new Vector3(7f, 4.0f, 136f), new Vector3(0.6f, 2.0f, 0.6f));
        AddConvexCollider(starTower);
        CreateStar(meshStar, matStarGlow, new Vector3(7f, 9.0f, 136f));

        // --- ZONA 5: La Gran Escalera Celestial y el Portal de Victoria ---
        for (int i = 0; i < 4; i++)
        {
            float stepZ = 152f + i * 8f;
            float stepY = 5.0f + i * 1.5f;
            GameObject stepPlat = CreateMeshObject("CelestialStep_" + i, meshHoverPlatform, matRock, new Vector3(0, stepY, stepZ), new Vector3(1.3f, 0.8f, 1.3f), ProceduralModelView.ModelType.HoverPlatform);
            AddConvexCollider(stepPlat);
        }

        // Gran Isla de Victoria
        GameObject islandVictory = CreateMeshObject("Island_Victory", meshHexIsland, matRock, new Vector3(0, 11.0f, 192f), new Vector3(3.0f, 2.5f, 3.0f));
        AddConvexCollider(islandVictory);

        // Gran Arco de Victoria con Disco de Teleportación
        GameObject victoryGate = CreateMeshObject("Victory_MonumentArch", meshVictoryArch, matGold, new Vector3(0, 11.2f, 196f), new Vector3(1.4f, 1.4f, 1.4f), ProceduralModelView.ModelType.VictoryArch);
        BoxCollider vc = victoryGate.AddComponent<BoxCollider>();
        vc.isTrigger = true;
        vc.size = new Vector3(4.5f, 7.0f, 3.0f);
        vc.center = new Vector3(0, 3.5f, 0);
        victoryGate.AddComponent<VictoryGoal>();

        // Luz mística del portal
        GameObject portalLightObj = new GameObject("PortalLight");
        portalLightObj.transform.SetParent(victoryGate.transform);
        portalLightObj.transform.localPosition = new Vector3(0, 3.5f, 0);
        Light pl = portalLightObj.AddComponent<Light>();
        pl.color = new Color(0.6f, 0.3f, 1.0f, 1f);
        pl.intensity = 3.5f;
        pl.range = 15f;

        // Guardar escena
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level1.unity");
        Debug.Log("¡Escena Assets/Scenes/Level1.unity guardada exitosamente!");

        // Añadir a Build Settings si no está
        EditorBuildSettingsScene[] originalScenes = EditorBuildSettings.scenes;
        bool exists = false;
        foreach (var s in originalScenes)
        {
            if (s.path == "Assets/Scenes/Level1.unity") { exists = true; break; }
        }
        if (!exists)
        {
            EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[originalScenes.Length + 1];
            for (int i = 0; i < originalScenes.Length; i++) newScenes[i] = originalScenes[i];
            newScenes[newScenes.Length - 1] = new EditorBuildSettingsScene("Assets/Scenes/Level1.unity", true);
            EditorBuildSettings.scenes = newScenes;
        }

        Debug.Log("--- ¡Nivel 1 Generado y Configurado al 100%! ---");
        EditorUtility.DisplayDialog("Nivel 1 Generado", "¡El Nivel 1 ha sido generado y configurado exitosamente!", "Aceptar");
    }

    private static Mesh GetNativeMesh(string filename, System.Func<Mesh> creator)
    {
        string assetPath = $"Assets/Art/Models/{filename}";
        Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (m != null) return m;

        // Fallback inmediato creando la malla en memoria y guardando el asset
        m = creator();
        AssetDatabase.CreateAsset(m, assetPath);
        AssetDatabase.SaveAssets();
        return m;
    }

    private static GameObject CreateMeshObject(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, ProceduralModelView.ModelType modelType = ProceduralModelView.ModelType.HexIsland)
    {
        GameObject go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = scale;
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        ProceduralModelView pmv = go.AddComponent<ProceduralModelView>();
        pmv.SelectedModelType = modelType;
        return go;
    }

    private static void AddConvexCollider(GameObject go)
    {
        MeshCollider mc = go.AddComponent<MeshCollider>();
        mc.convex = false; // Plataformas estáticas soportan non-convex mesh colliders perfectos
    }

    private static void CreateStar(Mesh starMesh, Material starMat, Vector3 pos)
    {
        GameObject star = CreateMeshObject("CollectibleStar", starMesh, starMat, pos, new Vector3(0.9f, 0.9f, 0.9f), ProceduralModelView.ModelType.CollectibleStar);
        SphereCollider sc = star.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 1.0f;
        star.AddComponent<CollectibleStar>();
        star.AddComponent<StarController>(); // Compatibilidad GameController existente

        // Luz propia de la estrella
        GameObject lightObj = new GameObject("StarGlowLight");
        lightObj.transform.SetParent(star.transform);
        lightObj.transform.localPosition = Vector3.zero;
        Light l = lightObj.AddComponent<Light>();
        l.color = new Color(1.0f, 0.85f, 0.2f, 1f);
        l.intensity = 2.0f;
        l.range = 6.0f;
    }

    private static void CreateCheckpoint(Mesh cpMesh, Material inactiveMat, Material activeMat, Vector3 pos)
    {
        GameObject cp = CreateMeshObject("CheckpointShrine", cpMesh, inactiveMat, pos, Vector3.one, ProceduralModelView.ModelType.CheckpointShrine);
        BoxCollider bc = cp.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(3.0f, 3.5f, 3.0f);
        bc.center = new Vector3(0, 1.5f, 0);

        EnhancedCheckpoint ec = cp.AddComponent<EnhancedCheckpoint>();
        cp.AddComponent<RespawnController>(); // Compatibilidad

        // Luz del checkpoint
        GameObject cpLightObj = new GameObject("CheckpointLight");
        cpLightObj.transform.SetParent(cp.transform);
        cpLightObj.transform.localPosition = new Vector3(0, 1.8f, 0);
        Light cpl = cpLightObj.AddComponent<Light>();
        cpl.color = new Color(0.9f, 0.6f, 0.1f, 1f);
        cpl.intensity = 1.5f;
        cpl.range = 8.0f;

        SetSerializedPrivate(ec, "activeMaterial", activeMat);
        SetSerializedPrivate(ec, "checkpointLight", cpl);
        SetSerializedPrivate(ec, "crystalRenderer", cp.GetComponent<MeshRenderer>());
    }

    private static void CreateEnergyCube(Mesh cubeMesh, Material mat, Vector3 pos)
    {
        GameObject cube = CreateMeshObject("EnergyCube_Poolable", cubeMesh, mat, pos, Vector3.one, ProceduralModelView.ModelType.EnergyCube);
        cube.tag = "Untagged";
        int poolLayer = LayerMask.NameToLayer("Poolable");
        if (poolLayer >= 0) cube.layer = poolLayer;

        BoxCollider bc = cube.AddComponent<BoxCollider>();
        bc.size = Vector3.one;

        Rigidbody rb = cube.AddComponent<Rigidbody>();
        rb.mass = 1.0f;

        cube.AddComponent<PoolableController>();
    }

    private static void CreateEnemyDrone(Mesh droneMesh, Material mat, Vector3 pos)
    {
        GameObject drone = CreateMeshObject("SentinelDrone", droneMesh, mat, pos, new Vector3(1.1f, 1.1f, 1.1f), ProceduralModelView.ModelType.SentinelDrone);
        SphereCollider col = drone.AddComponent<SphereCollider>();
        col.radius = 0.8f;

        drone.AddComponent<EnemyDroneController>();
    }

    private static void CreateLaserHazard(Mesh pillarMesh, Material redMat, Vector3 posA, Vector3 posB)
    {
        GameObject pylonA = CreateMeshObject("LaserPylon_A", pillarMesh, redMat, posA, Vector3.one, ProceduralModelView.ModelType.LaserPillar);
        pylonA.transform.LookAt(posB);
        BoxCollider ca = pylonA.AddComponent<BoxCollider>();
        ca.size = new Vector3(1f, 2.5f, 1f);
        ca.center = new Vector3(0, 1.25f, 0);

        GameObject pylonB = CreateMeshObject("LaserPylon_B", pillarMesh, redMat, posB, Vector3.one, ProceduralModelView.ModelType.LaserPillar);
        pylonB.transform.LookAt(posA);
        BoxCollider cb = pylonB.AddComponent<BoxCollider>();
        cb.size = new Vector3(1f, 2.5f, 1f);
        cb.center = new Vector3(0, 1.25f, 0);

        // Rayo Láser entre A y B
        Vector3 mid = (posA + posB) * 0.5f + Vector3.up * 1.5f;
        float dist = Vector3.Distance(posA, posB);
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "LaserBeam";
        beam.transform.position = mid;
        beam.transform.localScale = new Vector3(0.12f, dist * 0.5f, 0.12f);
        beam.transform.rotation = Quaternion.FromToRotation(Vector3.up, (posB - posA).normalized);
        beam.GetComponent<MeshRenderer>().sharedMaterial = redMat;

        CapsuleCollider cc = beam.GetComponent<CapsuleCollider>();
        cc.isTrigger = true;

        HazardObstacle ho = beam.AddComponent<HazardObstacle>();
        SetSerializedPrivate(ho, "instantKill", false);
        SetSerializedPrivate(ho, "damageAmount", 1);

        PulsingLaser pl = pylonA.AddComponent<PulsingLaser>();
        SetSerializedPrivate(pl, "laserBeamObject", beam);
        SetSerializedPrivate(pl, "laserDamageCollider", cc);
        SetSerializedPrivate(pl, "beamRenderer", beam.GetComponent<MeshRenderer>());
    }

    private static GameObject CreateLevelUI(PlayerController player, GameController gm)
    {
        GameObject canvasObj = new GameObject("Level_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject esObj = new GameObject("EventSystem");
        esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        LevelUIController uiController = canvasObj.AddComponent<LevelUIController>();
        SetSerializedPrivate(uiController, "playerController", player);

        // 1. HUD - Salud (3 Corazones en esquina superior izquierda)
        GameObject healthContainer = new GameObject("HealthContainer");
        healthContainer.transform.SetParent(canvasObj.transform, false);
        RectTransform hcRect = healthContainer.AddComponent<RectTransform>();
        hcRect.anchorMin = new Vector2(0, 1);
        hcRect.anchorMax = new Vector2(0, 1);
        hcRect.pivot = new Vector2(0, 1);
        hcRect.anchoredPosition = new Vector2(40, -40);
        hcRect.sizeDelta = new Vector2(200, 60);

        Image[] hearts = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject hObj = new GameObject("Heart_" + i);
            hObj.transform.SetParent(healthContainer.transform, false);
            RectTransform hr = hObj.AddComponent<RectTransform>();
            hr.anchoredPosition = new Vector2(i * 55, 0);
            hr.sizeDelta = new Vector2(45, 45);
            Image hImg = hObj.AddComponent<Image>();
            hImg.color = new Color(1f, 0.25f, 0.25f, 1f);
            hearts[i] = hImg;
        }
        SetSerializedPrivate(uiController, "healthHearts", hearts);

        // 2. HUD - Estrellas (Esquina superior derecha)
        GameObject starObj = new GameObject("StarHUD");
        starObj.transform.SetParent(canvasObj.transform, false);
        RectTransform stRect = starObj.AddComponent<RectTransform>();
        stRect.anchorMin = new Vector2(1, 1);
        stRect.anchorMax = new Vector2(1, 1);
        stRect.pivot = new Vector2(1, 1);
        stRect.anchoredPosition = new Vector2(-40, -40);
        stRect.sizeDelta = new Vector2(220, 60);

        TextMeshProUGUI starText = starObj.AddComponent<TextMeshProUGUI>();
        starText.fontSize = 34;
        starText.alignment = TextAlignmentOptions.Right;
        starText.color = new Color(1f, 0.85f, 0.2f, 1f);
        starText.text = "★ 0 / 3";
        SetSerializedPrivate(uiController, "starCounterText", starText);

        // 3. HUD - Herramienta (Esquina inferior izquierda)
        GameObject toolObj = new GameObject("ToolHUD");
        toolObj.transform.SetParent(canvasObj.transform, false);
        RectTransform tRect = toolObj.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0, 0);
        tRect.anchorMax = new Vector2(0, 0);
        tRect.pivot = new Vector2(0, 0);
        tRect.anchoredPosition = new Vector2(40, 40);
        tRect.sizeDelta = new Vector2(500, 80);

        TextMeshProUGUI toolText = toolObj.AddComponent<TextMeshProUGUI>();
        toolText.fontSize = 26;
        toolText.color = Color.white;
        toolText.text = "HERRAMIENTA: GANCHO\n[Q] Cambiar | Clic: Usar";
        SetSerializedPrivate(uiController, "currentToolText", toolText);

        // 4. Retícula (Centro)
        GameObject crosshairObj = new GameObject("Crosshair");
        crosshairObj.transform.SetParent(canvasObj.transform, false);
        RectTransform cRect = crosshairObj.AddComponent<RectTransform>();
        cRect.anchoredPosition = Vector2.zero;
        cRect.sizeDelta = new Vector2(10, 10);
        Image cImg = crosshairObj.AddComponent<Image>();
        cImg.color = new Color(1f, 1f, 1f, 0.85f);

        // 5. Flash de daño
        GameObject flashObj = new GameObject("DamageFlash");
        flashObj.transform.SetParent(canvasObj.transform, false);
        RectTransform fRect = flashObj.AddComponent<RectTransform>();
        fRect.anchorMin = Vector2.zero;
        fRect.anchorMax = Vector2.one;
        fRect.sizeDelta = Vector2.zero;
        Image fImg = flashObj.AddComponent<Image>();
        fImg.color = new Color(1f, 0f, 0f, 0.4f);
        CanvasGroup fGroup = flashObj.AddComponent<CanvasGroup>();
        fGroup.alpha = 0f;
        SetSerializedPrivate(uiController, "damageFlashGroup", fGroup);

        // 6. Notificaciones Banner
        GameObject notifObj = new GameObject("NotificationBanner");
        notifObj.transform.SetParent(canvasObj.transform, false);
        RectTransform nRect = notifObj.AddComponent<RectTransform>();
        nRect.anchorMin = new Vector2(0.5f, 0.85f);
        nRect.anchorMax = new Vector2(0.5f, 0.85f);
        nRect.sizeDelta = new Vector2(600, 50);
        TextMeshProUGUI nText = notifObj.AddComponent<TextMeshProUGUI>();
        nText.fontSize = 28;
        nText.alignment = TextAlignmentOptions.Center;
        nText.color = new Color(0.2f, 1f, 0.6f);
        CanvasGroup nGroup = notifObj.AddComponent<CanvasGroup>();
        nGroup.alpha = 0f;
        SetSerializedPrivate(uiController, "notificationText", nText);
        SetSerializedPrivate(uiController, "notificationGroup", nGroup);

        // 7. Pantalla de Victoria
        GameObject vicPanel = new GameObject("VictoryPanel");
        vicPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform vpRect = vicPanel.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;
        Image vpBg = vicPanel.AddComponent<Image>();
        vpBg.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);

        GameObject vicTitle = new GameObject("VictoryTitle");
        vicTitle.transform.SetParent(vicPanel.transform, false);
        RectTransform vtRect = vicTitle.AddComponent<RectTransform>();
        vtRect.anchoredPosition = new Vector2(0, 140);
        vtRect.sizeDelta = new Vector2(800, 100);
        TextMeshProUGUI vtText = vicTitle.AddComponent<TextMeshProUGUI>();
        vtText.fontSize = 58;
        vtText.alignment = TextAlignmentOptions.Center;
        vtText.color = new Color(1.0f, 0.85f, 0.2f);
        vtText.text = "¡NIVEL 1 COMPLETADO!";

        GameObject vicStars = new GameObject("VictoryStars");
        vicStars.transform.SetParent(vicPanel.transform, false);
        RectTransform vsRect = vicStars.AddComponent<RectTransform>();
        vsRect.anchoredPosition = new Vector2(0, 50);
        vsRect.sizeDelta = new Vector2(600, 60);
        TextMeshProUGUI vsText = vicStars.AddComponent<TextMeshProUGUI>();
        vsText.fontSize = 34;
        vsText.alignment = TextAlignmentOptions.Center;
        vsText.color = Color.white;
        vsText.text = "Estrellas: 3 / 3";
        SetSerializedPrivate(uiController, "victoryStarsText", vsText);

        GameObject vicTime = new GameObject("VictoryTime");
        vicTime.transform.SetParent(vicPanel.transform, false);
        RectTransform vtmRect = vicTime.AddComponent<RectTransform>();
        vtmRect.anchoredPosition = new Vector2(0, -10);
        vtmRect.sizeDelta = new Vector2(600, 60);
        TextMeshProUGUI vtmText = vicTime.AddComponent<TextMeshProUGUI>();
        vtmText.fontSize = 28;
        vtmText.alignment = TextAlignmentOptions.Center;
        vtmText.color = new Color(0.8f, 0.8f, 0.8f);
        vtmText.text = "Tiempo: 01:25";
        SetSerializedPrivate(uiController, "victoryTimeText", vtmText);

        GameObject btnRestartObj = new GameObject("BtnRestart");
        btnRestartObj.transform.SetParent(vicPanel.transform, false);
        RectTransform brRect = btnRestartObj.AddComponent<RectTransform>();
        brRect.anchoredPosition = new Vector2(0, -110);
        brRect.sizeDelta = new Vector2(260, 65);
        Image brImg = btnRestartObj.AddComponent<Image>();
        brImg.color = new Color(0.2f, 0.7f, 0.3f);
        Button brBtn = btnRestartObj.AddComponent<Button>();

        GameObject brTextObj = new GameObject("BtnText");
        brTextObj.transform.SetParent(btnRestartObj.transform, false);
        RectTransform brtRect = brTextObj.AddComponent<RectTransform>();
        brtRect.sizeDelta = new Vector2(260, 65);
        TextMeshProUGUI brtText = brTextObj.AddComponent<TextMeshProUGUI>();
        brtText.fontSize = 26;
        brtText.alignment = TextAlignmentOptions.Center;
        brtText.color = Color.white;
        brtText.text = "Jugar de Nuevo";

        SetSerializedPrivate(uiController, "victoryPanel", vicPanel);
        SetSerializedPrivate(uiController, "victoryRestartButton", brBtn);
        vicPanel.SetActive(false);

        // 8. Pantalla de Pausa
        GameObject pausePanel = new GameObject("PausePanel");
        pausePanel.transform.SetParent(canvasObj.transform, false);
        RectTransform ppRect = pausePanel.AddComponent<RectTransform>();
        ppRect.anchorMin = Vector2.zero;
        ppRect.anchorMax = Vector2.one;
        ppRect.sizeDelta = Vector2.zero;
        Image ppBg = pausePanel.AddComponent<Image>();
        ppBg.color = new Color(0.05f, 0.05f, 0.08f, 0.88f);

        GameObject pTitle = new GameObject("PauseTitle");
        pTitle.transform.SetParent(pausePanel.transform, false);
        RectTransform ptRect = pTitle.AddComponent<RectTransform>();
        ptRect.anchoredPosition = new Vector2(0, 100);
        ptRect.sizeDelta = new Vector2(600, 80);
        TextMeshProUGUI ptText = pTitle.AddComponent<TextMeshProUGUI>();
        ptText.fontSize = 50;
        ptText.alignment = TextAlignmentOptions.Center;
        ptText.color = Color.white;
        ptText.text = "JUEGO EN PAUSA";

        GameObject btnResume = new GameObject("BtnResume");
        btnResume.transform.SetParent(pausePanel.transform, false);
        RectTransform bresRect = btnResume.AddComponent<RectTransform>();
        bresRect.anchoredPosition = new Vector2(0, 0);
        bresRect.sizeDelta = new Vector2(240, 55);
        Image bresImg = btnResume.AddComponent<Image>();
        bresImg.color = new Color(0.2f, 0.5f, 0.8f);
        Button bresBtn = btnResume.AddComponent<Button>();

        GameObject bresTextObj = new GameObject("Text");
        bresTextObj.transform.SetParent(btnResume.transform, false);
        RectTransform bresTmpRect = bresTextObj.AddComponent<RectTransform>();
        bresTmpRect.sizeDelta = new Vector2(240, 55);
        TextMeshProUGUI bresTmpText = bresTextObj.AddComponent<TextMeshProUGUI>();
        bresTmpText.fontSize = 24;
        bresTmpText.alignment = TextAlignmentOptions.Center;
        bresTmpText.color = Color.white;
        bresTmpText.text = "Continuar";

        SetSerializedPrivate(uiController, "pausePanel", pausePanel);
        SetSerializedPrivate(uiController, "resumeButton", bresBtn);
        pausePanel.SetActive(false);

        return canvasObj;
    }

    private static void SetSerializedPrivate(object target, string propertyName, object value)
    {
        SerializedObject so = new SerializedObject(target as UnityEngine.Object);
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
        {
            if (value is bool b) prop.boolValue = b;
            else if (value is int i) prop.intValue = i;
            else if (value is float f) prop.floatValue = f;
            else if (value is string s) prop.stringValue = s;
            else if (value is Vector3 v) prop.vector3Value = v;
            else if (value is UnityEngine.Object obj) prop.objectReferenceValue = obj;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static Material GetMaterial(string path, Color fallbackColor, bool emissive = false)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(shader);
        m.color = fallbackColor;
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", fallbackColor * 1.5f);
        }
        return m;
    }
}
