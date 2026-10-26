using NostalgIA.Core;
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
using System;

/// <summary>
/// Detecta, almacena y lanza objetos absorbibles desde la dirección de la mirada.
/// </summary>
public class ObjectVacum : MonoBehaviour
{
    // Filtro de detección de absorción: solo la capa Absorbable (antes «Poolable»).
    // Se toma la máscara precalculada de NostalgIA.Core.Layers en lugar de buscar la capa por
    // nombre, de modo que el renombre de la capa no rompe la absorción.
    LayerMask layerMask;
    private PlayerAim aim;
    private ToolRayVisual rayVisual;

    [Tooltip("Activa la absorción mientras se mantiene el clic izquierdo. PlayerController actualiza este estado.")]
    public bool vacuumActive = false;

    [Tooltip("Alcance máximo del rayo de absorción, en unidades de Unity. Solo detecta la capa Poolable.")]
    [SerializeField] float maxDistance = 10f;
    [Tooltip("Controlador cuya cámara seguirá la aspiradora. Si no se asigna, se busca en los padres al iniciar.")]
    [SerializeField] private EasyPeasyFirstPersonController.FirstPersonController firstPersonController;

    [Tooltip("Objetos absorbidos, del más reciente al más antiguo. Stack no se muestra ni se serializa en el Inspector estándar de Unity.")]
    [SerializeField] private Stack<GameObject> objectsToPool = new Stack<GameObject>();
    [Tooltip("Impulso inicial hacia delante de la aspiradora, en N·s. A mayor masa del Rigidbody, menor velocidad de salida. Sustituye a Launch Speed.")]
    [FormerlySerializedAs("launchSpeed")]
    [SerializeField, Min(0f)] private float launchImpulse = 10f;
    [Tooltip("Separación adicional entre la aspiradora y el borde posterior de la caja del objeto al aparecer, en unidades de Unity.")]
    [SerializeField, Min(0.01f)] private float launchClearance = 0.2f;
    [Tooltip("Capas que pueden impedir un lanzamiento. Se ignoran triggers, el jugador y el propio objeto.")]
    [SerializeField] private LayerMask launchObstacleMask = ~0;
    [Tooltip("Intensidad del giro inicial: se divide por el tamaño del objeto. Para un objeto de una unidad equivale a radianes por segundo.")]
    [SerializeField, Min(0f)] private float launchSpin = 2f;
    [Tooltip("Límite del giro inicial en radianes por segundo. Evita que los objetos pequeños giren demasiado rápido.")]
    [SerializeField, Min(0f)] private float maxLaunchSpin = 5f;
	
	[Tooltip("Cantidad de objetos que puede almacenar la aspiradora.")]	
	[SerializeField, Min(1)] private int objectCapacity = 5;
	public int currentCapacity { get { return Mathf.Max(0, objectCapacity - objectsToPool.Count); } }
    public int StoredCount => objectsToPool != null ? objectsToPool.Count : 0;
    public int ObjectCapacity => Mathf.Max(1, objectCapacity);

    public event Action<GameObject> OnObjectAbsorbed;
    public event Action<GameObject> OnObjectLaunched;

    // Caja alineada con el mundo de cada objeto guardado: centro relativo a su posición y tamaño completo. Se captura antes de desactivarlo.
    private readonly Dictionary<GameObject, Bounds> storedBounds = new Dictionary<GameObject, Bounds>();

    /// <summary>
    /// Prepara el filtro de capas para detectar únicamente objetos absorbibles.
    /// </summary>
    void Awake()
    {
        layerMask = Layers.Masks.Absorbable;
        if (GetComponent<ToolVisualController>() == null)
        {
            gameObject.AddComponent<ToolVisualController>();
        }
    }

    /// <summary>
    /// Conecta la aspiradora a la cámara para seguir también la inclinación vertical de la mirada.
    /// </summary>
    void Start()
    {
        if (firstPersonController == null)
        {
            firstPersonController = GetComponentInParent<EasyPeasyFirstPersonController.FirstPersonController>();
        }

        aim = PlayerAim.GetOrCreate(firstPersonController);
        // Guía tenue gris neutra en reposo; al aspirar se oculta para dar protagonismo a las rayas grises de viento
        rayVisual = new ToolRayVisual(transform, new Color(0.75f, 0.78f, 0.82f, 0.28f));

        if (firstPersonController == null || firstPersonController.playerCamera == null) return;

        Transform lookCamera = firstPersonController.playerCamera;
        // Evita crear un ciclo en la jerarquía de transforms.
        if (lookCamera == transform || lookCamera.IsChildOf(transform)) return;

        // Mantiene la colocación y escala en el mundo al pasar a ser hijo de la cámara.
        if (!transform.IsChildOf(lookCamera))
        {
            transform.SetParent(lookCamera, true);
        }
    }

    /// <summary>
    /// Busca un objeto frente a la aspiradora durante cada paso de física mientras está activa.
    /// </summary>
    void FixedUpdate()
    {
        if (!vacuumActive || aim == null || !aim.IsReady) return;
        if (aim.TryGetHit(maxDistance, out RaycastHit hit) &&
            ((1 << hit.collider.gameObject.layer) & layerMask.value) != 0)
        {
            PoolableController poolable = hit.collider.GetComponentInParent<PoolableController>();
            if (poolable != null) poolable.pooller = this;
        }
    }

    void LateUpdate()
    {
        if (aim == null || !aim.IsReady || vacuumActive) { rayVisual?.Hide(); return; }
        rayVisual?.Draw(transform.position, aim.GetEndPoint(maxDistance), false);
    }

    void OnDisable()
    {
        vacuumActive = false;
        rayVisual?.Hide();
    }

    void OnDestroy() { rayVisual?.Dispose(); }

    /// <summary>
    /// Guarda una sola vez el objeto a tamaño completo, registra su volumen y lo desactiva.
    /// </summary>
    public void PoolObject(GameObject obj)
    {
        if (storedBounds.ContainsKey(obj)) return;
		
        // Sincroniza los cambios de escala antes de consultar los colliders todavía activos.
        Physics.SyncTransforms();
        Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
        foreach (Collider collider in obj.GetComponentsInChildren<Collider>())
        {
            if (collider.enabled && !collider.isTrigger) bounds.Encapsulate(collider.bounds);
        }
        // Conserva el desplazamiento entre el pivote y el centro de la caja.
        storedBounds[obj] = new Bounds(bounds.center - obj.transform.position, bounds.size);
        objectsToPool.Push(obj);
        obj.SetActive(false);

        OnObjectAbsorbed?.Invoke(obj);
    }

    /// <summary>
    /// Intenta lanzar un objeto por llamada, conservando los bloqueados y su orden en la pila.
    /// </summary>
    public void LaunchObject()
    {
        // Aparta temporalmente los objetos bloqueados para poder probar los siguientes.
        var skipped = new Stack<GameObject>();
        bool launched = false;
        GameObject launchedCandidate = null;
        while (objectsToPool.Count > 0)
        {
            GameObject candidate = objectsToPool.Pop();
            if (candidate == null) continue;
            if (TryLaunchObject(candidate))
            {
                launched = true;
                launchedCandidate = candidate;
                break;
            }
            skipped.Push(candidate);
        }

        // Repone los objetos descartados en su orden original.
        while (skipped.Count > 0) objectsToPool.Push(skipped.Pop());
        if (launched && launchedCandidate != null)
        {
            OnObjectLaunched?.Invoke(launchedCandidate);
        }
        else if (!launched && objectsToPool.Count > 0)
        {
            Debug.Log("No se pudo lanzar: comprueba el espacio libre y que los objetos tengan un Rigidbody en la raíz.", this);
        }
    }

    /// <summary>
    /// Comprueba la salida y aplica velocidad y giro. Devuelve false sin consumir el objeto si no puede salir.
    /// </summary>
    private bool TryLaunchObject(GameObject pooledObj)
    {
        if (!pooledObj.TryGetComponent<Rigidbody>(out var body) ||
            !storedBounds.TryGetValue(pooledObj, out Bounds bounds)) return false;

        // El proyectil converge al punto que marca la retícula desde la boquilla.
        Vector3 direction = aim != null && aim.IsReady
            ? (aim.GetEndPoint(maxDistance) - transform.position).normalized : transform.forward;
        if (direction.sqrMagnitude < 0.0001f) return false;
        Vector3 extents = Vector3.Max(bounds.extents, Vector3.one * 0.01f);
        // Proyecta las semidimensiones sobre la dirección de salida para calcular la separación.
        float forwardExtent = Mathf.Abs(direction.x) * extents.x +
                              Mathf.Abs(direction.y) * extents.y +
                              Mathf.Abs(direction.z) * extents.z;
        float distance = forwardExtent + Mathf.Max(0.01f, launchClearance);
        Vector3 spawnCenter = transform.position + direction * distance;
        Transform playerRoot = firstPersonController != null ? firstPersonController.transform : transform.root;

        Physics.SyncTransforms();
        // El volumen completo se necesita en la posición de aparición, no alrededor de la boquilla.
        // Comprueba el segmento de salida para evitar aparecer al otro lado de una pared.
        foreach (RaycastHit hit in Physics.RaycastAll(transform.position, direction,
                     distance, launchObstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (IsLaunchObstacle(hit.collider, playerRoot, pooledObj.transform)) return false;
        }

        // Coloca el pivote de modo que la caja quede centrada en spawnCenter.
        pooledObj.transform.SetParent(null, true);
        pooledObj.transform.position = spawnCenter - bounds.center;
        pooledObj.SetActive(true);
        body.isKinematic = false;
        // Parte del reposo y aplica el mismo impulso: el cambio de velocidad es impulso / masa.
        body.linearVelocity = Vector3.zero;
        body.AddForce(direction * Mathf.Max(0f, launchImpulse), ForceMode.Impulse);

        // Marcar como lanzado por el jugador para garantizar daño mínimo y cálculo de fuerza al impactar
        PoolableController poolable = pooledObj.GetComponent<PoolableController>();
        if (poolable != null) poolable.MarkAsThrownByPlayer(launchImpulse);
        ThrowableDamage throwable = pooledObj.GetComponent<ThrowableDamage>();
        if (throwable != null) throwable.MarkAsThrownByPlayer(launchImpulse);

        // El giro disminuye con el tamaño y tiene un máximo para los objetos diminutos.
        float size = Mathf.Max(0.1f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
        float spinSpeed = Mathf.Min(Mathf.Max(0f, maxLaunchSpin), Mathf.Max(0f, launchSpin) / size);
        // Inclina ligeramente el eje de giro para dar variedad a cada lanzamiento.
        Vector3 spinAxis = (transform.right + transform.up * UnityEngine.Random.Range(-0.35f, 0.35f)).normalized;
        body.maxAngularVelocity = Mathf.Max(body.maxAngularVelocity, spinSpeed);
        body.angularVelocity = spinAxis * spinSpeed;
        storedBounds.Remove(pooledObj);
        return true;
    }

    /// <summary>
    /// Excluye los colliders del jugador y del objeto que se está intentando lanzar.
    /// </summary>
    private bool IsLaunchObstacle(Collider obstacle, Transform playerRoot, Transform launchedObject)
    {
        return !obstacle.transform.IsChildOf(playerRoot) &&
               !obstacle.transform.IsChildOf(launchedObject);
    }
}
