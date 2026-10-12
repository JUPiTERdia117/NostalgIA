using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Meta final del nivel. Al entrar en el portal o altar sagrado,
/// activa la secuencia de victoria, efectos y pantalla de resultados.
/// </summary>
public class GoalVictory : MonoBehaviour
{
    [Header("UI de Victoria")]
    [SerializeField] private GameObject victoryCanvas;
    [SerializeField] private TextMeshProUGUI victoryTitleText;
    [SerializeField] private TextMeshProUGUI victoryStarsText;
    [SerializeField] private TextMeshProUGUI victoryTimeText;

    [Header("Efectos")]
    [SerializeField] private ParticleSystem victoryParticles;
    [SerializeField] private AudioSource victoryAudioSource;
    [SerializeField] private Light goalLight;

    private bool isCompleted = false;
    private float levelStartTime;

    void Start()
    {
        levelStartTime = Time.time;
        if (victoryCanvas != null)
        {
            victoryCanvas.SetActive(false);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isCompleted) return;

        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
        {
            TriggerVictory();
        }
    }

    public void TriggerVictory()
    {
        if (isCompleted) return;
        isCompleted = true;

        GameController gm = FindAnyObjectByType<GameController>();
        int stars = gm != null ? gm.StarsCollected : 0;
        float totalTime = Time.time - levelStartTime;
        int minutes = Mathf.FloorToInt(totalTime / 60f);
        int seconds = Mathf.FloorToInt(totalTime % 60f);

        if (victoryParticles != null)
        {
            victoryParticles.Play();
        }

        if (victoryAudioSource != null)
        {
            victoryAudioSource.Play();
        }

        if (goalLight != null)
        {
            StartCoroutine(PulseGoalLight());
        }

        if (victoryCanvas != null)
        {
            victoryCanvas.SetActive(true);

            if (victoryTitleText != null)
                victoryTitleText.text = "¡NIVEL 1 COMPLETADO!";

            if (victoryStarsText != null)
                victoryStarsText.text = $"Estrellas Conseguidas: {stars}/3";

            if (victoryTimeText != null)
                victoryTimeText.text = $"Tiempo: {minutes:00}:{seconds:00}";
        }

        // Liberar cursor para permitir interactuar con el menú de victoria
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Desactivar controles del jugador si es necesario
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var fpc = player.GetComponent<EasyPeasyFirstPersonController.FirstPersonController>();
            if (fpc != null) fpc.enabled = false;
        }
    }

    private IEnumerator PulseGoalLight()
    {
        float startIntensity = goalLight.intensity;
        float elapsed = 0f;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            goalLight.intensity = Mathf.Lerp(startIntensity, startIntensity * 4f, Mathf.Sin(elapsed * Mathf.PI / 1.5f));
            yield return null;
        }
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
