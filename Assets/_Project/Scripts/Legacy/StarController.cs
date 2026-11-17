using NostalgIA.Core.Events;
using NostalgIA.Core.Levels;
using NostalgIA.Gameplay.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Estrella coleccionable del Tutorial y de los niveles.
/// Rota y flota; al recogerla el jugador publica <see cref="StarCollected"/> y se destruye.
/// </summary>
/// <remarks>
/// La estrella ya no busca al controlador del juego; comunica la recolección por el bus
/// de eventos y el HUD, la guía y la progresión la escuchan. La rotación y la flotación siguen
/// en <c>Update</c> hasta que se trasladen a un shader de vértices.
/// Al cargarse cada escena se cuentan sus estrellas (también las ocultas) en <see cref="StarTracker"/>,
/// que es de donde el HUD, los resultados y el platino sacan el total del nivel.
/// </remarks>
public class StarController : MonoBehaviour
{
    [Header("Identidad")]
    [Tooltip("Índice de la estrella dentro de la estación (0–2). Se asigna a partir del nombre.")]
    [SerializeField] private int starIndex = -1;

    [Header("Animation")]
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobFrequency = 2f;

    private Vector3 startPosition;
    private float startLocalY;
    private bool collected;

    /// <summary>Índice de la estrella.</summary>
    public int StarIndex
    {
        get => starIndex;
        set => starIndex = value;
    }

    /// <summary>Cuenta las estrellas de cada escena que se carga (incluidas las desactivadas).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= CountStars;
        SceneManager.sceneLoaded += CountStars;
        // La primera escena ya se cargó cuando se ejecuta este método.
        for (int i = 0; i < SceneManager.sceneCount; i++) CountStars(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    private static void CountStars(Scene scene, LoadSceneMode mode)
    {
        StarController[] all = FindObjectsByType<StarController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < all.Length; i++) if (all[i].gameObject.scene == scene) count++;
        StarTracker.SetTotal(scene, count);
    }

    private void Awake()
    {
        startPosition = transform.position;
        startLocalY = transform.localPosition.y;
    }

    private void OnEnable()
    {
        // Si la estrella se suelta en la posición donde murió un enemigo antes de activarse,
        // actualizo su altura base para que flote exactamente en ese punto.
        startPosition = transform.position;
        startLocalY = transform.localPosition.y;
    }

    private void Update()
    {
        // Rotación continua y flotación senoidal: y = y0 + A·sin(ω·t)
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        float offset = Mathf.Sin(Time.time * bobFrequency) * bobHeight;
        if (transform.parent != null)
        {
            Vector3 local = transform.localPosition;
            local.y = startLocalY + offset;
            transform.localPosition = local;
        }
        else
        {
            float newY = startPosition.y + offset;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }

    private void OnTriggerEnter(Collider col)
    {
        if (collected || !col.TryGetComponent(out PlayerHealth health) || health.IsDead) return;

        collected = true;
        // Primero se cuenta, para que el HUD ya vea la estrella nueva al recibir el evento.
        StarTracker.Collect(gameObject.scene);
        EventBus<StarCollected>.Raise(new StarCollected(starIndex, transform.position));
        Destroy(gameObject);
    }
}
