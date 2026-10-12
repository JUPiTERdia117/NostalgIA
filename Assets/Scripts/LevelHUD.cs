using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Muestra información vital del jugador en pantalla: Puntos de salud (3 Corazones),
/// notificaciones de checkpoint y estado general del nivel.
/// </summary>
public class LevelHUD : MonoBehaviour
{
    private PlayerController player;
    private TextMeshProUGUI healthText;
    private TextMeshProUGUI checkpointBannerText;
    private float checkpointBannerTimer = 0f;
    private int lastHealth = -1;
    private System.Collections.Generic.List<GameObject> activeCheckpoints = new System.Collections.Generic.List<GameObject>();

    void Start()
    {
        CreateHUDCanvas();
        FindPlayer();
        InitializeCheckpoints();
    }

    private void InitializeCheckpoints()
    {
        RespawnController[] rcs = FindObjectsByType<RespawnController>(FindObjectsSortMode.None);
        foreach (var rc in rcs)
        {
            if (rc != null) activeCheckpoints.Add(rc.gameObject);
        }
    }

    void Update()
    {
        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        if (player.health != lastHealth)
        {
            lastHealth = player.health;
            UpdateHealthDisplay();
        }

        for (int i = activeCheckpoints.Count - 1; i >= 0; i--)
        {
            if (activeCheckpoints[i] == null)
            {
                activeCheckpoints.RemoveAt(i);
                ShowCheckpointNotification();
            }
        }

        if (checkpointBannerTimer > 0f)
        {
            checkpointBannerTimer -= Time.deltaTime;
            if (checkpointBannerText != null)
            {
                Color c = checkpointBannerText.color;
                c.a = Mathf.Clamp01(checkpointBannerTimer / 0.5f);
                checkpointBannerText.color = c;
            }
        }
    }

    private void FindPlayer()
    {
        GameObject pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null)
        {
            player = pObj.GetComponent<PlayerController>();
        }
    }

    private void UpdateHealthDisplay()
    {
        if (healthText == null || player == null) return;

        string pips = "";
        for (int i = 0; i < 3; i++)
        {
            pips += (i < player.health) ? "<color=#FF3344>[+]</color> " : "<color=#555555>[ ]</color> ";
        }

        healthText.text = $"VIDA: {player.health}/3  {pips}";
    }

    public void ShowCheckpointNotification()
    {
        if (checkpointBannerText != null)
        {
            checkpointBannerText.text = "¡PUNTO DE CONTROL GUARDADO!";
            checkpointBannerText.color = new Color(0.2f, 1f, 0.4f, 1f);
            checkpointBannerTimer = 2.5f;
        }
    }

    private void CreateHUDCanvas()
    {
        GameObject canvasObj = new GameObject("HUDCanvas_Runtime", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Indicador de Salud
        GameObject healthObj = new GameObject("HealthText", typeof(RectTransform), typeof(TextMeshProUGUI));
        healthObj.transform.SetParent(canvasObj.transform, false);
        RectTransform hRect = healthObj.GetComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0f, 1f);
        hRect.anchorMax = new Vector2(0f, 1f);
        hRect.pivot = new Vector2(0f, 1f);
        hRect.anchoredPosition = new Vector2(35f, -30f);
        hRect.sizeDelta = new Vector2(350f, 50f);

        healthText = healthObj.GetComponent<TextMeshProUGUI>();
        healthText.fontSize = 28;
        healthText.fontStyle = FontStyles.Bold;
        healthText.alignment = TextAlignmentOptions.Left;

        // Banner de Checkpoint (Centro arriba)
        GameObject cpObj = new GameObject("CheckpointBanner", typeof(RectTransform), typeof(TextMeshProUGUI));
        cpObj.transform.SetParent(canvasObj.transform, false);
        RectTransform cpRect = cpObj.GetComponent<RectTransform>();
        cpRect.anchorMin = new Vector2(0.5f, 1f);
        cpRect.anchorMax = new Vector2(0.5f, 1f);
        cpRect.pivot = new Vector2(0.5f, 1f);
        cpRect.anchoredPosition = new Vector2(0f, -80f);
        cpRect.sizeDelta = new Vector2(600f, 50f);

        checkpointBannerText = cpObj.GetComponent<TextMeshProUGUI>();
        checkpointBannerText.fontSize = 24;
        checkpointBannerText.fontStyle = FontStyles.Bold;
        checkpointBannerText.alignment = TextAlignmentOptions.Center;
        checkpointBannerText.color = new Color(0.2f, 1f, 0.4f, 0f);
    }
}
