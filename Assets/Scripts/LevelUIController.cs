using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Controlador completo de la interfaz de usuario (HUD, estado de salud, herramientas,
/// estrellas, viñeta de daño y pantallas de Fin de Juego y Victoria).
/// </summary>
public class LevelUIController : MonoBehaviour
{
    [Header("Referencias del Jugador")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private ObjectVacum vacuum;
    [SerializeField] private GameController gameController;

    [Header("Vidas / Corazones")]
    [SerializeField] private Image[] heartImages;
    [SerializeField] private Sprite heartFullSprite;
    [SerializeField] private Sprite heartEmptySprite;

    [Header("Indicador de Herramientas")]
    [SerializeField] private Image grappleIcon;
    [SerializeField] private Image vacuumIcon;
    [SerializeField] private TextMeshProUGUI grappleLabel;
    [SerializeField] private TextMeshProUGUI vacuumLabel;
    [SerializeField] private TextMeshProUGUI ammoText;

    [Header("Estrellas")]
    [SerializeField] private TextMeshProUGUI starsText;

    [Header("Viñeta de Daño")]
    [SerializeField] private Image damageVignette;

    [Header("Notificaciones")]
    [SerializeField] private TextMeshProUGUI toastText;
    [SerializeField] private CanvasGroup toastGroup;

    [Header("Pantalla de Victoria")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI victoryStarsText;
    [SerializeField] private TextMeshProUGUI victoryTimeText;
    [SerializeField] private Button restartButton;

    [Header("Pantalla de Game Over")]
    [SerializeField] private GameObject gameOverPanel;

    private Coroutine vignetteCoroutine;
    private Coroutine toastCoroutine;
    private float levelStartTime;
    private bool levelFinished = false;

    void Start()
    {
        levelStartTime = Time.time;

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();
        if (vacuum == null)
            vacuum = FindAnyObjectByType<ObjectVacum>();
        if (gameController == null)
            gameController = FindAnyObjectByType<GameController>();

        if (playerController != null)
        {
            playerController.onHealthChanged += UpdateHearts;
            playerController.onPlayerDied += OnPlayerDied;
            UpdateHearts(playerController.health);
        }

        if (gameController != null)
        {
            gameController.onStarCollected += UpdateStars;
            gameController.onPlayerRespawned += OnPlayerRespawned;
            UpdateStars(gameController.StarsCollected);
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartLevel);
        }

        if (damageVignette != null)
        {
            Color c = damageVignette.color;
            c.a = 0f;
            damageVignette.color = c;
        }

        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (toastGroup != null) toastGroup.alpha = 0f;
    }

    void Update()
    {
        UpdateToolDisplay();
    }

    private void UpdateHearts(int health)
    {
        if (heartImages == null) return;
        for (int i = 0; i < heartImages.Length; i++)
        {
            if (heartImages[i] != null)
            {
                bool isAlive = i < health;
                if (heartFullSprite != null && heartEmptySprite != null)
                {
                    heartImages[i].sprite = isAlive ? heartFullSprite : heartEmptySprite;
                    heartImages[i].color = isAlive ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.4f);
                }
                else
                {
                    heartImages[i].color = isAlive ? new Color(1f, 0.2f, 0.3f, 1f) : new Color(0.2f, 0.2f, 0.2f, 0.3f);
                }
            }
        }

        if (health < 3 && health > 0)
        {
            TriggerDamageFlash();
        }
    }

    private void UpdateToolDisplay()
    {
        if (playerController == null) return;

        bool isGrapple = playerController.SelectedTool == PlayerController.Tool.Grapple;

        if (grappleIcon != null)
            grappleIcon.color = isGrapple ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f);
        if (grappleLabel != null)
            grappleLabel.color = isGrapple ? new Color(1f, 0.7f, 0.2f) : new Color(0.7f, 0.7f, 0.7f);

        if (vacuumIcon != null)
            vacuumIcon.color = !isGrapple ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f);
        if (vacuumLabel != null)
            vacuumLabel.color = !isGrapple ? new Color(0.2f, 0.85f, 1f) : new Color(0.7f, 0.7f, 0.7f);

        if (ammoText != null && vacuum != null)
        {
            ammoText.text = "Cargas: " + vacuum.StoredCount + " / " + vacuum.currentCapacity;
        }
    }

    private void UpdateStars(int count)
    {
        if (starsText != null)
        {
            starsText.text = count.ToString() + " / 3";
        }
    }

    public void TriggerDamageFlash()
    {
        if (damageVignette == null) return;
        if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
        vignetteCoroutine = StartCoroutine(DamageFlashRoutine());
    }

    private IEnumerator DamageFlashRoutine()
    {
        float duration = 0.5f;
        float elapsed = 0f;
        Color c = damageVignette.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(0.65f, 0f, elapsed / duration);
            damageVignette.color = c;
            yield return null;
        }

        c.a = 0f;
        damageVignette.color = c;
    }

    public void ShowToast(string message)
    {
        if (toastText == null || toastGroup == null) return;
        toastText.text = message;
        if (toastCoroutine != null) StopCoroutine(toastCoroutine);
        toastCoroutine = StartCoroutine(ToastRoutine());
    }

    private IEnumerator ToastRoutine()
    {
        float fade = 0.3f;
        float elapsed = 0f;
        while (elapsed < fade)
        {
            elapsed += Time.deltaTime;
            toastGroup.alpha = elapsed / fade;
            yield return null;
        }
        toastGroup.alpha = 1f;

        yield return new WaitForSeconds(2.0f);

        elapsed = 0f;
        while (elapsed < fade)
        {
            elapsed += Time.deltaTime;
            toastGroup.alpha = 1f - (elapsed / fade);
            yield return null;
        }
        toastGroup.alpha = 0f;
    }

    private void OnPlayerDied()
    {
        TriggerDamageFlash();
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }

    private void OnPlayerRespawned()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        if (playerController != null)
        {
            UpdateHearts(playerController.health);
        }
    }

    public void ShowVictoryScreen()
    {
        if (levelFinished) return;
        levelFinished = true;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);

            float totalTime = Time.time - levelStartTime;
            int mins = Mathf.FloorToInt(totalTime / 60f);
            int secs = Mathf.FloorToInt(totalTime % 60f);

            if (victoryTimeText != null)
                victoryTimeText.text = string.Format("Tiempo: {0:00}:{1:00}", mins, secs);

            int stars = gameController != null ? gameController.StarsCollected : 0;
            if (victoryStarsText != null)
                victoryStarsText.text = string.Format("Estrellas: {0} / 3", stars);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnDestroy()
    {
        if (playerController != null)
        {
            playerController.onHealthChanged -= UpdateHearts;
            playerController.onPlayerDied -= OnPlayerDied;
        }
        if (gameController != null)
        {
            gameController.onStarCollected -= UpdateStars;
            gameController.onPlayerRespawned -= OnPlayerRespawned;
        }
    }
}
