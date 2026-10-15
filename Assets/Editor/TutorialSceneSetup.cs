using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using TMPro;

public static class TutorialSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Levels/Tutorial.unity";
    private const string TutorialMaterialsPath = "Assets/Materials/Tutorial";
    private const string TestingMaterialsPath = "Assets/Materials/Testing";
    private const string PrefabsPath = "Assets/Prefabs";
    private const string TMPFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string PrefKey = "TutorialSetup_RunOnce_v5";

    [InitializeOnLoadMethod]
    private static void AutoRunOnce()
    {
        if (!EditorPrefs.GetBool(PrefKey, false))
        {
            EditorPrefs.SetBool(PrefKey, true);
            EditorApplication.delayCall += SetupTutorialScene;
        }
    }

    [MenuItem("Tools/Setup Tutorial Level")]
    public static void SetupTutorialScene()
    {
        Debug.Log("[TutorialSetup] Abriendo escena de tutorial: " + ScenePath);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 1. Limpieza de Cámara standalone (el prefab del Jugador ya tiene su propia MainCamera y AudioListener)
        GameObject standaloneCam = GameObject.Find("Main Camera");
        if (standaloneCam != null && standaloneCam.transform.parent == null)
        {
            Debug.Log("[TutorialSetup] Eliminando cámara standalone duplicada...");
            Object.DestroyImmediate(standaloneCam);
        }

        // 2. Limpieza de pared bloqueadora PolyShape (8)
        GameObject polyShape8 = GameObject.Find("PolyShape (8)");
        if (polyShape8 != null)
        {
            Debug.Log("[TutorialSetup] Desactivando PolyShape (8) para abrir el paso...");
            polyShape8.SetActive(false);
        }

        // 3. Cargar Materiales
        Material matFloor = AssetDatabase.LoadAssetAtPath<Material>($"{TutorialMaterialsPath}/Tutorial_Floor.mat");
        Material matWall = AssetDatabase.LoadAssetAtPath<Material>($"{TutorialMaterialsPath}/Tutorial_Wall.mat");
        Material matLava = AssetDatabase.LoadAssetAtPath<Material>($"{TutorialMaterialsPath}/Tutorial_Lava.mat");
        Material matTrim = AssetDatabase.LoadAssetAtPath<Material>($"{TutorialMaterialsPath}/Tutorial_Trim.mat");
        Material matPortal = AssetDatabase.LoadAssetAtPath<Material>($"{TutorialMaterialsPath}/Tutorial_Portal.mat");
        Material matGrapple = AssetDatabase.LoadAssetAtPath<Material>($"{TutorialMaterialsPath}/Tutorial_Grapple.mat");
        Material matCollectible = AssetDatabase.LoadAssetAtPath<Material>($"{TutorialMaterialsPath}/Tutorial_Collectible.mat")
                               ?? AssetDatabase.LoadAssetAtPath<Material>($"{TestingMaterialsPath}/Star.mat");
        Material matObjects = AssetDatabase.LoadAssetAtPath<Material>($"{TestingMaterialsPath}/Objects.mat");
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TMPFontPath);

        // 4. Aplicar Materiales a la Maqueta Existente
        GameObject planeObj = GameObject.Find("Plane");
        if (planeObj != null && matFloor != null)
        {
            var rend = planeObj.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = matFloor;
        }

        foreach (var rend in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            string goName = rend.gameObject.name;
            if (goName.StartsWith("PolyShape") && matWall != null)
            {
                rend.sharedMaterial = matWall;
            }
            else if (goName == "Stairs" && matTrim != null)
            {
                rend.sharedMaterial = matTrim;
            }
            else if (goName == "Door" && matPortal != null)
            {
                rend.sharedMaterial = matPortal;
            }
        }

        // 5. Configurar Peligros de Lava (incluyendo la lava ardiente y letal del pasillo final)
        SetupLavaHazards(matLava);
        SetupKillZone();

        // 6. Plataformas seguras sobre la lava del pasillo final
        SetupHallwayPlatforms(matFloor, matTrim);

        // 7. Obstáculos tácticos (cobertura, saltos y barricadas)
        SetupObstacles(matTrim, matWall);

        // 8. Configurar Puerta Final (TutorialDoor)
        SetupDoorGoal(matPortal);

        // 9. Crear / Configurar GameController y UI Canvas de Alta Calidad
        GameController gameController = SetupGameControllerAndUI(fontAsset);

        // 10. Crear Checkpoints invisibles que abarcan una gran área
        SetupCheckpoints();

        // 11. Crear Zonas de Instrucción / Tutorial Prompts Dinámicos
        SetupTutorialPrompts();

        // 12. Crear Estrellas Coleccionables con Material HDR Amarillo
        SetupStars(matCollectible);

        // 13. Crear Objetos Absorbibles / Lanzables (Poolable / ThrowableDamage)
        SetupPoolableCrates(matObjects);

        // 14. Crear Anclajes de Gancho (Grapple Anchors)
        SetupGrappleAnchors(matGrapple);

        // 15. Spawnear Enemigos Actualizados con Modelos de Kris (WinRar letal, FlyingEmail, Hexbug y Troyano)
        SetupEnemies();

        // 16. Ajustar Jugador y Spawn inicial
        SetupPlayer(gameController);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[TutorialSetup] ¡El tutorial se ha configurado exitosamente y está listo para jugarse de principio a fin!");
    }

    private static void SetupLavaHazards(Material matLava)
    {
        int count = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            // Foso de lava del pasillo final (GO 434825407 / Floor_Courtyard)
            if (go.name == "Floor_Courtyard" || (go.name == "Lava" && go.transform.position.x > 220f))
            {
                go.name = "Lava_FinalHallway";
                go.tag = "Lava";

                if (matLava != null)
                {
                    var rend = go.GetComponent<Renderer>();
                    if (rend != null) rend.sharedMaterial = matLava;
                }

                BoxCollider box = go.GetComponent<BoxCollider>();
                if (box == null) box = go.AddComponent<BoxCollider>();
                MeshCollider meshCol = go.GetComponent<MeshCollider>();
                if (meshCol != null) Object.DestroyImmediate(meshCol);

                box.isTrigger = true;
                box.size = new Vector3(56f, 1.5f, 200f);
                box.center = new Vector3(0, 0, 0);

                Rigidbody rb = go.GetComponent<Rigidbody>();
                if (rb == null) rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                LavaHazard haz = go.GetComponent<LavaHazard>();
                if (haz == null) haz = go.AddComponent<LavaHazard>();

                // Luces de calor ardiente en el pasillo final
                CreateOrUpdateLavaGlow(go, "LavaGlow_1", new Vector3(0, 1.2f, -40f), 20f, 3.5f);
                CreateOrUpdateLavaGlow(go, "LavaGlow_2", new Vector3(0, 1.2f, 0f), 20f, 3.5f);
                CreateOrUpdateLavaGlow(go, "LavaGlow_3", new Vector3(0, 1.2f, 40f), 20f, 3.5f);

                count++;
                continue;
            }

            // Fosas de lava del cañón
            if (go.name == "Lava" || go.name.StartsWith("Lava "))
            {
                count++;
                go.tag = "Lava";

                if (matLava != null)
                {
                    var rend = go.GetComponent<Renderer>();
                    if (rend != null) rend.sharedMaterial = matLava;
                }

                BoxCollider box = go.GetComponent<BoxCollider>();
                if (box == null)
                {
                    MeshCollider meshCol = go.GetComponent<MeshCollider>();
                    Bounds b = meshCol != null ? meshCol.bounds : go.GetComponent<Renderer>().bounds;
                    if (meshCol != null) Object.DestroyImmediate(meshCol);

                    box = go.AddComponent<BoxCollider>();
                    box.size = new Vector3(Mathf.Abs(b.size.x), 1.2f, Mathf.Abs(b.size.z));
                    box.center = Vector3.zero;
                }
                else
                {
                    box.size = new Vector3(Mathf.Abs(box.size.x), 1.2f, Mathf.Abs(box.size.z));
                    box.center = Vector3.zero;
                }
                box.isTrigger = true;

                Rigidbody rb = go.GetComponent<Rigidbody>();
                if (rb == null) rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                if (go.GetComponent<LavaHazard>() == null)
                {
                    go.AddComponent<LavaHazard>();
                }

                CreateOrUpdateLavaGlow(go, "LavaGlow", new Vector3(0, 0.8f, 0), 16f, 2.8f);
            }
        }
        Debug.Log($"[TutorialSetup] {count} fosas de lava activas y letales configuradas.");
    }

    private static void CreateOrUpdateLavaGlow(GameObject parent, string name, Vector3 localPos, float range, float intensity)
    {
        Transform t = parent.transform.Find(name);
        GameObject lightObj = t != null ? t.gameObject : new GameObject(name);
        lightObj.transform.SetParent(parent.transform, false);
        lightObj.transform.localPosition = localPos;

        Light light = lightObj.GetComponent<Light>();
        if (light == null) light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1.0f, 0.35f, 0.04f);
        light.range = range;
        light.intensity = intensity;
    }

    private static void SetupHallwayPlatforms(Material matFloor, Material matTrim)
    {
        GameObject holder = GetOrCreateHolder("--- HALLWAY_PLATFORMS ---");

        var platforms = new (string name, Vector3 pos, Vector3 size)[]
        {
            ("Platform_Hallway_1", new Vector3(243.0f, 0.3f, -70.0f), new Vector3(7.0f, 0.8f, 7.0f)),
            ("Platform_Hallway_2", new Vector3(235.0f, 0.4f, -40.0f), new Vector3(7.5f, 0.8f, 7.5f)),
            ("Platform_Hallway_3", new Vector3(252.0f, 0.5f, -15.0f), new Vector3(8.0f, 0.8f, 8.0f)),
            ("Platform_Hallway_4", new Vector3(240.0f, 0.4f, 10.0f), new Vector3(7.5f, 0.8f, 7.5f)),
            ("Platform_Hallway_5", new Vector3(250.0f, 0.5f, 35.0f), new Vector3(7.5f, 0.8f, 7.5f)),
            ("Platform_Arena", new Vector3(243.2f, 0.3f, 65.0f), new Vector3(14.0f, 0.8f, 18.0f))
        };

        foreach (var p in platforms)
        {
            GameObject plat = GameObject.Find(p.name);
            if (plat == null)
            {
                plat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plat.name = p.name;
                plat.transform.SetParent(holder.transform, false);
            }

            plat.transform.position = p.pos;
            plat.transform.localScale = p.size;
            plat.tag = "Untagged";

            var rend = plat.GetComponent<Renderer>();
            if (rend != null && matTrim != null) rend.sharedMaterial = matTrim;

            var col = plat.GetComponent<BoxCollider>();
            if (col != null) col.isTrigger = false;
        }
    }

    private static void SetupObstacles(Material matTrim, Material matWall)
    {
        GameObject holder = GetOrCreateHolder("--- OBSTACLES ---");

        var obstacles = new (string name, Vector3 pos, Vector3 size)[]
        {
            // Pasillo inicial: barrera de salto y bloque de cobertura
            ("Obstacle_LowWall", new Vector3(-11.7f, 0.55f, 2.0f), new Vector3(8.0f, 1.1f, 1.2f)),
            ("Obstacle_CoverBlock", new Vector3(-8.8f, 1.25f, -12.0f), new Vector3(2.5f, 2.5f, 2.5f)),

            // Sala 2 (Armamento / Aspiradora): pilares y barricadas
            ("Obstacle_Sala2_Pillar1", new Vector3(16.0f, 2.0f, -35.0f), new Vector3(2.2f, 4.0f, 2.2f)),
            ("Obstacle_Sala2_Pillar2", new Vector3(21.0f, 2.0f, -58.0f), new Vector3(2.2f, 4.0f, 2.2f)),
            ("Obstacle_Sala2_Barrier", new Vector3(28.0f, 0.9f, -48.0f), new Vector3(1.2f, 1.8f, 5.0f)),

            // Cañón de lava: pilares de soporte
            ("Obstacle_Lava_Pillar1", new Vector3(115.0f, 2.5f, -114.0f), new Vector3(2.4f, 5.0f, 2.4f)),
            ("Obstacle_Lava_Pillar2", new Vector3(155.0f, 2.5f, -114.0f), new Vector3(2.4f, 5.0f, 2.4f)),

            // Pasillo final de lava: pilares y muros para cubrirse de los disparos aéreos
            ("Obstacle_Hallway_Pillar1", new Vector3(246.0f, 2.2f, -50.0f), new Vector3(2.6f, 4.4f, 2.6f)),
            ("Obstacle_Hallway_Pillar2", new Vector3(238.0f, 2.2f, -25.0f), new Vector3(2.6f, 4.4f, 2.6f)),
            ("Obstacle_Hallway_Pillar3", new Vector3(248.0f, 2.2f, 20.0f), new Vector3(2.6f, 4.4f, 2.6f)),
            ("Obstacle_Arena_Barrier", new Vector3(237.0f, 1.3f, 58.0f), new Vector3(1.4f, 2.6f, 6.0f))
        };

        foreach (var obs in obstacles)
        {
            GameObject obj = GameObject.Find(obs.name);
            if (obj == null)
            {
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = obs.name;
                obj.transform.SetParent(holder.transform, false);
            }

            obj.transform.position = obs.pos;
            obj.transform.localScale = obs.size;
            obj.tag = "Untagged";

            var rend = obj.GetComponent<Renderer>();
            if (rend != null && matTrim != null) rend.sharedMaterial = matTrim;

            var col = obj.GetComponent<BoxCollider>();
            if (col != null) col.isTrigger = false;
        }
    }

    private static void SetupKillZone()
    {
        GameObject existing = GameObject.Find("KillZone_Abyss");
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject killZone = new GameObject("KillZone_Abyss");
        killZone.tag = "Lava";
        killZone.transform.position = new Vector3(100f, -15f, 0f);

        BoxCollider box = killZone.AddComponent<BoxCollider>();
        box.size = new Vector3(1200f, 4f, 1200f);
        box.isTrigger = true;

        Rigidbody rb = killZone.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        killZone.AddComponent<LavaHazard>();
        Debug.Log("[TutorialSetup] KillZone de abismo creada a Y = -15.");
    }

    private static void SetupDoorGoal(Material matPortal)
    {
        GameObject doorObj = GameObject.Find("Door");
        if (doorObj != null)
        {
            if (matPortal != null)
            {
                var rend = doorObj.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = matPortal;
            }

            MeshCollider mc = doorObj.GetComponent<MeshCollider>();
            if (mc != null) Object.DestroyImmediate(mc);

            BoxCollider bc = doorObj.GetComponent<BoxCollider>();
            if (bc == null) bc = doorObj.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(6f, 6f, 6f);
            bc.center = Vector3.zero;

            Rigidbody rb = doorObj.GetComponent<Rigidbody>();
            if (rb == null) rb = doorObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            TutorialDoor td = doorObj.GetComponent<TutorialDoor>();
            if (td == null) td = doorObj.AddComponent<TutorialDoor>();

            Light dl = doorObj.GetComponentInChildren<Light>();
            if (dl == null)
            {
                GameObject lightObj = new GameObject("DoorLight");
                lightObj.transform.SetParent(doorObj.transform, false);
                dl = lightObj.AddComponent<Light>();
                dl.type = LightType.Point;
                dl.color = new Color(0.2f, 1f, 0.4f);
                dl.range = 15f;
                dl.intensity = 5f;
            }
        }
    }

    private static GameController SetupGameControllerAndUI(TMP_FontAsset fontAsset)
    {
        GameObject gcObj = GameObject.Find("GameController");
        if (gcObj == null) gcObj = new GameObject("GameController");

        GameController gc = gcObj.GetComponent<GameController>();
        if (gc == null) gc = gcObj.AddComponent<GameController>();

        GameObject canvasObj = GameObject.Find("TutorialCanvas");
        if (canvasObj == null) canvasObj = new GameObject("TutorialCanvas");

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            canvasObj.AddComponent<GraphicRaycaster>();

        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }

        Transform uiRoot = canvasObj.transform;

        // Limpiar elementos dinámicos previos
        for (int i = uiRoot.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(uiRoot.GetChild(i).gameObject);
        }

        // 1. Efecto Hurt Flash
        GameObject hurtObj = new GameObject("HurtOverlay");
        hurtObj.transform.SetParent(uiRoot, false);
        RectTransform rtHurt = hurtObj.AddComponent<RectTransform>();
        rtHurt.anchorMin = Vector2.zero;
        rtHurt.anchorMax = Vector2.one;
        rtHurt.sizeDelta = Vector2.zero;
        Image hurtImg = hurtObj.AddComponent<Image>();
        hurtImg.color = new Color(1f, 0f, 0f, 0f);
        hurtImg.raycastTarget = false;

        // 2. HUD Superior Izquierdo: Estado del Jugador
        GameObject statsCard = CreateCard(uiRoot, "Card_PlayerStats",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(25, -25), new Vector2(400, 140),
            new Color(0.06f, 0.08f, 0.12f, 0.88f), new Color(0.0f, 0.9f, 1.0f));

        TextMeshProUGUI healthText = CreateText(statsCard.transform, "HealthText",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(20, -22), new Vector2(-40, 32),
            "SALUD: <color=#FF3344>3 / 3 HP</color>", 24, TextAlignmentOptions.Left, fontAsset);

        TextMeshProUGUI toolText = CreateText(statsCard.transform, "ToolText",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(20, -60), new Vector2(-40, 28),
            "HERRAMIENTA: <color=#00E5FF>GANCHO [X / Clic]</color>  |  [Q] Cambiar", 18, TextAlignmentOptions.Left, fontAsset);

        TextMeshProUGUI scoreText = CreateText(statsCard.transform, "ScoreText",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(20, -96), new Vector2(-40, 28),
            "ESTRELLAS: <color=#FFE600>0 / 3</color>", 20, TextAlignmentOptions.Left, fontAsset);

        // 3. Guía Dinámica del Tutorial (Superior Centro/Derecha)
        GameObject guideCard = CreateCard(uiRoot, "Card_TutorialGuide",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-25, -25), new Vector2(580, 205),
            new Color(0.05f, 0.07f, 0.11f, 0.92f), new Color(1.0f, 0.75f, 0.0f));

        TextMeshProUGUI phaseText = CreateText(guideCard.transform, "PhaseBadge",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(20, -18), new Vector2(-40, 24),
            "FASE 1 / 4 · FUNDAMENTOS", 16, TextAlignmentOptions.Left, fontAsset);
        phaseText.color = new Color(1.0f, 0.75f, 0.1f);

        TextMeshProUGUI titleText = CreateText(guideCard.transform, "TitleText",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(20, -44), new Vector2(-40, 30),
            "1. EXPLORACIÓN Y MOVIMIENTO", 22, TextAlignmentOptions.Left, fontAsset);

        TextMeshProUGUI descText = CreateText(guideCard.transform, "DescText",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(20, -78), new Vector2(-40, 42),
            "Avanza por el pasillo inicial, aprende a moverte con fluidez y encuentra la primera estrella dorada.", 17, TextAlignmentOptions.Left, fontAsset);
        descText.color = new Color(0.85f, 0.88f, 0.92f);

        TextMeshProUGUI controlsText = CreateText(guideCard.transform, "ControlsText",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(20, -126), new Vector2(-40, 28),
            "<color=#FFE600><b>[ W A S D ]</b></color> Moverse   <color=#FFE600><b>[ ESPACIO ]</b></color> Saltar   <color=#FFE600><b>[ SHIFT ]</b></color> Correr",
            17, TextAlignmentOptions.Left, fontAsset);

        TextMeshProUGUI taskStatusText = CreateText(guideCard.transform, "TaskStatusText",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(20, -162), new Vector2(-40, 28),
            ">> Encuentra la 1ra Estrella dorada.", 18, TextAlignmentOptions.Left, fontAsset);
        taskStatusText.color = new Color(0.2f, 1f, 0.5f);

        // 4. Notificación Toast de Checkpoint (Centro Superior)
        GameObject cpToast = CreateCard(uiRoot, "Card_CheckpointToast",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -30), new Vector2(500, 60),
            new Color(0.04f, 0.12f, 0.08f, 0.92f), new Color(0.2f, 1f, 0.4f));

        TextMeshProUGUI checkpointText = CreateText(cpToast.transform, "CheckpointText",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            "¡PUNTO DE CONTROL GUARDADO!", 20, TextAlignmentOptions.Center, fontAsset);
        checkpointText.color = new Color(0.4f, 1f, 0.6f);
        cpToast.SetActive(false);

        // 5. Overlay de Muerte
        GameObject deathObj = CreateCard(uiRoot, "Card_DeathOverlay",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(650, 180),
            new Color(0.15f, 0.02f, 0.02f, 0.95f), new Color(1f, 0.1f, 0.1f));

        CreateText(deathObj.transform, "DeathTitle",
            new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(600, 50),
            "SISTEMA CRÍTICO - REAPARECIENDO...", 32, TextAlignmentOptions.Center, fontAsset).color = new Color(1f, 0.2f, 0.2f);

        CreateText(deathObj.transform, "DeathSub",
            new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(600, 40),
            "Reconstruyendo chasis en el último punto de control seguro.", 20, TextAlignmentOptions.Center, fontAsset);

        deathObj.SetActive(false);

        // 6. Panel de Victoria
        GameObject winPanel = CreateCard(uiRoot, "Card_WinPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(800, 380),
            new Color(0.04f, 0.08f, 0.12f, 0.96f), new Color(0.2f, 1f, 0.4f));

        CreateText(winPanel.transform, "WinTitle",
            new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(720, 60),
            "¡TUTORIAL COMPLETADO CON ÉXITO!", 36, TextAlignmentOptions.Center, fontAsset).color = new Color(0.2f, 1f, 0.4f);

        TextMeshProUGUI winSummary = CreateText(winPanel.transform, "WinSummary",
            new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 200),
            "Estrellas recolectadas: 3/3\n\n¡Has dominado el movimiento, la aspiradora, el combate y el gancho!\n\nPresiona [R] para reiniciar el tutorial.",
            22, TextAlignmentOptions.Center, fontAsset);

        winPanel.SetActive(false);

        // Serializar referencias en GameController
        SerializedObject soGc = new SerializedObject(gc);
        soGc.FindProperty("scoreText").objectReferenceValue = scoreText;
        soGc.FindProperty("healthText").objectReferenceValue = healthText;
        soGc.FindProperty("toolText").objectReferenceValue = toolText;
        soGc.FindProperty("checkpointNotificationText").objectReferenceValue = checkpointText;
        soGc.FindProperty("hurtOverlay").objectReferenceValue = hurtImg;
        soGc.FindProperty("deathOverlay").objectReferenceValue = deathObj;

        soGc.FindProperty("guidePanel").objectReferenceValue = guideCard;
        soGc.FindProperty("phaseBadgeText").objectReferenceValue = phaseText;
        soGc.FindProperty("zoneTitleText").objectReferenceValue = titleText;
        soGc.FindProperty("descriptionText").objectReferenceValue = descText;
        soGc.FindProperty("controlsText").objectReferenceValue = controlsText;
        soGc.FindProperty("taskText").objectReferenceValue = taskStatusText;

        soGc.FindProperty("winPanel").objectReferenceValue = winPanel;
        soGc.FindProperty("winSummaryText").objectReferenceValue = winSummary;
        soGc.FindProperty("spawnPoint").vector3Value = new Vector3(-11.7f, 1.0f, 21.0f);
        soGc.ApplyModifiedPropertiesWithoutUndo();

        return gc;
    }

    private static GameObject CreateCard(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size,
        Color bgColor, Color accentColor)
    {
        GameObject card = new GameObject(name);
        card.transform.SetParent(parent, false);
        RectTransform rt = card.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image bg = card.AddComponent<Image>();
        bg.color = bgColor;

        GameObject accent = new GameObject("AccentLine");
        accent.transform.SetParent(card.transform, false);
        RectTransform rtAcc = accent.AddComponent<RectTransform>();
        rtAcc.anchorMin = new Vector2(0, 1);
        rtAcc.anchorMax = new Vector2(1, 1);
        rtAcc.pivot = new Vector2(0.5f, 1);
        rtAcc.anchoredPosition = Vector2.zero;
        rtAcc.sizeDelta = new Vector2(0, 3.5f);
        Image accImg = accent.AddComponent<Image>();
        accImg.color = accentColor;

        return card;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size,
        string content, float fontSize, TextAlignmentOptions alignment, TMP_FontAsset font)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        if (size != Vector2.zero) rt.sizeDelta = size;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        if (font != null) tmp.font = font;
        return tmp;
    }

    private static void SetupCheckpoints()
    {
        GameObject holder = GetOrCreateHolder("--- CHECKPOINTS ---");

        var points = new (Vector3 pos, string name)[]
        {
            (new Vector3(-11.7f, 0.5f, 20.0f), "¡Punto de control 1: Spawn Inicial!"),
            (new Vector3(-11.7f, 0.5f, -2.0f), "¡Punto de control 2: Alerta WinRar!"),
            (new Vector3(8.0f, 0.5f, -40.0f), "¡Punto de control 3: Sala de Armas!"),
            (new Vector3(25.0f, 0.5f, -55.0f), "¡Punto de control 4: Combate Cinético!"),
            (new Vector3(40.0f, 0.5f, -75.0f), "¡Punto de control 5: Umbral del Abismo!"),
            (new Vector3(90.0f, 0.5f, -114.0f), "¡Punto de control 6: Plataforma de Lava 1!"),
            (new Vector3(145.0f, 0.5f, -114.0f), "¡Punto de control 7: Plataforma de Lava 2!"),
            (new Vector3(220.0f, 0.5f, -100.0f), "¡Punto de control 8: Entrada al Pasillo Final!"),
            (new Vector3(243.0f, 0.5f, -15.0f), "¡Punto de control 9: Pasillo de Lava!"),
            (new Vector3(243.2f, 0.5f, 65.0f), "¡Punto de control 10: Arena de la Escalera!"),
            (new Vector3(243.5f, 18.0f, 95.0f), "¡Punto de control 11: Escalera a la Meta!")
        };

        for (int i = 0; i < points.Length; i++)
        {
            string cpName = $"Checkpoint_{i + 1}";
            GameObject cp = GameObject.Find(cpName);
            if (cp == null)
            {
                cp = new GameObject(cpName);
                cp.transform.SetParent(holder.transform, false);
            }

            cp.transform.position = points[i].pos;

            // Asegurar que sea TOTALMENTE INVISIBLE eliminando cualquier pad, renderer o mesh anterior
            for (int c = cp.transform.childCount - 1; c >= 0; c--)
            {
                Object.DestroyImmediate(cp.transform.GetChild(c).gameObject);
            }
            var rend = cp.GetComponent<Renderer>();
            if (rend != null) Object.DestroyImmediate(rend);
            var mf = cp.GetComponent<MeshFilter>();
            if (mf != null) Object.DestroyImmediate(mf);

            // Trigger amplio que abarca toda la sección del corredor
            BoxCollider box = cp.GetComponent<BoxCollider>();
            if (box == null) box = cp.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(32f, 14f, 32f);
            box.center = Vector3.zero;

            // Remover SphereCollider si existía de versiones previas
            SphereCollider sc = cp.GetComponent<SphereCollider>();
            if (sc != null) Object.DestroyImmediate(sc);

            Rigidbody rb = cp.GetComponent<Rigidbody>();
            if (rb == null) rb = cp.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            RespawnController rc = cp.GetComponent<RespawnController>();
            if (rc == null) rc = cp.AddComponent<RespawnController>();

            SerializedObject soRc = new SerializedObject(rc);
            soRc.FindProperty("checkpointName").stringValue = points[i].name;
            soRc.ApplyModifiedPropertiesWithoutUndo();
        }
        Debug.Log($"[TutorialSetup] {points.Length} puntos de respawn invisibles de gran área configurados.");
    }

    private static void SetupTutorialPrompts()
    {
        GameObject holder = GetOrCreateHolder("--- TUTORIAL_PROMPTS ---");

        var promptConfigs = new (Vector3 pos, Vector3 size, string badge, string title, string desc, string controls, string task)[]
        {
            (new Vector3(-11.7f, 1.5f, 16f), new Vector3(15f, 8f, 14f),
             "FASE 1 / 4 · FUNDAMENTOS",
             "1. EXPLORACIÓN Y MOVIMIENTO",
             "Avanza por el pasillo inicial, salta sobre la barricada y encuentra la primera estrella dorada.",
             "<color=#FFE600><b>[ W A S D ]</b></color> Moverse   <color=#FFE600><b>[ ESPACIO ]</b></color> Saltar   <color=#FFE600><b>[ SHIFT ]</b></color> Correr",
             ">> Encuentra la 1ra Estrella dorada en el camino."),

            (new Vector3(-11.7f, 1.5f, -15f), new Vector3(16f, 8f, 16f),
             "FASE 1.5 / 4 · PRIMER CONTACTO",
             "ALERTA DE AMENAZA: BICHO WINRAR",
             "¡CUIDADO! WinRar te eliminará DE UN SOLO GOLPE lanzándose agresivamente sobre ti. Esquiva su salto hacia los lados.",
             "<color=#FF3344><b>[ A / D + SHIFT ]</b></color> Esquivar salto mortal   <color=#00E5FF><b>[ Q ]</b></color> Preparar Herramientas",
             ">> Evade la embestida letal y avanza hacia la sala de armamento."),

            (new Vector3(8f, 1.5f, -45f), new Vector3(20f, 8f, 20f),
             "FASE 2 / 4 · HERRAMIENTA ASPIRADORA",
             "2. ABSORCIÓN Y COMBATE CINÉTICO",
             "Equipa la aspiradora para succionar cajas pesadas del suelo y expulsarlas con fuerza para eliminar a los enemigos.",
             "<color=#00E5FF><b>[ Q ]</b></color> Equipar Aspiradora   <color=#00E5FF><b>[ Clic Izquierdo ]</b></color> Absorber Caja   <color=#00E5FF><b>[ Clic Derecho ]</b></color> Lanzar",
             ">> Destruye a los bichos arrojando cajas y toma la 2da estrella."),

            (new Vector3(55f, 1.5f, -113.8f), new Vector3(20f, 8f, 20f),
             "FASE 3 / 4 · MOVILIDAD VERTICAL",
             "3. EL GANCHO Y EL ABISMO DE LAVA",
             "¡PELIGRO! La lava te desintegrará al tocarla. Apunta a los orbes anaranjados para engancharte. Ojo: el gancho tiene tiempo de recarga.",
             "<color=#00E5FF><b>[ Q ]</b></color> Equipar Gancho   <color=#00E5FF><b>[ X / Clic ]</b></color> Disparar Gancho   <color=#FFAA00><b>[ HUD ]</b></color> Monitorear Recarga",
             ">> Balancéate de anclaje en anclaje sobre la lava y llega al pasillo final."),

            (new Vector3(230f, 1.5f, -80f), new Vector3(25f, 8f, 25f),
             "FASE 4 / 4 · DESAFÍO FINAL",
             "4. PASILLO DE LAVA Y GUARDIÁN TROYANO",
             "El pasillo final es un lago de lava ardiente. Salta entre plataformas y usa los anclajes para vencer a las amenazas y al Troyano.",
             "<color=#00E5FF><b>[ Q ]</b></color> Cambiar Herramienta   <color=#FFE600><b>[ W A S D + Espacio ]</b></color> Subir Escalera",
             ">> Vence al guardián, recoge la 3ra estrella y cruza el portal en la cima.")
        };

        for (int i = 0; i < promptConfigs.Length; i++)
        {
            string pName = $"PromptZone_{i + 1}";
            GameObject pObj = GameObject.Find(pName);
            if (pObj == null)
            {
                pObj = new GameObject(pName);
                pObj.transform.SetParent(holder.transform, false);
            }

            pObj.transform.position = promptConfigs[i].pos;

            BoxCollider box = pObj.GetComponent<BoxCollider>();
            if (box == null) box = pObj.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = promptConfigs[i].size;

            Rigidbody rb = pObj.GetComponent<Rigidbody>();
            if (rb == null) rb = pObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            TutorialZonePrompt promptScript = pObj.GetComponent<TutorialZonePrompt>();
            if (promptScript == null) promptScript = pObj.AddComponent<TutorialZonePrompt>();

            SerializedObject so = new SerializedObject(promptScript);
            so.FindProperty("phaseBadge").stringValue = promptConfigs[i].badge;
            so.FindProperty("zoneTitle").stringValue = promptConfigs[i].title;
            so.FindProperty("description").stringValue = promptConfigs[i].desc;
            so.FindProperty("controls").stringValue = promptConfigs[i].controls;
            so.FindProperty("task").stringValue = promptConfigs[i].task;
            so.FindProperty("triggerOnce").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetupStars(Material matCollectible)
    {
        GameObject holder = GetOrCreateHolder("--- STARS ---");

        Vector3[] starPositions = new Vector3[]
        {
            new Vector3(-11.7f, 1.2f, 5.0f),   // Estrella 1: Pasillo inicial
            new Vector3(15.0f, 1.2f, -50.0f),  // Estrella 2: Sala de aspiradora
            new Vector3(243.2f, 1.2f, 40.0f)   // Estrella 3: Pasillo de lava antes de la gran escalera
        };

        for (int i = 0; i < starPositions.Length; i++)
        {
            string sName = $"Star_{i + 1}";
            GameObject starObj = GameObject.Find(sName);
            if (starObj == null)
            {
                starObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                starObj.name = sName;
                starObj.transform.SetParent(holder.transform, false);
                starObj.transform.localScale = Vector3.one * 0.85f;

                SphereCollider col = starObj.GetComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0.9f;

                Rigidbody rb = starObj.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                starObj.AddComponent<StarController>();

                GameObject lightObj = new GameObject("StarGlow");
                lightObj.transform.SetParent(starObj.transform, false);
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1.0f, 0.9f, 0.1f);
                light.range = 7f;
                light.intensity = 3.5f;
            }

            starObj.transform.position = starPositions[i];
            if (matCollectible != null)
            {
                starObj.GetComponent<Renderer>().sharedMaterial = matCollectible;
            }
        }
    }

    private static void SetupPoolableCrates(Material matObjects)
    {
        GameObject holder = GetOrCreateHolder("--- POOLABLE_CRATES ---");

        Vector3[] cratePositions = new Vector3[]
        {
            // Pasillo inicial (entrenamiento de absorción)
            new Vector3(-11.7f, 0.5f, 14.0f),
            new Vector3(-9.5f, 0.5f, 12.0f),
            new Vector3(-13.5f, 0.5f, 12.0f),
            new Vector3(-11.7f, 0.5f, -8.0f),

            // Sala 2 (Armamento / Aspiradora)
            new Vector3(8.0f, 0.5f, -38.0f),
            new Vector3(10.0f, 0.5f, -38.0f),
            new Vector3(9.0f, 1.5f, -38.0f),
            new Vector3(24.0f, 0.5f, -48.0f),
            new Vector3(26.0f, 0.5f, -48.0f),
            new Vector3(25.0f, 1.5f, -48.0f),
            new Vector3(12.0f, 0.5f, -60.0f),
            new Vector3(14.0f, 0.5f, -60.0f),
            new Vector3(28.0f, 0.5f, -62.0f),
            new Vector3(28.0f, 1.5f, -62.0f),

            // Cañón de Lava (islas y descansos)
            new Vector3(90.0f, 1.0f, -112.0f),
            new Vector3(92.0f, 1.0f, -112.0f),
            new Vector3(145.0f, 1.0f, -112.0f),
            new Vector3(147.0f, 1.0f, -112.0f),

            // Pasillo Final de Lava (Plataformas y Arena)
            new Vector3(242.0f, 1.0f, -68.0f),
            new Vector3(244.0f, 1.0f, -68.0f),
            new Vector3(234.0f, 1.1f, -38.0f),
            new Vector3(236.0f, 1.1f, -38.0f),
            new Vector3(251.0f, 1.2f, -13.0f),
            new Vector3(253.0f, 1.2f, -13.0f),
            new Vector3(239.0f, 1.1f, 12.0f),
            new Vector3(241.0f, 1.1f, 12.0f),
            new Vector3(249.0f, 1.2f, 37.0f),
            new Vector3(251.0f, 1.2f, 37.0f),
            new Vector3(239.0f, 1.0f, 62.0f),
            new Vector3(241.0f, 1.0f, 62.0f),
            new Vector3(245.0f, 1.0f, 62.0f),
            new Vector3(247.0f, 1.0f, 62.0f)
        };

        int poolLayer = LayerMask.NameToLayer("Poolable");
        if (poolLayer < 0) poolLayer = 0;

        for (int i = 0; i < cratePositions.Length; i++)
        {
            string cName = $"Poolable_Crate_{i + 1}";
            GameObject crate = GameObject.Find(cName);
            if (crate == null)
            {
                crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = cName;
                crate.transform.SetParent(holder.transform, false);
                crate.transform.localScale = Vector3.one * 1.0f;

                Rigidbody rb = crate.AddComponent<Rigidbody>();
                rb.mass = 2.0f;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                PoolableController poolable = crate.AddComponent<PoolableController>();
                SerializedObject soPool = new SerializedObject(poolable);
                soPool.FindProperty("impactDamage").intValue = 3;
                soPool.ApplyModifiedPropertiesWithoutUndo();

                ThrowableDamage throwable = crate.AddComponent<ThrowableDamage>();
                SerializedObject soThrow = new SerializedObject(throwable);
                soThrow.FindProperty("damage").intValue = 3;
                soThrow.FindProperty("destroyOnImpact").boolValue = true;
                soThrow.ApplyModifiedPropertiesWithoutUndo();
            }

            crate.transform.position = cratePositions[i];
            crate.layer = poolLayer;

            if (matObjects != null)
            {
                crate.GetComponent<Renderer>().sharedMaterial = matObjects;
            }
        }
    }

    private static void SetupGrappleAnchors(Material matGrapple)
    {
        GameObject holder = GetOrCreateHolder("--- GRAPPLE_ANCHORS ---");

        Vector3[] anchorPositions = new Vector3[]
        {
            // Cañón de Lava
            new Vector3(90.0f, 9.0f, -113.8f),
            new Vector3(130.0f, 9.0f, -113.8f),
            new Vector3(175.0f, 9.0f, -113.8f),

            // Pasillo Final de Lava
            new Vector3(243.2f, 13.0f, -55.0f),
            new Vector3(243.2f, 15.0f, -15.0f),
            new Vector3(243.2f, 16.0f, 25.0f),
            new Vector3(243.2f, 20.0f, 55.0f)
        };

        int grappleLayer = LayerMask.NameToLayer("Graple");
        if (grappleLayer < 0) grappleLayer = 8;

        for (int i = 0; i < anchorPositions.Length; i++)
        {
            string aName = $"GrappleAnchor_{i + 1}";
            GameObject anchor = GameObject.Find(aName);
            if (anchor == null)
            {
                anchor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                anchor.name = aName;
                anchor.transform.SetParent(holder.transform, false);
                anchor.transform.localScale = Vector3.one * 1.5f;

                SphereCollider col = anchor.GetComponent<SphereCollider>();
                col.radius = 1.0f;

                GameObject lightObj = new GameObject("AnchorLight");
                lightObj.transform.SetParent(anchor.transform, false);
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1.0f, 0.55f, 0.12f);
                light.range = 10f;
                light.intensity = 3.2f;
            }

            anchor.transform.position = anchorPositions[i];
            anchor.layer = grappleLayer;

            if (matGrapple != null)
            {
                anchor.GetComponent<Renderer>().sharedMaterial = matGrapple;
            }
        }
    }

    private static void SetupEnemies()
    {
        GameObject holder = GetOrCreateHolder("--- ENEMIES ---");

        GameObject bugPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/WinRar.prefab");
        GameObject flyingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/FlyingEmail.prefab");
        GameObject horsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Horse.prefab");
        GameObject spiderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Hexbug.prefab");

        // 1. Enemigo WinRar en el pasillo inicial (introducción a su salto mortal de 1 golpe)
        SpawnOrUpdateEnemy(bugPrefab, holder.transform, "Enemy_Tutorial_WinRar_Intro",
            new Vector3(-11.7f, 0.2f, -18.0f), Quaternion.Euler(0, 0, 0), 2);

        // 2, 3 y 4. Enemigos WinRar en Sala 2 (combate cinético con aspiradora y cajas)
        SpawnOrUpdateEnemy(bugPrefab, holder.transform, "Enemy_Tutorial_WinRar_1",
            new Vector3(18.0f, 0.2f, -44.0f), Quaternion.Euler(0, -90f, 0), 3);

        SpawnOrUpdateEnemy(bugPrefab, holder.transform, "Enemy_Tutorial_WinRar_2",
            new Vector3(27.0f, 0.2f, -58.0f), Quaternion.Euler(0, 180f, 0), 3);

        SpawnOrUpdateEnemy(bugPrefab, holder.transform, "Enemy_Tutorial_WinRar_3",
            new Vector3(12.0f, 0.2f, -55.0f), Quaternion.Euler(0, 45f, 0), 3);

        // 5, 6 y 7. Enemigos Voladores (FlyingEmail) patrullando el cañón de lava
        SpawnOrUpdateEnemy(flyingPrefab, holder.transform, "Enemy_Tutorial_FlyingEmail_1",
            new Vector3(115.0f, 8.0f, -100.0f), Quaternion.Euler(0, 180f, 0), 2);

        SpawnOrUpdateEnemy(flyingPrefab, holder.transform, "Enemy_Tutorial_FlyingEmail_2",
            new Vector3(145.0f, 8.5f, -125.0f), Quaternion.Euler(0, -90f, 0), 2);

        SpawnOrUpdateEnemy(flyingPrefab, holder.transform, "Enemy_Tutorial_FlyingEmail_3",
            new Vector3(175.0f, 8.0f, -105.0f), Quaternion.Euler(0, 180f, 0), 2);

        // 8. WinRar custodiando la isla intermedia del cañón
        SpawnOrUpdateEnemy(bugPrefab, holder.transform, "Enemy_Tutorial_WinRar_LavaIsland",
            new Vector3(145.0f, 0.5f, -114.0f), Quaternion.Euler(0, 90f, 0), 3);

        // 9 y 10. Enemigos en plataformas del Pasillo Final de Lava
        SpawnOrUpdateEnemy(flyingPrefab, holder.transform, "Enemy_Tutorial_FlyingEmail_Hallway1",
            new Vector3(243.0f, 9.0f, -40.0f), Quaternion.Euler(0, 180f, 0), 2);

        SpawnOrUpdateEnemy(bugPrefab, holder.transform, "Enemy_Tutorial_WinRar_Platform1",
            new Vector3(235.0f, 0.5f, -40.0f), Quaternion.Euler(0, 45f, 0), 3);

        // 11 y 12. Enemigos en la segunda sección del pasillo de lava
        SpawnOrUpdateEnemy(flyingPrefab, holder.transform, "Enemy_Tutorial_FlyingEmail_Hallway2",
            new Vector3(248.0f, 9.0f, 10.0f), Quaternion.Euler(0, 180f, 0), 2);

        SpawnOrUpdateEnemy(bugPrefab, holder.transform, "Enemy_Tutorial_WinRar_Platform2",
            new Vector3(240.0f, 0.5f, 10.0f), Quaternion.Euler(0, -45f, 0), 3);

        // 13. Araña Hexbug en la plataforma antes de la arena
        SpawnOrUpdateEnemy(spiderPrefab, holder.transform, "Enemy_Tutorial_Hexbug_1",
            new Vector3(250.0f, 0.6f, 35.0f), Quaternion.Euler(0, -45f, 0), 2);

        // 14. ¡EL CABALLO DE TROYA! (Modelo Troyano de Kris) como Guardián Élite en la arena
        SpawnOrUpdateEnemy(horsePrefab, holder.transform, "Enemy_Tutorial_Horse_Guardian",
            new Vector3(243.2f, 0.5f, 68.0f), Quaternion.Euler(0, 180f, 0), 6);

        // 15. WinRar de guardia en los escalones
        SpawnOrUpdateEnemy(bugPrefab, holder.transform, "Enemy_Tutorial_WinRar_StairsGuard",
            new Vector3(243.2f, 10.0f, 90.0f), Quaternion.Euler(0, 180f, 0), 3);

        // 16. FlyingEmail Centinela custodiando la puerta en la cima
        SpawnOrUpdateEnemy(flyingPrefab, holder.transform, "Enemy_Tutorial_FlyingEmail_Sentinel",
            new Vector3(244.0f, 36.5f, 85.0f), Quaternion.Euler(0, 180f, 0), 2);
    }

    private static void SpawnOrUpdateEnemy(GameObject prefab, Transform parent, string name,
        Vector3 position, Quaternion rotation, int health)
    {
        if (prefab == null) return;
        GameObject enemy = GameObject.Find(name);
        if (enemy == null)
        {
            enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            enemy.name = name;
        }

        enemy.transform.position = position;
        enemy.transform.rotation = rotation;

        EnemyHealth eh = enemy.GetComponent<EnemyHealth>();
        if (eh != null)
        {
            SerializedObject soEh = new SerializedObject(eh);
            soEh.FindProperty("baseMaxHealth").intValue = health;
            soEh.FindProperty("maxHealth").intValue = health;
            soEh.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetupPlayer(GameController gameController)
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            Vector3 startPos = new Vector3(-11.7f, 0.8f, 21.0f);
            CharacterController cc = playerObj.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerObj.transform.position = startPos;
            playerObj.transform.rotation = Quaternion.Euler(0, 180f, 0);
            if (cc != null) cc.enabled = true;

            PlayerController pc = playerObj.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.dead = false;
                pc.health = 3;
            }
        }
    }

    private static GameObject GetOrCreateHolder(string name)
    {
        GameObject holder = GameObject.Find(name);
        if (holder == null)
        {
            holder = new GameObject(name);
        }
        return holder;
    }
}
