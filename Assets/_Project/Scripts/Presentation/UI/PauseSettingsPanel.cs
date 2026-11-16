// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: PauseSettingsPanel.cs
// Propósito: columna de ajustes del menú de pausa, con los mismos ajustes que el
// menú principal (escena Menu).
// -----------------------------------------------------------------------------

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
    /// Usa las mismas clases que el menú principal (<see cref="GameAudio"/>, <see cref="GameGraphics"/>
    /// y <see cref="LookSettings"/>), así que cambiar algo aquí también se ve en el menú y al revés.
    /// Las filas se crean por código para no repetir a mano en el prefab ocho controles casi iguales.
    /// Los controles son <see cref="Selectable"/> normales, así que funcionan con ratón, toque y mando.
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
        private const float RowHeight = 52f;
        private const string ShowHudKey = "ShowHUD"; // misma clave que el menú principal
        private static readonly string[] QualityNames = { "bajo", "medio", "alto" };

        private static readonly Color TextColor = new Color(0.91f, 0.957f, 1f, 1f);
        private static readonly Color AccentColor = new Color(0.227f, 0.42f, 1f, 1f);
        private static readonly Color ButtonColor = new Color(0.11f, 0.36f, 0.68f, 0.96f);
        private static readonly Color TrackColor = new Color(0.02f, 0.06f, 0.17f, 0.9f);

        private RectTransform column;
        private int rowCount;
        private Slider music, sfx, sensitivityX, sensitivityY;
        private TextMeshProUGUI hudText, fullScreenText, qualityText, invertText;

        /// <summary>Crea las filas.</summary>
        private void Awake()
        {
            if (window == null) return;
            column = NewRect("Settings", window, position, new Vector2(Width, 9f * RowHeight));

            TextMeshProUGUI title = NewText(column, "AJUSTES", new Vector2(0f, -RowHeight * 0.5f), new Vector2(Width, RowHeight), 30f, TextAlignmentOptions.Center);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f); // arriba de la columna
            title.color = AccentColor;
            rowCount = 1;

            music = AddSlider("Volumen de música", 0f, 1f, GameAudio.SetMusicVolume);
            sfx = AddSlider("Volumen de efectos", 0f, 1f, GameAudio.SetSfxVolume);
            hudText = AddButton("Mostrar HUD", ToggleHud);
            // En teléfonos la app siempre ocupa toda la pantalla, así que no tiene caso.
            if (!Application.isMobilePlatform) fullScreenText = AddButton("Pantalla completa", ToggleFullScreen);
            qualityText = AddButton("Calidad gráfica", NextQuality);
            sensitivityX = AddSlider("Sensibilidad X", LookSettings.Min, LookSettings.Max, value => LookSettings.SensitivityX = value);
            sensitivityY = AddSlider("Sensibilidad Y", LookSettings.Min, LookSettings.Max, value => LookSettings.SensitivityY = value);
            invertText = AddButton("Invertir mouse", ToggleInvert);

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
            hudText.SetText(OnOff(PlayerPrefs.GetInt(ShowHudKey) == 1));
            if (fullScreenText != null) fullScreenText.SetText(OnOff(Screen.fullScreen));
            qualityText.SetText(QualityNames[(int)GameGraphics.Current]);
            invertText.SetText(OnOff(LookSettings.Invert));
        }

        /// <summary>Escribe los ajustes en disco (se llama al cerrar la pausa).</summary>
        public void Save() => PlayerPrefs.Save();

        // ------------------------------------------------------------- acciones de los botones
        private void ToggleHud()
        {
            bool show = PlayerPrefs.GetInt(ShowHudKey) == 0;
            PlayerPrefs.SetInt(ShowHudKey, show ? 1 : 0);
            hudText.SetText(OnOff(show));
        }

        private void ToggleFullScreen()
        {
            // Screen.fullScreen cambia hasta el siguiente frame, por eso uso el valor nuevo.
            bool full = !Screen.fullScreen;
            Screen.fullScreen = full;
            fullScreenText.SetText(OnOff(full));
        }

        private void NextQuality()
        {
            // Bajo → medio → alto → bajo.
            var next = (GraphicsPreset)(((int)GameGraphics.Current + 1) % 3);
            GameGraphics.Set(next);
            qualityText.SetText(QualityNames[(int)next]);
        }

        private void ToggleInvert()
        {
            LookSettings.Invert = !LookSettings.Invert;
            invertText.SetText(OnOff(LookSettings.Invert));
        }

        private static string OnOff(bool on) => on ? "encendido" : "apagado";

        // ------------------------------------------------------------- construcción de filas
        /// <summary>Fila con su etiqueta a la izquierda; el control va a la derecha.</summary>
        private RectTransform AddRow(string label)
        {
            var row = NewRect(label, column, Vector2.zero, new Vector2(Width, RowHeight));
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -RowHeight * (rowCount + 0.5f));
            rowCount++;
            NewText(row, label, new Vector2(-135f, 0f), new Vector2(270f, RowHeight), 24f, TextAlignmentOptions.Left);
            return row;
        }

        /// <summary>Botón que muestra el valor actual y lo cambia al pulsarlo.</summary>
        private TextMeshProUGUI AddButton(string label, UnityAction onClick)
        {
            RectTransform row = AddRow(label);
            Image image = NewImage(row, "Button", new Vector2(140f, 0f), new Vector2(240f, 40f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = Colors(ButtonColor);
            button.onClick.AddListener(onClick);
            return NewText(image.rectTransform, string.Empty, Vector2.zero, new Vector2(230f, 40f), 22f, TextAlignmentOptions.Center);
        }

        /// <summary>Deslizador horizontal: barra, relleno y asa.</summary>
        private Slider AddSlider(string label, float min, float max, UnityAction<float> onChange)
        {
            RectTransform row = AddRow(label);
            Image track = NewImage(row, "Slider", new Vector2(140f, 0f), new Vector2(220f, 12f));
            track.color = TrackColor;

            // El Slider estira el relleno desde la izquierda hasta el valor.
            Image fill = NewImage(track.rectTransform, "Fill", Vector2.zero, Vector2.zero);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.color = AccentColor;
            fill.raycastTarget = false;

            Image handle = NewImage(track.rectTransform, "Handle", Vector2.zero, new Vector2(18f, 32f));

            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.colors = Colors(TextColor);
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        /// <summary>Colores por estado: el control seleccionado (ratón o mando) se ve en el acento.</summary>
        private static ColorBlock Colors(Color normal)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normal;
            colors.highlightedColor = AccentColor;
            colors.selectedColor = AccentColor;
            colors.pressedColor = AccentColor * 0.8f;
            return colors;
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image NewImage(Transform parent, string name, Vector2 position, Vector2 size)
        {
            // El color final lo pone el ColorBlock del control, así que la imagen queda en blanco.
            return NewRect(name, parent, position, size).gameObject.AddComponent<Image>();
        }

        private TextMeshProUGUI NewText(Transform parent, string text, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var label = NewRect("Text", parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = TextColor;
            label.raycastTarget = false;
            return label;
        }
    }
}
