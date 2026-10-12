using UnityEngine;

/// <summary>Estado de partida, checkpoint y progreso del primer nivel.</summary>
public sealed class Level1WorldFlow : MonoBehaviour
{
    public static Level1WorldFlow Instance { get; private set; }

    [SerializeField, Min(1)] private int requiredCores = 3;
    [SerializeField] private Level1HUD hud;

    private PlayerController player;
    private GameController gameController;
    private Vector3 checkpoint;
    private float nextDamageTime;
    private int coresCollected;
    private bool complete;

    public PlayerController Player => player;
    public int CoresCollected => coresCollected;
    public int RequiredCores => requiredCores;
    public bool IsComplete => complete;

    private void Awake()
    {
        Instance = this;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.GetComponent<PlayerController>() : null;
        gameController = FindAnyObjectByType<GameController>();
        if (playerObject != null)
        {
            checkpoint = playerObject.transform.position;
            if (gameController != null) gameController.UpdateSpawnPoint(checkpoint);
        }

        if (hud == null) hud = GetComponent<Level1HUD>();
    }

    private void Update()
    {
        if (!complete && player != null && player.transform.position.y < -12f)
            Respawn(true);
    }

    public void SetCheckpoint(Vector3 position)
    {
        if (complete) return;
        checkpoint = position;
        if (gameController != null) gameController.UpdateSpawnPoint(checkpoint);
        hud?.ShowMessage("Punto de retorno activado", new Color(0.43f, 0.93f, 0.83f, 1f));
    }

    public void CollectCore()
    {
        if (complete) return;
        coresCollected = Mathf.Min(coresCollected + 1, requiredCores);
        if (gameController != null) gameController.CollectStar();
        hud?.ShowMessage("Núcleo de bóveda recuperado", new Color(0.48f, 0.92f, 1f, 1f));
    }

    public void Notify(string message, Color color)
    {
        hud?.ShowMessage(message, color);
    }

    public void DamagePlayer(int amount, bool lethal = false)
    {
        if (complete || player == null || Time.time < nextDamageTime) return;

        nextDamageTime = Time.time + 0.85f;
        if (lethal) player.health = 0;
        else player.health = Mathf.Max(0, player.health - Mathf.Max(1, amount));

        hud?.ShowDamage();
        if (player.health <= 0) Respawn(false);
    }

    private void Respawn(bool fell)
    {
        if (player == null) return;
        if (fell) hud?.ShowMessage("Te has precipitado. Volviendo al santuario…", new Color(1f, 0.69f, 0.48f, 1f));

        player.dead = true;
        if (gameController != null)
        {
            gameController.UpdateSpawnPoint(checkpoint);
            gameController.RespawnPlayer();
        }
        else
        {
            player.transform.position = checkpoint;
            player.health = 3;
            player.dead = false;
        }

        Physics.SyncTransforms();
        nextDamageTime = Time.time + 1.2f;
    }

    public void Win()
    {
        if (complete) return;
        complete = true;
        hud?.ShowVictory(coresCollected, requiredCores);
        Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
    }
}
