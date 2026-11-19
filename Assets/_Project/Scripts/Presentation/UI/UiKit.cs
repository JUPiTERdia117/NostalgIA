// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: UiKit.cs
// Propósito: piezas de interfaz creadas por código (rectángulos, textos, botones
// y lienzos) con el estilo de la pausa.
// -----------------------------------------------------------------------------

using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NostalgIA.Presentation.UI
{
    /// <summary>
    /// Funciones para armar interfaz por código con los colores del menú de pausa. Las usan los
    /// ajustes de la pausa y las pantallas de resultados y de muerte, para no repetir el mismo
    /// código de construcción en cada una.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color TextColor = new Color(0.91f, 0.957f, 1f, 1f);
        public static readonly Color AccentColor = new Color(0.227f, 0.42f, 1f, 1f);
        public static readonly Color ButtonColor = new Color(0.11f, 0.36f, 0.68f, 0.96f);
        public static readonly Color TrackColor = new Color(0.02f, 0.06f, 0.17f, 0.9f);
        public static readonly Color WindowColor = new Color(0.03f, 0.08f, 0.24f, 0.96f);
        public static readonly Color GoldColor = new Color(1f, 0.82f, 0.4f, 1f);

        /// <summary>
        /// Lienzo de pantalla completa con la misma escala que la pausa (1920×1080, mezcla 0,5).
        /// </summary>
        public static Canvas NewCanvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>Rectángulo anclado al centro de su padre.</summary>
        public static RectTransform NewRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Rectángulo que cubre todo su padre.</summary>
        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Imagen sólida (sin sprite).</summary>
        public static Image NewImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            Image image = NewRect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>Texto TextMeshPro sin recibir clics.</summary>
        public static TextMeshProUGUI NewText(Transform parent, TMP_FontAsset font, string text, Vector2 position,
                                              Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var label = NewRect("Text", parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            // Si el texto no cabe en su caja se reduce (hasta el 55 %) en vez de salirse o encimarse.
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = Mathf.Max(10f, Mathf.Round(fontSize * 0.55f));
            label.alignment = alignment;
            label.color = TextColor;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>Botón con texto centrado. Devuelve el botón; su texto es el primer hijo.</summary>
        public static Button NewButton(Transform parent, TMP_FontAsset font, string text, Vector2 position,
                                       Vector2 size, float fontSize, UnityAction onClick)
        {
            Image image = NewImage(parent, "Button", position, size, Color.white);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = Colors(ButtonColor);
            if (onClick != null) button.onClick.AddListener(onClick);
            NewText(image.rectTransform, font, text, Vector2.zero, size - new Vector2(10f, 0f), fontSize, TextAlignmentOptions.Center);
            return button;
        }

        /// <summary>Colores por estado: el control seleccionado (ratón o mando) se ve en el acento.</summary>
        public static ColorBlock Colors(Color normal)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normal;
            colors.highlightedColor = AccentColor;
            colors.selectedColor = AccentColor;
            colors.pressedColor = AccentColor * 0.8f;
            return colors;
        }

        /// <summary>Etiqueta «encendido»/«apagado» de los ajustes.</summary>
        public static string OnOff(bool on) => on ? "encendido" : "apagado";
    }
}
