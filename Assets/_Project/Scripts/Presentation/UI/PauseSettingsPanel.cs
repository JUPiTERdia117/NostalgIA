// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: PauseSettingsPanel.cs
// Propósito: columna de ajustes del menú de pausa, con los mismos ajustes que el
// menú principal (escena Menu) y las teclas configurables.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Game;
using NostalgIA.Core.Services;
using NostalgIA.Input;
using NostalgIA.Presentation.Audio;
using NostalgIA.Presentation.VFX;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NostalgIA.Presentation.UI
{
    /// <summary>
    /// Arma en <c>Awake</c> una fila por ajuste dentro de la ventana de pausa y la mantiene
    /// sincronizada con lo guardado.
    /// </summary>
    /// <remarks>
    /// Usa las mismas clases que el menú principal (<see cref="GameAudio"/>, <see cref="GameGraphics"/>,
    /// <see cref="LookSettings"/> y <see cref="HudSettings"/>), así que cambiar algo aquí también se
    /// ve en el menú y al revés. Las filas se crean por código con <see cref="UiKit"/>. Los controles
    /// son <see cref="Selectable"/> normales, así que funcionan con ratón, toque y mando.
    /// Las filas de teclas (agacharse y cambiar herramienta) solo aparecen en PC.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PauseSettingsPanel : MonoBehaviour
    {
        [Tooltip("Ventana de pausa donde se crea la columna de ajustes.")]
        [SerializeField] private RectTransform window;
        [Tooltip("Fuente de los textos (la misma del resto de la pausa).")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Centro de la columna dentro de la ventana.")]
        [SerializeField] private Vector2 position = new Vector2(300f, -30f);

        private const float Width = 540f;
        private const float RowHeight = 44f;
        private static readonly string[] QualityNames = { "bajo", "medio", "alto", "muy alto" };

        private RectTransform column;
        private int rowCount;
        private Slider music, sfx, sensitivityX, sensitivityY;
        private TextMeshProUGUI hudText, fullScreenText, qualityText, invertText, crouchKeyText, switchKeyText;

        /// <summary>Crea las filas.</summary>
        private void Awake()
        {
            if (window == null) return;
            bool desktop = !Application.isMobilePlatform;
            int rows = desktop ? 12 : 9;
            column = UiKit.NewRect("Settings", window, position, new Vector2(Width, rows * RowHeight));

            TextMeshProUGUI title = UiKit.NewText(column, font, "AJUSTES", new Vector2(0f, -RowHeight * 0.5f),
                                                  new Vector2(Width, RowHeight), 30f, TextAlignmentOptions.Center);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f); // arriba de la columna
            title.color = UiKit.AccentColor;
            rowCount = 1;

            music = AddSlider("Volumen de música", 0f, 1f, GameAudio.SetMusicVolume);
            sfx = AddSlider("Volumen de efectos", 0f, 1f, GameAudio.SetSfxVolume);
            hudText = AddButton("Mostrar HUD", ToggleHud);
            // En teléfonos la app siempre ocupa toda la pantalla, así que no tiene caso.
            if (desktop) fullScreenText = AddButton("Pantalla completa", ToggleFullScreen);
            qualityText = AddButton("Calidad gráfica", NextQuality);
            sensitivityX = AddSlider("Sensibilidad X", LookSettings.Min, LookSettings.Max, value => LookSettings.SensitivityX = value);
            sensitivityY = AddSlider("Sensibilidad Y", LookSettings.Min, LookSettings.Max, value => LookSettings.SensitivityY = value);
            invertText = AddButton("Invertir mouse", ToggleInvert);
            if (desktop)
            {
                // El botón muestra la tecla actual; al pulsarlo espera la nueva.
                crouchKeyText = AddButton("Agacharse", () => Rebind(InputActionNames.Crouch, crouchKeyText));
                switchKeyText = AddButton("Cambiar herramienta", () => Rebind(InputActionNames.SwitchTool, switchKeyText));
            }

            Refresh();
        }

        /// <summary>Muestra los valores guardados (se llama al abrir la pausa).</summary>
        public void Refresh()
        {
            if (column == null) return; // todavía no se crean las filas
            music.SetValueWithoutNotify(GameAudio.MusicVolume);
            sfx.SetValueWithoutNotify(GameAudio.SfxVolume);
            sensitivityX.SetValueWithoutNotify(LookSettings.SensitivityX);
            sensitivityY.SetValueWithoutNotify(LookSettings.SensitivityY);
            hudText.SetText(UiKit.OnOff(HudSettings.Show));
            if (fullScreenText != null) fullScreenText.SetText(UiKit.OnOff(Screen.fullScreen));
            qualityText.SetText(QualityNames[(int)GameGraphics.Current]);
            invertText.SetText(UiKit.OnOff(LookSettings.Invert));
            RefreshKeys();
        }

        /// <summary>Escribe los ajustes en disco (se llama al cerrar la pausa).</summary>
        public void Save()
        {
            if (ServiceLocator.TryGet(out InputReader reader)) reader.CancelRebind();
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------- acciones de los botones
        private void ToggleHud()
        {
            HudSettings.Show = !HudSettings.Show;
            hudText.SetText(UiKit.OnOff(HudSettings.Show));
        }

        private void ToggleFullScreen()
        {
            // Screen.fullScreen cambia hasta el siguiente frame, por eso uso el valor nuevo.
            bool full = !Screen.fullScreen;
            Screen.fullScreen = full;
            fullScreenText.SetText(UiKit.OnOff(full));
        }

        private void NextQuality()
        {
            // Bajo → medio → alto → muy alto → bajo.
            var next = (GraphicsPreset)(((int)GameGraphics.Current + 1) % QualityNames.Length);
            GameGraphics.Set(next);
            qualityText.SetText(QualityNames[(int)next]);
        }

        private void ToggleInvert()
        {
            LookSettings.Invert = !LookSettings.Invert;
            invertText.SetText(UiKit.OnOff(LookSettings.Invert));
        }

        /// <summary>Espera la tecla nueva; mientras, el botón dice «Pulsa una tecla…».</summary>
        private void Rebind(string action, TextMeshProUGUI label)
        {
            if (!ServiceLocator.TryGet(out InputReader reader)) return;
            label.SetText("Pulsa una tecla…");
            reader.StartRebind(action, RefreshKeys);
        }

        /// <summary>Muestra la tecla actual de cada acción configurable.</summary>
        private void RefreshKeys()
        {
            if (crouchKeyText == null || !ServiceLocator.TryGet(out InputReader reader)) return;
            crouchKeyText.SetText(reader.GetKeyLabel(InputActionNames.Crouch));
            switchKeyText.SetText(reader.GetKeyLabel(InputActionNames.SwitchTool));
        }

        // ------------------------------------------------------------- construcción de filas
        /// <summary>Fila con su etiqueta a la izquierda; el control va a la derecha.</summary>
        private RectTransform AddRow(string label)
        {
            RectTransform row = UiKit.NewRect(label, column, Vector2.zero, new Vector2(Width, RowHeight));
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -RowHeight * (rowCount + 0.5f));
            rowCount++;
            UiKit.NewText(row, font, label, new Vector2(-135f, 0f), new Vector2(270f, RowHeight), 22f, TextAlignmentOptions.Left);
            return row;
        }

        /// <summary>Botón que muestra el valor actual y lo cambia al pulsarlo.</summary>
        private TextMeshProUGUI AddButton(string label, UnityAction onClick)
        {
            RectTransform row = AddRow(label);
            Button button = UiKit.NewButton(row, font, string.Empty, new Vector2(140f, 0f), new Vector2(240f, 36f), 21f, onClick);
            return button.GetComponentInChildren<TextMeshProUGUI>();
        }

        /// <summary>Deslizador horizontal: barra, relleno y asa.</summary>
        private Slider AddSlider(string label, float min, float max, UnityAction<float> onChange)
        {
            RectTransform row = AddRow(label);
            Image track = UiKit.NewImage(row, "Slider", new Vector2(140f, 0f), new Vector2(220f, 12f), UiKit.TrackColor);

            // El Slider estira el relleno desde la izquierda hasta el valor.
            Image fill = UiKit.NewImage(track.rectTransform, "Fill", Vector2.zero, Vector2.zero, UiKit.AccentColor);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.raycastTarget = false;

            Image handle = UiKit.NewImage(track.rectTransform, "Handle", Vector2.zero, new Vector2(18f, 30f), Color.white);

            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.colors = UiKit.Colors(UiKit.TextColor);
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }
    }
}
