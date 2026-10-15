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
    private const string PrefKey = "TutorialSetup_RunOnce_v4";

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
        Material matRespawn = AssetDatabase.LoadAssetAtPath<Material>($"{TestingMaterialsPath}/Respawn.mat");
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

        // 5. Configurar Peligros de Lava y KillZone
        SetupLavaHazards(matFloor, matLava);
        SetupKillZone();

        // 6. Configurar Puerta Final (TutorialDoor)
        SetupDoorGoal(matPortal);

        // 7. Crear / Configurar GameController y UI Canvas de Alta Calidad
        GameController gameController = SetupGameControllerAndUI(fontAsset);

        // 8. Crear Checkpoints visuales con RespawnController
        SetupCheckpoints(matRespawn);

        // 9. Crear Zonas de Instrucción / Tutorial Prompts Dinámicos
        SetupTutorialPrompts();

        // 10. Crear Estrellas Coleccionables con Material HDR Amarillo
        SetupStars(matCollectible);

        // 11. Crear Objetos Absorbibles / Lanzables (Poolable / ThrowableDamage)
        SetupPoolableCrates(matObjects);

        // 12. Crear Anclajes de Gancho (Grapple Anchors)
        SetupGrappleAnchors(matGrapple);

        // 13. Spawnear Enemigos con Distribución Temática Equilibrada
        SetupEnemies();

        // 14. Ajustar Jugador y Spawn inicial
        SetupPlayer(gameController);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[TutorialSetup] ¡El tutorial se ha configurado exitosamente y está listo para jugarse de principio a fin!");
    }

    private static void SetupLavaHazards(Material matFloor, Material matLava)
    {
        int count = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            // Corregir el piso del patio (GO 434825407) que erróneamente se nombró Lava
            if (go.name == "Lava" && go.transform.position.x > 220f)
            {
                Debug.Log("[TutorialSetup] Restaurando piso del patio (no es lava): " + go.name);
                go.name = "Floor_Courtyard";
                go.tag = "Untagged";

                var haz = go.GetComponent<LavaHazard>();
                if (haz != null) Object.DestroyImmediate(haz);

                var glow = go.transform.Find("LavaGlow");
                if (glow != null) Object.DestroyImmediate(glow.gameObject);

                var rb = go.GetComponent<Rigidbody>();
                if (rb != null) Object.DestroyImmediate(rb);

                if (matFloor != null)
                {
                    var rend = go.GetComponent<Renderer>();
                    if (rend != null) rend.sharedMaterial = matFloor;
                }

                BoxCollider bCol = go.GetComponent<BoxCollider>();
                if (bCol != null) bCol.isTrigger = false;

                continue;
            }

            // Solo los fosos del cañón de lava son verdaderos peligros de lava
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
                    box.size = new Vector3(b.size.x, 1.2f, b.size.z);
                    box.center = new Vector3(0, 0, 0);
                }
                else
                {
                    // Limitar la altura del collider a 1.2m para no matar al jugador fuera de la lava
                    box.size = new Vector3(box.size.x, 1.2f, box.size.z);
                    box.center = new Vector3(0, 0, 0);
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

                // Luz ambiental de calor
                if (go.GetComponentInChildren<Light>() == null)
                {
                    GameObject lightObj = new GameObject("LavaGlow");
                    lightObj.transform.SetParent(go.transform, false);
                    lightObj.transform.localPosition = new Vector3(0, 0.4f, 0);
                    Light light = lightObj.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1.0f, 0.35f, 0.05f);
                    light.range = 14f;
                    light.intensity = 2.5f;
                }
            }
        }
        Debug.Log($"[TutorialSetup] {count} fosas de lava reales configuradas en el cañón.");
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

            // Remover MeshCollider bloqueador si existía
            MeshCollider mc = doorObj.GetComponent<MeshCollider>();
            if (mc != null) Object.DestroyImmediate(mc);

            BoxCollider box = doorObj.GetComponent<BoxCollider>();
            if (box == null) box = doorObj.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(6f, 6f, 6f);
            box.center = new Vector3(0, 1.5f, 0);

            Rigidbody rb = doorObj.GetComponent<Rigidbody>();
            if (rb == null) rb = doorObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            TutorialDoor doorScript = doorObj.GetComponent<TutorialDoor>();
            if (doorScript == null) doorScript = doorObj.AddComponent<TutorialDoor>();

            Light portalLight = doorObj.GetComponentInChildren<Light>();
            if (portalLight == null)
            {
                GameObject lightObj = new GameObject("PortalGlow");
                lightObj.transform.SetParent(doorObj.transform, false);
                lightObj.transform.localPosition = new Vector3(0, 1.5f, 0);
                portalLight = lightObj.AddComponent<Light>();
                portalLight.type = LightType.Point;
                portalLight.color = new Color(0.1f, 0.85f, 1.0f);
                portalLight.range = 15f;
                portalLight.intensity = 4.0f;
            }

            SerializedObject soDoor = new SerializedObject(doorScript);
            soDoor.FindProperty("doorLight").objectReferenceValue = portalLight;
            soDoor.FindProperty("triggerDistance").floatValue = 4.5f;
            soDoor.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static GameController SetupGameControllerAndUI(TMP_FontAsset fontAsset)
    {
        // 1. GameObject GameController
        GameObject gcObj = GameObject.Find("GameController");
        if (gcObj == null) gcObj = new GameObject("GameController");
        GameController gc = gcObj.GetComponent<GameController>();
        if (gc == null) gc = gcObj.AddComponent<GameController>();

        // 2. EventSystem
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }

        // 3. Canvas
        GameObject existingCanvas = GameObject.Find("Tutorial_Canvas");
        if (existingCanvas != null) Object.DestroyImmediate(existingCanvas);

        GameObject canvasObj = new GameObject("Tutorial_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        // --- Hurt Overlay (Full screen red flash) ---
        GameObject hurtObj = new GameObject("HurtOverlay");
        hurtObj.transform.SetParent(canvasObj.transform, false);
        RectTransform rtHurt = hurtObj.AddComponent<RectTransform>();
        rtHurt.anchorMin = Vector2.zero;
        rtHurt.anchorMax = Vector2.one;
        rtHurt.sizeDelta = Vector2.zero;
        Image hurtImg = hurtObj.AddComponent<Image>();
        hurtImg.color = new Color(1f, 0f, 0f, 0f);
        hurtImg.raycastTarget = false;

        // --- Death Overlay (Full screen death banner) ---
        GameObject deathObj = new GameObject("DeathOverlay");
        deathObj.transform.SetParent(canvasObj.transform, false);
        RectTransform rtDeath = deathObj.AddComponent<RectTransform>();
        rtDeath.anchorMin = Vector2.zero;
        rtDeath.anchorMax = Vector2.one;
        rtDeath.sizeDelta = Vector2.zero;
        Image deathBg = deathObj.AddComponent<Image>();
        deathBg.color = new Color(0.06f, 0.01f, 0.01f, 0.92f);

        TextMeshProUGUI deathTitle = CreateText(deathObj.transform, "DeathTitle",
            new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 80),
            "¡HAS CAÍDO!", 52, TextAlignmentOptions.Center, fontAsset);
        deathTitle.color = new Color(1f, 0.15f, 0.15f);
        deathTitle.fontStyle = FontStyles.Bold;

        TextMeshProUGUI deathSub = CreateText(deathObj.transform, "DeathSubtitle",
            new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 50),
            "Reapareciendo en el último punto de control...", 22, TextAlignmentOptions.Center, fontAsset);
        deathSub.color = new Color(0.9f, 0.9f, 0.9f);
        deathObj.SetActive(false);

        // --- HUD Top-Left: Health Card ---
        GameObject healthCard = CreateCard(canvasObj.transform, "HealthCard",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(35, -25), new Vector2(260, 48),
            new Color(0.05f, 0.08f, 0.14f, 0.92f), new Color(1f, 0.2f, 0.3f, 1f));

        TextMeshProUGUI healthText = CreateText(healthCard.transform, "HealthText",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            "SALUD: <color=#FF3344>3 / 3 HP</color>", 22, TextAlignmentOptions.Center, fontAsset);
        healthText.fontStyle = FontStyles.Bold;

        // --- HUD Top-Right: Score Card ---
        GameObject scoreCard = CreateCard(canvasObj.transform, "ScoreCard",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-35, -25), new Vector2(260, 48),
            new Color(0.05f, 0.08f, 0.14f, 0.92f), new Color(1f, 0.85f, 0.15f, 1f));

        TextMeshProUGUI scoreText = CreateText(scoreCard.transform, "ScoreText",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            "ESTRELLAS: <color=#FFE600>0 / 3</color>", 22, TextAlignmentOptions.Center, fontAsset);
        scoreText.fontStyle = FontStyles.Bold;

        // --- HUD Bottom-Center: Tool Card ---
        GameObject toolCard = CreateCard(canvasObj.transform, "ToolCard",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 25), new Vector2(560, 46),
            new Color(0.05f, 0.08f, 0.14f, 0.92f), new Color(0.0f, 0.85f, 1.0f, 1f));

        TextMeshProUGUI toolText = CreateText(toolCard.transform, "ToolText",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            "HERRAMIENTA: <color=#00E5FF>GANCHO [X / Clic]</color>  |  [Q] Cambiar", 20, TextAlignmentOptions.Center, fontAsset);

        // --- Checkpoint Toast Notification (Center Left) ---
        GameObject cpToast = CreateCard(canvasObj.transform, "CheckpointCard",
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(35, 0), new Vector2(440, 56),
            new Color(0.04f, 0.12f, 0.08f, 0.92f), new Color(0.2f, 1f, 0.4f, 1f));

        TextMeshProUGUI checkpointText = CreateText(cpToast.transform, "CheckpointNotification",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            "¡Punto de control activado!", 22, TextAlignmentOptions.Center, fontAsset);
        checkpointText.color = new Color(0.3f, 1f, 0.5f);
        cpToast.SetActive(false);

        // --- DYNAMIC TUTORIAL GUIDE PANEL (Top Center) ---
        // Contenedor principal centrado en la parte superior
        GameObject guideCard = CreateCard(canvasObj.transform, "GuidePanel",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -25), new Vector2(920, 130),
            new Color(0.04f, 0.07f, 0.12f, 0.95f), new Color(0.0f, 0.9f, 1.0f, 1f));

        // Todos los textos internos anclados estrictamente a la esquina superior izquierda de GuidePanel
        // 1. Phase Badge Text (Línea 1)
        TextMeshProUGUI phaseText = CreateText(guideCard.transform, "PhaseBadgeText",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(25, -12), new Vector2(870, 18),
            "FASE 1 / 4 · FUNDAMENTOS", 12, TextAlignmentOptions.Left, fontAsset);
        phaseText.color = new Color(1.0f, 0.75f, 0.15f);
        phaseText.fontStyle = FontStyles.Bold;

        // 2. Zone Title Text (Línea 2)
        TextMeshProUGUI titleText = CreateText(guideCard.transform, "ZoneTitleText",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(25, -32), new Vector2(870, 26),
            "1. EXPLORACIÓN Y MOVIMIENTO", 18, TextAlignmentOptions.Left, fontAsset);
        titleText.color = Color.white;
        titleText.fontStyle = FontStyles.Bold;

        // 3. Description Text (Línea 3)
        TextMeshProUGUI descText = CreateText(guideCard.transform, "DescriptionText",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(25, -60), new Vector2(870, 24),
            "Avanza por el pasillo inicial y acostúmbrate a los controles del traje.", 14, TextAlignmentOptions.Left, fontAsset);
        descText.color = new Color(0.82f, 0.89f, 0.95f);

        // 4. Controls Badges (Línea 4, inferior izquierda)
        TextMeshProUGUI controlsText = CreateText(guideCard.transform, "ControlsText",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(25, -92), new Vector2(540, 26),
            "<color=#FFE600><b>[ W A S D ]</b></color> Moverse   <color=#FFE600><b>[ ESPACIO ]</b></color> Saltar   <color=#FFE600><b>[ SHIFT ]</b></color> Correr", 13, TextAlignmentOptions.Left, fontAsset);

        // 5. Task Status Indicator (Línea 4, inferior derecha)
        TextMeshProUGUI taskStatusText = CreateText(guideCard.transform, "TaskText",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-25, 14), new Vector2(330, 24),
            ">> Encuentra la 1ra Estrella dorada.", 13, TextAlignmentOptions.Right, fontAsset);
        taskStatusText.color = new Color(0.35f, 1.0f, 0.55f);
        taskStatusText.fontStyle = FontStyles.Bold;

        // --- WIN PANEL (Victory Modal) ---
        GameObject winPanel = new GameObject("WinPanel");
        winPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform rtWin = winPanel.AddComponent<RectTransform>();
        rtWin.anchorMin = Vector2.zero;
        rtWin.anchorMax = Vector2.one;
        rtWin.sizeDelta = Vector2.zero;

        Image winBg = winPanel.AddComponent<Image>();
        winBg.color = new Color(0.02f, 0.04f, 0.08f, 0.92f);

        GameObject winCard = CreateCard(winPanel.transform, "WinCard",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 420),
            new Color(0.06f, 0.1f, 0.16f, 0.98f), new Color(1f, 0.85f, 0.2f, 1f));

        TextMeshProUGUI winTitle = CreateText(winCard.transform, "WinTitle",
            new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 60),
            "¡TUTORIAL COMPLETADO!", 38, TextAlignmentOptions.Center, fontAsset);
        winTitle.color = new Color(1f, 0.85f, 0.2f);
        winTitle.fontStyle = FontStyles.Bold;

        TextMeshProUGUI winSummary = CreateText(winCard.transform, "WinSummary",
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

        // Top Accent Neon Line
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

    private static void SetupCheckpoints(Material matRespawn)
    {
        GameObject holder = GetOrCreateHolder("--- CHECKPOINTS ---");

        Vector3[] points = new Vector3[]
        {
            new Vector3(-11.7f, 0.05f, 21.0f),  // Checkpoint 1: Spawn
            new Vector3(35.0f, 0.05f, -65.0f),  // Checkpoint 2: Salida sala combate/aspiradora
            new Vector3(220.0f, 0.05f, -100.0f),// Checkpoint 3: Llegada tras gancho y lava
            new Vector3(243.2f, 0.05f, 95.0f)   // Checkpoint 4: Base de la escalera
        };

        for (int i = 0; i < points.Length; i++)
        {
            string cpName = $"Checkpoint_{i + 1}";
            GameObject cp = GameObject.Find(cpName);
            if (cp == null)
            {
                cp = new GameObject(cpName);
                cp.transform.SetParent(holder.transform, false);
                cp.transform.position = points[i];

                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pad.name = "Pad";
                pad.transform.SetParent(cp.transform, false);
                pad.transform.localPosition = Vector3.zero;
                pad.transform.localScale = new Vector3(2.8f, 0.06f, 2.8f);
                if (matRespawn != null) pad.GetComponent<Renderer>().sharedMaterial = matRespawn;
                Object.DestroyImmediate(pad.GetComponent<Collider>());

                GameObject lightObj = new GameObject("Light");
                lightObj.transform.SetParent(cp.transform, false);
                lightObj.transform.localPosition = new Vector3(0, 0.8f, 0);
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1.0f, 0.75f, 0.2f);
                light.range = 7f;
                light.intensity = 2.0f;

                SphereCollider col = cp.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 2.2f;

                Rigidbody rb = cp.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                RespawnController rc = cp.AddComponent<RespawnController>();
                SerializedObject soRc = new SerializedObject(rc);
                soRc.FindProperty("checkpointName").stringValue = $"¡Punto de control {i + 1} alcanzado!";
                soRc.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static void SetupTutorialPrompts()
    {
        GameObject holder = GetOrCreateHolder("--- TUTORIAL_PROMPTS ---");

        var promptConfigs = new (Vector3 pos, Vector3 size, string badge, string title, string desc, string controls, string task)[]
        {
            (new Vector3(-11.7f, 1.5f, 16f), new Vector3(15f, 8f, 14f),
             "FASE 1 / 4 · FUNDAMENTOS",
             "1. EXPLORACIÓN Y MOVIMIENTO",
             "Avanza por el pasillo inicial, aprende a moverte con fluidez y encuentra la primera estrella dorada.",
             "<color=#FFE600><b>[ W A S D ]</b></color> Moverse   <color=#FFE600><b>[ ESPACIO ]</b></color> Saltar   <color=#FFE600><b>[ SHIFT ]</b></color> Correr",
             ">> Encuentra la 1ra Estrella dorada en el camino."),

            (new Vector3(-11.7f, 1.5f, -20f), new Vector3(16f, 8f, 16f),
             "FASE 1.5 / 4 · PRIMER CONTACTO",
             "ALERTA DE AMENAZA: BICHO WINRAR",
             "Un enemigo hostil bloquea la curva. Si te acercas, se lanzará a morderte cuerpo a cuerpo. ¡Mantén tu distancia!",
             "<color=#FF3344><b>[ W A S D + SHIFT ]</b></color> Esquivar embestida   <color=#00E5FF><b>[ Q ]</b></color> Preparar Herramientas",
             ">> Evade al bicho y avanza hacia la sala de armamento."),

            (new Vector3(8f, 1.5f, -45f), new Vector3(20f, 8f, 20f),
             "FASE 2 / 4 · HERRAMIENTA ASPIRADORA",
             "2. ABSORCIÓN Y COMBATE CINÉTICO",
             "Equipa la aspiradora para succionar cajas pesadas del suelo y expulsarlas con fuerza contra los enemigos.",
             "<color=#00E5FF><b>[ Q ]</b></color> Equipar Aspiradora   <color=#00E5FF><b>[ Clic Izquierdo ]</b></color> Absorber Caja   <color=#00E5FF><b>[ Clic Derecho ]</b></color> Lanzar",
             ">> Destruye a los bichos arrojando cajas y toma la 2da estrella."),

            (new Vector3(55f, 1.5f, -113.8f), new Vector3(20f, 8f, 20f),
             "FASE 3 / 4 · MOVILIDAD VERTICAL",
             "3. EL GANCHO Y EL ABISMO DE LAVA",
             "¡PELIGRO MORTAL! La lava y las caídas te eliminarán al instante. Apunta a los orbes anaranjados en el aire para impulsarte.",
             "<color=#00E5FF><b>[ Q ]</b></color> Equipar Gancho   <color=#00E5FF><b>[ X / Clic ]</b></color> Disparar Gancho hacia Orbe   <color=#FFE600><b>[ A / D ]</b></color> Control Aéreo",
             ">> Balancéate de anclaje en anclaje sobre la lava y llega al patio."),

            (new Vector3(230f, 1.5f, -80f), new Vector3(25f, 8f, 25f),
             "FASE 4 / 4 · DESAFÍO FINAL",
             "4. PATIO DE COMBATE Y LA PUERTA DE SALIDA",
             "Neutraliza a los guardianes del patio, recoge la última estrella y asciende por la gran escalera hacia la meta.",
             "<color=#00E5FF><b>[ Q ]</b></color> Cambiar Herramienta   <color=#FFE600><b>[ W A S D + Espacio ]</b></color> Subir Escalera",
             ">> Cruza el portal luminoso en la cima de la escalera para ganar.")
        };

        for (int i = 0; i < promptConfigs.Length; i++)
        {
            string pName = $"PromptZone_{i + 1}";
            GameObject pObj = GameObject.Find(pName);
            if (pObj == null)
            {
                pObj = new GameObject(pName);
                pObj.transform.SetParent(holder.transform, false);
                pObj.transform.position = promptConfigs[i].pos;

                BoxCollider box = pObj.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = promptConfigs[i].size;

                Rigidbody rb = pObj.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                pObj.AddComponent<TutorialZonePrompt>();
            }

            TutorialZonePrompt promptScript = pObj.GetComponent<TutorialZonePrompt>();
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
            new Vector3(250.0f, 1.2f, 10.0f)   // Estrella 3: Patio antes de la escalera
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
                starObj.transform.position = starPositions[i];
                starObj.transform.localScale = Vector3.one * 0.85f;

                if (matCollectible != null)
                {
                    starObj.GetComponent<Renderer>().sharedMaterial = matCollectible;
                }

                SphereCollider col = starObj.GetComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0.9f;

                Rigidbody rb = starObj.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                starObj.AddComponent<StarController>();

                // Luz dorada brillante
                GameObject lightObj = new GameObject("StarGlow");
                lightObj.transform.SetParent(starObj.transform, false);
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1.0f, 0.9f, 0.1f);
                light.range = 7f;
                light.intensity = 3.5f;
            }
            else
            {
                // Actualizar material existente con HDR
                if (matCollectible != null)
                {
                    starObj.GetComponent<Renderer>().sharedMaterial = matCollectible;
                }
            }
        }
    }

    private static void SetupPoolableCrates(Material matObjects)
    {
        GameObject holder = GetOrCreateHolder("--- POOLABLE_CRATES ---");

        Vector3[] cratePositions = new Vector3[]
        {
            // Sala 2 (Armamento / Aspiradora)
            new Vector3(8.0f, 0.5f, -38.0f),
            new Vector3(10.0f, 0.5f, -38.0f),
            new Vector3(9.0f, 1.5f, -38.0f),
            new Vector3(24.0f, 0.5f, -48.0f),
            new Vector3(26.0f, 0.5f, -48.0f),
            new Vector3(25.0f, 1.5f, -48.0f),
            new Vector3(12.0f, 0.5f, -60.0f),
            new Vector3(14.0f, 0.5f, -60.0f),

            // Patio de combate (Sala 4)
            new Vector3(230.0f, 0.5f, -20.0f),
            new Vector3(232.0f, 0.5f, -20.0f),
            new Vector3(245.0f, 0.5f, 25.0f),
            new Vector3(247.0f, 0.5f, 25.0f)
        };

        int poolLayer = LayerMask.NameToLayer("Poolable");
        for (int i = 0; i < cratePositions.Length; i++)
        {
            string cName = $"Crate_{i + 1}";
            GameObject crate = GameObject.Find(cName);
            if (crate != null && poolLayer >= 0) crate.layer = poolLayer;
            if (crate == null)
            {
                crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = cName;
                crate.transform.SetParent(holder.transform, false);
                crate.transform.position = cratePositions[i];
                crate.transform.localScale = Vector3.one * 1.0f;
                if (poolLayer >= 0) crate.layer = poolLayer;

                if (matObjects != null)
                {
                    crate.GetComponent<Renderer>().sharedMaterial = matObjects;
                }

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
        }
    }

    private static void SetupGrappleAnchors(Material matGrapple)
    {
        GameObject holder = GetOrCreateHolder("--- GRAPPLE_ANCHORS ---");

        Vector3[] anchorPositions = new Vector3[]
        {
            new Vector3(90.0f, 9.0f, -113.8f),
            new Vector3(130.0f, 9.0f, -113.8f),
            new Vector3(175.0f, 9.0f, -113.8f),
            new Vector3(243.2f, 18.0f, 50.0f)
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
                anchor.transform.position = anchorPositions[i];
                anchor.transform.localScale = Vector3.one * 1.5f;
                anchor.layer = grappleLayer;

                if (matGrapple != null)
                {
                    anchor.GetComponent<Renderer>().sharedMaterial = matGrapple;
                }

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
        }
    }

    private static void SetupEnemies()
    {
        GameObject holder = GetOrCreateHolder("--- ENEMIES ---");

        GameObject bugPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/WinRar.prefab");
        GameObject flyingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/FlyingEmail.prefab");
        GameObject spiderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Hexbug.prefab");

        // 1. Enemigo WinRar en el pasillo inicial (introducción al combate)
        SpawnOrUpdateEnemy(bugPrefab, holder.transform, "Enemy_Tutorial_WinRar_Intro",
            new Vector3(-11.7f, 0.2f, -18.0f), Quaternion.Euler(0, 0, 0), 2);

        // 2 y 3. Enemigos WinRar en Sala 2 (combate con aspiradora y cajas)
        SpawnOrUpdateEnemy(bugPrefab, holder.transform, "Enemy_Tutorial_WinRar_1",
            new Vector3(18.0f, 0.2f, -44.0f), Quaternion.Euler(0, -90f, 0), 3);

        SpawnOrUpdateEnemy(bugPrefab, holder.transform, "Enemy_Tutorial_WinRar_2",
            new Vector3(27.0f, 0.2f, -58.0f), Quaternion.Euler(0, 180f, 0), 3);

        // 4 y 5. Enemigos Voladores (FlyingEmail) sobre el cañón de lava
        SpawnOrUpdateEnemy(flyingPrefab, holder.transform, "Enemy_Tutorial_FlyingEmail_1",
            new Vector3(115.0f, 8.0f, -100.0f), Quaternion.Euler(0, 180f, 0), 2);

        SpawnOrUpdateEnemy(flyingPrefab, holder.transform, "Enemy_Tutorial_FlyingEmail_2",
            new Vector3(170.0f, 8.5f, -125.0f), Quaternion.Euler(0, -90f, 0), 2);

        // 6 y 7. Enemigos Araña (Hexbug) en el patio final
        SpawnOrUpdateEnemy(spiderPrefab, holder.transform, "Enemy_Tutorial_Hexbug_1",
            new Vector3(235.0f, 0.2f, -35.0f), Quaternion.Euler(0, 45f, 0), 2);

        SpawnOrUpdateEnemy(spiderPrefab, holder.transform, "Enemy_Tutorial_Hexbug_2",
            new Vector3(258.0f, 0.2f, -10.0f), Quaternion.Euler(0, -45f, 0), 2);

        // 8. Enemigo WinRar Guardia en la base de la gran escalera
        SpawnOrUpdateEnemy(bugPrefab, holder.transform, "Enemy_Tutorial_WinRar_Guard",
            new Vector3(243.2f, 0.2f, 70.0f), Quaternion.Euler(0, 180f, 0), 3);

        // 9. Enemigo Volador Centinela en la cima custodiando la puerta
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
            enemy.transform.position = position;
            enemy.transform.rotation = rotation;
        }

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
