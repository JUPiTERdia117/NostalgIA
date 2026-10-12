using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Muestra en pantalla la vida del jugador, la herramienta seleccionada y consejos de control.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameController gameController;

    [Header("Elementos de Vida")]
    [SerializeField] private GameObject[] healthHearts;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Herramienta Actual")]
    [SerializeField] private TextMeshProUGUI toolNameText;
    [SerializeField] private Image toolIconImage;
    [SerializeField] private Color grappleColor = new Color(1f, 0.55f, 0.15f);
    [SerializeField] private Color vacuumColor = new Color(0.15f, 0.85f, 1f);

    [Header("Estrellas")]
    [SerializeField] private TextMeshProUGUI starsText;

    private int lastHealth = -1;
    private PlayerController.Tool lastTool = (PlayerController.Tool)(-1);
    private int lastStars = -1;

    void Start()
    {
        if (playerController == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerController = player.GetComponent<PlayerController>();
        }

        if (gameController == null)
        {
            gameController = FindAnyObjectByType<GameController>();
        }
    }

    void Update()
    {
        if (playerController == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerController = player.GetComponent<PlayerController>();
            return;
        }

        // Actualizar corazones de vida
        if (playerController.health != lastHealth)
        {
            lastHealth = playerController.health;
            UpdateHealthUI(lastHealth);
        }

        // Actualizar herramienta
        if (playerController.SelectedTool != lastTool)
        {
            lastTool = playerController.SelectedTool;
            UpdateToolUI(lastTool);
        }

        // Actualizar contador de estrellas
        if (gameController != null && gameController.StarsCollected != lastStars)
        {
            lastStars = gameController.StarsCollected;
            if (starsText != null)
            {
                starsText.text = $"★ {lastStars} / 3";
            }
        }
    }

    private void UpdateHealthUI(int health)
    {
        if (healthHearts != null && healthHearts.Length > 0)
        {
            for (int i = 0; i < healthHearts.Length; i++)
            {
                if (healthHearts[i] != null)
                {
                    healthHearts[i].SetActive(i < health);
                }
            }
        }

        if (healthText != null)
        {
            healthText.text = $"HP: {Mathf.Max(0, health)} / 3";
        }
    }

    private void UpdateToolUI(PlayerController.Tool tool)
    {
        bool isGrapple = tool == PlayerController.Tool.Grapple;
        if (toolNameText != null)
        {
            toolNameText.text = isGrapple ? "HERRAMIENTA: GANCHO (X / Q para cambiar)" : "HERRAMIENTA: ASPIRADORA (Clic Izq: Absorber | Clic Der: Lanzar)";
            toolNameText.color = isGrapple ? grappleColor : vacuumColor;
        }

        if (toolIconImage != null)
        {
            toolIconImage.color = isGrapple ? grappleColor : vacuumColor;
        }
    }
}
