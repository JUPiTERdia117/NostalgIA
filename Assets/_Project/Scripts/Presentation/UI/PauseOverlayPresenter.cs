// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: PauseOverlayPresenter.cs
// Propósito: capa de pausa con resumen de controles por esquema, botón para
// continuar y ajustes del juego, visible solo en GameState.Paused.
// -----------------------------------------------------------------------------

using System.Text;
using NostalgIA.Core.Events;
using NostalgIA.Core.Game;
using NostalgIA.Core.Services;
using NostalgIA.Input;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NostalgIA.Presentation.UI
{
    /// <summary>
    /// Muestra la capa de pausa y permite reanudar con el botón «Continuar».
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Corrección de QA.</b> Durante la pausa, <c>TouchControlsVisibility</c> oculta los
    /// controles táctiles (incluido el botón de pausa), así que en móvil no había forma de
    /// reanudar. Esta capa aporta un botón de interfaz que funciona con toque, ratón y mando
    /// (se selecciona al abrirse para la navegación con el mando).
    /// </para>
    /// <para>
    /// Los tres textos de controles (uno por esquema) se componen una sola vez en <c>Awake</c> a
    /// partir de <see cref="InputGlyphSet"/>; cambiar de esquema solo cambia qué cadena se muestra.
    /// La capa se oculta desactivando el <see cref="Canvas"/>, lo que evita su dibujo y su
    /// reconstrucción mientras se juega.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    public sealed class PauseOverlayPresenter : MonoBehaviour
    {
        [SerializeField] private InputGlyphSet glyphs;
        [SerializeField] private TextMeshProUGUI controlsText;
        [SerializeField] private TextMeshProUGUI resumeHintText;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private TextMeshProUGUI nextLevelLabel;
        [Tooltip("Columna de ajustes (los mismos del menú principal).")]
        [SerializeField] private PauseSettingsPanel settings;

        /// <summary>Acciones listadas en la capa, en orden de lectura.</summary>
        private static readonly string[] ListedActions =
        {
            InputActionNames.Move, InputActionNames.Jump, InputActionNames.Sprint, InputActionNames.Crouch,
            InputActionNames.Absorb, InputActionNames.Fire, InputActionNames.SwitchTool, InputActionNames.Grapple
        };

        private Canvas canvas;
        private string[] controlsByScheme;
        private string[] resumeByScheme;
        private InputScheme scheme = InputScheme.KeyboardMouse;

        /// <summary>Compone los textos y conecta el botón.</summary>
        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            EnsureNextLevelTestButton();
            BuildTexts();
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (nextLevelButton != null) nextLevelButton.onClick.AddListener(RequestNextLevelSkip);
            SetVisible(false);
        }

        /// <summary>Suscripciones y sincronización con el estado actual.</summary>
        private void OnEnable()
        {
            EventBus<GameStateChanged>.Subscribe(OnGameStateChanged);
            EventBus<InputSchemeChanged>.Subscribe(OnSchemeChanged);
            EventBus<NextLevelStatusReported>.Subscribe(OnNextLevelStatusReported);
            if (ServiceLocator.TryGet(out InputReader reader))
            {
                scheme = reader.CurrentScheme;
                reader.BindingsChanged += OnBindingsChanged;
            }
            bool paused = ServiceLocator.TryGet(out GameStateMachine machine) && machine.State == GameState.Paused;
            SetVisible(paused);
        }

        /// <summary>Cancela suscripciones.</summary>
        private void OnDisable()
        {
            EventBus<GameStateChanged>.Unsubscribe(OnGameStateChanged);
            EventBus<InputSchemeChanged>.Unsubscribe(OnSchemeChanged);
            EventBus<NextLevelStatusReported>.Unsubscribe(OnNextLevelStatusReported);
            if (ServiceLocator.TryGet(out InputReader reader)) reader.BindingsChanged -= OnBindingsChanged;
        }

        /// <summary>El jugador cambió una tecla en los ajustes: se rehace la lista de controles.</summary>
        private void OnBindingsChanged()
        {
            BuildTexts();
            if (canvas.enabled) RefreshTexts();
        }

        private void OnGameStateChanged(in GameStateChanged evt) => SetVisible(evt.Current == GameState.Paused);

        private void OnSchemeChanged(in InputSchemeChanged evt)
        {
            scheme = evt.Current;
            if (canvas.enabled) RefreshTexts();
        }

        private void OnNextLevelStatusReported(in NextLevelStatusReported evt)
        {
            if (nextLevelLabel != null)
            {
                nextLevelLabel.SetText(evt.StatusText);
                nextLevelLabel.color = evt.HasNextLevel
                    ? new Color(0.91f, 0.96f, 1f, 1f)
                    : new Color(1f, 0.72f, 0.56f, 1f);
            }
            if (nextLevelButton != null && nextLevelButton.targetGraphic != null)
            {
                nextLevelButton.targetGraphic.color = evt.HasNextLevel
                    ? new Color(0.11f, 0.36f, 0.68f, 0.96f)
                    : new Color(0.35f, 0.18f, 0.24f, 0.96f);
            }
        }

        /// <summary>Reanuda el juego (botón «Continuar»).</summary>
        public void Resume()
        {
            if (ServiceLocator.TryGet(out GameStateMachine machine) && machine.State == GameState.Paused)
                machine.TogglePause();
        }

        /// <summary>Solicita saltar inmediatamente al siguiente nivel (botón de prueba en pausa).</summary>
        public void RequestNextLevelSkip()
        {
            EventBus<NextLevelSkipRequested>.Raise(new NextLevelSkipRequested());
        }

        /// <summary>
        /// Garantiza que el botón de prueba «Siguiente nivel» exista dentro de la ventana de pausa
        /// incluso si el prefab <c>GameRoot</c> aún no se regeneró desde el menú de editor.
        /// </summary>
        private void EnsureNextLevelTestButton()
        {
            if (nextLevelButton != null && nextLevelLabel != null) return;
            if (resumeButton == null) return;

            Transform window = resumeButton.transform.parent;
            if (window == null) return;

            RectTransform resumeRect = resumeButton.GetComponent<RectTransform>();
            if (resumeRect != null && resumeRect.anchoredPosition.y < -150f)
            {
                resumeRect.anchoredPosition = new Vector2(0f, -135f);
                resumeRect.sizeDelta = new Vector2(360f, 58f);
            }

            Transform existing = window.Find("NextLevelTestButton");
            GameObject btnGo = existing != null ? existing.gameObject : new GameObject("NextLevelTestButton", typeof(RectTransform));
            if (existing == null) btnGo.transform.SetParent(window, false);

            var rect = (RectTransform)btnGo.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -206f);
            rect.sizeDelta = new Vector2(440f, 54f);

            Image img = btnGo.GetComponent<Image>();
            if (img == null) img = btnGo.AddComponent<Image>();
            img.color = new Color(0.11f, 0.36f, 0.68f, 0.96f);
            img.raycastTarget = true;

            nextLevelButton = btnGo.GetComponent<Button>();
            if (nextLevelButton == null) nextLevelButton = btnGo.AddComponent<Button>();
            nextLevelButton.targetGraphic = img;

            Transform labelTransform = btnGo.transform.Find("Label");
            GameObject labelGo = labelTransform != null ? labelTransform.gameObject : new GameObject("Label", typeof(RectTransform));
            if (labelTransform == null) labelGo.transform.SetParent(btnGo.transform, false);

            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(420f, 48f);

            nextLevelLabel = labelGo.GetComponent<TextMeshProUGUI>();
            if (nextLevelLabel == null) nextLevelLabel = labelGo.AddComponent<TextMeshProUGUI>();
            if (resumeHintText != null && resumeHintText.font != null) nextLevelLabel.font = resumeHintText.font;
            nextLevelLabel.fontSize = 21f;
            nextLevelLabel.alignment = TextAlignmentOptions.Center;
            nextLevelLabel.color = new Color(0.91f, 0.96f, 1f, 1f);
            nextLevelLabel.raycastTarget = false;
            if (string.IsNullOrEmpty(nextLevelLabel.text))
                nextLevelLabel.text = "Siguiente nivel (Test)";
        }

        /// <summary>Muestra u oculta la capa.</summary>
        private void SetVisible(bool visible)
        {
            if (canvas == null) return;
            canvas.enabled = visible;
            if (!visible)
            {
                // Al cerrar la pausa se escriben en disco los ajustes que se hayan cambiado.
                if (settings != null) settings.Save();
                return;
            }
            RefreshTexts();
            if (settings != null) settings.Refresh();
            // Seleccionamos el botón para que el mando pueda pulsarlo sin apuntar.
            if (resumeButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }

        /// <summary>Muestra las cadenas del esquema actual.</summary>
        private void RefreshTexts()
        {
            int s = Mathf.Clamp((int)scheme, 0, controlsByScheme.Length - 1);
            if (controlsText != null) controlsText.SetText(controlsByScheme[s]);
            if (resumeHintText != null) resumeHintText.SetText(resumeByScheme[s]);
        }

        /// <summary>
        /// Compone una cadena por esquema: «[etiqueta]  Acción» por línea, con la etiqueta en el
        /// color de acento de interfaz. Solo se ejecuta una vez.
        /// </summary>
        private void BuildTexts()
        {
            const int schemeCount = 3; // KeyboardMouse, Gamepad, Touch (orden de InputScheme).
            controlsByScheme = new string[schemeCount];
            resumeByScheme = new string[schemeCount];
            var builder = new StringBuilder(512);

            for (int s = 0; s < schemeCount; s++)
            {
                var current = (InputScheme)s;
                builder.Clear();
                for (int i = 0; i < ListedActions.Length; i++)
                {
                    if (glyphs == null || !glyphs.TryGet(ListedActions[i], out InputGlyphSet.Entry entry)) continue;
                    builder.Append("<color=#9DB8FF><b>[")
                           .Append(glyphs.GetLabel(ListedActions[i], current))
                           .Append("]</b></color>  ")
                           .Append(entry.displayName)
                           .Append('\n');
                }
                controlsByScheme[s] = builder.ToString();

                string pauseLabel = glyphs != null ? glyphs.GetLabel(InputActionNames.Pause, current) : string.Empty;
                resumeByScheme[s] = string.IsNullOrEmpty(pauseLabel)
                    ? "Continuar"
                    : "<color=#C9D8FF>[" + pauseLabel + "]</color>  Continuar";
            }
        }
    }
}
