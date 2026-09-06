using UnityEngine;

/// <summary>
/// Controla la animación y el estado de un objeto que puede ser absorbido.
/// </summary>
public class PoolableController : MonoBehaviour
{
    [Tooltip("Indica si hay una trayectoria de absorción iniciada. El script gestiona este estado automáticamente.")]
    public bool beingPulled = false;
    [Tooltip("Aspiradora que atrae este objeto. Se asigna al detectarlo con el rayo y se borra al terminar o cancelar la absorción.")]
    public ObjectVacum pooller;

    [Tooltip("Duración de la trayectoria en segundos, independiente de la distancia. Puede terminar antes por cercanía.")]
    [SerializeField, Min(0.01f)] private float absorptionDuration = 1.0f;
    [Tooltip("Radio máximo de la espiral en unidades de Unity. Empieza y termina en cero; cero elimina la espiral.")]
    [SerializeField, Min(0f)] private float spiralRadius = 0.5f;
    [Tooltip("Número de vueltas de la espiral durante una trayectoria completa de absorción.")]
    [SerializeField, Min(0f)] private float spiralTurns = 2f;
    [Tooltip("Segundos que tarda en recuperar su tamaño original al interrumpir la absorción.")]
    [SerializeField, Min(0.01f)] private float scaleRestoreDuration = 0.2f;
    [Tooltip("Distancia al centro de la aspiradora que completa la absorción y guarda el objeto, en unidades de Unity.")]
    [SerializeField, Min(0f)] private float absorptionDistance = 0.2f;

    // Escala local de referencia, incluidos tamaños no uniformes.
    private Vector3 originalScale;
    // Origen fijo de la trayectoria actual, en coordenadas del mundo.
    private Vector3 startPosition;
    // Escala al iniciar la absorción, que puede ser menor si se interrumpió una recuperación.
    private Vector3 pullStartScale;
    // Escala desde la que se recupera el tamaño al dejar de absorber.
    private Vector3 restoreStartScale;
    // Primer eje perpendicular a la dirección inicial de absorción.
    private Vector3 spiralRight;
    // Segundo eje perpendicular; junto con spiralRight forma el plano de la espiral.
    private Vector3 spiralUp;
    // Referencia usada para detectar un cambio de aspiradora durante la trayectoria.
    private ObjectVacum activePuller;
    // Progreso normalizado de absorción: cero al iniciar y uno al llegar.
    private float progress;
    // Progreso normalizado de recuperación; uno significa tamaño ya restaurado.
    private float restoreProgress = 1f;

    /// <summary>
    /// Conserva la escala local original para restaurarla tras la absorción o su cancelación.
    /// </summary>
    void Awake()
    {
        originalScale = transform.localScale;
    }

    /// <summary>
    /// Avanza la absorción o recupera gradualmente el tamaño cuando deja de haber una aspiradora activa.
    /// </summary>
    void FixedUpdate()
    {
        if (pooller == null || !pooller.vacuumActive)
        {
            // Captura la escala una sola vez al interrumpir la trayectoria.
            if (beingPulled)
            {
                restoreStartScale = transform.localScale;
                restoreProgress = 0f;
                ResetPull();
            }

            restoreProgress = Mathf.Clamp01(restoreProgress + Time.fixedDeltaTime / Mathf.Max(0.01f, scaleRestoreDuration));
            transform.localScale = Vector3.Lerp(restoreStartScale, originalScale, restoreProgress);
            return;
        }

        // Una nueva aspiradora o un nuevo intento requiere reiniciar la trayectoria.
        if (!beingPulled || activePuller != pooller)
        {
            BeginPull();
        }

        MoveToVacuum();
    }

    /// <summary>
    /// Inicia una trayectoria desde la posición y escala actuales, sin saltos al retomar la absorción.
    /// </summary>
    void BeginPull()
    {
        startPosition = transform.position;
        pullStartScale = transform.localScale;
        activePuller = pooller;
        progress = 0f;

        Vector3 direction = pooller.transform.position - startPosition;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        // Si la dirección es vertical, usa otro eje de referencia para evitar un producto cruzado nulo.
        Vector3 reference = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f
            ? Vector3.right
            : Vector3.up;
        spiralRight = Vector3.Cross(direction, reference).normalized;
        spiralUp = Vector3.Cross(direction, spiralRight).normalized;


        //GetComponent<BoxCollider>().enabled = false;
        
        beingPulled = true;
        
    }

    /// <summary>
    /// Combina avance hacia la aspiradora, desplazamiento en espiral y reducción de tamaño.
    /// </summary>
    void MoveToVacuum()
    {
        Vector3 destination = pooller.transform.position;
        float absorbRadius = Mathf.Max(0f, absorptionDistance);
        if ((transform.position - destination).sqrMagnitude <= absorbRadius * absorbRadius)
        {
            FinishAbsorption();
            return;
        }

        // El progreso depende del tiempo, no de la distancia; limita el divisor para evitar cero.
        progress = Mathf.Clamp01(progress + Time.fixedDeltaTime / Mathf.Max(0.01f, absorptionDuration));

        if (progress >= 1f)
        {
            transform.position = destination;
            FinishAbsorption();
            return;
        }

        // El seno hace crecer y desaparecer el radio sin saltos en los extremos de la trayectoria.
        float angle = progress * spiralTurns * Mathf.PI * 2f;
        float radius = spiralRadius * Mathf.Sin(progress * Mathf.PI);
        Vector3 offset = (spiralRight * Mathf.Cos(angle) + spiralUp * Mathf.Sin(angle)) * radius;

        // El destino se actualiza para seguir a la aspiradora aunque el jugador se mueva.
        transform.position = Vector3.Lerp(startPosition, destination, progress) + offset;
        transform.localScale = pullStartScale * (1f - progress);

        if ((transform.position - destination).sqrMagnitude <= absorbRadius * absorbRadius)
        {
            FinishAbsorption();
        }
    }

    /// <summary>
    /// Restaura el tamaño antes de guardar el volumen de colisión y desactivar el objeto.
    /// </summary>
    void FinishAbsorption()
    {
        transform.localScale = originalScale;
        pooller.PoolObject(gameObject);
    }

    /// <summary>
    /// Limpia la trayectoria y sus referencias sin cambiar la posición ni la escala actuales.
    /// </summary>
    void ResetPull()
    {
        beingPulled = false;
        pooller = null;
        activePuller = null;
        progress = 0f;
    }

    /// <summary>
    /// Deja el objeto con su tamaño y estado iniciales para que pueda volver a lanzarse y absorberse.
    /// </summary>
    void OnDisable()
    {
        ResetPull();
        transform.localScale = originalScale;
        restoreProgress = 1f;
        pooller = null;
    }
}
