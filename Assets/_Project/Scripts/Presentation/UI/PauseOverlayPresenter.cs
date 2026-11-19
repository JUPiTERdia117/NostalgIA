// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: PauseOverlayPresenter.cs
// Propósito: capa de pausa con resumen de controles por esquema, botones para
// continuar y volver al menú, y ajustes del juego, visible solo en GameState.Paused.
// -----------------------------------------------------------------------------

using System.Text;
using NostalgIA.Core.Events;
using NostalgIA.Core.Game;
using NostalgIA.Core.Services;
using NostalgIA.Input;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NostalgIA.Presentation.UI
{
    /// <summary>
    /// Muestra la capa de pausa; permite reanudar con «Continuar» o salir con «Volver al menú».
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
        [Tooltip("Botón «Volver al menú».")]
        [SerializeField] private Button menuButton;
        [SerializeField] private TextMeshProUGUI menuLabel;
        [Tooltip("Columna de ajustes (los mismos del menú principal).")]
        [SerializeField] private PauseSettingsPanel settings;

        /// <summary>Acciones listadas en la capa, en orden de lectura.</summary>
        private static readonly string[] ListedActions =
        {
            InputActionNames.Move, InputActionNames.Jump, InputActionNames.Sprint, InputActionNames.Crouch,
            InputActionNames.Absorb, InputActionNames.Fire, InputActionNames.SwitchTool, InputActionNames.Grapple
        };

        private const string MenuScene = "Menu";

        private Canvas canvas;
        private string[] controlsByScheme;
        private string[] resumeByScheme;
        private InputScheme scheme = InputScheme.KeyboardMouse;

        /// <summary>Compone los textos y conecta el botón.</summary>
        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            BuildTexts();
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);
            if (menuLabel != null) menuLabel.text = "Volver al menú";
            SetVisible(false);
        }

        /// <summary>Suscripciones y sincronización con el estado actual.</summary>
        private void OnEnable()
        {
            EventBus<GameStateChanged>.Subscribe(OnGameStateChanged);
            EventBus<InputSchemeChanged>.Subscribe(OnSchemeChanged);
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

        /// <summary>Reanuda el juego (botón «Continuar»).</summary>
        public void Resume()
        {
            if (ServiceLocator.TryGet(out GameStateMachine machine) && machine.State == GameState.Paused)
                machine.TogglePause();
        }

        /// <summary>Sale del nivel al menú principal (botón «Volver al menú»).</summary>
        /// <remarks>
        /// El progreso ya guardado (niveles completados y estrellas) se conserva; lo jugado en el
        /// nivel actual sin cruzar la puerta se pierde, igual que al cerrar el juego.
        /// </remarks>
        public void GoToMenu()
        {
            // La pausa detiene el tiempo: se reanuda antes de cambiar de escena.
            Time.timeScale = 1f;
            SceneManager.LoadScene(MenuScene);
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
