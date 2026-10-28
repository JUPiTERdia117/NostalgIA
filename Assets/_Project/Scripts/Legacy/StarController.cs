using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Estrella coleccionable del Tutorial y de los niveles.
/// Rota y flota; al recogerla el jugador publica <see cref="StarCollected"/> y se destruye.
/// </summary>
/// <remarks>
/// Fase 1: la estrella ya no busca al controlador del juego; comunica la recolección por el bus
/// de eventos y el HUD, la guía y la progresión la escuchan. La rotación y la flotación siguen
/// en <c>Update</c> hasta que la Fase 3 las traslade a un shader de vértices.
/// </remarks>
public class StarController : MonoBehaviour
{
    [Header("Identidad")]
    [Tooltip("Índice de la estrella dentro de la estación (0–2). Lo asigna el constructor de la Fase 1 a partir del nombre.")]
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
        EventBus<StarCollected>.Raise(new StarCollected(starIndex, transform.position));
        Destroy(gameObject);
    }
}
