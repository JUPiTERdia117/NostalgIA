using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Gestiona la meta de victoria del nivel.
/// Detecta la llegada del jugador, activa la pantalla de victoria, feedback visual/partículas,
/// calcula estadísticas (estrellas y tiempo) y gestiona el reinicio o continuación.
/// </summary>
public class VictoryGoal : MonoBehaviour
{
    [Header("Configuración de Victoria")]
    [Tooltip("Panel UI de victoria (si se asigna manualmente, o se genera por código)")]
    [SerializeField] private GameObject victoryCanvas;
    [Tooltip("Efectos de partículas a activar en la meta")]
    [SerializeField] private ParticleSystem celebrationParticles;
    [Tooltip("Luz o cristal a intensificar en la meta")]
    [SerializeField] private Light goalLight;
    [Tooltip("Color de iluminación de victoria")]
    [SerializeField] private Color victoryLightColor = new Color(0.2f, 1f, 0.4f);

    [Header("Requisitos Opcionales")]
    [Tooltip("¿Requiere todas las estrellas para ganar?")]
    [SerializeField] private bool requireAllStars = false;

    private bool levelFinished = false;
    private float levelStartTime;
    private GameController gameController;

    void Start()
    {
        levelStartTime = Time.time;
        gameController = FindAnyObjectByType<GameController>();

        if (victoryCanvas != null)
        {
            victoryCanvas.SetActive(false);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (levelFinished) return;

        if (other.CompareTag("Player"))
        {
            CompleteLevel(other.gameObject);
        }
    }

    public void CompleteLevel(GameObject player)
    {
        levelFinished = true;
        float totalTime = Time.time - levelStartTime;
        int minutes = Mathf.FloorToInt(totalTime / 60f);
        int seconds = Mathf.FloorToInt(totalTime % 60f);
        string timeFormatted = string.Format("{0:00}:{1:00}", minutes, seconds);

        // Feedback de luz y partículas
        if (celebrationParticles != null)
        {
            celebrationParticles.Play();
        }

        if (goalLight != null)
        {
            goalLight.color = victoryLightColor;
            goalLight.intensity *= 2.5f;
        }

        // Si existe un panel de victoria configurado, activarlo
        if (victoryCanvas != null)
        {
            victoryCanvas.SetActive(true);
        }
        else
        {
            CreateRuntimeVictoryUI(timeFormatted);
        }

        // Desactivar daño al jugador durante la victoria
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.enabled = false;
        }

        // Desbloquear cursor para interactuar con la UI de victoria
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CreateRuntimeVictoryUI(string timeString)
    {
        GameObject canvasObj = new GameObject("VictoryCanvas_Runtime", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Fondo oscuro translúcido
        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.color = new Color(0.05f, 0.08f, 0.15f, 0.88f);

        // Tarjeta central
        GameObject cardObj = new GameObject("VictoryCard", typeof(RectTransform), typeof(Image));
        cardObj.transform.SetParent(bgObj.transform, false);
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(650, 480);
        Image cardImg = cardObj.GetComponent<Image>();
        cardImg.color = new Color(0.12f, 0.18f, 0.28f, 0.95f);

        // Título de Victoria
        CreateText(cardObj.transform, "Title", "¡NIVEL 1 COMPLETADO!", new Vector2(0, 150), 38, Color.yellow, FontStyles.Bold);

        // Subtítulo
        CreateText(cardObj.transform, "Subtitle", "¡Has superado el Santuario Celeste!", new Vector2(0, 90), 22, Color.white, FontStyles.Normal);

        // Tiempo
        CreateText(cardObj.transform, "TimeText", "Tiempo: " + timeString, new Vector2(0, 20), 24, new Color(0.8f, 0.9f, 1f), FontStyles.Normal);

        // Botón Reiniciar
        GameObject restartBtn = CreateButton(cardObj.transform, "RestartButton", "REINICIAR NIVEL", new Vector2(0, -70), new Color(0.18f, 0.6f, 0.9f));
        restartBtn.GetComponent<Button>().onClick.AddListener(() =>
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        });

        // Botón Continuar / Salir
        GameObject quitBtn = CreateButton(cardObj.transform, "QuitButton", "SALIR AL MENÚ", new Vector2(0, -150), new Color(0.3f, 0.35f, 0.45f));
        quitBtn.GetComponent<Button>().onClick.AddListener(() =>
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        });
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 pos, float fontSize, Color color, FontStyles style)
    {
        GameObject textObj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(parent, false);
        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(580, 50);

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private GameObject CreateButton(Transform parent, string name, string label, Vector2 pos, Color btnColor)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(320, 55);

        Image img = btnObj.GetComponent<Image>();
        img.color = btnColor;

        Button btn = btnObj.GetComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = btnColor * 1.25f;
        colors.pressedColor = btnColor * 0.8f;
        btn.colors = colors;

        GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = rect.sizeDelta;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return btnObj;
    }
}
