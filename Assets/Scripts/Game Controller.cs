using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controlador principal del juego y del tutorial.
/// Gestiona la recolección de estrellas, puntos de control persistentes, reaparición limpia,
/// efectos de daño y muerte, interfaz de objetivos y condición de victoria.
/// </summary>
public class GameController : MonoBehaviour
{
    // Estado estático persistente entre recargas de escena tras morir
    public static Vector3? LastCheckpointPosition = null;
    public static int SavedScore = 0;
    public static HashSet<string> SavedCollectedStars = new HashSet<string>();

    [Header("Stars & Score")]
    [SerializeField] private int maxStars = 3;
    private int starsCollected = 0;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Spawn & Fallback")]
    [SerializeField] private Vector3 spawnPoint = new Vector3(-11.7f, 1.0f, 21.0f);
    [SerializeField] private float fallThreshold = -15f;

    [Header("HUD Feedback")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI toolText;
    [SerializeField] private TextMeshProUGUI checkpointNotificationText;
    [SerializeField] private Image hurtOverlay;
    [SerializeField] private GameObject deathOverlay;

    [Header("Dynamic Tutorial Guide")]
    [SerializeField] private GameObject guidePanel;
    [SerializeField] private TextMeshProUGUI phaseBadgeText;
    [SerializeField] private TextMeshProUGUI zoneTitleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI controlsText;
    [SerializeField] private TextMeshProUGUI taskText;

    [Header("Victory Screen")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private TextMeshProUGUI winSummaryText;

    public int StarsCollected => starsCollected;
    public bool IsLevelWon { get; private set; }

    private PlayerController playerController;
    private Coroutine checkpointCoroutine;
    private Coroutine hurtCoroutine;
    private Coroutine deathCoroutine;
    private Coroutine guidePulseCoroutine;

    private void Awake()
    {
        FindPlayer();
    }

    private void Start()
    {
        // 1. Restaurar punto de control si se viene de una reaparición por muerte
        if (LastCheckpointPosition.HasValue)
        {
            spawnPoint = LastCheckpointPosition.Value;
            starsCollected = SavedScore;

            // Eliminar estrellas previamente recolectadas antes de morir
            foreach (var starName in SavedCollectedStars)
            {
                GameObject s = GameObject.Find(starName);
                if (s != null) Destroy(s);
            }

            // Ubicar al jugador en el checkpoint
            if (playerController != null)
            {
                CharacterController cc = playerController.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerController.transform.position = spawnPoint;
                if (cc != null) cc.enabled = true;
            }

            // Activar visualmente los checkpoints alcanzados
            foreach (var cp in FindObjectsByType<RespawnController>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(cp.transform.position, spawnPoint) <= 14f)
                {
                    cp.ActivateVisuals();
                }
            }

            Debug.Log($"[GameController] Reaparición de nivel limpia en Checkpoint: {spawnPoint}");
        }

        UpdateScoreUI();
        UpdateHealthUI();
        UpdateToolUI();

        if (winPanel != null) winPanel.SetActive(false);
        if (deathOverlay != null) deathOverlay.SetActive(false);
        if (checkpointNotificationText != null) checkpointNotificationText.gameObject.SetActive(false);
        if (hurtOverlay != null) hurtOverlay.color = new Color(1f, 0f, 0f, 0f);

        // Inicializar guía
        if (!LastCheckpointPosition.HasValue)
        {
            SetTutorialGuide(
                "FASE 1 / 4 · FUNDAMENTOS",
                "1. EXPLORACIÓN Y MOVIMIENTO",
                "Avanza por el pasillo inicial, aprende a moverte con agilidad y recoge tu primera estrella dorada.",
                "<color=#FFE600><b>[ W A S D ]</b></color> Moverse   <color=#FFE600><b>[ ESPACIO ]</b></color> Saltar   <color=#FFE600><b>[ SHIFT ]</b></color> Correr",
                ">> Encuentra la 1ra Estrella dorada."
            );
        }
    }

    private void Update()
    {
        if (playerController == null)
        {
            FindPlayer();
        }

        if (playerController != null && !playerController.dead)
        {
            UpdateHealthUI();
            UpdateToolUI();

            Vector3 pPos = playerController.transform.position;

            // Detección de caída al abismo (altura menor a -15m)
            if (pPos.y < fallThreshold)
            {
                Debug.Log("[GameController] El jugador cayó al abismo. Activando secuencia de muerte...");
                playerController.TakeDamage(99);
            }
        }

        // Tecla R para reiniciar en cualquier momento o tras ganar
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
        {
            RestartLevel();
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    public void CollectStar(string starName = "")
    {
        starsCollected++;
        if (!string.IsNullOrEmpty(starName))
        {
            SavedCollectedStars.Add(starName);
        }

        UpdateScoreUI();
        NotifyCheckpoint($"¡Estrella dorada obtenida! ({starsCollected}/{maxStars})");

        if (taskText != null)
        {
            taskText.text = $">> Estrellas obtenidas: {starsCollected}/{maxStars}";
        }
    }

    public void CollectStar()
    {
        CollectStar("");
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"ESTRELLAS: <color=#FFE600>{starsCollected} / {maxStars}</color>";
        }
    }

    public void UpdateHealthUI()
    {
        if (healthText != null && playerController != null)
        {
            int hp = Mathf.Clamp(playerController.health, 0, 3);
            healthText.text = $"SALUD: <color=#FF3344>{hp} / 3 HP</color>";
        }
    }

    public void UpdateToolUI()
    {
        if (toolText != null && playerController != null)
        {
            bool isGrapple = playerController.SelectedTool == PlayerController.Tool.Grapple;
            string toolName;
            if (isGrapple)
            {
                if (playerController.GrapleController != null && playerController.GrapleController.IsOnCooldown)
                {
                    float cd = playerController.GrapleController.CooldownRemaining;
                    toolName = $"<color=#FFB300>GANCHO [Recarga: {cd:F1}s]</color>";
                }
                else
                {
                    toolName = "<color=#00E5FF>GANCHO [X / Clic]</color>";
                }
            }
            else
            {
                toolName = "<color=#00E5FF>ASPIRADORA [Clics]</color>";
            }
            toolText.text = $"HERRAMIENTA: {toolName}  |  [Q] Cambiar";
        }
    }

    public void UpdateSpawnPoint(Vector3 newSpawnPoint)
    {
        spawnPoint = newSpawnPoint + Vector3.up * 0.25f;
        Debug.Log($"[GameController] Nuevo punto de control establecido en: {spawnPoint}");
    }

    public void NotifyCheckpoint(string message)
    {
        if (checkpointNotificationText == null) return;
        if (checkpointCoroutine != null) StopCoroutine(checkpointCoroutine);
        checkpointCoroutine = StartCoroutine(ShowCheckpointToast(message));
    }

    private IEnumerator ShowCheckpointToast(string message)
    {
        checkpointNotificationText.text = message;
        checkpointNotificationText.gameObject.SetActive(true);
        yield return new WaitForSeconds(3.5f);
        checkpointNotificationText.gameObject.SetActive(false);
    }

    public void SetTutorialGuide(string phase, string title, string description, string controls, string task)
    {
        if (phaseBadgeText != null) phaseBadgeText.text = phase;
        if (zoneTitleText != null) zoneTitleText.text = title;
        if (descriptionText != null) descriptionText.text = description;
        if (controlsText != null) controlsText.text = controls;
        if (taskText != null) taskText.text = task;

        if (guidePanel != null)
        {
            guidePanel.SetActive(true);
            if (guidePulseCoroutine != null) StopCoroutine(guidePulseCoroutine);
            guidePulseCoroutine = StartCoroutine(PulseGuidePanel());
        }
    }

    public void SetTutorialPrompt(string text, float duration = 5f)
    {
        if (taskText != null) taskText.text = text;
        NotifyCheckpoint(text);
    }

    private IEnumerator PulseGuidePanel()
    {
        if (guidePanel == null) yield break;
        Transform t = guidePanel.transform;
        Vector3 baseScale = Vector3.one;

        float elapsed = 0f;
        float duration = 0.22f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = elapsed / duration;
            float s = 1f + Mathf.Sin(progress * Mathf.PI) * 0.05f;
            t.localScale = baseScale * s;
            yield return null;
        }
        t.localScale = baseScale;
    }

    public void OnPlayerTookDamage()
    {
        UpdateHealthUI();
        if (hurtOverlay != null)
        {
            if (hurtCoroutine != null) StopCoroutine(hurtCoroutine);
            hurtCoroutine = StartCoroutine(FlashHurtOverlay());
        }
    }

    private IEnumerator FlashHurtOverlay()
    {
        if (hurtOverlay == null) yield break;
        hurtOverlay.color = new Color(1f, 0.08f, 0.08f, 0.45f);
        float elapsed = 0f;
        float dur = 0.35f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0.45f, 0f, elapsed / dur);
            hurtOverlay.color = new Color(1f, 0.08f, 0.08f, alpha);
            yield return null;
        }
        hurtOverlay.color = new Color(1f, 0.08f, 0.08f, 0f);
    }

    public void OnPlayerDied()
    {
        if (deathCoroutine != null) StopCoroutine(deathCoroutine);
        deathCoroutine = StartCoroutine(DeathAndRespawnRoutine());
    }

    [ContextMenu("Respawn Player")]
    public void RespawnPlayer()
    {
        OnPlayerDied();
    }

    private IEnumerator DeathAndRespawnRoutine()
    {
        UpdateHealthUI();

        // 1. Mostrar pantalla de muerte con retroalimentación clara
        if (deathOverlay != null)
        {
            deathOverlay.SetActive(true);
        }

        if (hurtOverlay != null)
        {
            hurtOverlay.color = new Color(0.85f, 0f, 0f, 0.65f);
        }

        // Breve pausa para percibir el estado de muerte
        yield return new WaitForSeconds(0.85f);

        // 2. Guardar estado del último checkpoint
        LastCheckpointPosition = spawnPoint;
        SavedScore = starsCollected;

        // 3. Reiniciar la escena de forma limpia (restaura enemigos, objetos arrojables, proyectiles)
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void WinLevel()
    {
        if (IsLevelWon) return;
        IsLevelWon = true;

        Debug.Log("[GameController] ¡Nivel de tutorial completado!");

        if (winPanel != null)
        {
            winPanel.SetActive(true);
            if (winSummaryText != null)
            {
                string starEvaluation = starsCollected == maxStars
                    ? "¡EXCELENTE! Has recolectado todas las estrellas secretas."
                    : $"Recolectaste {starsCollected}/{maxStars} estrellas. ¡Vuelve a intentarlo para conseguir todas!";

                winSummaryText.text = @"¡TUTORIAL COMPLETADO!

" + starEvaluation + @"

¡Has dominado el movimiento ágil, la aspiradora de combate, el esquive de peligros y el gancho!

Presiona [R] para reiniciar el tutorial.";
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RestartLevel()
    {
        // Reiniciar desde el principio del tutorial
        LastCheckpointPosition = null;
        SavedScore = 0;
        SavedCollectedStars.Clear();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
