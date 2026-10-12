using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Constructor profesional del Nivel 1 ("La Senda Celeste").
/// Genera un diseño de nivel coherente, intuitivo, guiado visualmente y con colisionadores BoxCollider 100% sólidos.
/// Adaptado perfectamente a las mecánicas de caminar, saltar, aspiradora y gancho sin bloqueos ni trabas.
/// </summary>
public static class Level1Builder
{
    private const string SCENE_PATH = "Assets/Scenes/Level1.unity";

    // Materiales
    private static Material matStone;
    private static Material matStoneDark;
    private static Material matCyan;
    private static Material matGold;
    private static Material matEmerald;
    private static Material matRuby;
    private static Material matEnemyArmor;
    private static Material matPortal;
    private static Material matHazardMetal;

    // Modelos
    private const string MODEL_HEX = "Assets/Art/Models/StylizedPlatform_Hex.obj";
    private const string MODEL_LONG = "Assets/Art/Models/StylizedPlatform_Long.obj";
    private const string MODEL_LANTERN = "Assets/Art/Models/GrappleLantern.obj";
    private const string MODEL_TOTEM = "Assets/Art/Models/CheckpointTotem.obj";
    private const string MODEL_ALTAR = "Assets/Art/Models/VictoryAltar.obj";
    private const string MODEL_DRONE = "Assets/Art/Models/EnemyDrone.obj";
    private const string MODEL_SPIKES = "Assets/Art/Models/SpikeTrap.obj";
    private const string MODEL_ROTATING = "Assets/Art/Models/RotatingHazardBeam.obj";
    private const string MODEL_ORB = "Assets/Art/Models/EnergyOrb.obj";
    private const string MODEL_ISLAND = "Assets/Customizable Skybox/Model/Low Poly Island.fbx";

    // Texturas UI
    private const string SPRITE_HEART_FULL = "Assets/Art/Textures/UI/Heart_Full.png";
    private const string SPRITE_HEART_EMPTY = "Assets/Art/Textures/UI/Heart_Empty.png";
    private const string SPRITE_STAR = "Assets/Art/Textures/UI/Icon_Star.png";
    private const string SPRITE_GRAPPLE = "Assets/Art/Textures/UI/Icon_Grapple.png";
    private const string SPRITE_VACUUM = "Assets/Art/Textures/UI/Icon_Vacuum.png";
    private const string SPRITE_CROSSHAIR = "Assets/Art/Textures/UI/Crosshair.png";
    private const string SPRITE_VIGNETTE = "Assets/Art/Textures/UI/Vignette_Damage.png";
    private const string SPRITE_PANEL = "Assets/Art/Textures/UI/Panel_Background.png";

    [MenuItem("Tools/Build Level 1")]
    public static void BuildLevel1Scene()
    {
        Debug.Log("Iniciando construcción rediseñada y perfeccionada de Level 1: La Senda Celeste...");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        LoadMaterials();

        SetupLightingAndAtmosphere();

        SetupCoreManagers(out Level1Manager levelMgr, out GameController gameCtrl);

        SetupKillZone();

        SetupPlayer(out GameObject playerObj, out PlayerController playerCtrl, out ObjectVacum vacuum, out GrapleController grapple);

        SetupUI(playerCtrl, vacuum, gameCtrl, out LevelUIController uiCtrl);

        BuildLevelArchitecture(levelMgr, gameCtrl);

        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        Debug.Log($"Escena guardada exitosamente en {SCENE_PATH}");

        RegisterSceneInBuildSettings();

        Debug.Log("¡Nivel 1 reconstruido exitosamente con colisiones perfectas y flujo de juego continuo!");
    }

    private static void LoadMaterials()
    {
        matStone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Mat_StylizedStone.mat");
        matStoneDark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Mat_StylizedStoneDark.mat");
        matCyan = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Mat_CrystalCyan.mat");
        matGold = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Mat_CrystalGold.mat");
        matEmerald = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Mat_CrystalEmerald.mat");
        matRuby = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Mat_CrystalRuby.mat");
        matEnemyArmor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Mat_EnemyArmor.mat");
        matPortal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Mat_PortalVortex.mat");
        matHazardMetal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Mat_HazardMetal.mat");
    }

    private static void SetupLightingAndAtmosphere()
    {
        Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Customizable Skybox/Stylized Sky/Materials/Sunset3.mat");
        if (skyboxMat != null) RenderSettings.skybox = skyboxMat;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.95f, 0.88f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.70f, 0.65f, 0.72f);
        RenderSettings.ambientGroundColor = new Color(0.40f, 0.36f, 0.42f);

        GameObject sunObj = new GameObject("Sun Light");
        Light sun = sunObj.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1.0f, 0.95f, 0.88f);
        sun.intensity = 1.35f;
        sun.shadows = LightShadows.Soft;
        sunObj.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
    }

    private static void SetupCoreManagers(out Level1Manager levelMgr, out GameController gameCtrl)
    {
        GameObject audioObj = new GameObject("GameAudio");
        audioObj.AddComponent<GameAudio>();

        GameObject gmObj = new GameObject("GM");
        gmObj.tag = "GameController";
        gameCtrl = gmObj.AddComponent<GameController>();

        GameObject mgrObj = new GameObject("Level1Manager");
        levelMgr = mgrObj.AddComponent<Level1Manager>();
    }

    private static void SetupKillZone()
    {
        GameObject killObj = new GameObject("KillZone");
        killObj.transform.position = new Vector3(0, -25f, 90f);
        BoxCollider col = killObj.AddComponent<BoxCollider>();
        col.size = new Vector3(800f, 15f, 800f);
        col.isTrigger = true;
        killObj.AddComponent<KillZone>();
    }

    private static void SetupPlayer(out GameObject playerObj, out PlayerController playerCtrl, out ObjectVacum vacuum, out GrapleController grapple)
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EasyPeasyFirstPersonController/Prefabs/FirstPersonController.prefab");
        playerObj = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        playerObj.name = "Player";
        playerObj.tag = "Player";
        playerObj.layer = LayerMask.NameToLayer("Player");
        playerObj.transform.position = new Vector3(0, 1.2f, 1f);

        // Desactivar ledge grab para evitar teletransportes
        EasyPeasyFirstPersonController.FirstPersonController fpc = playerObj.GetComponent<EasyPeasyFirstPersonController.FirstPersonController>();
        if (fpc != null)
        {
            fpc.walkSpeed = 5f;
            fpc.sprintSpeed = 8f;
            fpc.jumpSpeed = 5f;
            fpc.slideDuration = 1.5f;
            fpc.lookSensitivityX = 15f;
            fpc.lookSensitivityY = 15f;
            fpc.ledgeLayer = 0;
            fpc.ledgeDetectionDistance = 0f;
            fpc.climbDuration = 0f;
            fpc.climbHeightArc = 0f;
            fpc.climbTiltAmount = 0f;
        }

        playerCtrl = playerObj.AddComponent<PlayerController>();
        playerCtrl.health = 3;

        GameObject toolObj = new GameObject("Tool");
        toolObj.transform.SetParent(playerObj.transform, false);
        toolObj.transform.localPosition = new Vector3(0, 1.44f, 0.54f);

        vacuum = toolObj.AddComponent<ObjectVacum>();
        grapple = toolObj.AddComponent<GrapleController>();

        SerializedObject soPlayer = new SerializedObject(playerCtrl);
        soPlayer.FindProperty("vacuum").objectReferenceValue = vacuum;
        soPlayer.FindProperty("graple").objectReferenceValue = grapple;
        soPlayer.FindProperty("health").intValue = 3;
        soPlayer.ApplyModifiedProperties();
    }

    private static void SetupUI(PlayerController playerCtrl, ObjectVacum vacuum, GameController gameCtrl, out LevelUIController uiCtrl)
    {
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        uiCtrl = canvasObj.AddComponent<LevelUIController>();

        GameObject esObj = new GameObject("EventSystem");
        esObj.AddComponent<EventSystem>();
        esObj.AddComponent<InputSystemUIInputModule>();

        Sprite spHeartFull = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_HEART_FULL);
        Sprite spHeartEmpty = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_HEART_EMPTY);
        Sprite spStar = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_STAR);
        Sprite spGrapple = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_GRAPPLE);
        Sprite spVacuum = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_VACUUM);
        Sprite spCrosshair = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_CROSSHAIR);
        Sprite spVignette = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_VIGNETTE);
        Sprite spPanel = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_PANEL);

        // 1. Viñeta de daño
        GameObject vigObj = CreateUIElement("DamageVignette", canvasObj.transform);
        RectTransform vigRT = vigObj.GetComponent<RectTransform>();
        vigRT.anchorMin = Vector2.zero;
        vigRT.anchorMax = Vector2.one;
        vigRT.sizeDelta = Vector2.zero;
        Image vigImg = vigObj.AddComponent<Image>();
        vigImg.sprite = spVignette;
        vigImg.color = new Color(1, 0, 0, 0);
        vigImg.raycastTarget = false;

        // 2. Retícula / Crosshair
        GameObject crossObj = CreateUIElement("Crosshair", canvasObj.transform);
        RectTransform crossRT = crossObj.GetComponent<RectTransform>();
        crossRT.anchoredPosition = Vector2.zero;
        crossRT.sizeDelta = new Vector2(40, 40);
        Image crossImg = crossObj.AddComponent<Image>();
        crossImg.sprite = spCrosshair;
        crossImg.color = new Color(1, 1, 1, 0.85f);
        crossImg.raycastTarget = false;

        // 3. Panel de Salud (3 Corazones)
        GameObject healthPanel = CreatePanel("HealthPanel", canvasObj.transform, new Vector2(30, -30), new Vector2(220, 75), new Vector2(0, 1), new Vector2(0, 1), spPanel);
        Image[] hearts = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject hObj = CreateUIElement($"Heart_{i}", healthPanel.transform);
            RectTransform hRT = hObj.GetComponent<RectTransform>();
            hRT.anchorMin = new Vector2(0, 0.5f);
            hRT.anchorMax = new Vector2(0, 0.5f);
            hRT.anchoredPosition = new Vector2(45 + i * 55, 0);
            hRT.sizeDelta = new Vector2(46, 46);
            Image hImg = hObj.AddComponent<Image>();
            hImg.sprite = spHeartFull;
            hImg.color = Color.white;
            hearts[i] = hImg;
        }

        // 4. Panel de Estrellas (0 / 3)
        GameObject starsPanel = CreatePanel("StarsPanel", canvasObj.transform, new Vector2(-30, -30), new Vector2(170, 75), new Vector2(1, 1), new Vector2(1, 1), spPanel);
        GameObject sIconObj = CreateUIElement("StarIcon", starsPanel.transform);
        RectTransform sIconRT = sIconObj.GetComponent<RectTransform>();
        sIconRT.anchorMin = new Vector2(0, 0.5f);
        sIconRT.anchorMax = new Vector2(0, 0.5f);
        sIconRT.anchoredPosition = new Vector2(42, 0);
        sIconRT.sizeDelta = new Vector2(44, 44);
        Image sIconImg = sIconObj.AddComponent<Image>();
        sIconImg.sprite = spStar;

        GameObject sTextObj = CreateUIElement("StarsText", starsPanel.transform);
        RectTransform sTextRT = sTextObj.GetComponent<RectTransform>();
        sTextRT.anchorMin = new Vector2(0, 0.5f);
        sTextRT.anchorMax = new Vector2(1, 0.5f);
        sTextRT.anchoredPosition = new Vector2(105, 0);
        sTextRT.sizeDelta = new Vector2(100, 50);
        TextMeshProUGUI sTMP = sTextObj.AddComponent<TextMeshProUGUI>();
        sTMP.text = "0 / 3";
        sTMP.fontSize = 28;
        sTMP.fontStyle = FontStyles.Bold;
        sTMP.alignment = TextAlignmentOptions.Left;
        sTMP.color = new Color(1f, 0.88f, 0.2f);

        // 5. Panel de Selector de Herramienta y Munición
        GameObject toolPanel = CreatePanel("ToolPanel", canvasObj.transform, new Vector2(30, 30), new Vector2(260, 115), new Vector2(0, 0), new Vector2(0, 0), spPanel);

        GameObject gIconObj = CreateUIElement("GrappleIcon", toolPanel.transform);
        RectTransform gIconRT = gIconObj.GetComponent<RectTransform>();
        gIconRT.anchoredPosition = new Vector2(-95, 22);
        gIconRT.sizeDelta = new Vector2(36, 36);
        Image gIconImg = gIconObj.AddComponent<Image>();
        gIconImg.sprite = spGrapple;

        GameObject gTextObj = CreateUIElement("GrappleText", toolPanel.transform);
        RectTransform gTextRT = gTextObj.GetComponent<RectTransform>();
        gTextRT.anchoredPosition = new Vector2(25, 22);
        gTextRT.sizeDelta = new Vector2(180, 32);
        TextMeshProUGUI gTMP = gTextObj.AddComponent<TextMeshProUGUI>();
        gTMP.text = "GANCHO [Q / X]";
        gTMP.fontSize = 17;
        gTMP.fontStyle = FontStyles.Bold;
        gTMP.alignment = TextAlignmentOptions.Left;

        GameObject vIconObj = CreateUIElement("VacuumIcon", toolPanel.transform);
        RectTransform vIconRT = vIconObj.GetComponent<RectTransform>();
        vIconRT.anchoredPosition = new Vector2(-95, -22);
        vIconRT.sizeDelta = new Vector2(36, 36);
        Image vIconImg = vIconObj.AddComponent<Image>();
        vIconImg.sprite = spVacuum;

        GameObject vTextObj = CreateUIElement("VacuumText", toolPanel.transform);
        RectTransform vTextRT = vTextObj.GetComponent<RectTransform>();
        vTextRT.anchoredPosition = new Vector2(25, -22);
        vTextRT.sizeDelta = new Vector2(180, 32);
        TextMeshProUGUI vTMP = vTextObj.AddComponent<TextMeshProUGUI>();
        vTMP.text = "ASPIRADORA [Q / LMB]";
        vTMP.fontSize = 17;
        vTMP.fontStyle = FontStyles.Bold;
        vTMP.alignment = TextAlignmentOptions.Left;

        GameObject ammoObj = CreateUIElement("AmmoText", toolPanel.transform);
        RectTransform ammoRT = ammoObj.GetComponent<RectTransform>();
        ammoRT.anchoredPosition = new Vector2(75, -22);
        ammoRT.sizeDelta = new Vector2(80, 24);
        TextMeshProUGUI ammoTMP = ammoObj.AddComponent<TextMeshProUGUI>();
        ammoTMP.text = "0 / 1";
        ammoTMP.fontSize = 15;
        ammoTMP.alignment = TextAlignmentOptions.Right;
        ammoTMP.color = new Color(0.3f, 0.9f, 1f);

        // 6. Guía de Controles
        GameObject guidePanel = CreatePanel("GuidePanel", canvasObj.transform, new Vector2(-30, 30), new Vector2(280, 130), new Vector2(1, 0), new Vector2(1, 0), spPanel);
        GameObject guideTextObj = CreateUIElement("GuideText", guidePanel.transform);
        RectTransform guideRT = guideTextObj.GetComponent<RectTransform>();
        guideRT.sizeDelta = new Vector2(250, 110);
        TextMeshProUGUI guideTMP = guideTextObj.AddComponent<TextMeshProUGUI>();
        guideTMP.text = "<color=#FFE066>WASD</color> : Moverse  |  <color=#FFE066>ESPACIO</color> : Saltar\n<color=#FFE066>SHIFT</color> : Correr  |  <color=#FFE066>Q</color> : Cambiar Herramienta\n<color=#66DDFF>CLIC IZQ</color> : Aspirar Núcleos\n<color=#66DDFF>CLIC DER</color> : Disparar Proyectil\n<color=#FFAA33>X / BOTÓN</color> : Disparar Gancho";
        guideTMP.fontSize = 13.5f;
        guideTMP.lineSpacing = 8f;
        guideTMP.alignment = TextAlignmentOptions.Center;

        // 7. Toast Banner
        GameObject toastObj = CreateUIElement("ToastBanner", canvasObj.transform);
        RectTransform toastRT = toastObj.GetComponent<RectTransform>();
        toastRT.anchorMin = new Vector2(0.5f, 1);
        toastRT.anchorMax = new Vector2(0.5f, 1);
        toastRT.anchoredPosition = new Vector2(0, -90);
        toastRT.sizeDelta = new Vector2(650, 50);
        CanvasGroup toastCG = toastObj.AddComponent<CanvasGroup>();
        toastCG.alpha = 0f;
        TextMeshProUGUI toastTMP = toastObj.AddComponent<TextMeshProUGUI>();
        toastTMP.text = "";
        toastTMP.fontSize = 24;
        toastTMP.fontStyle = FontStyles.Bold;
        toastTMP.alignment = TextAlignmentOptions.Center;
        toastTMP.color = new Color(0.3f, 1f, 0.6f);

        // 8. Pantalla de Victoria
        GameObject victoryPanel = CreatePanel("VictoryPanel", canvasObj.transform, Vector2.zero, new Vector2(540, 380), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), spPanel);
        GameObject vTitleObj = CreateUIElement("VictoryTitle", victoryPanel.transform);
        RectTransform vTitleRT = vTitleObj.GetComponent<RectTransform>();
        vTitleRT.anchoredPosition = new Vector2(0, 120);
        vTitleRT.sizeDelta = new Vector2(480, 60);
        TextMeshProUGUI vTitleTMP = vTitleObj.AddComponent<TextMeshProUGUI>();
        vTitleTMP.text = "¡VICTORIA!";
        vTitleTMP.fontSize = 46;
        vTitleTMP.fontStyle = FontStyles.Bold;
        vTitleTMP.alignment = TextAlignmentOptions.Center;
        vTitleTMP.color = new Color(1f, 0.85f, 0.2f);

        GameObject vSubObj = CreateUIElement("VictorySubtitle", victoryPanel.transform);
        RectTransform vSubRT = vSubObj.GetComponent<RectTransform>();
        vSubRT.anchoredPosition = new Vector2(0, 75);
        vSubRT.sizeDelta = new Vector2(480, 40);
        TextMeshProUGUI vSubTMP = vSubObj.AddComponent<TextMeshProUGUI>();
        vSubTMP.text = "Nivel 1: La Senda Celeste Completado";
        vSubTMP.fontSize = 20;
        vSubTMP.alignment = TextAlignmentOptions.Center;
        vSubTMP.color = new Color(0.85f, 0.85f, 0.9f);

        GameObject vStarsObj = CreateUIElement("VictoryStars", victoryPanel.transform);
        RectTransform vStarsRT = vStarsObj.GetComponent<RectTransform>();
        vStarsRT.anchoredPosition = new Vector2(0, 15);
        vStarsRT.sizeDelta = new Vector2(400, 40);
        TextMeshProUGUI vStarsTMP = vStarsObj.AddComponent<TextMeshProUGUI>();
        vStarsTMP.text = "Estrellas: 3 / 3";
        vStarsTMP.fontSize = 26;
        vStarsTMP.fontStyle = FontStyles.Bold;
        vStarsTMP.alignment = TextAlignmentOptions.Center;
        vStarsTMP.color = new Color(1f, 0.92f, 0.4f);

        GameObject vTimeObj = CreateUIElement("VictoryTime", victoryPanel.transform);
        RectTransform vTimeRT = vTimeObj.GetComponent<RectTransform>();
        vTimeRT.anchoredPosition = new Vector2(0, -35);
        vTimeRT.sizeDelta = new Vector2(400, 40);
        TextMeshProUGUI vTimeTMP = vTimeObj.AddComponent<TextMeshProUGUI>();
        vTimeTMP.text = "Tiempo: 00:00";
        vTimeTMP.fontSize = 22;
        vTimeTMP.alignment = TextAlignmentOptions.Center;
        vTimeTMP.color = Color.white;

        GameObject btnObj = CreatePanel("RestartButton", victoryPanel.transform, new Vector2(0, -110), new Vector2(240, 55), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), spPanel);
        Button restartBtn = btnObj.AddComponent<Button>();
        Image btnImg = btnObj.GetComponent<Image>();
        btnImg.color = new Color(0.2f, 0.5f, 0.9f, 0.95f);
        GameObject btnTextObj = CreateUIElement("BtnText", btnObj.transform);
        RectTransform btnTextRT = btnTextObj.GetComponent<RectTransform>();
        btnTextRT.sizeDelta = new Vector2(220, 50);
        TextMeshProUGUI btnTMP = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnTMP.text = "JUGAR DE NUEVO";
        btnTMP.fontSize = 20;
        btnTMP.fontStyle = FontStyles.Bold;
        btnTMP.alignment = TextAlignmentOptions.Center;
        btnTMP.color = Color.white;

        // 9. Pantalla de Game Over
        GameObject gameOverPanel = CreatePanel("GameOverPanel", canvasObj.transform, Vector2.zero, new Vector2(460, 200), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), spPanel);
        GameObject goTextObj = CreateUIElement("GOText", gameOverPanel.transform);
        RectTransform goTextRT = goTextObj.GetComponent<RectTransform>();
        goTextRT.sizeDelta = new Vector2(420, 160);
        TextMeshProUGUI goTMP = goTextObj.AddComponent<TextMeshProUGUI>();
        goTMP.text = "<color=#FF3344><size=38><b>HAS CAÍDO</b></size></color>\n\n<size=20>Reapareciendo en el último punto de control...</size>";
        goTMP.alignment = TextAlignmentOptions.Center;

        SerializedObject soUI = new SerializedObject(uiCtrl);
        soUI.FindProperty("playerController").objectReferenceValue = playerCtrl;
        soUI.FindProperty("vacuum").objectReferenceValue = vacuum;
        soUI.FindProperty("gameController").objectReferenceValue = gameCtrl;

        SerializedProperty spHearts = soUI.FindProperty("heartImages");
        spHearts.arraySize = 3;
        for (int i = 0; i < 3; i++) spHearts.GetArrayElementAtIndex(i).objectReferenceValue = hearts[i];

        soUI.FindProperty("heartFullSprite").objectReferenceValue = spHeartFull;
        soUI.FindProperty("heartEmptySprite").objectReferenceValue = spHeartEmpty;

        soUI.FindProperty("grappleIcon").objectReferenceValue = gIconImg;
        soUI.FindProperty("vacuumIcon").objectReferenceValue = vIconImg;
        soUI.FindProperty("grappleLabel").objectReferenceValue = gTMP;
        soUI.FindProperty("vacuumLabel").objectReferenceValue = vTMP;
        soUI.FindProperty("ammoText").objectReferenceValue = ammoTMP;

        soUI.FindProperty("starsText").objectReferenceValue = sTMP;
        soUI.FindProperty("damageVignette").objectReferenceValue = vigImg;

        soUI.FindProperty("toastText").objectReferenceValue = toastTMP;
        soUI.FindProperty("toastGroup").objectReferenceValue = toastCG;

        soUI.FindProperty("victoryPanel").objectReferenceValue = victoryPanel;
        soUI.FindProperty("victoryStarsText").objectReferenceValue = vStarsTMP;
        soUI.FindProperty("victoryTimeText").objectReferenceValue = vTimeTMP;
        soUI.FindProperty("restartButton").objectReferenceValue = restartBtn;

        soUI.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
        soUI.ApplyModifiedProperties();
    }

    private static GameObject CreateUIElement(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 anchoredPos, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Sprite sprite)
    {
        GameObject go = CreateUIElement(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        return go;
    }

    /// <summary>
    /// Crea un suelo perfectamente plano a una cota Y exacta con BoxCollider 100% plano.
    /// Garantiza que el CharacterController nunca se trabe en aristas ni uniones.
    /// </summary>
    private static GameObject CreateSolidFloor(string name, Vector3 surfaceCenterPos, Vector3 size, Material mat, Transform parent)
    {
        GameObject floor = new GameObject(name);
        floor.transform.SetParent(parent, false);
        floor.transform.position = surfaceCenterPos;

        // Visual: Se ubica de la superficie hacia abajo para mantener la cota Y
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "FloorVisual";
        visual.transform.SetParent(floor.transform, false);
        visual.transform.localScale = size;
        visual.transform.localPosition = new Vector3(0, -size.y * 0.5f, 0);
        visual.GetComponent<Renderer>().material = mat != null ? mat : matStone;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        // Colisionador plano
        BoxCollider col = floor.AddComponent<BoxCollider>();
        col.size = size;
        col.center = new Vector3(0, -size.y * 0.5f, 0);

        return floor;
    }

    /// <summary>
    /// Crea una rampa de transición perfectamente suave entre dos alturas con BoxCollider inclinado.
    /// Elimina por completo los atascos de escalones en el CharacterController.
    /// </summary>
    private static GameObject CreateSmoothRamp(string name, Vector3 bottomPos, Vector3 topPos, float width, float thickness, Material mat, Transform parent)
    {
        GameObject ramp = new GameObject(name);
        ramp.transform.SetParent(parent, false);

        Vector3 mid = (bottomPos + topPos) * 0.5f;
        ramp.transform.position = mid;

        Vector3 delta = topPos - bottomPos;
        float horizontalDist = Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z);
        float hypotenuse = Mathf.Sqrt(horizontalDist * horizontalDist + delta.y * delta.y);
        float angleX = Mathf.Atan2(delta.y, horizontalDist) * Mathf.Rad2Deg;

        Quaternion rot = Quaternion.LookRotation(new Vector3(delta.x, 0, delta.z)) * Quaternion.Euler(-angleX, 0, 0);
        ramp.transform.rotation = rot;

        Vector3 rampSize = new Vector3(width, thickness, hypotenuse);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "RampVisual";
        visual.transform.SetParent(ramp.transform, false);
        visual.transform.localScale = rampSize;
        visual.transform.localPosition = new Vector3(0, -thickness * 0.5f, 0);
        visual.GetComponent<Renderer>().material = mat != null ? mat : matStoneDark;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        BoxCollider col = ramp.AddComponent<BoxCollider>();
        col.size = rampSize;
        col.center = new Vector3(0, -thickness * 0.5f, 0);

        return ramp;
    }

    private static GameObject CreateRailing(string name, Vector3 bottomPos, Vector3 size, Transform parent)
    {
        GameObject rail = new GameObject(name);
        rail.transform.SetParent(parent, false);
        rail.transform.position = bottomPos;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "RailVisual";
        visual.transform.SetParent(rail.transform, false);
        visual.transform.localScale = size;
        visual.transform.localPosition = new Vector3(0, size.y * 0.5f, 0);
        visual.GetComponent<Renderer>().material = matStoneDark;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        BoxCollider col = rail.AddComponent<BoxCollider>();
        col.size = size;
        col.center = new Vector3(0, size.y * 0.5f, 0);

        return rail;
    }

    private static GameObject CreatePillar(string name, Vector3 bottomPos, float height, float radius, Material mat, Transform parent)
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = name;
        pillar.transform.SetParent(parent, false);
        pillar.transform.position = new Vector3(bottomPos.x, bottomPos.y + height * 0.5f, bottomPos.z);
        pillar.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        pillar.GetComponent<Renderer>().material = mat != null ? mat : matStoneDark;
        return pillar;
    }

    private static GameObject CreateArchway(string name, Vector3 bottomPos, float openingWidth, float height, Transform parent)
    {
        GameObject arch = new GameObject(name);
        arch.transform.SetParent(parent, false);
        arch.transform.position = bottomPos;

        float columnRadius = 0.6f;
        float halfW = (openingWidth * 0.5f) + columnRadius;

        CreatePillar("ColumnaIzq", bottomPos + new Vector3(-halfW, 0, 0), height, columnRadius, matStoneDark, arch.transform);
        CreatePillar("ColumnaDer", bottomPos + new Vector3(halfW, 0, 0), height, columnRadius, matStoneDark, arch.transform);

        // Viga superior
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "VigaSuperior";
        beam.transform.SetParent(arch.transform, false);
        beam.transform.position = bottomPos + new Vector3(0, height + 0.5f, 0);
        beam.transform.localScale = new Vector3(openingWidth + columnRadius * 4f, 1.0f, columnRadius * 2.5f);
        beam.GetComponent<Renderer>().material = matStoneDark;

        return arch;
    }

    private static void BuildLevelArchitecture(Level1Manager levelMgr, GameController gameCtrl)
    {
        GameObject envRoot = new GameObject("Environment");

        // ==============================================================
        // ZONA 1: EL GRAN ATRIO DE INICIO (TUTORIAL Y APRENDIZAJE)
        // Elevación uniforme a Y = 0.0 para fluidez total
        // ==============================================================
        GameObject zone1 = new GameObject("Zona1_AtrioDeInicio");
        zone1.transform.SetParent(envRoot.transform, false);

        // Suelo principal del Atrio (12m ancho x 26m largo, Y = 0.0)
        CreateSolidFloor("PisoAtrio", new Vector3(0, 0f, 11f), new Vector3(12f, 2f, 26f), matStone, zone1.transform);

        // Muros protectores laterales (evitan caídas accidentales al inicio)
        CreateRailing("MuroIzquierdo", new Vector3(-6f, 0f, 11f), new Vector3(0.6f, 1.4f, 26f), zone1.transform);
        CreateRailing("MuroDerecho", new Vector3(6f, 0f, 11f), new Vector3(0.6f, 1.4f, 26f), zone1.transform);
        CreateRailing("MuroFondoAtras", new Vector3(0, 0f, -2f), new Vector3(12f, 1.4f, 0.6f), zone1.transform);

        // Punto de reaparición inicial
        GameObject spawnPt = new GameObject("InitialSpawnPoint");
        spawnPt.transform.position = new Vector3(0, 1.2f, 1f);
        spawnPt.transform.SetParent(zone1.transform, false);

        SerializedObject soMgr = new SerializedObject(levelMgr);
        soMgr.FindProperty("initialSpawnPoint").objectReferenceValue = spawnPt.transform;
        soMgr.ApplyModifiedProperties();
        gameCtrl.UpdateSpawnPoint(new Vector3(0, 1.2f, 1f));

        // Cartel flotante de bienvenida / objetivo principal
        CreateWorldSign("Sign_Bienvenida", new Vector3(0, 2.3f, 4f), "¡BIENVENIDO A LA SENDA CELESTE!", "Recoge las 3 Estrellas Celestiales y llega al Portal de la Cumbre.", zone1.transform);

        // Pedestal y ESTRELLA 1 (1/3) justo al frente del jugador
        CreateSolidFloor("PedestalEstrella1", new Vector3(0, 0.5f, 8f), new Vector3(3f, 1f, 3f), matStoneDark, zone1.transform);
        CreateStarObject("Star_1", new Vector3(0, 1.8f, 8f), zone1.transform);

        // Altar de Energía y Aspiradora (Izquierda, Z = 14)
        CreateSolidFloor("AltarEnergia", new Vector3(-4f, 0.5f, 14f), new Vector3(3f, 1f, 3f), matStoneDark, zone1.transform);
        GameObject corePrefab = CreatePoolableOrb("EnergyCore_1", new Vector3(-4f, 1.3f, 14f), zone1.transform);
        EnergyDispenser dispenser1 = CreateSolidFloor("Dispenser_1", new Vector3(-4f, 0.6f, 14f), new Vector3(1f, 0.2f, 1f), matStoneDark, zone1.transform).AddComponent<EnergyDispenser>();
        dispenser1.SetPrefab(corePrefab);

        CreateWorldSign("Sign_Aspiradora", new Vector3(-4f, 2.4f, 12f), "ASPIRADORA [Q / LMB]", "Mantén Clic Izquierdo para absorber núcleos.\nClic Derecho para dispararlos.", zone1.transform);

        // Gran Arco Triunfal Abierto (Paso libre y despejado para no trabar jamás al jugador)
        CreateArchway("ArcoTriunfal_1", new Vector3(0, 0f, 22f), 5.5f, 4.0f, zone1.transform);

        // Diana de Cristal Opcional en el arco: dispara para sonido y toast
        GameObject crystalTarget = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crystalTarget.name = "DianaDeCristal";
        crystalTarget.transform.SetParent(zone1.transform, false);
        crystalTarget.transform.position = new Vector3(0, 2.5f, 22f);
        crystalTarget.transform.localScale = new Vector3(0.9f, 1.3f, 0.9f);
        crystalTarget.GetComponent<Renderer>().material = matRuby;
        crystalTarget.AddComponent<GateCrystalTarget>();

        CreateWorldSign("Sign_Arco", new Vector3(0, 3.8f, 19f), "EL ARCO CELESTE", "Camina hacia el desfiladero. ¡Apunta al cristal rojo para practicar tu tiro!", zone1.transform);

        // PUNTO DE CONTROL 1 (Pasando el arco)
        CreateCheckpointTotem("Checkpoint_1", new Vector3(0, 0f, 26f), Vector3.zero, zone1.transform);

        // ==============================================================
        // ZONA 2: EL DESFILADERO DEL GANCHO Y EL FERRY MÓVIL
        // Dos caminos simultáneos: Gancho por arriba, o Plataforma Móvil segura por abajo
        // ==============================================================
        GameObject zone2 = new GameObject("Zona2_DesfiladeroGancho");
        zone2.transform.SetParent(envRoot.transform, false);

        // Terraza de salida (Z = 28 a Z = 34)
        CreateSolidFloor("TerrazaSalidaZ2", new Vector3(0, 0f, 31f), new Vector3(12f, 2f, 6f), matStone, zone2.transform);
        CreateWorldSign("Sign_Gancho", new Vector3(0, 2.4f, 31f), "2. EL DESFILADERO [Q / X]", "Usa el Gancho en el Farol Dorado para volar,\no espera al transbordador flotante para cruzar seguro.", zone2.transform);

        // Farol de Gancho celestial en el aire (Z = 45, Y = 7.5)
        CreateGrappleLantern("FarolGancho_Desfiladero", new Vector3(0, 7.5f, 45f), zone2.transform);

        // Transbordador móvil amplio y seguro (8m x 8m) navegando por el abismo
        GameObject movPlat = CreateSolidFloor("FerryMovil_Desfiladero", new Vector3(0, -0.4f, 37f), new Vector3(8f, 1f, 8f), matStoneDark, zone2.transform);
        MovingPlatform mpComp = movPlat.AddComponent<MovingPlatform>();
        SerializedObject soMP = new SerializedObject(mpComp);
        soMP.FindProperty("offset").vector3Value = new Vector3(0, 0, 16f); // Viaja de Z = 37 a Z = 53
        soMP.FindProperty("speed").floatValue = 3.2f;
        soMP.ApplyModifiedProperties();

        // Terraza de llegada amplia (Z = 56 a Z = 64)
        CreateSolidFloor("TerrazaLlegadaZ2", new Vector3(0, 0f, 60f), new Vector3(14f, 2f, 8f), matStone, zone2.transform);

        // ==============================================================
        // ZONA 3: LA TERRAZA DE LOS CENTINELAS (COMBATE Y DESTREZA)
        // Arena amplia y abierta con salida despejada; derrotar drones da Estrella 2
        // ==============================================================
        GameObject zone3 = new GameObject("Zona3_TerrazaCentinelas");
        zone3.transform.SetParent(envRoot.transform, false);

        // Plaza de combate amplia (16m ancho x 26m largo, Z = 66 a Z = 92)
        CreateSolidFloor("PlazaCombate", new Vector3(0, 0f, 79f), new Vector3(16f, 2f, 26f), matStone, zone3.transform);
        CreateRailing("BordeCombateIzq", new Vector3(-8f, 0f, 79f), new Vector3(0.6f, 1.2f, 26f), zone3.transform);
        CreateRailing("BordeCombateDer", new Vector3(8f, 0f, 79f), new Vector3(0.6f, 1.2f, 26f), zone3.transform);

        // 4 Dispensadores de núcleos en las 4 esquinas de la plaza (munición infinita garantizada)
        CreateSolidFloor("AltarEsquina1", new Vector3(-6.5f, 0.5f, 69f), new Vector3(2f, 1f, 2f), matStoneDark, zone3.transform).AddComponent<EnergyDispenser>().SetPrefab(corePrefab);
        CreateSolidFloor("AltarEsquina2", new Vector3(6.5f, 0.5f, 69f), new Vector3(2f, 1f, 2f), matStoneDark, zone3.transform).AddComponent<EnergyDispenser>().SetPrefab(corePrefab);
        CreateSolidFloor("AltarEsquina3", new Vector3(-6.5f, 0.5f, 89f), new Vector3(2f, 1f, 2f), matStoneDark, zone3.transform).AddComponent<EnergyDispenser>().SetPrefab(corePrefab);
        CreateSolidFloor("AltarEsquina4", new Vector3(6.5f, 0.5f, 89f), new Vector3(2f, 1f, 2f), matStoneDark, zone3.transform).AddComponent<EnergyDispenser>().SetPrefab(corePrefab);

        CreateWorldSign("Sign_Centinelas", new Vector3(0, 2.4f, 67f), "3. TERRAZA DE LOS CENTINELAS", "Los drones patrullan este templo. ¡Absorbe núcleos y dispáraselos\npara destruirlos y reclamar la Estrella 2!", zone3.transform);

        // 2 Drones Centinelas patrullando
        GameObject drone1Obj = CreateEnemyDrone("Centinela_Alfa", new Vector3(-4f, 2.8f, 80f), zone3.transform);
        GameObject drone2Obj = CreateEnemyDrone("Centinela_Beta", new Vector3(4f, 2.8f, 82f), zone3.transform);

        // Pedestal Central con ESTRELLA 2 (2/3) desbloqueable
        CreateSolidFloor("PedestalEstrella2", new Vector3(0, 0.5f, 79f), new Vector3(3f, 1f, 3f), matStoneDark, zone3.transform);
        GameObject star2Obj = new GameObject("Star_2_Container");
        star2Obj.transform.SetParent(zone3.transform, false);
        CreateStarObject("Star_2", new Vector3(0, 1.8f, 79f), star2Obj.transform);

        // Manager de recompensa de la Arena
        ArenaGateManager arenaMgr = zone3.AddComponent<ArenaGateManager>();
        SerializedObject soArena = new SerializedObject(arenaMgr);
        SerializedProperty spEnemies = soArena.FindProperty("enemies");
        spEnemies.arraySize = 2;
        spEnemies.GetArrayElementAtIndex(0).objectReferenceValue = drone1Obj.GetComponent<BasicEnemy>();
        spEnemies.GetArrayElementAtIndex(1).objectReferenceValue = drone2Obj.GetComponent<BasicEnemy>();
        soArena.FindProperty("starReward").objectReferenceValue = star2Obj;
        soArena.ApplyModifiedProperties();

        // Gran Arco de Salida Abierto hacia Zona 4 (sin rejas ni puertas que atrapen al jugador)
        CreateArchway("ArcoSalidaCombate", new Vector3(0, 0f, 92f), 6.0f, 4.2f, zone3.transform);

        // PUNTO DE CONTROL 2 (Pasando la terraza de combate)
        CreateCheckpointTotem("Checkpoint_2", new Vector3(0, 0f, 96f), Vector3.zero, zone3.transform);

        // ==============================================================
        // ZONA 4: LA SENDA DEL EQUILIBRIO Y PELIGROS (PLATAFORMEO)
        // Plataformas generosas con huecos de 2m (fáciles de saltar) y puente con trampas
        // ==============================================================
        GameObject zone4 = new GameObject("Zona4_SendaEquilibrio");
        zone4.transform.SetParent(envRoot.transform, false);

        CreateWorldSign("Sign_Equilibrio", new Vector3(0, 2.4f, 99f), "4. LA SENDA DEL EQUILIBRIO", "Salta con ritmo sobre las plataformas quebradizas.\n¡Cuidado con los pinchos y la cuchilla giratoria!", zone4.transform);

        // Plataforma base sólida de salto (8m x 6m, Z = 104)
        CreateSolidFloor("PlataformaInicioSalto", new Vector3(0, 0f, 104f), new Vector3(8f, 2f, 6f), matStone, zone4.transform);

        // 2 Plataformas Quebradizas amplias (6m ancho x 4m largo), huecos de solo 2m a cota Y = 0.0
        CreateCrumblingSolidPlatform("PlataformaQuebradiza_1", new Vector3(0, 0f, 111f), new Vector3(6f, 0.8f, 4f), zone4.transform);
        CreateCrumblingSolidPlatform("PlataformaQuebradiza_2", new Vector3(0, 0f, 117f), new Vector3(6f, 0.8f, 4f), zone4.transform);

        // Gran Puente Sólido con Peligros (8m ancho x 22m largo, Z = 123 a Z = 145)
        CreateSolidFloor("GranPuentePeligros", new Vector3(0, 0f, 134f), new Vector3(8f, 2f, 22f), matStone, zone4.transform);

        // Trampas de Pinchos a los lados del puente (dejando un pasillo central seguro de 3m)
        CreateSpikeHazard("PinchosIzq", new Vector3(-2.6f, 0.1f, 130f), new Vector3(2.2f, 0.3f, 6f), zone4.transform);
        CreateSpikeHazard("PinchosDer", new Vector3(2.6f, 0.1f, 130f), new Vector3(2.2f, 0.3f, 6f), zone4.transform);

        // Cuchilla Giratoria elevada en el puente (Z = 138)
        GameObject rotAxle = new GameObject("CuchillaGiratoria");
        rotAxle.transform.SetParent(zone4.transform, false);
        rotAxle.transform.position = new Vector3(0, 1.4f, 138f);
        rotAxle.AddComponent<RotatingHazard>();

        GameObject rotBlade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rotBlade.name = "BladeVisual";
        rotBlade.transform.SetParent(rotAxle.transform, false);
        rotBlade.transform.localScale = new Vector3(6.5f, 0.4f, 0.4f);
        rotBlade.GetComponent<Renderer>().material = matHazardMetal;
        rotBlade.GetComponent<BoxCollider>().isTrigger = true;
        rotBlade.AddComponent<Hazard>();

        // Ruta Secreta Opcional: ESTRELLA 3 (3/3)
        CreateGrappleLantern("FarolGancho_Secreto", new Vector3(9f, 6.0f, 134f), zone4.transform);
        CreateSolidFloor("IslaSecretaEstrella3", new Vector3(13f, 0f, 134f), new Vector3(6f, 2f, 6f), matStoneDark, zone4.transform);
        CreateStarObject("Star_3", new Vector3(13f, 1.8f, 134f), zone4.transform);

        // ==============================================================
        // ZONA 5: LA CUMBRE CELESTE Y PORTAL DE LA VICTORIA (VICTORIA)
        // Rampa suave 100% transitable sin atascos ascendiendo a la gran plaza
        // ==============================================================
        GameObject zone5 = new GameObject("Zona5_CumbreVictoria");
        zone5.transform.SetParent(envRoot.transform, false);

        // Rampa suave continua ascendiendo de Y = 0.0 a Y = 3.5 (Z = 146 a Z = 164)
        CreateSmoothRamp("RampaCeremonial", new Vector3(0, 0f, 146f), new Vector3(0, 3.5f, 164f), 10f, 1.5f, matStoneDark, zone5.transform);

        // Gran Plaza del Portal de la Victoria (16m x 18m a Y = 3.5f, Z = 165 a Z = 183)
        CreateSolidFloor("PlazaVictoria", new Vector3(0, 3.5f, 174f), new Vector3(16f, 2f, 18f), matStone, zone5.transform);
        CreateRailing("VictoriaBordeIzq", new Vector3(-8f, 3.5f, 174f), new Vector3(0.6f, 1.4f, 18f), zone5.transform);
        CreateRailing("VictoriaBordeDer", new Vector3(8f, 3.5f, 174f), new Vector3(0.6f, 1.4f, 18f), zone5.transform);
        CreateRailing("VictoriaBordeFondo", new Vector3(0, 3.5f, 183f), new Vector3(16f, 1.4f, 0.6f), zone5.transform);

        // Altar Ceremonial de la Victoria
        GameObject altar = CreateMeshObject("AltarVisual", MODEL_ALTAR, matStoneDark, new Vector3(0, 3.7f, 177f), Vector3.zero, new Vector3(1.6f, 1.6f, 1.6f), false, 0, zone5.transform);

        // Vórtice luminoso del Portal de Victoria
        GameObject vortex = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        vortex.name = "PortalVortexDisc";
        vortex.transform.SetParent(zone5.transform, false);
        vortex.transform.position = new Vector3(0, 7.2f, 177f);
        vortex.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        vortex.transform.localScale = new Vector3(3.5f, 0.05f, 3.5f);
        vortex.GetComponent<Renderer>().material = matPortal;
        Object.DestroyImmediate(vortex.GetComponent<Collider>());

        // Luz celestial del portal
        GameObject portalLight = new GameObject("PortalLight");
        portalLight.transform.SetParent(zone5.transform, false);
        portalLight.transform.position = new Vector3(0, 7.2f, 176f);
        Light pl = portalLight.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.color = new Color(0.2f, 0.85f, 1f);
        pl.range = 15f;
        pl.intensity = 3f;

        // Trigger de Victoria amplio (6m x 6m x 6m)
        GameObject vicGoal = new GameObject("VictoryGoalTrigger");
        vicGoal.transform.SetParent(zone5.transform, false);
        vicGoal.transform.position = new Vector3(0, 5.5f, 177f);
        BoxCollider vicCol = vicGoal.AddComponent<BoxCollider>();
        vicCol.size = new Vector3(6f, 6f, 6f);
        vicCol.isTrigger = true;
        vicGoal.AddComponent<VictoryGoal>();

        CreateWorldSign("Sign_Victoria", new Vector3(0, 6.2f, 169f), "¡PORTAL DE LA CUMBRE!", "Entra al portal luminoso para completar el Nivel 1 y reclamar tu victoria.", zone5.transform);

        // ==============================================================
        // ESCENOGRAFÍA DE FONDO (Islas Flotantes Distantes)
        // ==============================================================
        GameObject scenery = new GameObject("Scenery_DistantIslands");
        scenery.transform.SetParent(envRoot.transform, false);

        CreateMeshObject("IslaDistante1", MODEL_ISLAND, matStoneDark, new Vector3(-55f, -10f, 70f), new Vector3(0, 35f, 0), new Vector3(0.06f, 0.06f, 0.06f), false, 0, scenery.transform);
        CreateMeshObject("IslaDistante2", MODEL_ISLAND, matStoneDark, new Vector3(60f, -5f, 110f), new Vector3(0, -50f, 0), new Vector3(0.08f, 0.08f, 0.08f), false, 0, scenery.transform);
        CreateMeshObject("IslaDistante3", MODEL_ISLAND, matStoneDark, new Vector3(-45f, 15f, 160f), new Vector3(0, 80f, 0), new Vector3(0.09f, 0.09f, 0.09f), false, 0, scenery.transform);
        CreateMeshObject("IslaDistante4", MODEL_ISLAND, matStoneDark, new Vector3(45f, 25f, 185f), new Vector3(0, 15f, 0), new Vector3(0.12f, 0.12f, 0.12f), false, 0, scenery.transform);
    }

    private static void CreateSpikeHazard(string name, Vector3 pos, Vector3 size, Transform parent)
    {
        GameObject spikes = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spikes.name = name;
        spikes.transform.SetParent(parent, false);
        spikes.transform.position = pos;
        spikes.transform.localScale = size;
        spikes.GetComponent<Renderer>().material = matRuby;
        spikes.GetComponent<BoxCollider>().isTrigger = true;
        spikes.AddComponent<Hazard>();
    }

    private static void CreateWorldSign(string name, Vector3 pos, string title, string instruction, Transform parent)
    {
        GameObject signObj = new GameObject(name);
        signObj.transform.SetParent(parent, false);
        signObj.transform.position = pos;

        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "SignBackground";
        bg.transform.SetParent(signObj.transform, false);
        bg.transform.localScale = new Vector3(4.8f, 2.0f, 1f);
        bg.GetComponent<Renderer>().material = matStoneDark;
        Object.DestroyImmediate(bg.GetComponent<Collider>());

        GameObject textObj = new GameObject("SignText");
        textObj.transform.SetParent(signObj.transform, false);
        textObj.transform.localPosition = new Vector3(0, 0, -0.05f);

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = $"<color=#FFE066><b>{title}</b></color>\n<size=75%>{instruction}</size>";
        tmp.fontSize = 4.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(4.6f, 1.8f);

        signObj.AddComponent<WorldFloatingSign>().SetText(title, instruction);
    }

    private static void CreateCrumblingSolidPlatform(string name, Vector3 pos, Vector3 size, Transform parent)
    {
        GameObject plat = new GameObject(name);
        plat.transform.SetParent(parent, false);
        plat.transform.position = pos;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "PlatformVisual";
        visual.transform.SetParent(plat.transform, false);
        visual.transform.localScale = size;
        visual.transform.localPosition = new Vector3(0, -size.y * 0.5f, 0);
        visual.GetComponent<Renderer>().material = matStoneDark;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        BoxCollider col = plat.AddComponent<BoxCollider>();
        col.size = size;
        col.center = new Vector3(0, -size.y * 0.5f, 0);

        // Trigger de detección de contacto
        BoxCollider trig = plat.AddComponent<BoxCollider>();
        trig.size = new Vector3(size.x * 0.98f, 0.4f, size.z * 0.98f);
        trig.center = new Vector3(0, 0.2f, 0);
        trig.isTrigger = true;

        plat.AddComponent<CrumblingPlatform>();
    }

    private static void CreateGrappleLantern(string name, Vector3 pos, Transform parent)
    {
        int grappleLayer = LayerMask.NameToLayer("Graple");

        GameObject lantern = new GameObject(name);
        lantern.transform.SetParent(parent, false);
        lantern.transform.position = pos;
        lantern.layer = grappleLayer;

        // Visual del farol
        GameObject visual = CreateMeshObject("LanternVisual", MODEL_LANTERN, matGold, pos, Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f), false, grappleLayer, lantern.transform);
        visual.transform.localPosition = Vector3.zero;

        // Cristal central luminoso
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "LanternCore";
        core.transform.SetParent(lantern.transform, false);
        core.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
        core.GetComponent<Renderer>().material = matCyan;
        Object.DestroyImmediate(core.GetComponent<Collider>());

        // Colisionador generoso para anclaje fácil
        BoxCollider col = lantern.AddComponent<BoxCollider>();
        col.size = new Vector3(2.5f, 2.5f, 2.5f);

        // Luz de farol
        GameObject lightObj = new GameObject("LanternLight");
        lightObj.transform.SetParent(lantern.transform, false);
        Light l = lightObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.2f, 0.8f, 1f);
        l.range = 10f;
        l.intensity = 2.5f;
    }

    private static void CreateCheckpointTotem(string name, Vector3 pos, Vector3 rot, Transform parent)
    {
        GameObject totem = new GameObject(name);
        totem.transform.SetParent(parent, false);
        totem.transform.position = pos;
        totem.transform.rotation = Quaternion.Euler(rot);

        CreateMeshObject("TotemVisual", MODEL_TOTEM, matStoneDark, pos, rot, new Vector3(1.2f, 1.2f, 1.2f), false, 0, totem.transform).transform.localPosition = Vector3.zero;

        // Cristal del totem
        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crystal.name = "TotemCrystal";
        crystal.transform.SetParent(totem.transform, false);
        crystal.transform.localPosition = new Vector3(0, 2.2f, 0);
        crystal.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
        Renderer crRend = crystal.GetComponent<Renderer>();
        crRend.material = matRuby;
        Object.DestroyImmediate(crystal.GetComponent<Collider>());

        // Trigger de activación
        BoxCollider col = totem.AddComponent<BoxCollider>();
        col.size = new Vector3(3.5f, 4f, 3.5f);
        col.center = new Vector3(0, 1.8f, 0);
        col.isTrigger = true;

        Checkpoint cp = totem.AddComponent<Checkpoint>();
        SerializedObject soCP = new SerializedObject(cp);
        soCP.FindProperty("crystalRenderer").objectReferenceValue = crRend;
        soCP.FindProperty("activeMaterial").objectReferenceValue = matEmerald;
        soCP.ApplyModifiedProperties();
    }

    private static void CreateStarObject(string name, Vector3 pos, Transform parent)
    {
        GameObject star = new GameObject(name);
        star.transform.SetParent(parent, false);
        star.transform.position = pos;
        star.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

        GameObject orbVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbVisual.name = "StarVisual";
        orbVisual.transform.SetParent(star.transform, false);
        orbVisual.transform.localScale = Vector3.one;
        orbVisual.GetComponent<Renderer>().material = matGold;
        Object.DestroyImmediate(orbVisual.GetComponent<Collider>());

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "StarRing";
        ring.transform.SetParent(star.transform, false);
        ring.transform.localScale = new Vector3(1.7f, 0.04f, 1.7f);
        ring.transform.localRotation = Quaternion.Euler(45f, 45f, 0);
        ring.GetComponent<Renderer>().material = matGold;
        Object.DestroyImmediate(ring.GetComponent<Collider>());

        SphereCollider col = star.AddComponent<SphereCollider>();
        col.radius = 1.2f;
        col.isTrigger = true;

        star.AddComponent<StarController>();
        star.AddComponent<RotatingHazard>();
    }

    private static GameObject CreatePoolableOrb(string name, Vector3 pos, Transform parent)
    {
        int poolableLayer = LayerMask.NameToLayer("Poolable");
        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = name;
        orb.transform.SetParent(parent, false);
        orb.transform.position = pos;
        orb.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        orb.layer = poolableLayer;

        orb.GetComponent<Renderer>().material = matCyan;

        Rigidbody rb = orb.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.5f;

        orb.AddComponent<PoolableController>();
        return orb;
    }

    private static GameObject CreateEnemyDrone(string name, Vector3 pos, Transform parent)
    {
        GameObject drone = new GameObject(name);
        drone.transform.SetParent(parent, false);
        drone.transform.position = pos;

        GameObject visual = CreateMeshObject("DroneVisual", MODEL_DRONE, matEnemyArmor, pos, Vector3.zero, new Vector3(1.1f, 1.1f, 1.1f), false, 0, drone.transform);
        visual.transform.localPosition = Vector3.zero;

        // Núcleo energético del dron
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "DroneCore";
        core.transform.SetParent(drone.transform, false);
        core.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        core.GetComponent<Renderer>().material = matRuby;
        Object.DestroyImmediate(core.GetComponent<Collider>());

        SphereCollider col = drone.AddComponent<SphereCollider>();
        col.radius = 0.9f;

        BasicEnemy be = drone.AddComponent<BasicEnemy>();
        SerializedObject soBE = new SerializedObject(be);
        soBE.FindProperty("mainRenderer").objectReferenceValue = visual.GetComponent<Renderer>();
        soBE.ApplyModifiedProperties();

        return drone;
    }

    private static GameObject CreateMeshObject(string name, string modelPath, Material mat, Vector3 pos, Vector3 rot, Vector3 scale, bool addCollider, int layer, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        GameObject go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(rot);
        go.transform.localScale = scale;
        if (layer != 0) go.layer = layer;

        if (mat != null)
        {
            Renderer[] rends = go.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in rends) r.material = mat;
        }

        if (!addCollider)
        {
            Collider[] cols = go.GetComponentsInChildren<Collider>();
            foreach (Collider c in cols) Object.DestroyImmediate(c);
        }

        return go;
    }

    private static void RegisterSceneInBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool found = false;
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == SCENE_PATH)
            {
                scenes[i] = new EditorBuildSettingsScene(SCENE_PATH, true);
                found = true;
                break;
            }
        }
        if (!found)
        {
            scenes.Add(new EditorBuildSettingsScene(SCENE_PATH, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
