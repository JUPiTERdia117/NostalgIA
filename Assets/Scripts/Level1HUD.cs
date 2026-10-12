using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Interfaz diegética limpia para el progreso, la vitalidad y la victoria.</summary>
public sealed class Level1HUD : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.035f, 0.07f, 0.105f, 0.94f);
    private static readonly Color Pale = new Color(0.88f, 0.95f, 0.94f, 1f);
    private static readonly Color Muted = new Color(0.55f, 0.70f, 0.73f, 1f);
    private static readonly Color Gold = new Color(1f, 0.72f, 0.36f, 1f);
    private static readonly Color Aqua = new Color(0.35f, 0.88f, 0.91f, 1f);

    private Level1WorldFlow flow;
    private PlayerController player;
    private Image[] healthPips;
    private Image[] corePips;
    private TMP_Text coreCountText;
    private Image hurtOverlay;
    private GameObject victoryOverlay;
    private TMP_Text toastText;
    private float toastEnd;
    private float hurtEnd;
    private Sprite roundedSprite;
    private Sprite squareSprite;

    private void Start()
    {
        flow = GetComponent<Level1WorldFlow>();
        player = flow != null ? flow.Player : null;
        BuildCanvas();
    }

    private void Update()
    {
        if (flow == null) flow = GetComponent<Level1WorldFlow>();
        if (player == null && flow != null) player = flow.Player;

        if (healthPips != null && player != null)
        {
            for (int i = 0; i < healthPips.Length; i++)
            {
                bool alive = i < player.health;
                healthPips[i].color = alive ? Gold : new Color(0.30f, 0.39f, 0.43f, 0.72f);
            }
        }

        if (corePips != null && flow != null)
        {
            if (coreCountText != null) coreCountText.text = flow.CoresCollected + "  /  " + flow.RequiredCores;
            for (int i = 0; i < corePips.Length; i++)
                corePips[i].color = i < flow.CoresCollected ? Aqua : new Color(0.18f, 0.34f, 0.39f, 0.86f);
        }

        if (toastText != null)
        {
            float remaining = toastEnd - Time.unscaledTime;
            Color color = toastText.color;
            color.a = Mathf.Clamp01(remaining * 2.2f);
            toastText.color = color;
        }

        if (hurtOverlay != null)
        {
            float remaining = hurtEnd - Time.unscaledTime;
            Color color = hurtOverlay.color;
            color.a = Mathf.Clamp01(remaining * 0.8f) * 0.32f;
            hurtOverlay.color = color;
        }
    }

    public void ShowMessage(string message, Color color)
    {
        if (toastText == null) return;
        toastText.text = message;
        toastText.color = new Color(color.r, color.g, color.b, 1f);
        toastEnd = Time.unscaledTime + 2.7f;
    }

    public void ShowDamage()
    {
        hurtEnd = Time.unscaledTime + 0.65f;
        ShowMessage("¡Impacto! Busca distancia del centinela", new Color(1f, 0.57f, 0.45f, 1f));
    }

    public void ShowVictory(int collected, int total)
    {
        if (victoryOverlay == null) return;
        victoryOverlay.SetActive(true);
        Transform title = victoryOverlay.transform.Find("Panel/Título");
        Transform detail = victoryOverlay.transform.Find("Panel/Detalle");
        if (title != null) title.GetComponent<TMP_Text>().text = "BÓVEDA RESTAURADA";
        if (detail != null)
        {
            string epilogue = collected >= total
                ? "Los tres núcleos cantan de nuevo. La aurora vuelve a las islas."
                : "Has alcanzado el portal. Aún quedan núcleos ocultos en la ruta.";
            detail.GetComponent<TMP_Text>().text = epilogue + "\n\nNÚCLEOS RECUPERADOS     " + collected + " / " + total;
        }
    }

    private void BuildCanvas()
    {
        roundedSprite = CreateRoundedSprite();
        squareSprite = CreateSquareSprite();
        squareSpriteForAccent = squareSprite;

        GameObject canvasObject = new GameObject("Interfaz · Templo de la Bóveda", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        BuildMissionCard(root);
        BuildCoreCard(root);
        BuildControlsHint(root);
        BuildMessage(root);
        BuildHurtOverlay(root);
        BuildVictoryOverlay(root);
    }

    private void BuildMissionCard(RectTransform root)
    {
        RectTransform card = Panel(root, "Misión", new Vector2(0f, 1f), new Vector2(52f, -42f), new Vector2(460f, 142f), Ink, roundedSprite);
        Accent(card, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(4f, 84f), Gold);
        Text(card, "Etiqueta", "EXPEDICIÓN   /   SECTOR 01", new Vector2(1f, 1f), new Vector2(26f, -19f), new Vector2(400f, 24f), 14f, Gold, FontStyles.Bold, TextAlignmentOptions.Left);
        Text(card, "Nombre", "Templo de la Bóveda", new Vector2(1f, 1f), new Vector2(26f, -48f), new Vector2(412f, 40f), 27f, Pale, FontStyles.Bold, TextAlignmentOptions.Left);
        Text(card, "Objetivo", "Activa los tres núcleos y alcanza el portal.", new Vector2(1f, 1f), new Vector2(26f, -78f), new Vector2(412f, 27f), 16f, Muted, FontStyles.Normal, TextAlignmentOptions.Left);

        Text(card, "Vitalidad", "VITALIDAD", new Vector2(0f, 1f), new Vector2(26f, -112f), new Vector2(102f, 20f), 12f, Muted, FontStyles.Bold, TextAlignmentOptions.Left);
        healthPips = new Image[3];
        for (int i = 0; i < healthPips.Length; i++)
        {
            Image pip = ImageRect(card, "Carga de vitalidad " + (i + 1), squareSprite, Gold, new Vector2(0f, 1f), new Vector2(134f + i * 28f, -115f), new Vector2(14f, 14f));
            pip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            healthPips[i] = pip;
        }
    }

    private void BuildCoreCard(RectTransform root)
    {
        RectTransform card = Panel(root, "Núcleos", new Vector2(1f, 1f), new Vector2(-52f, -42f), new Vector2(304f, 112f), Ink, roundedSprite);
        Text(card, "Etiqueta", "FRAGMENTOS DE AURORA", new Vector2(1f, 1f), new Vector2(24f, -17f), new Vector2(260f, 25f), 13f, Muted, FontStyles.Bold, TextAlignmentOptions.Left);
        coreCountText = Text(card, "Contador", "0  /  3", new Vector2(1f, 1f), new Vector2(24f, -44f), new Vector2(260f, 36f), 26f, Pale, FontStyles.Bold, TextAlignmentOptions.Left);
        corePips = new Image[3];
        for (int i = 0; i < corePips.Length; i++)
            corePips[i] = ImageRect(card, "Núcleo " + (i + 1), roundedSprite, new Color(0.18f, 0.34f, 0.39f, 0.86f), new Vector2(0f, 1f), new Vector2(24f + i * 76f, -83f), new Vector2(62f, 5f));
    }

    private void BuildControlsHint(RectTransform root)
    {
        RectTransform hint = Panel(root, "Controles", new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(880f, 43f), new Color(0.035f, 0.07f, 0.105f, 0.72f), roundedSprite);
        Text(hint, "Acciones", "WASD MOVER · MOUSE MIRAR · SPACE SALTAR · SHIFT CORRER · Q HERRAMIENTA · X GANCHO · LMB ASPIRAR · RMB LANZAR", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(850f, 29f), 10.5f, Pale, FontStyles.Bold, TextAlignmentOptions.Center);
    }

    private void BuildMessage(RectTransform root)
    {
        toastText = Text(root, "Aviso de expedición", string.Empty, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(720f, 42f), 17f, Aqua, FontStyles.Bold, TextAlignmentOptions.Center);
        toastText.color = new Color(Aqua.r, Aqua.g, Aqua.b, 0f);
    }

    private void BuildHurtOverlay(RectTransform root)
    {
        hurtOverlay = ImageRect(root, "Destello de daño", squareSprite, new Color(0.85f, 0.12f, 0.1f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        hurtOverlay.rectTransform.anchorMin = Vector2.zero;
        hurtOverlay.rectTransform.anchorMax = Vector2.one;
        hurtOverlay.rectTransform.offsetMin = Vector2.zero;
        hurtOverlay.rectTransform.offsetMax = Vector2.zero;
        hurtOverlay.raycastTarget = false;
    }

    private void BuildVictoryOverlay(RectTransform root)
    {
        victoryOverlay = new GameObject("Victoria", typeof(RectTransform), typeof(Image));
        RectTransform overlay = victoryOverlay.GetComponent<RectTransform>();
        overlay.SetParent(root, false);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;
        Image dim = victoryOverlay.GetComponent<Image>();
        dim.color = new Color(0.015f, 0.035f, 0.055f, 0.82f);
        dim.raycastTarget = true;

        RectTransform card = Panel(overlay, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 390f), new Color(0.045f, 0.09f, 0.125f, 0.98f), roundedSprite);
        Accent(card, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(470f, 4f), Gold);
        Text(card, "Capítulo", "FIN DEL CAPÍTULO 01", new Vector2(0.5f, 1f), new Vector2(0f, -61f), new Vector2(540f, 28f), 15f, Gold, FontStyles.Bold, TextAlignmentOptions.Center);
        Text(card, "Título", "BÓVEDA RESTAURADA", new Vector2(0.5f, 1f), new Vector2(0f, -111f), new Vector2(580f, 56f), 34f, Pale, FontStyles.Bold, TextAlignmentOptions.Center);
        Text(card, "Detalle", "La aurora vuelve a las islas.", new Vector2(0.5f, 0.5f), new Vector2(0f, 19f), new Vector2(550f, 105f), 18f, Muted, FontStyles.Normal, TextAlignmentOptions.Center);

        Image buttonImage = ImageRect(card, "Volver a jugar", roundedSprite, new Color(0.64f, 0.40f, 0.18f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(280f, 56f));
        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        buttonImage.raycastTarget = true;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.82f, 0.58f, 0.30f, 1f);
        colors.pressedColor = new Color(0.50f, 0.30f, 0.14f, 1f);
        button.colors = colors;
        button.onClick.AddListener(RestartLevel);
        Text(buttonImage.rectTransform, "Texto", "REPETIR NIVEL", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 36f), 15f, Pale, FontStyles.Bold, TextAlignmentOptions.Center);

        victoryOverlay.SetActive(false);
    }

    private void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color, Sprite sprite)
    {
        Image image = ImageRect(parent, name, sprite, color, anchor, position, size);
        image.type = Image.Type.Sliced;
        return image.rectTransform;
    }

    private static Image ImageRect(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(Transform parent, string name, string text, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, Color color, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    private static void Accent(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        ImageRect(parent, "Filete de latón", squareSpriteForAccent, color, anchor, position, size);
    }

    private static Sprite squareSpriteForAccent;

    private Sprite CreateSquareSprite()
    {
        return Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    private static Sprite CreateRoundedSprite()
    {
        const int dimension = 64;
        const float radius = 13f;
        Texture2D texture = new Texture2D(dimension, dimension, TextureFormat.RGBA32, false, true);
        texture.name = "Level1_UI_Rounded_SDF";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[dimension * dimension];
        for (int y = 0; y < dimension; y++)
        {
            for (int x = 0; x < dimension; x++)
            {
                float px = Mathf.Min(x + 0.5f, dimension - x - 0.5f);
                float py = Mathf.Min(y + 0.5f, dimension - y - 0.5f);
                float dx = radius - px;
                float dy = radius - py;
                float distance = Mathf.Sqrt(Mathf.Max(0f, dx) * Mathf.Max(0f, dx) + Mathf.Max(0f, dy) * Mathf.Max(0f, dy));
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius - distance + 0.5f) * 255f);
                pixels[y * dimension + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, dimension, dimension), new Vector2(0.5f, 0.5f), dimension, 0, SpriteMeshType.FullRect, new Vector4(20f, 20f, 20f, 20f));
    }

    private void OnDestroy()
    {
        if (roundedSprite != null)
        {
            Texture2D texture = roundedSprite.texture;
            Destroy(roundedSprite);
            if (texture != null && texture != Texture2D.whiteTexture) Destroy(texture);
        }
        if (squareSprite != null && squareSprite.texture != Texture2D.whiteTexture) Destroy(squareSprite.texture);
    }
}
