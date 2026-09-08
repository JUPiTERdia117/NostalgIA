using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Gancho con aceleración e inercia, integrado con el CharacterController.</summary>
public class GrapleController : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float maxDistance = 60f;
    [Tooltip("Radio de llegada alrededor del ancla, medido desde el centro del jugador. Se respeta como mínimo el espacio de su collider.")]
    [FormerlySerializedAs("minDistance")]
    [SerializeField, Min(0f)] private float arrivalRadius = 1.5f;
    [Tooltip("Tiempo máximo de tracción en segundos por enganche. Al agotarse, suelta el gancho conservando el impulso.")]
    [SerializeField, Min(0.1f)] private float maxPullDuration = 3f;
    [Tooltip("Distancia que puedes rebasar el ancla en la dirección inicial del enganche antes de soltarlo, aunque pases por un lado.")]
    [SerializeField, Min(0.1f)] private float overshootDistance = 2f;
    [Tooltip("Gravedad durante el enganche. Cero usa el 35% de la gravedad del jugador.")]
    [SerializeField, Min(0f)] private float gravity;
    [SerializeField] private EasyPeasyFirstPersonController.FirstPersonController firstPersonController;
    [Tooltip("Superficies válidas. Si existe la capa Graple, se usa por defecto.")]
    [SerializeField] private LayerMask grappleMask = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0f)] private float pullAcceleration = 38f;
    [Tooltip("Límite de velocidad hacia el ancla; conserva la velocidad lateral y el exceso de impulso previo.")]
    [SerializeField, Min(1f)] private float pullSpeed = 24f;
    [SerializeField, Min(0f)] private float airControl = 8f;
    [Tooltip("Fracción de velocidad conservada al soltar el gancho. 0.85 conserva el 85%; se aplica una sola vez por enganche.")]
    [SerializeField, Range(0f, 1f)] private float releaseMomentumRetention = 0.85f;
    [SerializeField, Min(0f)] private float launchLift = 4f;
    [SerializeField, Min(0.01f)] private float pullRampTime = 0.2f;

    private PlayerAim aim;
    private ToolRayVisual rayVisual;
    private Collider anchorCollider;
    private Vector3 localAnchor;
    private Vector3 approachDirection;
    private float elapsed;
    public bool IsGrappling { get; private set; }
    public Vector3 AnchorPoint => anchorCollider != null
        ? anchorCollider.transform.TransformPoint(localAnchor) : Vector3.zero;
    public float AirControl => airControl;
    public float ReleaseMomentumRetention => Mathf.Clamp01(releaseMomentumRetention);
    private Vector3 PlayerCenter => firstPersonController.transform.TransformPoint(
        firstPersonController.characterController.center);
    private float ArrivalDistance => Mathf.Max(arrivalRadius,
        Mathf.Max(firstPersonController.characterController.radius,
            firstPersonController.characterController.height * 0.5f) +
        firstPersonController.characterController.skinWidth + 0.15f);

    void Awake()
    {
        if (firstPersonController == null)
            firstPersonController = GetComponentInParent<EasyPeasyFirstPersonController.FirstPersonController>();
        int layer = LayerMask.NameToLayer("Graple");
        if (layer >= 0 && grappleMask.value == Physics.DefaultRaycastLayers)
            grappleMask = 1 << layer;
    }

    void Start()
    {
        aim = PlayerAim.GetOrCreate(firstPersonController);
        rayVisual = new ToolRayVisual(transform, new Color(1f, 0.55f, 0.12f, 0.9f));
        if (firstPersonController == null || firstPersonController.playerCamera == null) return;
        Transform lookCamera = firstPersonController.playerCamera;
        if (lookCamera != transform && !lookCamera.IsChildOf(transform) && !transform.IsChildOf(lookCamera))
            transform.SetParent(lookCamera, true);
    }

    public void Graple()
    {
        if (IsGrappling) { Release(); return; }
        if (firstPersonController == null || !firstPersonController.isActiveAndEnabled ||
            firstPersonController.characterController == null ||
            !firstPersonController.characterController.enabled || firstPersonController.isInWater) return;

        // Exactamente el mismo rayo de cámara que usa la aspiradora y su retícula.
        if (aim == null || !aim.IsReady || !aim.TryGetHit(maxDistance, out RaycastHit nearest)) return;
        if (nearest.collider == null || ((1 << nearest.collider.gameObject.layer) & grappleMask.value) == 0) return;
        if (Vector3.Distance(PlayerCenter, nearest.point) <= ArrivalDistance) return;

        anchorCollider = nearest.collider;
        localAnchor = anchorCollider.transform.InverseTransformPoint(nearest.point);
        approachDirection = (nearest.point - PlayerCenter).normalized;
        elapsed = 0f;
        IsGrappling = true;
        firstPersonController.BeginGrappleMotion(this, launchLift);

    }

    public Vector3 Accelerate(Vector3 velocity, float deltaTime)
    {
        if (!IsGrappling) return velocity;
        elapsed += deltaTime;
        if (elapsed >= Mathf.Max(0.1f, maxPullDuration))
        { Release(); return velocity; }
        if (anchorCollider == null || !anchorCollider.enabled || !anchorCollider.gameObject.activeInHierarchy)
        { Release(); return velocity; }
        Vector3 offset = AnchorPoint - PlayerCenter;
        if (offset.magnitude <= ArrivalDistance) { Release(); return velocity; }
        Vector3 direction = offset.normalized;
        float acceleration = pullAcceleration * Mathf.Clamp01(elapsed / Mathf.Max(0.01f, pullRampTime));
        // Solo limita el impulso añadido hacia el ancla, nunca sustituye la velocidad existente.
        float speedToAdd = Mathf.Min(acceleration * deltaTime,
            Mathf.Max(0f, pullSpeed - Vector3.Dot(velocity, direction)));
        velocity += direction * speedToAdd;
        velocity += Vector3.down * (gravity > 0f ? gravity : firstPersonController.gravity * 0.35f) * deltaTime;
        return velocity;
    }

    public void CheckArrival(Vector3 previousCenter)
    {
        if (!IsGrappling || anchorCollider == null) return;
        // Comprueba el segmento recorrido para no saltarse el destino a gran velocidad.
        Vector3 travel = PlayerCenter - previousCenter;
        float t = travel.sqrMagnitude > 0.000001f
            ? Mathf.Clamp01(Vector3.Dot(AnchorPoint - previousCenter, travel) / travel.sqrMagnitude) : 0f;
        if (Vector3.Distance(previousCenter + travel * t, AnchorPoint) <= ArrivalDistance)
        {
            Release();
            return;
        }

        // Mide cuánto se rebasó el destino, incluso pasando fuera del radio de llegada.
        // Alejarse al inicio por la inercia previa no cuenta como haberlo sobrepasado.
        float distancePastAnchor = Vector3.Dot(PlayerCenter - AnchorPoint, approachDirection);
        if (distancePastAnchor >= Mathf.Max(0.1f, overshootDistance)) Release();
    }

    public void Release()
    {
        IsGrappling = false;
        anchorCollider = null;
    }

    void LateUpdate()
    {
        if (aim == null || !aim.IsReady) { rayVisual?.Hide(); return; }
        Vector3 destination = IsGrappling && anchorCollider != null
            ? AnchorPoint : aim.GetEndPoint(maxDistance);
        rayVisual?.Draw(transform.position, destination, IsGrappling);
    }

    void OnDisable() { Release(); rayVisual?.Hide(); }
    void OnDestroy() { rayVisual?.Dispose(); }
}
