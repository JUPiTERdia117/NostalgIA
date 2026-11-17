// -----------------------------------------------------------------------------
// NostalgIA — Menú principal
// Archivo: LevelSelectCard.cs
// Propósito: tarjeta de un nivel en la selección de niveles: bloqueo, carga del
// nivel y récord de estrellas y platino.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Levels;
using SlimUI.ModernMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Se agrega a un botón de la selección de niveles (panel EXTRAS del menú).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Si el nivel requiere otro que no se ha completado (Verano requiere el Tutorial), el botón
/// queda desactivado, la miniatura se oscurece y la etiqueta dice que está bloqueado.</item>
/// <item>Si está disponible, la etiqueta muestra el récord de estrellas y «PLATINO» cuando se
/// consiguieron todas (<see cref="GameProgress"/>).</item>
/// <item>Con <see cref="loadOnClick"/> el botón carga el nivel con la pantalla de carga del menú;
/// el Tutorial ya tenía esa llamada en la escena, así que ahí queda apagado.</item>
/// </list>
/// La etiqueta se crea por código encima de la parte baja de la tarjeta, con la fuente del menú.
/// </remarks>
[DisallowMultipleComponent]
public sealed class LevelSelectCard : MonoBehaviour
{
    [Tooltip("Escena del nivel (por ejemplo, 20_Verano_Frutiger).")]
    [SerializeField] private string sceneName = string.Empty;
    [Tooltip("Carga el nivel al pulsar (desactívalo si el botón ya tiene su propia llamada).")]
    [SerializeField] private bool loadOnClick = true;
    [Tooltip("Gestor del menú que muestra la pantalla de carga.")]
    [SerializeField] private UIMenuManager menu;
    [Tooltip("Fuente de la etiqueta.")]
    [SerializeField] private TMP_FontAsset font;

    private Button button;
    private TextMeshProUGUI status;
    private Image thumbnail;

    private void Awake()
    {
        button = GetComponent<Button>();
        Transform thumb = transform.Find("Thumbnail");
        if (thumb != null) thumbnail = thumb.GetComponent<Image>();
        if (menu == null) menu = FindAnyObjectByType<UIMenuManager>();
        if (loadOnClick && button != null) button.onClick.AddListener(Load);
        status = MakeStatusLabel();
    }

    /// <summary>Se actualiza cada vez que se abre la selección de niveles.</summary>
    private void OnEnable()
    {
        bool unlocked = LevelCatalog.IsUnlocked(sceneName);
        if (button != null) button.interactable = unlocked;
        if (thumbnail != null) thumbnail.color = unlocked ? Color.white : new Color(0.3f, 0.3f, 0.35f, 1f);

        if (!unlocked)
        {
            status.text = "BLOQUEADO\n<size=75%>Completa el Tutorial</size>";
            return;
        }

        int total = GameProgress.TotalStars(sceneName);
        if (!GameProgress.IsCompleted(sceneName)) status.text = "Sin completar";
        // Sin el símbolo ♦: la fuente del menú no lo incluye.
        else if (GameProgress.IsPlatinum(sceneName)) status.text = $"<color=#FFD166>PLATINO · Estrellas {GameProgress.BestStars(sceneName)}/{total}</color>";
        else status.text = total > 0 ? $"Completado · Estrellas {GameProgress.BestStars(sceneName)}/{total}" : "Completado";
    }

    private void Load()
    {
        if (menu != null && LevelCatalog.IsUnlocked(sceneName)) menu.LoadScene(sceneName);
    }

    /// <summary>Franja oscura con texto en la parte baja de la tarjeta.</summary>
    private TextMeshProUGUI MakeStatusLabel()
    {
        var band = new GameObject("Status", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        band.SetParent(transform, false);
        band.anchorMin = new Vector2(0f, 0f);
        band.anchorMax = new Vector2(1f, 0f);
        band.pivot = new Vector2(0.5f, 0f);
        band.sizeDelta = new Vector2(0f, 64f);
        band.anchoredPosition = Vector2.zero;
        Image bg = band.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.65f);
        bg.raycastTarget = false;

        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(band, false);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        if (font != null) text.font = font;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
