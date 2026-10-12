using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// Controlador principal de la interfaz de usuario (UI / HUD) del Nivel 1.
/// Gestiona la barra de salud, contador de estrellas, indicador de herramientas, retícula,
/// efecto de flash de daño, menú de pausa y pantalla de victoria.
/// </summary>
public class LevelUIController : MonoBehaviour
{
    public static LevelUIController Instance { get; private set; }

    [Header("Referencias de Jugador")]
    [SerializeField] private PlayerController playerController;

    [Header("HUD - Salud")]
    [SerializeField] private Image[] healthHearts;
    [SerializeField] private Color heartActiveColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color heartEmptyColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);

    [Header("HUD - Estrellas")]
    [SerializeField] private TextMeshProUGUI starCounterText;
    [SerializeField] private Image starIcon;
    private int starsCollected = 0;
    private const int totalStars = 3;

    [Header("HUD - Herramientas")]
    [SerializeField] private TextMeshProUGUI currentToolText;
    [SerializeField] private TextMeshProUGUI toolControlsText;

    [Header("HUD - Daño y Notificaciones")]
    [SerializeField] private CanvasGroup damageFlashGroup;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private CanvasGroup notificationGroup;

    [Header("Pantalla de Victoria")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI victoryStarsText;
    [SerializeField] private TextMeshProUGUI victoryTimeText;
    [SerializeField] private Button victoryRestartButton;

    [Header("Pantalla de Pausa")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseRestartButton;

    private float levelStartTime;
    private bool isPaused = false;
    private bool isVictory = false;
    private int lastHealth = 3;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        levelStartTime = Time.time;
    }

    void Start()
    {
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        // Configuración de botones
        if (victoryRestartButton != null)
            victoryRestartButton.onClick.AddListener(RestartLevel);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeGame);

        if (pauseRestartButton != null)
            pauseRestartButton.onClick.AddListener(RestartLevel);

        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (damageFlashGroup != null) damageFlashGroup.alpha = 0f;
        if (notificationGroup != null) notificationGroup.alpha = 0f;

        UpdateStarHUD();
    }

    void Update()
    {
        // Control de pausa con ESC
        if (Input.GetKeyDown(KeyCode.Escape) && !isVictory)
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }

        if (isVictory) return;

        UpdatePlayerHealthHUD();
        UpdateToolHUD();
    }

    private void UpdatePlayerHealthHUD()
    {
        if (playerController == null) return;

        int currentHp = playerController.health;

        // Si el jugador recibió daño, disparar flash rojo
        if (currentHp < lastHealth)
        {
            TriggerDamageFlash();
        }
        lastHealth = currentHp;

        if (healthHearts != null)
        {
            for (int i = 0; i < healthHearts.Length; i++)
            {
                if (healthHearts[i] != null)
                {
                    healthHearts[i].color = (i < currentHp) ? heartActiveColor : heartEmptyColor;
                }
            }
        }
    }

    private void UpdateToolHUD()
    {
        if (playerController == null) return;

        bool isGrapple = playerController.SelectedTool == PlayerController.Tool.Grapple;

        if (currentToolText != null)
        {
            currentToolText.text = isGrapple ? "HERRAMIENTA: GANCHO" : "HERRAMIENTA: ASPIRADORA";
            currentToolText.color = isGrapple ? new Color(1f, 0.65f, 0.2f) : new Color(0.2f, 0.85f, 1f);
        }

        if (toolControlsText != null)
        {
            toolControlsText.text = isGrapple 
                ? "[Q] Cambiar | Clic: Enganchar" 
                : "[Q] Cambiar | Clic Izq: Aspirar | Clic Der: Disparar";
        }
    }

    public void OnStarCollected()
    {
        starsCollected++;
        UpdateStarHUD();
        ShowNotification("¡ESTRELLA OBTENIDA! (" + starsCollected + "/" + totalStars + ")");

        if (starIcon != null)
        {
            StartCoroutine(PulseIconRoutine(starIcon.transform));
        }
    }

    private void UpdateStarHUD()
    {
        if (starCounterText != null)
        {
            starCounterText.text = "★ " + starsCollected + " / " + totalStars;
        }
    }

    public void TriggerDamageFlash()
    {
        if (damageFlashGroup != null)
        {
            StopCoroutine("DamageFlashRoutine");
            StartCoroutine(DamageFlashRoutine());
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        damageFlashGroup.alpha = 0.65f;
        while (damageFlashGroup.alpha > 0.01f)
        {
            damageFlashGroup.alpha = Mathf.MoveTowards(damageFlashGroup.alpha, 0f, Time.deltaTime * 2.5f);
            yield return null;
        }
        damageFlashGroup.alpha = 0f;
    }

    public void ShowNotification(string message)
    {
        if (notificationText != null && notificationGroup != null)
        {
            notificationText.text = message;
            StopCoroutine("NotificationRoutine");
            StartCoroutine(NotificationRoutine());
        }
    }

    private IEnumerator NotificationRoutine()
    {
        notificationGroup.alpha = 1f;
        yield return new WaitForSeconds(2.0f);
        while (notificationGroup.alpha > 0.01f)
        {
            notificationGroup.alpha = Mathf.MoveTowards(notificationGroup.alpha, 0f, Time.deltaTime * 2.0f);
            yield return null;
        }
        notificationGroup.alpha = 0f;
    }

    private IEnumerator PulseIconRoutine(Transform icon)
    {
        Vector3 origScale = Vector3.one;
        Vector3 maxScale = Vector3.one * 1.35f;
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            icon.localScale = Vector3.Lerp(origScale, maxScale, elapsed / 0.2f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < 0.2f)
        {
            icon.localScale = Vector3.Lerp(maxScale, origScale, elapsed / 0.2f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        icon.localScale = origScale;
    }

    public void ShowVictoryScreen()
    {
        isVictory = true;
        Time.timeScale = 0.2f; // Cámara lenta estilizada de victoria

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        if (victoryStarsText != null)
        {
            victoryStarsText.text = "Estrellas Recolectadas: " + starsCollected + " / " + totalStars;
        }

        if (victoryTimeText != null)
        {
            float totalTime = Time.time - levelStartTime;
            int mins = (int)(totalTime / 60);
            int secs = (int)(totalTime % 60);
            victoryTimeText.text = string.Format("Tiempo de Completado: {0:00}:{1:00}", mins, secs);
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
