// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: FlowScreensPresenter.cs
// Propósito: pantalla de resultados al completar un nivel y pantalla de muerte
// con el botón para reaparecer.
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Core.Events;
using NostalgIA.Core.Game;
using NostalgIA.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NostalgIA.Presentation.UI
{
    /// <summary>
    /// Pantallas del flujo de partida, creadas por código en un lienzo propio (vive en <c>GameRoot</c>,
    /// así que existe en todos los niveles).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><b>Resultados</b> (<see cref="LevelCompleted"/>): estrellas, platino, tiempo y mejora
    /// obtenida. Con nivel siguiente ofrece «Continuar» y «Menú principal»; en la meta final, solo
    /// «Volver al menú».</item>
    /// <item><b>Muerte</b> (<see cref="PlayerDied"/>): aparece tras una pausa breve, para que se vea
    /// el robot desarmado en el suelo, con el botón «Reaparecer» (o «Reiniciar tutorial» en el
    /// Tutorial), que publica <see cref="RespawnRequested"/>.</item>
    /// </list>
    /// Al abrir cada pantalla se selecciona su botón principal para poder usar el mando.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FlowScreensPresenter : MonoBehaviour
    {
        [Tooltip("Fuente de los textos (la misma de la pausa).")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Segundos entre la muerte y la aparición del botón.")]
        [SerializeField, Min(0f)] private float deathPanelDelay = 1.6f;

        private const string MenuScene = "Menu";

        private GameObject resultsPanel;
        private TextMeshProUGUI resultsTitle, resultsBody;
        private Button continueButton, menuButton;

        private GameObject deathPanel;
        private TextMeshProUGUI respawnLabel;
        private Button respawnButton;
        private float deathTimer = -1f;

        private void Awake()
        {
            Canvas canvas = UiKit.NewCanvas("FlowScreens", transform, 250);
            BuildResults(canvas.transform);
            BuildDeath(canvas.transform);
            resultsPanel.SetActive(false);
            deathPanel.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus<LevelCompleted>.Subscribe(OnLevelCompleted);
            EventBus<PlayerDied>.Subscribe(OnPlayerDied);
            EventBus<PlayerRespawned>.Subscribe(OnPlayerRespawned);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            EventBus<LevelCompleted>.Unsubscribe(OnLevelCompleted);
            EventBus<PlayerDied>.Unsubscribe(OnPlayerDied);
            EventBus<PlayerRespawned>.Unsubscribe(OnPlayerRespawned);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>La pantalla de muerte aparece después de la pausa (tiempo sin escalar).</summary>
        private void Update()
        {
            if (deathTimer < 0f) return;
            deathTimer -= Time.unscaledDeltaTime;
            if (deathTimer > 0f) return;
            deathTimer = -1f;
            respawnLabel.SetText(SceneManager.GetActiveScene().name == SceneNames.Tutorial ? "Reiniciar tutorial" : "Reaparecer");
            deathPanel.SetActive(true);
            Select(respawnButton);
        }

        // ------------------------------------------------------------------ eventos
        private void OnLevelCompleted(in LevelCompleted evt)
        {
            HideDeath();
            resultsTitle.SetText($"¡{evt.Title} completado!");

            string stars = evt.TotalStars > 0 ? $"♦  Estrellas: {evt.Stars} / {evt.TotalStars}" : "♦  Este nivel no tiene estrellas";
            string platinum = evt.IsPlatinum
                ? "<color=#FFD166>√  NIVEL PLATINADO</color>"
                : "<color=#8D99AE>Consigue todas las estrellas para platinarlo</color>";
            int minutes = Mathf.FloorToInt(evt.TimeSeconds / 60f);
            int seconds = Mathf.FloorToInt(evt.TimeSeconds % 60f);
            string body = $"{stars}\n{platinum}\nTiempo: {minutes}:{seconds:00}";
            if (evt.UpgradeText.Length > 0) body += $"\n\n<color=#9DB8FF>Mejoras obtenidas</color>\n{evt.UpgradeText}";
            resultsBody.SetText(body);

            // Con nivel siguiente: «Continuar» y «Menú principal»; en la meta final, solo el menú.
            continueButton.gameObject.SetActive(evt.HasNextLevel);
            menuButton.GetComponentInChildren<TextMeshProUGUI>().SetText(evt.HasNextLevel ? "Menú principal" : "Volver al menú");
            resultsPanel.SetActive(true);
            Select(evt.HasNextLevel ? continueButton : menuButton);
        }

        private void OnPlayerDied(in PlayerDied evt) => deathTimer = deathPanelDelay;

        private void OnPlayerRespawned(in PlayerRespawned evt) => HideDeath();

        /// <summary>Al cargar otra escena (menú o reinicio) se cierran las pantallas.</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            HideDeath();
            resultsPanel.SetActive(false);
        }

        // ------------------------------------------------------------------ botones
        private void Continue()
        {
            resultsPanel.SetActive(false);
            if (ServiceLocator.TryGet(out GameStateMachine machine)) machine.ChangeState(GameState.Playing);
        }

        private void GoToMenu()
        {
            resultsPanel.SetActive(false);
            SceneManager.LoadScene(MenuScene);
        }

        private void Respawn()
        {
            HideDeath();
            EventBus<RespawnRequested>.Raise(new RespawnRequested());
        }

        private void HideDeath()
        {
            deathTimer = -1f;
            deathPanel.SetActive(false);
        }

        private static void Select(Selectable selectable)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        // ------------------------------------------------------------------ construcción
        /// <summary>Ventana centrada con borde de acento y fondo oscurecido, como la pausa.</summary>
        private static RectTransform NewWindow(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform root = UiKit.Stretch(UiKit.NewRect(name, parent, Vector2.zero, Vector2.zero));
            UiKit.Stretch(UiKit.NewImage(root, "Backdrop", Vector2.zero, Vector2.zero, new Color(0.02f, 0.05f, 0.14f, 0.7f)).rectTransform);
            UiKit.NewImage(root, "Border", position, size + new Vector2(4f, 4f), UiKit.AccentColor);
            return UiKit.NewImage(root, "Window", position, size, UiKit.WindowColor).rectTransform;
        }

        private void BuildResults(Transform canvas)
        {
            RectTransform window = NewWindow(canvas, "Results", Vector2.zero, new Vector2(720f, 520f));
            resultsPanel = window.parent.gameObject;
            resultsTitle = UiKit.NewText(window, font, string.Empty, new Vector2(0f, 200f), new Vector2(660f, 70f), 40f, TextAlignmentOptions.Center);
            resultsBody = UiKit.NewText(window, font, string.Empty, new Vector2(0f, 20f), new Vector2(640f, 280f), 26f, TextAlignmentOptions.Center);
            continueButton = UiKit.NewButton(window, font, "Continuar", new Vector2(-170f, -200f), new Vector2(300f, 60f), 26f, Continue);
            menuButton = UiKit.NewButton(window, font, "Menú principal", new Vector2(170f, -200f), new Vector2(300f, 60f), 26f, GoToMenu);
        }

        private void BuildDeath(Transform canvas)
        {
            // Va en la parte baja de la pantalla para no tapar las piezas del robot.
            RectTransform window = NewWindow(canvas, "Death", new Vector2(0f, -330f), new Vector2(620f, 300f));
            deathPanel = window.parent.gameObject;
            // El fondo de la muerte es casi transparente por la misma razón.
            window.parent.GetChild(0).GetComponent<Image>().color = new Color(0.1f, 0f, 0.02f, 0.25f);
            TextMeshProUGUI title = UiKit.NewText(window, font, "TE DESARMASTE", new Vector2(0f, 80f), new Vector2(560f, 70f), 42f, TextAlignmentOptions.Center);
            title.color = new Color(1f, 0.45f, 0.45f, 1f);
            UiKit.NewText(window, font, "Tus piezas quedaron en el suelo.", new Vector2(0f, 15f), new Vector2(560f, 40f), 24f, TextAlignmentOptions.Center);
            respawnButton = UiKit.NewButton(window, font, "Reaparecer", new Vector2(0f, -80f), new Vector2(340f, 60f), 26f, Respawn);
            respawnLabel = respawnButton.GetComponentInChildren<TextMeshProUGUI>();
        }
    }
}
