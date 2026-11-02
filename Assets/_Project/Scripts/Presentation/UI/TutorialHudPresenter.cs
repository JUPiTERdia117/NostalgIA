// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: TutorialHudPresenter.cs
// Propósito: HUD provisional del Tutorial (vida, estrellas, herramienta,
// notificaciones) y guía de siete pasos, alimentados solo por eventos
// (el HUD definitivo llega después).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Services;
using NostalgIA.Core.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NostalgIA.Presentation.UI
{
    /// <summary>
    /// Presentación del HUD del Tutorial.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sustituye a la parte visual del antiguo <c>GameController</c> (reutiliza su GUID, por lo que
    /// la escena conserva todas las referencias a los textos e imágenes). El resto de aquel
    /// controlador monolítico se repartió: la reaparición en <c>RespawnService</c>, las
    /// coordenadas de checkpoints en la propia escena y la disposición del HUD, que antes se
    /// reajustaba por código en cada arranque, quedó aplicada en el editor.
    /// </para>
    /// <para>
    /// <b>Sin sondeo ni basura.</b> Cada dato se actualiza al recibir su evento. Los textos con
    /// números usan <c>TMP_Text.SetText(formato, a, b)</c>, que escribe los dígitos en el búfer
    /// interno de TextMeshPro sin crear cadenas; los textos fijos se construyen una vez en
    /// <c>Awake</c>. Las animaciones (aparición de la guía, destello de daño, notificaciones) se
    /// llevan con temporizadores en <c>Update</c>, sin corrutinas. La única lectura continua es la
    /// posición del jugador para detectar la zona del tutorial, cada 0,2 s.
    /// </para>
    /// <para>
    /// <b>Animaciones sin reconstruir el lienzo.</b> Asignar <see cref="Graphic.color"/> marca la
    /// malla del gráfico como sucia y obliga a regenerarla. Por eso, en este módulo el color de la
    /// línea de acento y del destello de daño se escribe solo cuando cambia de estado, y la
    /// animación por frame modifica únicamente <see cref="CanvasRenderer.SetAlpha"/>, que es un
    /// multiplicador aplicado al dibujar y no regenera vértices.
    /// </para>
    /// <para>
    /// <b>Glifos.</b> La fuente de interfaz actual (Liberation Sans) cubre el conjunto WGL4, pero
    /// no los símbolos ★, ✓ ni ✦ del prototipo, que se dibujaban como cuadros vacíos. Usamos sus
    /// equivalentes disponibles: ♦ (estrella), √ (completado) y » (aviso). La fuente monoespaciada
    /// definitiva llega con el HUD final.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TutorialHudPresenter : MonoBehaviour
    {
        // ------------------------------------------------------------------ referencias de escena
        [Header("Estrellas")]
        [SerializeField, Min(1)] private int maxStars = 3;
        [SerializeField] private TextMeshProUGUI scoreText;

        [Header("HUD")]
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI toolText;
        [SerializeField] private TextMeshProUGUI checkpointNotificationText;
        [SerializeField] private Image hurtOverlay;
        [SerializeField] private GameObject deathOverlay;

        [Header("Guía dinámica del Tutorial")]
        [SerializeField] private GameObject guidePanel;
        [SerializeField] private TextMeshProUGUI phaseBadgeText;
        [SerializeField] private TextMeshProUGUI zoneTitleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI controlsText;
        [SerializeField] private TextMeshProUGUI taskText;
        [Tooltip("Línea de acento de la guía (opcional; se busca como hija «AccentLine»).")]
        [SerializeField] private Image guideAccent;

        [Header("Pantalla de victoria (no usada en el Tutorial)")]
        [SerializeField] private GameObject winPanel;
        [SerializeField] private TextMeshProUGUI winSummaryText;

        [Header("Tiempos (s)")]
        [SerializeField, Min(0.1f)] private float toastDuration = 2.2f;
        [SerializeField, Min(0.05f)] private float hurtFlashDuration = 0.35f;

        // ------------------------------------------------------------------ textos fijos
        private const int TotalSteps = 7;
        private const float ZonePollInterval = 0.2f;

        private static readonly string[] StepTags = { "1/7", "2/7", "3/7", "4/7", "5/7", "6/7", "7/7" };

        private static readonly string[] StepTitles =
        {
            "Absorbe un objeto y colapsa el obstáculo",
            "Salta el obstáculo",
            "Agáchate para cruzar",
            "Corre y deslízate",
            "Absorbe, supera enemigos y recoge la 1.ª estrella",
            "Absorbe objetos para colapsar el obstáculo",
            "Usa el gancho sobre la lava y llega al final"
        };

        /// <summary>Controles por paso y esquema: [paso, esquema], con el orden de <see cref="InputScheme"/>.</summary>
        private static readonly string[,] StepControls =
        {
            {
                "<color=#00E5FF><b>[Clic Der]</b></color> Absorber   <color=#FFD166><b>[Clic Izq]</b></color> Disparar",
                "<color=#00E5FF><b>[LT]</b></color> Absorber   <color=#FFD166><b>[RT]</b></color> Disparar",
                "<color=#00E5FF><b>[Mantén el pad derecho]</b></color> Absorber   <color=#FFD166><b>[Toca]</b></color> Disparar"
            },
            {
                "<color=#06D6A0><b>[Espacio]</b></color> Saltar   <color=#FFD166><b>[W A S D]</b></color> Moverse",
                "<color=#06D6A0><b>[A]</b></color> Saltar   <color=#FFD166><b>[Stick izq.]</b></color> Moverse",
                "<color=#06D6A0><b>[Saltar]</b></color> Saltar   <color=#FFD166><b>[Pad izquierdo]</b></color> Moverse"
            },
            {
                "<color=#06D6A0><b>[C]</b></color> Agacharse   <color=#FFD166><b>[W]</b></color> Avanzar",
                "<color=#06D6A0><b>[R3]</b></color> Agacharse   <color=#FFD166><b>[Stick izq.]</b></color> Avanzar",
                "<color=#06D6A0><b>[Agacharse]</b></color> Agacharse   <color=#FFD166><b>[Pad izquierdo]</b></color> Avanzar"
            },
            {
                "<color=#FFD166><b>[Shift + W]</b></color> Correr  +  <color=#06D6A0><b>[C]</b></color> Deslizar",
                "<color=#FFD166><b>[L3 + Stick]</b></color> Correr  +  <color=#06D6A0><b>[R3]</b></color> Deslizar",
                "<color=#FFD166><b>[Pad hasta arriba]</b></color> Correr  +  <color=#06D6A0><b>[Agacharse]</b></color> Deslizar"
            },
            {
                "<color=#00E5FF><b>[Clic Der]</b></color> Absorber   <color=#FFD166><b>[Clic Izq]</b></color> Disparar a enemigos",
                "<color=#00E5FF><b>[LT]</b></color> Absorber   <color=#FFD166><b>[RT]</b></color> Disparar a enemigos",
                "<color=#00E5FF><b>[Mantén el pad]</b></color> Absorber   <color=#FFD166><b>[Toca]</b></color> Disparar a enemigos"
            },
            {
                "<color=#00E5FF><b>[Clic Der]</b></color> Absorber cajas   <color=#FFD166><b>[Clic Izq]</b></color> Derribar el muro",
                "<color=#00E5FF><b>[LT]</b></color> Absorber cajas   <color=#FFD166><b>[RT]</b></color> Derribar el muro",
                "<color=#00E5FF><b>[Mantén el pad]</b></color> Absorber cajas   <color=#FFD166><b>[Toca]</b></color> Derribar el muro"
            },
            {
                "<color=#06D6A0><b>[F]</b></color> Modo gancho   <color=#00E5FF><b>[Clic Der]</b></color> Enganchar",
                "<color=#06D6A0><b>[Y]</b></color> Modo gancho   <color=#00E5FF><b>[RT]</b></color> Enganchar",
                "<color=#06D6A0><b>[Cambiar]</b></color> Modo gancho   <color=#00E5FF><b>[Gancho]</b></color> Enganchar"
            }
        };

        /// <summary>Sugerencia de cambio de herramienta por esquema.</summary>
        private static readonly string[] SwitchHints =
        {
            "   <color=#FFD166><b>[F]</b></color> <color=#8D99AE>Modo</color>",
            "   <color=#FFD166><b>[Y]</b></color> <color=#8D99AE>Modo</color>",
            string.Empty
        };

        private const string StepDoneControls = "<color=#689F80>Acción completada</color>";
        private const string ReturnedMessage = "Regresaste a la dimensión del Tutorial.";
        private const string GoalMessage = "» Otra Dimensión · Cruza el portal para ir y volver";

        // Cadenas construidas una sola vez en Awake.
        private string[] pendingTitles;
        private string[] doneTitles;
        private string[] healthFormats;
        private string[] vacuumFormats;
        private string[] grappleReadyTexts;
        private string[] grappleCooldownFormats;
        private string starToastFormat;

        // ------------------------------------------------------------------ estado
        private int hearts = 3;
        private int heartsMax = 3;
        private int stars;
        private int stored;
        private int capacity = 20;
        private ToolId tool = ToolId.Vacuum;
        private bool onCooldown;
        private InputScheme scheme = InputScheme.KeyboardMouse;

        private readonly bool[] stepCompleted = new bool[TotalSteps];
        private readonly string[] customPendingTitles = new string[TotalSteps];
        private readonly string[] customStepControls = new string[TotalSteps];
        private bool hasActivePromptZone;
        private int currentStep = -1;
        private bool absorbedInStep0;
        private bool absorbedInStep5;

        private IPlayerMotionState motion;
        private IToolState toolState;
        private float zonePollTimer;

        /// <summary>
        /// Verdadero tras cruzar una puerta de nivel: el HUD acompaña al jugador al nivel siguiente
        /// (lleva el marcador <c>PersistAcrossLevels</c>), pero la guía del Tutorial ya no aplica y
        /// sus zonas están definidas en coordenadas del Tutorial, así que se oculta y deja de sondearse.
        /// </summary>
        private bool tutorialLeft;
        private float toastTimer;
        private float hurtTimer;
        private GameObject toastContainer;
        private CanvasGroup guideGroup;

        private float guideAnimTime = -1f;
        private bool guideAnimCompleted;
        private float guideStartAlpha = 1f;
        private float lastCooldownTenths = -1f;

        // Colores fijos de las animaciones (el alfa animado va en el CanvasRenderer).
        private static readonly Color HurtColor = new Color(1f, 0.08f, 0.08f, 1f);
        private static readonly Color DeathColor = new Color(0.85f, 0f, 0f, 1f);
        private static readonly Color AccentPendingColor = new Color(1f, 0.8f, 0.15f, 1f);
        private static readonly Color AccentDoneColor = new Color(0.22f, 0.9f, 0.35f, 1f);
        private const float HurtPeakAlpha = 0.45f;
        private const float DeathAlpha = 0.65f;
        private const float AccentDoneAlpha = 0.65f;

        // ------------------------------------------------------------------ ciclo de vida
        /// <summary>Construye los textos fijos y resuelve referencias auxiliares.</summary>
        private void Awake()
        {
            pendingTitles = new string[TotalSteps];
            doneTitles = new string[TotalSteps];
            for (int i = 0; i < TotalSteps; i++)
            {
                pendingTitles[i] = $"<color=#FFD166><b>{StepTags[i]}</b></color>  {StepTitles[i]}";
                doneTitles[i] = $"<color=#38E54D>√ {StepTags[i]}</color>  <color=#A8D5BA>{StepTitles[i]}</color>";
            }

            BuildHealthFormats(heartsMax);
            starToastFormat = "♦ Estrella {0}/" + maxStars;

            int schemes = SwitchHints.Length;
            vacuumFormats = new string[schemes];
            grappleReadyTexts = new string[schemes];
            grappleCooldownFormats = new string[schemes];
            for (int s = 0; s < schemes; s++)
            {
                vacuumFormats[s] = "<color=#00F5D4>● ASPIRADORA</color> <color=#A8DADC>{0}/{1}</color>" + SwitchHints[s];
                grappleReadyTexts[s] = "<color=#00BBF9>● GANCHO</color> <color=#A8DADC>Listo</color>" + SwitchHints[s];
                grappleCooldownFormats[s] = "<color=#FFB703>● GANCHO</color> <color=#99AABB>{0:1}s</color>" + SwitchHints[s];
            }

            if (checkpointNotificationText != null)
            {
                Transform parent = checkpointNotificationText.transform.parent;
                toastContainer = parent != null && parent.GetComponent<Image>() != null ? parent.gameObject : checkpointNotificationText.gameObject;
            }

            if (guidePanel == null && zoneTitleText != null)
            {
                Transform parent = zoneTitleText.transform.parent;
                guidePanel = parent != null ? parent.gameObject : null;
            }

            if (guidePanel != null)
            {
                guideGroup = guidePanel.GetComponent<CanvasGroup>();
                if (guideAccent == null)
                {
                    Transform accent = guidePanel.transform.Find("AccentLine");
                    if (accent != null) guideAccent = accent.GetComponent<Image>();
                }
            }
        }

        /// <summary>Suscripciones y estado inicial.</summary>
        private void OnEnable()
        {
            EventBus<PlayerDamaged>.Subscribe(OnPlayerDamaged);
            EventBus<PlayerDied>.Subscribe(OnPlayerDied);
            EventBus<PlayerRespawned>.Subscribe(OnPlayerRespawned);
            EventBus<StarCollected>.Subscribe(OnStarCollected);
            EventBus<ToolSwitched>.Subscribe(OnToolSwitched);
            EventBus<ToolCooldownChanged>.Subscribe(OnCooldownChanged);
            EventBus<ObjectAbsorbed>.Subscribe(OnObjectAbsorbed);
            EventBus<ObjectLaunched>.Subscribe(OnObjectLaunched);
            EventBus<CheckpointActivated>.Subscribe(OnCheckpointActivated);
            EventBus<HudMessageRequested>.Subscribe(OnHudMessage);
            EventBus<DimensionalPortalCrossed>.Subscribe(OnPortalCrossed);
            EventBus<LevelGateCrossed>.Subscribe(OnLevelGateCrossed);
            EventBus<PlayerJumped>.Subscribe(OnPlayerJumped);
            EventBus<LocomotionStateChanged>.Subscribe(OnLocomotionChanged);
            EventBus<InputSchemeChanged>.Subscribe(OnSchemeChanged);
            EventBus<PromptTriggered>.Subscribe(OnPromptTriggered);
            EventBus<TutorialStepTriggered>.Subscribe(OnTutorialStepTriggered);
        }

        /// <summary>Cancela suscripciones.</summary>
        private void OnDisable()
        {
            EventBus<PlayerDamaged>.Unsubscribe(OnPlayerDamaged);
            EventBus<PlayerDied>.Unsubscribe(OnPlayerDied);
            EventBus<PlayerRespawned>.Unsubscribe(OnPlayerRespawned);
            EventBus<StarCollected>.Unsubscribe(OnStarCollected);
            EventBus<ToolSwitched>.Unsubscribe(OnToolSwitched);
            EventBus<ToolCooldownChanged>.Unsubscribe(OnCooldownChanged);
            EventBus<ObjectAbsorbed>.Unsubscribe(OnObjectAbsorbed);
            EventBus<ObjectLaunched>.Unsubscribe(OnObjectLaunched);
            EventBus<CheckpointActivated>.Unsubscribe(OnCheckpointActivated);
            EventBus<HudMessageRequested>.Unsubscribe(OnHudMessage);
            EventBus<DimensionalPortalCrossed>.Unsubscribe(OnPortalCrossed);
            EventBus<LevelGateCrossed>.Unsubscribe(OnLevelGateCrossed);
            EventBus<PlayerJumped>.Unsubscribe(OnPlayerJumped);
            EventBus<LocomotionStateChanged>.Unsubscribe(OnLocomotionChanged);
            EventBus<InputSchemeChanged>.Unsubscribe(OnSchemeChanged);
            EventBus<PromptTriggered>.Unsubscribe(OnPromptTriggered);
            EventBus<TutorialStepTriggered>.Unsubscribe(OnTutorialStepTriggered);
        }

        /// <summary>Estado visual inicial.</summary>
        private void Start()
        {
            if (ServiceLocator.TryGet(out NostalgIA.Input.InputReader reader)) scheme = reader.CurrentScheme;
            if (ServiceLocator.TryGet(out IVacuumState vacuum))
            {
                stored = vacuum.StoredCount;
                capacity = vacuum.Capacity;
            }

            if (winPanel != null) winPanel.SetActive(false);
            if (deathOverlay != null) deathOverlay.SetActive(false);
            if (toastContainer != null) toastContainer.SetActive(false);
            SetHurtOverlay(HurtColor, 0f);

            string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(activeSceneName) && activeSceneName != NostalgIA.Core.SceneNames.Tutorial)
            {
                tutorialLeft = true;
                if (guidePanel != null) guidePanel.SetActive(false);
                if (activeSceneName == NostalgIA.Core.SceneNames.InviernoIA && maxStars < 5)
                {
                    maxStars = 5;
                    starToastFormat = "♦ Estrella {0}/" + maxStars;
                }
            }

            RefreshHealth();
            RefreshTool();
            RefreshScore();
            if (!tutorialLeft) EvaluateZone(true);
        }

        /// <summary>Temporizadores de animación y sondeo de zona.</summary>
        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            zonePollTimer -= dt;
            if (!tutorialLeft && zonePollTimer <= 0f)
            {
                zonePollTimer = ZonePollInterval;
                EvaluateZone(false);
            }

            if (toastTimer > 0f)
            {
                toastTimer -= dt;
                if (toastTimer <= 0f && toastContainer != null) toastContainer.SetActive(false);
            }

            if (hurtTimer > 0f && hurtOverlay != null)
            {
                hurtTimer -= dt;
                // α(t) = α_pico · t/T: decae linealmente del pico a cero en la duración del destello.
                float alpha = HurtPeakAlpha * Mathf.Clamp01(hurtTimer / hurtFlashDuration);
                hurtOverlay.canvasRenderer.SetAlpha(alpha);
            }

            if (onCooldown && tool == ToolId.Grapple) RefreshCooldownText();
            AnimateGuide(dt);
        }

        // ------------------------------------------------------------------ eventos del jugador
        private void OnPlayerDamaged(in PlayerDamaged evt)
        {
            hearts = evt.Current;
            if (evt.Max != heartsMax) BuildHealthFormats(evt.Max);
            RefreshHealth();
            if (hurtTimer <= 0f) SetHurtOverlay(HurtColor, HurtPeakAlpha); // El color solo se escribe al iniciar el destello.
            hurtTimer = hurtFlashDuration;
        }

        private void OnPlayerDied(in PlayerDied evt)
        {
            hearts = 0;
            RefreshHealth();
            hurtTimer = 0f;
            SetHurtOverlay(DeathColor, DeathAlpha);
            if (deathOverlay != null) deathOverlay.SetActive(true);
        }

        private void OnPlayerRespawned(in PlayerRespawned evt)
        {
            hearts = evt.Hearts;
            RefreshHealth();
            if (deathOverlay != null) deathOverlay.SetActive(false);
            hurtTimer = 0f;
            SetHurtOverlay(HurtColor, 0f);
        }

        /// <summary>
        /// Fija el color opaco del destello y su alfa visible. El color se asigna solo si cambió
        /// (evita reconstruir el lienzo); el alfa va al <see cref="CanvasRenderer"/>.
        /// </summary>
        private void SetHurtOverlay(Color color, float alpha)
        {
            if (hurtOverlay == null) return;
            if (hurtOverlay.color != color) hurtOverlay.color = color;
            hurtOverlay.canvasRenderer.SetAlpha(alpha);
        }

        private void OnStarCollected(in StarCollected evt)
        {
            stars = Mathf.Min(stars + 1, maxStars);
            RefreshHealth();
            RefreshScore();
            ShowToastFormat(starToastFormat, stars);
            if (stars >= 1) CompleteStep(4);
        }

        private void OnCheckpointActivated(in CheckpointActivated evt) => ShowToast(evt.Label);

        private void OnHudMessage(in HudMessageRequested evt) => ShowToast(evt.Message);

        private void OnPromptTriggered(in PromptTriggered evt)
        {
            hasActivePromptZone = true;

            if (evt.Hide || evt.Mode == PromptZoneMode.Complete)
            {
                if (guidePanel != null) guidePanel.SetActive(false);
                return;
            }

            if (guidePanel != null)
            {
                guidePanel.SetActive(true);
            }
            else if (zoneTitleText != null && zoneTitleText.transform.parent != null)
            {
                zoneTitleText.transform.parent.gameObject.SetActive(true);
            }

            if (zoneTitleText != null && !string.IsNullOrEmpty(evt.Title))
            {
                zoneTitleText.SetText(evt.Title);
            }

            if (descriptionText != null && !string.IsNullOrEmpty(evt.Description))
            {
                descriptionText.SetText(evt.Description);
            }
            else if (taskText != null && !string.IsNullOrEmpty(evt.Description))
            {
                taskText.SetText(evt.Description);
            }

            if (controlsText != null && !string.IsNullOrEmpty(evt.Controls))
            {
                controlsText.SetText(evt.Controls);
            }

            if (guideAccent != null)
            {
                guideAccent.color = new Color(0f, 0.9f, 1f, 1f);
                guideAccent.canvasRenderer.SetAlpha(1f);
            }

            // Reinicia la animación y restaura la opacidad del panel al 100%
            if (guideGroup != null)
            {
                guideGroup.alpha = 1f;
            }
            guideAnimCompleted = false;
            guideAnimTime = 0f;
        }

        private void OnTutorialStepTriggered(in TutorialStepTriggered evt)
        {
            if (tutorialLeft) return;
            hasActivePromptZone = true;

            if (evt.MarkCompleted)
            {
                CompleteStep(evt.StepIndex);
                return;
            }

            if (evt.StepIndex >= 0 && evt.StepIndex < TotalSteps)
            {
                if (!string.IsNullOrEmpty(evt.CustomTitle))
                {
                    customPendingTitles[evt.StepIndex] = $"<color=#FFD166><b>{StepTags[evt.StepIndex]}</b></color>  {evt.CustomTitle}";
                }
                if (!string.IsNullOrEmpty(evt.CustomControls))
                {
                    customStepControls[evt.StepIndex] = evt.CustomControls;
                }

                currentStep = evt.StepIndex;
                RefreshGuide(true);
            }
        }

        private void OnPortalCrossed(in DimensionalPortalCrossed evt)
        {
            if (evt.EnteredLinkedDimension)
            {
                CompleteStep(6);
                ShowToast(GoalMessage);
            }
            else
            {
                ShowToast(ReturnedMessage);
            }
        }

        /// <summary>Al pasar al nivel siguiente se retira la guía del Tutorial; vida y herramienta siguen.</summary>
        private void OnLevelGateCrossed(in LevelGateCrossed evt)
        {
            tutorialLeft = true;
            if (guidePanel != null) guidePanel.SetActive(false);
            stars = 0;
            if (evt.Destination.IsValid() && evt.Destination.name == NostalgIA.Core.SceneNames.InviernoIA)
            {
                maxStars = 5;
            }
            else
            {
                maxStars = 3;
            }
            starToastFormat = "♦ Estrella {0}/" + maxStars;
            RefreshHealth();
            RefreshScore();
        }

        private void OnSchemeChanged(in InputSchemeChanged evt)
        {
            scheme = evt.Current;
            RefreshTool();
            RefreshGuide(false);
        }

        // ------------------------------------------------------------------ eventos de herramientas
        private void OnToolSwitched(in ToolSwitched evt)
        {
            tool = evt.Tool;
            RefreshTool();
        }

        private void OnCooldownChanged(in ToolCooldownChanged evt)
        {
            onCooldown = evt.Remaining > 0f;
            lastCooldownTenths = -1f;
            RefreshTool();
        }

        private void OnObjectAbsorbed(in ObjectAbsorbed evt)
        {
            stored = evt.Stored;
            capacity = evt.Capacity;
            RefreshTool();

            if (TryGetPlayerPosition(out Vector3 pos))
            {
                if (pos.z > 4f) absorbedInStep0 = true;
                else if (pos.x >= 30f && pos.z > -95f) absorbedInStep5 = true;
            }
        }

        private void OnObjectLaunched(in ObjectLaunched evt)
        {
            stored = evt.Stored;
            capacity = evt.Capacity;
            RefreshTool();

            if (TryGetPlayerPosition(out Vector3 pos))
            {
                if (pos.z > 2f && absorbedInStep0) CompleteStep(0);
                else if (pos.x >= 28f && pos.z > -95f && absorbedInStep5) CompleteStep(5);
            }
        }

        // ------------------------------------------------------------------ eventos de locomoción (guía)
        private void OnPlayerJumped(in PlayerJumped evt)
        {
            if (evt.Position.z <= 10f && evt.Position.z >= -6f) CompleteStep(1);
        }

        private void OnLocomotionChanged(in LocomotionStateChanged evt)
        {
            if (!TryGetPlayerPosition(out Vector3 pos)) return;
            if (evt.Current == LocomotionState.Crouching && pos.z <= 0f && pos.z >= -20f) CompleteStep(2);
            if (evt.Current == LocomotionState.Sliding && pos.z <= -5f) CompleteStep(3);
        }

        // ------------------------------------------------------------------ textos
        /// <summary>Formatos de la línea de vida y estrellas, uno por cantidad de corazones.</summary>
        private void BuildHealthFormats(int max)
        {
            heartsMax = Mathf.Max(1, max);
            healthFormats = new string[heartsMax + 1];
            for (int hp = 0; hp <= heartsMax; hp++)
            {
                string full = new string('♥', hp);
                string empty = new string('♥', heartsMax - hp);
                healthFormats[hp] = $"<color=#FF4D6D>{full}<color=#44333A>{empty}</color></color> <size=84%>{hp}/{heartsMax}</size>" +
                                    "     <color=#FFD166>♦</color> <size=88%>{0}/{1}</size>";
            }
        }

        private void RefreshHealth()
        {
            if (healthText == null) return;
            int hp = Mathf.Clamp(hearts, 0, heartsMax);
            healthText.SetText(healthFormats[hp], stars, maxStars);
        }

        private void RefreshScore()
        {
            if (scoreText != null && scoreText.gameObject.activeInHierarchy)
                scoreText.SetText("♦ <color=#FFD166>{0}/{1}</color>", stars, maxStars);
        }

        private void RefreshTool()
        {
            if (toolText == null) return;
            int s = (int)scheme;
            if (tool == ToolId.Vacuum) toolText.SetText(vacuumFormats[s], stored, capacity);
            else if (onCooldown) RefreshCooldownText();
            else toolText.SetText(grappleReadyTexts[s]);
        }

        /// <summary>Actualiza los segundos restantes solo cuando cambia la décima mostrada.</summary>
        private void RefreshCooldownText()
        {
            if (toolText == null) return;
            if (toolState == null && !ServiceLocator.TryGet(out toolState)) return;
            float tenths = Mathf.Ceil(toolState.CooldownRemaining * 10f);
            if (Mathf.Approximately(tenths, lastCooldownTenths)) return;
            lastCooldownTenths = tenths;
            toolText.SetText(grappleCooldownFormats[(int)scheme], tenths * 0.1f);
        }

        private void ShowToast(string message)
        {
            if (checkpointNotificationText == null || string.IsNullOrEmpty(message)) return;
            checkpointNotificationText.SetText(message);
            OpenToast();
        }

        private void ShowToastFormat(string format, float value)
        {
            if (checkpointNotificationText == null) return;
            checkpointNotificationText.SetText(format, value);
            OpenToast();
        }

        private void OpenToast()
        {
            checkpointNotificationText.gameObject.SetActive(true);
            if (toastContainer != null) toastContainer.SetActive(true);
            toastTimer = toastDuration;
        }

        // ------------------------------------------------------------------ guía del tutorial
        private bool TryGetPlayerPosition(out Vector3 position)
        {
            if (motion == null) ServiceLocator.TryGet(out motion);
            position = motion != null ? motion.Position : Vector3.zero;
            return motion != null;
        }

        /// <summary>
        /// Zona del tutorial por posición (mismas fronteras que el prototipo) y pasos que se
        /// completan al dejar atrás su zona.
        /// </summary>
        private void EvaluateZone(bool force)
        {
            if (!TryGetPlayerPosition(out Vector3 pos)) return;

            if (!stepCompleted[0] && ((absorbedInStep0 && pos.z < 9.5f) || pos.z < 5f)) CompleteStep(0);
            if (!stepCompleted[1] && pos.z < -2.2f) CompleteStep(1);
            if (!stepCompleted[2] && pos.z < -12f) CompleteStep(2);
            if (!stepCompleted[3] && pos.z < -31f) CompleteStep(3);
            if (!stepCompleted[5] && pos.z < -78f && pos.x > 35f) CompleteStep(5);

            if (!hasActivePromptZone)
            {
                int step = DetermineStep(pos);
                if (step != currentStep || force)
                {
                    currentStep = step;
                    RefreshGuide(true);
                }
            }
        }

        private static int DetermineStep(Vector3 pos)
        {
            if (pos.x < -1f && pos.z > -34f)
            {
                if (pos.z > 6.5f) return 0;
                if (pos.z > -1.8f) return 1;
                if (pos.z > -10.5f) return 2;
                return 3;
            }
            if (pos.x < 35f && pos.z > -72f) return 4;
            if (pos.z > -88f && pos.x < 75f) return 5;
            return 6;
        }

        private void CompleteStep(int step)
        {
            if (step < 0 || step >= TotalSteps || stepCompleted[step]) return;
            stepCompleted[step] = true;
            if (step == currentStep) RefreshGuide(true);
        }

        /// <summary>Escribe el paso actual y, si se pide, reinicia la animación de aparición.</summary>
        private void RefreshGuide(bool animate)
        {
            if (currentStep < 0 || tutorialLeft) return;
            bool done = stepCompleted[currentStep];
            if (zoneTitleText != null)
            {
                string title = done ? doneTitles[currentStep] : (customPendingTitles[currentStep] ?? pendingTitles[currentStep]);
                zoneTitleText.SetText(title);
            }
            if (controlsText != null)
            {
                string controls = done ? StepDoneControls : (customStepControls[currentStep] ?? StepControls[currentStep, (int)scheme]);
                controlsText.SetText(controls);
            }
            if (guideAccent != null)
            {
                // Color por estado (una escritura al cambiar de paso); el pulso del paso pendiente
                // se anima después solo con el alfa del CanvasRenderer.
                Color accent = done ? AccentDoneColor : AccentPendingColor;
                if (guideAccent.color != accent) guideAccent.color = accent;
                guideAccent.canvasRenderer.SetAlpha(done ? AccentDoneAlpha : 1f);
            }

            if (!animate || guidePanel == null) return;
            guidePanel.SetActive(true);
            guideAnimTime = 0f;
            guideAnimCompleted = done;
            guideStartAlpha = guideGroup != null ? guideGroup.alpha : 1f;
        }

        /// <summary>
        /// Animación de la guía: «pop» de 0,22 s con escala <c>1 + 0,035·sin(π·p)</c>; si el paso
        /// está completo, tras 0,9 s se atenúa a 0,18 en 0,45 s. Si está pendiente, la línea de
        /// acento pulsa con <c>α = 0,65 + 0,35·sin(4,5·t)</c>.
        /// </summary>
        private void AnimateGuide(float dt)
        {
            if (guidePanel == null) return;

            if (currentStep >= 0 && !stepCompleted[currentStep] && guideAccent != null)
            {
                // α(t) = 0,65 + 0,35·sin(4,5·t): pulso de ≈ 0,7 Hz sin regenerar la malla.
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4.5f);
                guideAccent.canvasRenderer.SetAlpha(pulse);
            }

            if (guideAnimTime < 0f) return;
            guideAnimTime += dt;
            const float pop = 0.22f;
            const float hold = 0.9f;
            const float fade = 0.45f;
            float target = guideAnimCompleted ? 0.85f : 1f;
            Transform panel = guidePanel.transform;

            if (guideAnimTime < pop)
            {
                float p = guideAnimTime / pop;
                panel.localScale = Vector3.one * (1f + Mathf.Sin(p * Mathf.PI) * 0.035f);
                if (guideGroup != null) guideGroup.alpha = Mathf.Lerp(guideStartAlpha, target, p);
                return;
            }

            panel.localScale = Vector3.one;
            if (!guideAnimCompleted)
            {
                if (guideGroup != null) guideGroup.alpha = 1f;
                guideAnimTime = -1f;
                return;
            }

            float fadeT = (guideAnimTime - pop - hold) / fade;
            if (fadeT <= 0f)
            {
                if (guideGroup != null) guideGroup.alpha = target;
                return;
            }
            if (guideGroup != null) guideGroup.alpha = Mathf.Lerp(target, 0.18f, Mathf.Clamp01(fadeT));
            if (fadeT >= 1f) guideAnimTime = -1f;
        }
    }
}
