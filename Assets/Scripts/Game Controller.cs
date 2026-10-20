using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using EasyPeasyFirstPersonController;

/// <summary>
/// Controlador principal del juego y del tutorial.
/// - Garantiza puntos de aparición y checkpoints 100% dentro de la zona principal (lejos de paredes exteriores, enemigos y lava).
/// - UI minimalista y dinámica: muestra únicamente la acción pendiente actual y sus controles con transiciones suaves,
///   atenuándose automáticamente cuando el jugador ya completó la acción de esa zona.
/// </summary>
public class GameController : MonoBehaviour
{
    public static Vector3? LastCheckpointPosition = null;
    public static Quaternion? LastCheckpointRotation = null;
    public static int SavedScore = 0;
    public static HashSet<string> SavedCollectedStars = new HashSet<string>();
    public static HashSet<int> SavedCompletedTutorialSteps = new HashSet<int>();

    [Header("Stars & Score")]
    [SerializeField] private int maxStars = 3;
    private int starsCollected = 0;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Spawn & Fallback")]
    [SerializeField] private Vector3 spawnPoint = new Vector3(-12.3f, 1.0f, 21.0f);
    [SerializeField] private Quaternion spawnRotation = Quaternion.Euler(0f, 180f, 0f);
    [SerializeField] private float fallThreshold = -15f;

    [Header("HUD Feedback")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI toolText;
    [SerializeField] private TextMeshProUGUI checkpointNotificationText;
    [SerializeField] private Image hurtOverlay;
    [SerializeField] private GameObject deathOverlay;

    [Header("Dynamic Tutorial Guide")]
    [SerializeField] private GameObject guidePanel;
    [SerializeField] private TextMeshProUGUI phaseBadgeText;
    [SerializeField] private TextMeshProUGUI zoneTitleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI controlsText;
    [SerializeField] private TextMeshProUGUI taskText;

    [Header("Victory Screen")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private TextMeshProUGUI winSummaryText;

    public int StarsCollected => starsCollected;
    public bool IsLevelWon { get; private set; }

    private PlayerController playerController;
    private FirstPersonController firstPersonController;
    private ObjectVacum vacuumController;
    private GrapleController grappleController;

    private CanvasGroup guideCanvasGroup;
    private RectTransform guideRectTransform;
    private Image guideAccentImage;
    private Image statsAccentImage;

    private Coroutine checkpointCoroutine;
    private Coroutine hurtCoroutine;
    private Coroutine deathCoroutine;
    private Coroutine guideAnimCoroutine;

    // =========================================================================
    // FLUJO DE 7 PASOS DEL TUTORIAL CON DETECCIÓN EN TIEMPO REAL
    // =========================================================================
    private const int TotalTutorialSteps = 7;
    private readonly bool[] stepCompleted = new bool[TotalTutorialSteps];
    private int currentActiveStep = -1;
    private bool hasAbsorbedInStep0 = false;
    private bool hasAbsorbedInStep4 = false;
    private bool hasAbsorbedInStep5 = false;
    private bool hasUsedGrappleOverLava = false;
    private bool reachedDimensionalGoal = false;

    private struct TutorialStepDefinition
    {
        public string stepTag;
        public string title;
        public string controlsHighlight;
    }

    private static readonly TutorialStepDefinition[] Steps = new TutorialStepDefinition[]
    {
        // 0: Absorbe objeto y colapsa obstáculo
        new TutorialStepDefinition
        {
            stepTag = "1/7",
            title = "Absorbe objeto y colapsa el obstáculo",
            controlsHighlight = "<color=#00E5FF><b>[Clic Izq]</b></color> Absorber   <color=#FFD166><b>[Clic Der]</b></color> Lanzar"
        },
        // 1: Salta obstáculo
        new TutorialStepDefinition
        {
            stepTag = "2/7",
            title = "Salta el obstáculo",
            controlsHighlight = "<color=#06D6A0><b>[Espacio]</b></color> Saltar   <color=#FFD166><b>[W A S D]</b></color> Moverse"
        },
        // 2: Agáchate
        new TutorialStepDefinition
        {
            stepTag = "3/7",
            title = "Agáchate para cruzar",
            controlsHighlight = "<color=#06D6A0><b>[Ctrl / C]</b></color> Agacharse   <color=#FFD166><b>[W]</b></color> Avanzar"
        },
        // 3: Corre y deslízate
        new TutorialStepDefinition
        {
            stepTag = "4/7",
            title = "Corre y deslízate",
            controlsHighlight = "<color=#FFD166><b>[Shift + W]</b></color> Correr  +  <color=#06D6A0><b>[Ctrl / C]</b></color> Deslizar"
        },
        // 4: Absorbe objeto, supera enemigos y recoge primera estrella
        new TutorialStepDefinition
        {
            stepTag = "5/7",
            title = "Absorbe, supera enemigos y recoge 1ra estrella",
            controlsHighlight = "<color=#00E5FF><b>[Clic Izq]</b></color> Absorber   <color=#FFD166><b>[Clic Der]</b></color> Lanzar a enemigos"
        },
        // 5: Absorbe objeto para colapsar obstáculo (muralla de cajas)
        new TutorialStepDefinition
        {
            stepTag = "6/7",
            title = "Absorbe objeto para colapsar el obstáculo",
            controlsHighlight = "<color=#00E5FF><b>[Clic Izq]</b></color> Absorber cajas   <color=#FFD166><b>[Clic Der]</b></color> Derribar muro"
        },
        // 6: Usa el gancho para superar la lava y llega al final
        new TutorialStepDefinition
        {
            stepTag = "7/7",
            title = "Usa el gancho sobre la lava y llega al final",
            controlsHighlight = "<color=#06D6A0><b>[Q]</b></color> Modo Gancho   <color=#00E5FF><b>[Clic Izq / X]</b></color> Enganchar"
        }
    };

    // Puntos de control verificados geométricamente dentro de los muros (PolyShape 1..12) y sobre piso firme
    private static readonly (string name, Vector3 pos, Quaternion rot, Vector3 triggerSize, string label)[] SafeCheckpoints =
    {
        ("Checkpoint_1",  new Vector3(-12.3f, 1.0f,  21.0f), Quaternion.Euler(0f, 180f, 0f), new Vector3(10f, 5f, 5f), "Punto de control: Inicio"),
        ("Checkpoint_2",  new Vector3(-12.3f, 1.0f, -14.0f), Quaternion.Euler(0f, 180f, 0f), new Vector3(10f, 5f, 5f), "Punto de control: Agilidad"),
        ("Checkpoint_3",  new Vector3(-10.0f, 1.0f, -48.0f), Quaternion.Euler(0f,  90f, 0f), new Vector3(10f, 5f, 8f), "Punto de control: Sala de Combate"),
        ("Checkpoint_4",  new Vector3( 42.0f, 1.0f, -49.0f), Quaternion.Euler(0f, 135f, 0f), new Vector3(10f, 5f, 8f), "Punto de control: Zona Este"),
        ("Checkpoint_5",  new Vector3( 54.5f, 1.0f, -61.0f), Quaternion.Euler(0f, 180f, 0f), new Vector3(10f, 5f, 6f), "Punto de control: Muralla de Cajas"),
        ("Checkpoint_6",  new Vector3( 56.5f, 1.0f,-114.0f), Quaternion.Euler(0f,  90f, 0f), new Vector3(10f, 5f, 9f), "Punto de control: Abismo Azul"),
        ("Checkpoint_7",  new Vector3( 98.0f, 1.0f,-114.0f), Quaternion.Euler(0f,  90f, 0f), new Vector3( 9f, 5f, 9f), "Punto de control: Isla Segura 1"),
        ("Checkpoint_8",  new Vector3(138.0f, 1.0f,-114.0f), Quaternion.Euler(0f,  90f, 0f), new Vector3( 9f, 5f, 9f), "Punto de control: Isla Segura 2"),
        ("Checkpoint_9",  new Vector3(246.0f, 1.0f,-114.0f), Quaternion.Euler(0f,   0f, 0f), new Vector3(12f, 5f, 9f), "Punto de control: Gran Canal"),
        ("Checkpoint_10", new Vector3(243.2f, 1.2f,  61.0f), Quaternion.Euler(0f,   0f, 0f), new Vector3( 9f, 5f, 6f), "Punto de control: Plataforma Arena"),
        ("Checkpoint_11", new Vector3(243.5f,19.0f,  95.0f), Quaternion.Euler(0f,   0f, 0f), new Vector3(10f, 6f, 6f), "Punto de control: Escalera al Portal")
    };

    private void Awake()
    {
        FindPlayer();
        // Ejecutar en Awake ANTES de cualquier evento OnTriggerEnter para que ningún checkpoint
        // con tamaño antiguo (32x14x32) o posición fuera de muro se active por error.
        SanitizeSceneCheckpointsAndPlayerSpawn();
        ApplyMinimalistHUDLayout();
    }

    private void Start()
    {
        foreach (int idx in SavedCompletedTutorialSteps)
        {
            if (idx >= 0 && idx < TotalTutorialSteps) stepCompleted[idx] = true;
        }

        SanitizeSceneCheckpointsAndPlayerSpawn();

        if (LastCheckpointPosition.HasValue)
        {
            ValidateAndClampSpawnPoint(LastCheckpointPosition.Value, LastCheckpointRotation ?? spawnRotation, out spawnPoint, out spawnRotation);
            starsCollected = SavedScore;

            foreach (var starName in SavedCollectedStars)
            {
                GameObject s = GameObject.Find(starName);
                if (s != null) Destroy(s);
            }
        }
        else
        {
            spawnPoint = SafeCheckpoints[0].pos;
            spawnRotation = SafeCheckpoints[0].rot;
        }

        // Posicionar siempre al jugador dentro de la zona principal verificada
        if (playerController != null)
        {
            CharacterController cc = playerController.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerController.transform.SetPositionAndRotation(spawnPoint, spawnRotation);
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
        }

        UpdateScoreUI();
        UpdateHealthUI();
        UpdateToolUI();

        if (winPanel != null) winPanel.SetActive(false);
        if (deathOverlay != null) deathOverlay.SetActive(false);
        if (checkpointNotificationText != null) checkpointNotificationText.gameObject.SetActive(false);
        if (hurtOverlay != null) hurtOverlay.color = new Color(1f, 0f, 0f, 0f);

        EvaluateTutorialProgress(true);
    }

    /// <summary>
    /// Verifica que cualquier coordenada de spawn coincida con uno de los puntos seguros dentro del mapa.
    /// Si una coordenada antigua quedaba fuera de la zona principal, la corrige al punto seguro más cercano.
    /// </summary>
    private void ValidateAndClampSpawnPoint(Vector3 candidatePos, Quaternion candidateRot, out Vector3 validPos, out Quaternion validRot)
    {
        int bestIndex = 0;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < SafeCheckpoints.Length; i++)
        {
            float dSq = (SafeCheckpoints[i].pos - candidatePos).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                bestIndex = i;
            }
        }

        // Usar siempre la coordenada exacta del punto seguro más cercano para garantizar piso firme interior
        validPos = SafeCheckpoints[bestIndex].pos;
        validRot = bestDistSq < 16f ? candidateRot : SafeCheckpoints[bestIndex].rot;
    }

    /// <summary>
    /// Reubica todos los checkpoints en zonas interiores 100% seguras, reduce sus colliders para que no atraviesen paredes,
    /// y asegura que las islas gigantes sobre la lava sean plataformas estáticas sólidas.
    /// </summary>
    private void SanitizeSceneCheckpointsAndPlayerSpawn()
    {
        var lookup = new Dictionary<string, (Vector3 pos, Quaternion rot, Vector3 triggerSize, string label)>();
        foreach (var item in SafeCheckpoints)
        {
            lookup[item.name] = (item.pos, item.rot, item.triggerSize, item.label);
        }

        foreach (var cp in FindObjectsByType<RespawnController>(FindObjectsSortMode.None))
        {
            if (lookup.TryGetValue(cp.gameObject.name, out var cfg))
            {
                cp.transform.SetPositionAndRotation(cfg.pos, cfg.rot);
                BoxCollider bc = cp.GetComponent<BoxCollider>();
                if (bc != null)
                {
                    bc.size = cfg.triggerSize;
                    bc.center = Vector3.zero;
                }
            }
        }

        foreach (var zp in FindObjectsByType<TutorialZonePrompt>(FindObjectsSortMode.None))
        {
            zp.enabled = false;
        }

        // Convertir las 3 grandes plataformas-isla (Poolable_Crate_15 con escala > 5) en plataformas estáticas firmes
        foreach (var pc in FindObjectsByType<PoolableController>(FindObjectsSortMode.None))
        {
            if (pc.transform.localScale.x > 5f || pc.transform.localScale.z > 5f)
            {
                pc.enabled = false;
                pc.gameObject.layer = 0; // Default
                Rigidbody rb = pc.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
                ThrowableDamage td = pc.GetComponent<ThrowableDamage>();
                if (td != null) td.enabled = false;
            }
        }

        GameObject star1 = GameObject.Find("Star_1");
        GameObject star2 = GameObject.Find("Star_2");
        if (star1 != null && star1.transform.position.z > 0f && star2 != null)
        {
            star1.transform.position = new Vector3(15.0f, 1.25f, -50.0f);
            star2.transform.position = new Vector3(213.42f, 7.25f, -106.87f);
        }

        GameObject winrarGuard = GameObject.Find("Enemy_Tutorial_WinRar_Guard");
        if (winrarGuard != null && Vector3.Distance(winrarGuard.transform.position, new Vector3(243.2f, 1.2f, 61.0f)) < 11f)
        {
            winrarGuard.transform.position = new Vector3(243.2f, 0.8f, 73.2f);
        }

        Physics.SyncTransforms();
    }

    /// <summary>
    /// Elimina cualquier Canvas duplicado en la escena y transforma el HUD en un diseño minimalista,
    /// limpio y dinámico sin paneles amontonados.
    /// </summary>
    private void ApplyMinimalistHUDLayout()
    {
        // 1. Si existen tanto Tutorial_Canvas como TutorialCanvas, desactivar el que no esté referenciado
        GameObject legacyCanvas = GameObject.Find("Tutorial_Canvas");
        GameObject mainCanvas = GameObject.Find("TutorialCanvas");
        if (legacyCanvas != null && mainCanvas != null && legacyCanvas != mainCanvas)
        {
            bool usingMain = guidePanel != null && guidePanel.transform.IsChildOf(mainCanvas.transform);
            if (usingMain)
            {
                legacyCanvas.SetActive(false);
            }
            else
            {
                mainCanvas.SetActive(false);
            }
        }

        // 2. Compactar tarjeta de estado (arriba a la izquierda: Salud + Estrellas + Herramienta en 2 líneas limpias)
        if (healthText != null && healthText.transform.parent != null)
        {
            RectTransform statsCardRt = healthText.transform.parent as RectTransform;
            if (statsCardRt != null)
            {
                statsCardRt.anchorMin = new Vector2(0f, 1f);
                statsCardRt.anchorMax = new Vector2(0f, 1f);
                statsCardRt.pivot = new Vector2(0f, 1f);
                statsCardRt.anchoredPosition = new Vector2(22f, -20f);
                statsCardRt.sizeDelta = new Vector2(315f, 74f);

                Image bg = statsCardRt.GetComponent<Image>();
                if (bg != null) bg.color = new Color(0.04f, 0.06f, 0.10f, 0.56f);

                Transform accent = statsCardRt.Find("AccentLine");
                if (accent != null)
                {
                    statsAccentImage = accent.GetComponent<Image>();
                    RectTransform rtAcc = accent as RectTransform;
                    if (rtAcc != null) rtAcc.sizeDelta = new Vector2(0f, 2f);
                }
            }

            RectTransform rtHp = healthText.rectTransform;
            rtHp.anchorMin = new Vector2(0f, 1f);
            rtHp.anchorMax = new Vector2(1f, 1f);
            rtHp.pivot = new Vector2(0f, 1f);
            rtHp.anchoredPosition = new Vector2(16f, -12f);
            rtHp.sizeDelta = new Vector2(-32f, 26f);
            healthText.fontSize = 18f;
            healthText.alignment = TextAlignmentOptions.Left;
        }

        if (toolText != null)
        {
            RectTransform rtTool = toolText.rectTransform;
            rtTool.anchorMin = new Vector2(0f, 1f);
            rtTool.anchorMax = new Vector2(1f, 1f);
            rtTool.pivot = new Vector2(0f, 1f);
            rtTool.anchoredPosition = new Vector2(16f, -40f);
            rtTool.sizeDelta = new Vector2(-32f, 24f);
            toolText.fontSize = 15.5f;
            toolText.alignment = TextAlignmentOptions.Left;
        }

        // Ocultar el renglón separado de ScoreText porque ahora se integra de forma minimalista junto a la salud
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(false);
        }

        // 3. Estilizar el panel de Guía del Tutorial: cápsula minimalista de solo 2 líneas (Acción + Controles)
        if (guidePanel != null)
        {
            guideRectTransform = guidePanel.GetComponent<RectTransform>();
            if (guideRectTransform != null)
            {
                guideRectTransform.anchorMin = new Vector2(1f, 1f);
                guideRectTransform.anchorMax = new Vector2(1f, 1f);
                guideRectTransform.pivot = new Vector2(1f, 1f);
                guideRectTransform.anchoredPosition = new Vector2(-22f, -20f);
                guideRectTransform.sizeDelta = new Vector2(450f, 84f);
            }

            Image guideBg = guidePanel.GetComponent<Image>();
            if (guideBg != null)
            {
                guideBg.color = new Color(0.04f, 0.06f, 0.10f, 0.62f);
            }

            Transform accent = guidePanel.transform.Find("AccentLine");
            if (accent != null)
            {
                guideAccentImage = accent.GetComponent<Image>();
                RectTransform rtAcc = accent as RectTransform;
                if (rtAcc != null) rtAcc.sizeDelta = new Vector2(0f, 2f);
            }

            guideCanvasGroup = guidePanel.GetComponent<CanvasGroup>();
            if (guideCanvasGroup == null)
            {
                guideCanvasGroup = guidePanel.AddComponent<CanvasGroup>();
            }
            guideCanvasGroup.alpha = 1f;
        }

        // Ocultar textos redundantes para eliminar el amontonamiento visual
        if (phaseBadgeText != null) phaseBadgeText.gameObject.SetActive(false);
        if (descriptionText != null) descriptionText.gameObject.SetActive(false);
        if (taskText != null) taskText.gameObject.SetActive(false);

        if (zoneTitleText != null)
        {
            RectTransform rtTitle = zoneTitleText.rectTransform;
            rtTitle.anchorMin = new Vector2(0f, 1f);
            rtTitle.anchorMax = new Vector2(1f, 1f);
            rtTitle.pivot = new Vector2(0f, 1f);
            rtTitle.anchoredPosition = new Vector2(18f, -13f);
            rtTitle.sizeDelta = new Vector2(-36f, 28f);
            zoneTitleText.fontSize = 17.5f;
            zoneTitleText.alignment = TextAlignmentOptions.Left;
            zoneTitleText.textWrappingMode = TextWrappingModes.NoWrap;
            zoneTitleText.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (controlsText != null)
        {
            RectTransform rtCtrl = controlsText.rectTransform;
            rtCtrl.anchorMin = new Vector2(0f, 1f);
            rtCtrl.anchorMax = new Vector2(1f, 1f);
            rtCtrl.pivot = new Vector2(0f, 1f);
            rtCtrl.anchoredPosition = new Vector2(18f, -44f);
            rtCtrl.sizeDelta = new Vector2(-36f, 26f);
            controlsText.fontSize = 15f;
            controlsText.alignment = TextAlignmentOptions.Left;
            controlsText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // 4. Hacer el Toast de Checkpoint más discreto y flotante en la parte inferior-central
        if (checkpointNotificationText != null && checkpointNotificationText.transform.parent != null)
        {
            RectTransform toastRt = checkpointNotificationText.transform.parent as RectTransform;
            if (toastRt != null && toastRt.name.Contains("Toast") || (toastRt != null && toastRt.name.Contains("Checkpoint")))
            {
                toastRt.anchorMin = new Vector2(0.5f, 0f);
                toastRt.anchorMax = new Vector2(0.5f, 0f);
                toastRt.pivot = new Vector2(0.5f, 0f);
                toastRt.anchoredPosition = new Vector2(0f, 36f);
                toastRt.sizeDelta = new Vector2(380f, 40f);
                Image img = toastRt.GetComponent<Image>();
                if (img != null) img.color = new Color(0.04f, 0.08f, 0.06f, 0.68f);
            }
            checkpointNotificationText.fontSize = 15.5f;
        }
    }

    private void Update()
    {
        if (playerController == null || firstPersonController == null)
        {
            FindPlayer();
        }

        if (playerController != null && !playerController.dead)
        {
            UpdateHealthUI();
            UpdateToolUI();
            TrackPlayerTutorialActions();
            EvaluateTutorialProgress(false);

            // Animación de pulso suave en el indicador cuando el paso actual está pendiente
            if (currentActiveStep >= 0 && currentActiveStep < TotalTutorialSteps && !stepCompleted[currentActiveStep])
            {
                if (guideAccentImage != null)
                {
                    float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4.5f);
                    guideAccentImage.color = new Color(1.0f, 0.80f, 0.15f, pulse);
                }
            }

            Vector3 pPos = playerController.transform.position;
            if (pPos.y < fallThreshold)
            {
                playerController.TakeDamage(99);
            }
        }

        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
        {
            RestartLevel();
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerController = playerObj.GetComponent<PlayerController>();
            firstPersonController = playerObj.GetComponent<FirstPersonController>();
            if (vacuumController == null)
            {
                vacuumController = playerObj.GetComponentInChildren<ObjectVacum>(true);
                if (vacuumController != null)
                {
                    vacuumController.OnObjectAbsorbed -= HandleObjectAbsorbed;
                    vacuumController.OnObjectAbsorbed += HandleObjectAbsorbed;
                    vacuumController.OnObjectLaunched -= HandleObjectLaunched;
                    vacuumController.OnObjectLaunched += HandleObjectLaunched;
                }
            }
            if (grappleController == null)
            {
                grappleController = playerObj.GetComponentInChildren<GrapleController>(true);
            }
        }
    }

    private void OnDestroy()
    {
        if (vacuumController != null)
        {
            vacuumController.OnObjectAbsorbed -= HandleObjectAbsorbed;
            vacuumController.OnObjectLaunched -= HandleObjectLaunched;
        }
    }

    private void HandleObjectAbsorbed(GameObject obj)
    {
        if (playerController == null) return;
        Vector3 pos = playerController.transform.position;

        if (pos.z > 4f)
        {
            hasAbsorbedInStep0 = true;
        }
        else if (pos.z <= -30f && pos.x < 33f)
        {
            hasAbsorbedInStep4 = true;
        }
        else if (pos.x >= 30f && pos.z > -95f)
        {
            hasAbsorbedInStep5 = true;
            CompleteTutorialStep(5);
        }
    }

    private void HandleObjectLaunched(GameObject obj)
    {
        if (playerController == null) return;
        Vector3 pos = playerController.transform.position;

        if (pos.z > 2f && hasAbsorbedInStep0)
        {
            CompleteTutorialStep(0);
        }
        else if (pos.x >= 28f && pos.z > -95f && hasAbsorbedInStep5)
        {
            CompleteTutorialStep(5);
        }
    }

    private void TrackPlayerTutorialActions()
    {
        if (firstPersonController == null || playerController == null) return;

        Vector3 pos = playerController.transform.position;
        string stateName = firstPersonController.CurrentState != null
            ? firstPersonController.CurrentState.GetType().Name
            : "";

        if (!stepCompleted[0] && ((hasAbsorbedInStep0 && pos.z < 9.5f) || pos.z < 5.0f))
        {
            CompleteTutorialStep(0);
        }

        if (!stepCompleted[1])
        {
            bool jumpedNearObstacle = (stateName == "PlayerJumpingState" || firstPersonController.input.jump) && pos.z <= 10f && pos.z >= -6f;
            if (jumpedNearObstacle || pos.z < -2.2f)
            {
                CompleteTutorialStep(1);
            }
        }

        if (!stepCompleted[2])
        {
            bool crouchedNearBarrier = (stateName == "PlayerCrouchingState" || firstPersonController.input.crouch) && pos.z <= 0f && pos.z >= -20f;
            if (crouchedNearBarrier || pos.z < -12.0f)
            {
                CompleteTutorialStep(2);
            }
        }

        if (!stepCompleted[3])
        {
            bool slid = stateName == "PlayerSlidingState" || (firstPersonController.input.sprint && firstPersonController.input.slide && pos.z <= -5f);
            if (slid || pos.z < -31.0f)
            {
                CompleteTutorialStep(3);
            }
        }

        if (!stepCompleted[4] && starsCollected >= 1)
        {
            CompleteTutorialStep(4);
        }

        if (!stepCompleted[5] && pos.z < -78.0f && pos.x > 35.0f)
        {
            CompleteTutorialStep(5);
        }

        if (grappleController != null && grappleController.IsGrappling && pos.x > 55f)
        {
            hasUsedGrappleOverLava = true;
        }
        if (!stepCompleted[6] && reachedDimensionalGoal)
        {
            CompleteTutorialStep(6);
        }
    }

    private void CompleteTutorialStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= TotalTutorialSteps) return;
        if (stepCompleted[stepIndex]) return;

        stepCompleted[stepIndex] = true;
        SavedCompletedTutorialSteps.Add(stepIndex);
        EvaluateTutorialProgress(true);
    }

    private void EvaluateTutorialProgress(bool forceRefresh)
    {
        if (playerController == null) return;

        Vector3 pos = playerController.transform.position;
        int targetStep = DetermineZoneStepByPosition(pos);

        if (targetStep != currentActiveStep || forceRefresh)
        {
            currentActiveStep = targetStep;
            RefreshGuideUIForStep(currentActiveStep);
        }
    }

    private int DetermineZoneStepByPosition(Vector3 pos)
    {
        if (pos.x < -1.0f && pos.z > -34.0f)
        {
            if (pos.z > 6.5f) return 0;
            if (pos.z > -1.8f) return 1;
            if (pos.z > -10.5f) return 2;
            return 3;
        }

        if (pos.x < 35.0f && pos.z > -72.0f)
        {
            return 4;
        }

        if (pos.z > -88.0f && pos.x < 75.0f)
        {
            return 5;
        }

        return 6;
    }

    private void RefreshGuideUIForStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= TotalTutorialSteps) return;

        TutorialStepDefinition def = Steps[stepIndex];
        bool done = stepCompleted[stepIndex];

        if (zoneTitleText != null)
        {
            zoneTitleText.text = done
                ? $"<color=#38E54D>✓ {def.stepTag}</color>  <color=#A8D5BA>{def.title}</color>"
                : $"<color=#FFD166><b>{def.stepTag}</b></color>  {def.title}";
        }

        if (controlsText != null)
        {
            controlsText.text = done
                ? "<color=#689F80>Acción completada</color>"
                : def.controlsHighlight;
        }

        if (guideAccentImage != null && done)
        {
            guideAccentImage.color = new Color(0.22f, 0.90f, 0.35f, 0.65f);
        }

        if (guidePanel != null)
        {
            guidePanel.SetActive(true);
            if (guideAnimCoroutine != null) StopCoroutine(guideAnimCoroutine);
            guideAnimCoroutine = StartCoroutine(AnimateGuideTransition(done));
        }
    }

    /// <summary>
    /// Si el jugador aún NO ha hecho la acción, resalta el panel con opacidad completa y una entrada elástica suave.
    /// Si YA la completó, muestra la confirmación verde y atenúa el panel de forma minimalista para despejar la vista.
    /// </summary>
    private IEnumerator AnimateGuideTransition(bool isCompleted)
    {
        if (guideCanvasGroup == null && guidePanel != null)
        {
            guideCanvasGroup = guidePanel.GetComponent<CanvasGroup>() ?? guidePanel.AddComponent<CanvasGroup>();
        }

        Transform t = guidePanel.transform;
        float elapsed = 0f;
        float popDuration = 0.22f;
        float startAlpha = guideCanvasGroup != null ? guideCanvasGroup.alpha : 1f;
        float targetImmediateAlpha = isCompleted ? 0.85f : 1.0f;

        while (elapsed < popDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / popDuration);
            float scale = 1f + Mathf.Sin(p * Mathf.PI) * 0.035f;
            t.localScale = Vector3.one * scale;
            if (guideCanvasGroup != null)
            {
                guideCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetImmediateAlpha, p);
            }
            yield return null;
        }

        t.localScale = Vector3.one;

        if (isCompleted && guideCanvasGroup != null)
        {
            yield return new WaitForSecondsRealtime(0.9f);
            float fadeDur = 0.45f;
            float f = 0f;
            while (f < fadeDur)
            {
                f += Time.unscaledDeltaTime;
                guideCanvasGroup.alpha = Mathf.Lerp(targetImmediateAlpha, 0.18f, f / fadeDur);
                yield return null;
            }
            guideCanvasGroup.alpha = 0.18f;
        }
    }

    public void CollectStar(string starName = "")
    {
        starsCollected++;
        if (!string.IsNullOrEmpty(starName))
        {
            SavedCollectedStars.Add(starName);
        }

        UpdateScoreUI();
        UpdateHealthUI();
        NotifyCheckpoint($"★ Estrella {starsCollected}/{maxStars}");

        if (starsCollected >= 1 && !stepCompleted[4])
        {
            CompleteTutorialStep(4);
        }
        else
        {
            RefreshGuideUIForStep(currentActiveStep);
        }
    }

    public void CollectStar()
    {
        CollectStar("");
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null && scoreText.gameObject.activeSelf)
        {
            scoreText.text = $"★ <color=#FFD166>{starsCollected}/{maxStars}</color>";
        }
    }

    public void UpdateHealthUI()
    {
        if (healthText != null && playerController != null)
        {
            int hp = Mathf.Clamp(playerController.health, 0, 3);
            string hearts = new string('♥', hp) + "<color=#44333A>" + new string('♥', 3 - hp) + "</color>";
            healthText.text = $"<color=#FF4D6D>{hearts}</color> <size=84%>{hp}/3</size>     <color=#FFD166>★</color> <size=88%>{starsCollected}/{maxStars}</size>";
        }
    }

    public void UpdateToolUI()
    {
        if (toolText != null && playerController != null)
        {
            bool isGrapple = playerController.SelectedTool == PlayerController.Tool.Grapple;
            string toolBadge;
            if (isGrapple)
            {
                if (playerController.GrapleController != null && playerController.GrapleController.IsOnCooldown)
                {
                    float cd = playerController.GrapleController.CooldownRemaining;
                    toolBadge = $"<color=#FFB703>● GANCHO</color> <color=#99AABB>{cd:F1}s</color>";
                }
                else
                {
                    toolBadge = "<color=#00BBF9>● GANCHO</color> <color=#A8DADC>Listo</color>";
                }
                if (statsAccentImage != null) statsAccentImage.color = new Color(0f, 0.73f, 0.98f, 0.85f);
            }
            else
            {
                int stored = vacuumController != null ? vacuumController.StoredCount : 0;
                int cap = vacuumController != null ? vacuumController.ObjectCapacity : 5;
                toolBadge = $"<color=#00F5D4>● ASPIRADORA</color> <color=#A8DADC>{stored}/{cap}</color>";
                if (statsAccentImage != null) statsAccentImage.color = new Color(0f, 0.96f, 0.83f, 0.85f);
            }
            toolText.text = $"{toolBadge}   <color=#FFD166><b>[Q]</b></color> <color=#8D99AE>Modo</color>";
        }
    }

    public void UpdateSpawnPoint(Vector3 newSpawnPoint)
    {
        Quaternion currentYaw = playerController != null
            ? Quaternion.Euler(0f, playerController.transform.eulerAngles.y, 0f)
            : spawnRotation;

        ValidateAndClampSpawnPoint(newSpawnPoint, currentYaw, out spawnPoint, out spawnRotation);
        Debug.Log($"[GameController] Punto de control seguro establecido en: {spawnPoint}");
    }

    public void NotifyCheckpoint(string message)
    {
        if (checkpointNotificationText == null) return;
        if (checkpointCoroutine != null) StopCoroutine(checkpointCoroutine);
        checkpointCoroutine = StartCoroutine(ShowCheckpointToast(message));
    }

    private IEnumerator ShowCheckpointToast(string message)
    {
        GameObject toastContainer = checkpointNotificationText.transform.parent != null &&
                                    (checkpointNotificationText.transform.parent.name.Contains("Toast") ||
                                     checkpointNotificationText.transform.parent.name.Contains("Checkpoint"))
            ? checkpointNotificationText.transform.parent.gameObject
            : checkpointNotificationText.gameObject;

        checkpointNotificationText.text = message;
        checkpointNotificationText.gameObject.SetActive(true);
        toastContainer.SetActive(true);
        yield return new WaitForSeconds(2.2f);
        toastContainer.SetActive(false);
    }

    public void SetTutorialGuide(string phase, string title, string description, string controls, string task)
    {
        EvaluateTutorialProgress(true);
    }

    public void SetTutorialPrompt(string text, float duration = 5f)
    {
        NotifyCheckpoint(text);
    }

    public void OnPlayerTookDamage()
    {
        UpdateHealthUI();
        if (hurtOverlay != null)
        {
            if (hurtCoroutine != null) StopCoroutine(hurtCoroutine);
            hurtCoroutine = StartCoroutine(FlashHurtOverlay());
        }
    }

    private IEnumerator FlashHurtOverlay()
    {
        if (hurtOverlay == null) yield break;
        hurtOverlay.color = new Color(1f, 0.08f, 0.08f, 0.45f);
        float elapsed = 0f;
        float dur = 0.35f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0.45f, 0f, elapsed / dur);
            hurtOverlay.color = new Color(1f, 0.08f, 0.08f, alpha);
            yield return null;
        }
        hurtOverlay.color = new Color(1f, 0.08f, 0.08f, 0f);
    }

    public void OnPlayerDied()
    {
        if (deathCoroutine != null) StopCoroutine(deathCoroutine);
        deathCoroutine = StartCoroutine(DeathAndRespawnRoutine());
    }

    [ContextMenu("Respawn Player")]
    public void RespawnPlayer()
    {
        OnPlayerDied();
    }

    private IEnumerator DeathAndRespawnRoutine()
    {
        UpdateHealthUI();

        if (deathOverlay != null)
        {
            deathOverlay.SetActive(true);
        }

        if (hurtOverlay != null)
        {
            hurtOverlay.color = new Color(0.85f, 0f, 0f, 0.65f);
        }

        yield return new WaitForSeconds(0.85f);

        LastCheckpointPosition = spawnPoint;
        LastCheckpointRotation = spawnRotation;
        SavedScore = starsCollected;

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnPlayerReachedDimensionalGoal()
    {
        reachedDimensionalGoal = true;
        CompleteTutorialStep(6);
        IsLevelWon = true;

        NotifyCheckpoint("✦ Otra Dimensión · Cruza el portal para ir y volver");
    }

    public void WinLevel()
    {
        OnPlayerReachedDimensionalGoal();
    }

    public void RestartLevel()
    {
        LastCheckpointPosition = null;
        LastCheckpointRotation = null;
        SavedScore = 0;
        SavedCollectedStars.Clear();
        SavedCompletedTutorialSteps.Clear();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
