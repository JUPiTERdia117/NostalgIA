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
    private const string TMPFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Tools/Setup Tutorial Level (Safe Refresh)")]
    public static void SetupTutorialScene()
    {
        Debug.Log("[TutorialSetup] Actualizando componentes seguros de la escena de tutorial: " + ScenePath);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Material matLava = AssetDatabase.LoadAssetAtPath<Material>($"{TutorialMaterialsPath}/Tutorial_Lava.mat");
        Material matPortal = AssetDatabase.LoadAssetAtPath<Material>($"{TutorialMaterialsPath}/Tutorial_Portal.mat");
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TMPFontPath);

        Shader blueCodeShader = Shader.Find("Tutorial/BlueCodeLava") ?? Resources.Load<Shader>("BlueCodeLava");
        if (matLava != null && blueCodeShader != null)
        {
            matLava.shader = blueCodeShader;
            EditorUtility.SetDirty(matLava);
        }

        SetupLavaHazards(matLava);
        SetupKillZone();
        SetupDoorGoal(matPortal);
        GameController gameController = SetupGameControllerAndUI(fontAsset);
        SetupCheckpoints();
        SetupPlayer(gameController);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[TutorialSetup] ¡El tutorial se ha actualizado exitosamente!");
    }

    private static void SetupLavaHazards(Material matLava)
    {
        int count = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name == "Floor_Courtyard" || go.name == "Lava_FinalHallway" || go.name == "Lava" || go.name.StartsWith("Lava "))
            {
                count++;
                go.tag = "Lava";

                if (matLava != null)
                {
                    var rend = go.GetComponent<Renderer>();
                    if (rend != null) rend.sharedMaterial = matLava;
                }

                LavaHazard haz = go.GetComponent<LavaHazard>();
                if (haz == null) haz = go.AddComponent<LavaHazard>();

                CreateOrUpdateLavaGlow(go, "LavaGlow", new Vector3(0, 0.8f, 0), 16f, 2.8f);
            }
        }
        Debug.Log($"[TutorialSetup] {count} piscinas azules de código activas configuradas.");
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
        light.color = new Color(0.05f, 0.45f, 1.0f);
        light.range = range;
        light.intensity = intensity;
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
        }
    }

    private static GameController SetupGameControllerAndUI(TMP_FontAsset fontAsset)
    {
        GameObject gcObj = GameObject.Find("GameController");
        if (gcObj == null) gcObj = new GameObject("GameController");

        GameController gc = gcObj.GetComponent<GameController>();
        if (gc == null) gc = gcObj.AddComponent<GameController>();

        // Eliminar canvas duplicado si existe
        GameObject oldDup = GameObject.Find("Tutorial_Canvas");
        if (oldDup != null) Object.DestroyImmediate(oldDup);

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

        for (int i = uiRoot.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(uiRoot.GetChild(i).gameObject);
        }

        // 1. Hurt Flash
        GameObject hurtObj = new GameObject("HurtOverlay");
        hurtObj.transform.SetParent(uiRoot, false);
        RectTransform rtHurt = hurtObj.AddComponent<RectTransform>();
        rtHurt.anchorMin = Vector2.zero;
        rtHurt.anchorMax = Vector2.one;
        rtHurt.sizeDelta = Vector2.zero;
        Image hurtImg = hurtObj.AddComponent<Image>();
        hurtImg.color = new Color(1f, 0f, 0f, 0f);
        hurtImg.raycastTarget = false;

        // 2. HUD Minimalista Superior Izquierdo (315x74)
        GameObject statsCard = CreateCard(uiRoot, "Card_PlayerStats",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(22, -20), new Vector2(315, 74),
            new Color(0.04f, 0.06f, 0.10f, 0.56f), new Color(0.0f, 0.96f, 0.83f, 0.85f));

        TextMeshProUGUI healthText = CreateText(statsCard.transform, "HealthText",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(16, -12), new Vector2(-32, 26),
            "<color=#FF4D6D>♥♥♥</color> <size=84%>3/3</size>     <color=#FFD166>★</color> <size=88%>0/3</size>", 18, TextAlignmentOptions.Left, fontAsset);

        TextMeshProUGUI toolText = CreateText(statsCard.transform, "ToolText",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(16, -40), new Vector2(-32, 24),
            "<color=#00F5D4>● ASPIRADORA</color> <color=#A8DADC>0/20</color>   <color=#FFD166><b>[Q]</b></color> <color=#8D99AE>Modo</color>", 15.5f, TextAlignmentOptions.Left, fontAsset);

        // 3. Guía Minimalista Dinámica Superior Derecha (450x84, solo 2 líneas)
        GameObject guideCard = CreateCard(uiRoot, "Card_TutorialGuide",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-22, -20), new Vector2(450, 84),
            new Color(0.04f, 0.06f, 0.10f, 0.62f), new Color(1.0f, 0.80f, 0.15f, 0.85f));
        guideCard.AddComponent<CanvasGroup>();

        TextMeshProUGUI titleText = CreateText(guideCard.transform, "TitleText",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(18, -13), new Vector2(-36, 28),
            "<color=#FFD166><b>1/7</b></color>  Absorbe objeto y colapsa el obstáculo", 17.5f, TextAlignmentOptions.Left, fontAsset);

        TextMeshProUGUI controlsText = CreateText(guideCard.transform, "ControlsText",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(18, -44), new Vector2(-36, 26),
            "<color=#00E5FF><b>[Clic Izq]</b></color> Absorber   <color=#FFD166><b>[Clic Der]</b></color> Lanzar",
            15f, TextAlignmentOptions.Left, fontAsset);

        // 4. Toast Inferior Minimalista
        GameObject cpToast = CreateCard(uiRoot, "Card_CheckpointToast",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 36), new Vector2(380, 40),
            new Color(0.04f, 0.08f, 0.06f, 0.68f), new Color(0.2f, 1f, 0.4f, 0.8f));

        TextMeshProUGUI checkpointText = CreateText(cpToast.transform, "CheckpointText",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            "Punto de control guardado", 15.5f, TextAlignmentOptions.Center, fontAsset);
        checkpointText.color = new Color(0.5f, 1f, 0.7f);
        cpToast.SetActive(false);

        // 5. Overlay de Muerte
        GameObject deathObj = CreateCard(uiRoot, "Card_DeathOverlay",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(520, 130),
            new Color(0.12f, 0.02f, 0.02f, 0.88f), new Color(1f, 0.2f, 0.2f));

        CreateText(deathObj.transform, "DeathTitle",
            new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(480, 40),
            "REAPARECIENDO...", 26, TextAlignmentOptions.Center, fontAsset).color = new Color(1f, 0.3f, 0.3f);

        CreateText(deathObj.transform, "DeathSub",
            new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(480, 32),
            "Volviendo al último punto seguro", 16, TextAlignmentOptions.Center, fontAsset);

        deathObj.SetActive(false);

        // 6. Panel de Victoria
        GameObject winPanel = CreateCard(uiRoot, "Card_WinPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(640, 280),
            new Color(0.04f, 0.08f, 0.12f, 0.90f), new Color(0.2f, 1f, 0.4f));

        CreateText(winPanel.transform, "WinTitle",
            new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(580, 50),
            "¡TUTORIAL COMPLETADO!", 30, TextAlignmentOptions.Center, fontAsset).color = new Color(0.2f, 1f, 0.4f);

        TextMeshProUGUI winSummary = CreateText(winPanel.transform, "WinSummary",
            new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580, 140),
            "Has cruzado el Portal Dimensional.",
            19, TextAlignmentOptions.Center, fontAsset);

        winPanel.SetActive(false);

        SerializedObject soGc = new SerializedObject(gc);
        soGc.FindProperty("scoreText").objectReferenceValue = null;
        soGc.FindProperty("healthText").objectReferenceValue = healthText;
        soGc.FindProperty("toolText").objectReferenceValue = toolText;
        soGc.FindProperty("checkpointNotificationText").objectReferenceValue = checkpointText;
        soGc.FindProperty("hurtOverlay").objectReferenceValue = hurtImg;
        soGc.FindProperty("deathOverlay").objectReferenceValue = deathObj;

        soGc.FindProperty("guidePanel").objectReferenceValue = guideCard;
        soGc.FindProperty("phaseBadgeText").objectReferenceValue = null;
        soGc.FindProperty("zoneTitleText").objectReferenceValue = titleText;
        soGc.FindProperty("descriptionText").objectReferenceValue = null;
        soGc.FindProperty("controlsText").objectReferenceValue = controlsText;
        soGc.FindProperty("taskText").objectReferenceValue = null;

        soGc.FindProperty("winPanel").objectReferenceValue = winPanel;
        soGc.FindProperty("winSummaryText").objectReferenceValue = winSummary;
        soGc.FindProperty("spawnPoint").vector3Value = new Vector3(-12.3f, 1.0f, 21.0f);
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
        rtAcc.sizeDelta = new Vector2(0, 2f);
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

        var points = new (Vector3 pos, Vector3 size, string name)[]
        {
            (new Vector3(-12.3f, 1.0f,  21.0f), new Vector3(10f, 5f, 5f), "Punto de control: Inicio"),
            (new Vector3(-12.3f, 1.0f, -14.0f), new Vector3(10f, 5f, 5f), "Punto de control: Agilidad"),
            (new Vector3(-10.0f, 1.0f, -48.0f), new Vector3(10f, 5f, 8f), "Punto de control: Sala de Combate"),
            (new Vector3( 42.0f, 1.0f, -49.0f), new Vector3(10f, 5f, 8f), "Punto de control: Zona Este"),
            (new Vector3( 54.5f, 1.0f, -61.0f), new Vector3(10f, 5f, 6f), "Punto de control: Muralla de Cajas"),
            (new Vector3( 56.5f, 1.0f,-114.0f), new Vector3(10f, 5f, 9f), "Punto de control: Abismo Azul"),
            (new Vector3( 98.0f, 1.0f,-114.0f), new Vector3( 9f, 5f, 9f), "Punto de control: Isla Segura 1"),
            (new Vector3(138.0f, 1.0f,-114.0f), new Vector3( 9f, 5f, 9f), "Punto de control: Isla Segura 2"),
            (new Vector3(246.0f, 1.0f,-114.0f), new Vector3(12f, 5f, 9f), "Punto de control: Gran Canal"),
            (new Vector3(243.2f, 1.2f,  61.0f), new Vector3( 9f, 5f, 6f), "Punto de control: Plataforma Arena"),
            (new Vector3(243.5f,19.0f,  95.0f), new Vector3(10f, 6f, 6f), "Punto de control: Escalera al Portal")
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

            BoxCollider box = cp.GetComponent<BoxCollider>();
            if (box == null) box = cp.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = points[i].size;
            box.center = Vector3.zero;

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
    }

    private static void SetupPlayer(GameController gameController)
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            Vector3 startPos = new Vector3(-12.3f, 1.0f, 21.0f);
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

            ObjectVacum vac = playerObj.GetComponentInChildren<ObjectVacum>(true);
            if (vac != null && vac.GetComponent<ToolVisualController>() == null)
            {
                vac.gameObject.AddComponent<ToolVisualController>();
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
