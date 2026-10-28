using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Tools;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Controla la animación procedural del modelo de la herramienta (robot.fbx) en primera persona.
/// <list type="number">
/// <item><b>Recámara inflable:</b> la caja trasera (recámara) y los tanques del tubo se inflan en función
/// de la ocupación real del almacén (objetos guardados / capacidad). El inflado se realiza escalando cada
/// malla alrededor del centro de su volumen, de modo que la pieza crece "en su sitio" y el arma nunca se
/// desplaza ni se levanta. Al tragar un objeto recorre el arma una onda peristáltica breve, y al disparar
/// se produce un pequeño rebote de desinflado.</item>
/// <item><b>Garras siempre visibles:</b> en modo Gancho las tres garras quedan abiertas, listas para
/// agarrarse; en modo Aspiradora se cierran sobre la boquilla. La transición es escalonada (base, falange
/// media y punta) y va acompañada de un giro corto del cabezal, como el mandril de una herramienta.
/// En modo Aspiradora se abren al aspirar (para dejar entrar los objetos) y al disparar (para dejarlos
/// salir).</item>
/// <item><b>Disparo del gancho:</b> el cabezal completo (Bone.002) viaja hasta el ancla estirando un tubo
/// dinámico, y se retrae al soltar.</item>
/// </list>
/// </summary>
/// <remarks>
/// En esta sección se decidió no animar por <c>Animator</c>: el modelo final no incluye clips, y todas las
/// poses dependen de valores continuos de jugabilidad (ocupación, mezcla de modo, extensión del gancho).
/// Las poses se calculan en <c>LateUpdate</c> a partir de las poses de reposo capturadas al iniciar, por lo
/// que nunca se acumulan errores numéricos entre frames. El bucle de animación no genera asignaciones.
/// </remarks>
public class ToolVisualController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Controlador de herramientas del jugador; se usa para saber qué herramienta está activa.")]
    [SerializeField] private ToolController playerController;
    [Tooltip("Aspiradora cuya ocupación determina el inflado de la recámara.")]
    [SerializeField] private VacuumTool vacuum;
    [Tooltip("Gancho cuyo anclaje determina el disparo del cabezal.")]
    [SerializeField] private GrappleTool grapple;
    [Tooltip("Raíz del modelo robot.fbx. Si se deja vacío se busca un hijo llamado 'robot'.")]
    [SerializeField] private Transform robotRoot;

    [Header("Cambio de modo")]
    [Tooltip("Velocidad de la transición entre modos (1/s). 4 equivale a una transición de 0,25 s.")]
    [SerializeField] private float modeTransitionSpeed = 4f;
    [Tooltip("Giro máximo del cabezal sobre el eje del tubo durante la transición (grados).")]
    [SerializeField] private float transitionTwistDegrees = 28f;
    [Tooltip("Ángulo adicional de apertura de la base de cada garra en modo Gancho (grados).")]
    [SerializeField] private float grappleOpenExtraDegrees = 6f;
    [Tooltip("Fracción del ángulo de reposo que se cierra la base de cada garra en modo Aspiradora (0-1).")]
    [SerializeField, Range(0f, 1.2f)] private float vacuumCloseFraction = 0.55f;
    [Tooltip("Curvatura hacia dentro de la falange media en modo Aspiradora (grados).")]
    [SerializeField] private float vacuumMidCurlDegrees = 12f;
    [Tooltip("Curvatura hacia dentro de la punta en modo Aspiradora (grados).")]
    [SerializeField] private float vacuumTipCurlDegrees = 5f;

    [Header("Aspiración")]
    [Tooltip("Apertura de las garras mientras se aspira (0 = cerradas, 1 = abiertas como en modo Gancho).")]
    [SerializeField, Range(0f, 1.2f)] private float suctionClawOpen = 0.85f;
    [Tooltip("Velocidad con que las garras se abren o cierran al empezar o dejar de aspirar (1/s).")]
    [SerializeField] private float suctionClawSpeed = 7f;
    [Tooltip("Tiempo que las garras permanecen abiertas tras disparar un objeto (s).")]
    [SerializeField] private float ejectOpenTime = 0.25f;
    [Tooltip("Velocidad con que las garras se abren al disparar (1/s). Mayor que la de aspiración para " +
             "que la boquilla quede despejada casi en el mismo frame en que sale el objeto.")]
    [SerializeField] private float ejectClawSpeed = 24f;

    [Header("Disparo del gancho")]
    [Tooltip("Velocidad de vuelo del cabezal hacia el ancla (m/s). El tiempo de vuelo se limita a 0,06–0,2 s.")]
    [SerializeField] private float hookFlightSpeed = 75f;
    [Tooltip("Tiempo que tardan las garras en cerrarse sobre la superficie al llegar (s).")]
    [SerializeField] private float gripCloseTime = 0.09f;
    [Tooltip("Fracción del enfriamiento dedicada a zafarse de la superficie antes de retraerse (0-1).")]
    [SerializeField, Range(0.05f, 0.5f)] private float unhookFraction = 0.2f;
    [Tooltip("Fracción final del enfriamiento dedicada a acoplarse de nuevo en el arma (0-1).")]
    [SerializeField, Range(0.02f, 0.3f)] private float dockFraction = 0.1f;
    [Tooltip("Distancia que se separa el cabezal de la superficie al zafarse (m).")]
    [SerializeField] private float unhookPushDistance = 0.25f;
    [Tooltip("Alcance visual del cabezal cuando el disparo falla (m).")]
    [SerializeField] private float missReachDistance = 7f;
    [Tooltip("Duración total del disparo fallido: ida y vuelta (s).")]
    [SerializeField] private float missDuration = 0.32f;

    [Header("Recámara inflable")]
    [Tooltip("Crecimiento transversal máximo de la recámara con el almacén lleno (0,45 = +45 %).")]
    [SerializeField] private float chamberRadialGrowth = 0.35f;
    [Tooltip("Ancla vertical del inflado de la recámara: 0 = crece igual hacia arriba y hacia abajo; " +
             "1 = la cara superior queda fija y crece solo hacia abajo. Evita que en primera persona la " +
             "recámara invada la vista al llenarse.")]
    [SerializeField, Range(0f, 1f)] private float chamberTopAnchor = 0.6f;
    [Tooltip("Crecimiento longitudinal máximo de la recámara con el almacén lleno.")]
    [SerializeField] private float chamberLengthGrowth = 0.12f;
    [Tooltip("Crecimiento transversal máximo de los tanques del tubo con el almacén lleno.")]
    [SerializeField] private float tankRadialGrowth = 0.22f;
    [Tooltip("Crecimiento transversal máximo de los anillos de la boquilla con el almacén lleno.")]
    [SerializeField] private float ringRadialGrowth = 0.1f;
    [Tooltip("Frecuencia natural del resorte de inflado (Hz). Mayor = responde más rápido.")]
    [SerializeField] private float inflateFrequency = 3.2f;
    [Tooltip("Amortiguamiento del resorte de inflado (1 = crítico, <1 = rebota ligeramente).")]
    [SerializeField, Range(0.2f, 1.5f)] private float inflateDamping = 0.55f;
    [Tooltip("Amplitud de la onda peristáltica al tragar un objeto (fracción de escala).")]
    [SerializeField] private float gulpAmplitude = 0.14f;
    [Tooltip("Duración total de la onda al tragar (s).")]
    [SerializeField] private float gulpDuration = 0.45f;
    [Tooltip("Amplitud del desinflado momentáneo al disparar un objeto (fracción de escala).")]
    [SerializeField] private float deflateAmplitude = 0.08f;
    [Tooltip("Amplitud de la vibración de presión con la recámara llena (fracción de escala).")]
    [SerializeField] private float pressureWobbleAmplitude = 0.015f;

    /// <summary>
    /// Pieza inflable: se guarda su pose de reposo y el centro de su malla para escalarla alrededor
    /// de dicho centro en lugar de alrededor del pivote.
    /// </summary>
    private struct InflatablePart
    {
        public Transform transform;
        public Vector3 restPosition;
        public Vector3 restScale;
        public Vector3 meshCenter;   // Centro de la malla en espacio local (m del modelo).
        public Vector3 anchor;       // Punto fijo del escalado en espacio local de la malla.
        public int lengthAxis;       // Eje local alineado con el tubo (0 = X, 1 = Y, 2 = Z).
        public float radialGrowth;   // Crecimiento transversal con el almacén lleno.
        public float lengthGrowth;   // Crecimiento longitudinal con el almacén lleno.
        public float gulpDelay;      // Retardo de la onda al tragar (0 = boquilla, 1 = recámara).
        public bool isChamber;
    }

    // Nodos del rig de robot.fbx.
    private Transform boneArm;          // Bone.001 (cuerpo del tubo)
    private Transform boneForearm;      // Bone.002 (cabezal con garras y aletas)
    private Transform boneCompartmentB; // Bone.004 (tanque delantero, se re-emparenta al cuerpo)

    private InflatablePart[] inflatables;
    private int inflatableCount;

    // Cadena de garras: 3 dedos articulados (base -> media -> punta) y 3 aletas de la boquilla.
    private Transform[] clawBaseBones;
    private Transform[] clawMidBones;
    private Transform[] clawTipBones;
    private Transform[] nozzleFlapBones;

    private Quaternion[] clawBaseRestRots;
    private Quaternion[] clawMidRestRots;
    private Quaternion[] clawTipRestRots;
    private Quaternion[] flapRestRots;

    // Ejes de bisagra expresados en el espacio local de cada hueso (ver ComputeHingeAxes).
    private Vector3[] clawBaseHinges;
    private Vector3[] clawMidHinges;
    private Vector3[] clawTipHinges;
    private Vector3[] flapHinges;
    private float[] clawRestOpenAngles;

    private Vector3 robotRestLocalPos;
    private Quaternion robotRestLocalRot;
    private Vector3 robotRestLocalScale;
    private Vector3 forearmRestLocalPos;
    private Quaternion forearmRestLocalRot;
    private Vector3 forearmRestLocalScale;

    // Estado continuo de la animación.
    private float grappleModeBlend;     // 0 = Aspiradora, 1 = Gancho.
    private float fillValue;            // Ocupación visual (resorte), 0..1 con posible sobrepaso.
    private float fillVelocity;         // Velocidad del resorte de ocupación (1/s).
    private float gulpTime = -1f;       // Tiempo transcurrido de la onda al tragar (s); -1 = inactiva.
    private float deflateTime = -1f;    // Tiempo transcurrido del rebote al disparar (s); -1 = inactivo.
    private int lastStoredCount;
    private bool suctioning;
    private float ejectHoldTime;        // Tiempo restante con las garras abiertas tras un disparo (s).

    /// <summary>Fases del cabezal del gancho.</summary>
    private enum HookPhase { Docked, Outbound, Attached, Releasing, Miss }

    private HookPhase hookPhase = HookPhase.Docked;
    private float hookTime;             // Tiempo transcurrido en la fase actual (s).
    private float hookPhaseDuration;    // Duración prevista de la fase actual (s).
    private Vector3 hookTarget;         // Punto de anclaje o destino del disparo (mundo).
    private Vector3 hookNormal;         // Normal de la superficie anclada (mundo).
    private Vector3 hookDetachPoint;    // Posición del cabezal al iniciar la retracción (mundo).
    private Vector3 hookWorldPos;       // Posición actual del cabezal fuera del arma (mundo).
    private bool releasePending;        // Se soltó el gancho antes de que el cabezal llegara.
    private float clawReach;            // Distancia del pivote del cabezal a la base de las garras (m).

    // Estado continuo de las garras (0-1): apertura por aspiración, cierre de agarre y apertura extra.
    private float suctionOpen;
    private float gripBlend;
    private float flareBlend;

    // Tubo de aspiradora extensible al lanzar el gancho.
    private LineRenderer vacuumTubeRenderer;
    private Material vacuumTubeMaterial;
    private const int TubeSegments = 20;

    // Efecto de viento heredado (solo si no existe el vórtice de la Fase 1).
    private ParticleSystem suctionWindParticles;
    private ParticleSystemRenderer suctionWindRenderer;
    private Material windParticleMaterial;
    private Texture2D windStreakTexture;
    private Transform nozzleTipTransform;

    private const int WindStreamlineCount = 6;
    private const int WindStreamlinePoints = 14;
    private LineRenderer[] windStreamlines;
    private Material windStreamlineMaterial;

    private bool initialized;

    private void Awake() => InitializeReferences();

    private void Start()
    {
        if (!initialized) InitializeReferences();
    }

    /// <summary>
    /// Localiza los nodos del rig, captura las poses de reposo y prepara las piezas inflables.
    /// Se ejecuta una sola vez; todas las búsquedas por nombre ocurren aquí y nunca en el bucle.
    /// </summary>
    private void InitializeReferences()
    {
        if (initialized) return;

        if (playerController == null) playerController = GetComponentInParent<ToolController>();
        if (vacuum == null) vacuum = GetComponent<VacuumTool>() ?? GetComponentInChildren<VacuumTool>(true);
        if (grapple == null) grapple = GetComponent<GrappleTool>() ?? GetComponentInChildren<GrappleTool>(true);

        if (robotRoot == null)
        {
            robotRoot = transform.Find("robot");
            if (robotRoot == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.ToLowerInvariant().Contains("robot")) { robotRoot = child; break; }
                }
            }
        }
        if (robotRoot == null) return;

        robotRestLocalPos = robotRoot.localPosition;
        robotRestLocalRot = robotRoot.localRotation;
        robotRestLocalScale = robotRoot.localScale;

        boneArm = FindDeepChild(robotRoot, "Bone.001");
        boneForearm = FindDeepChild(robotRoot, "Bone.002");
        boneCompartmentB = FindDeepChild(robotRoot, "Bone.004");

        // Se re-emparenta el tanque delantero (Bone.004) al cuerpo (Bone.001) para que permanezca en
        // el arma cuando el cabezal (Bone.002) sale disparado con el gancho.
        if (boneCompartmentB != null && boneArm != null && boneCompartmentB.parent == boneForearm)
            boneCompartmentB.SetParent(boneArm, true);

        clawBaseBones = FindAll("Bone.009", "Bone.010", "Bone.011");
        clawMidBones = FindAll("Bone.014", "Bone.012", "Bone.016");
        clawTipBones = FindAll("Bone.015", "Bone.013", "Bone.017");
        nozzleFlapBones = FindAll("Bone.006", "Bone.007", "Bone.008");

        if (boneForearm != null)
        {
            forearmRestLocalPos = boneForearm.localPosition;
            forearmRestLocalRot = boneForearm.localRotation;
            forearmRestLocalScale = boneForearm.localScale;
        }

        clawBaseRestRots = CaptureRotations(clawBaseBones);
        clawMidRestRots = CaptureRotations(clawMidBones);
        clawTipRestRots = CaptureRotations(clawTipBones);
        flapRestRots = CaptureRotations(nozzleFlapBones);

        ComputeHingeAxes();
        SetupInflatables();
        if (boneForearm != null && clawBaseBones[0] != null)
            clawReach = Vector3.Distance(boneForearm.position, clawBaseBones[0].position) + 0.05f;

        if (playerController != null)
            grappleModeBlend = playerController.ActiveTool == ToolId.Grapple ? 1f : 0f;

        SetupNozzleTip();
        SetupVacuumTubeRenderer();
        // Si la herramienta ya tiene el vórtice de partículas de la Fase 1 (SuctionVortexFx, con
        // material como asset), no se crea el viento heredado.
        if (GetComponentInChildren<NostalgIA.Presentation.VFX.SuctionVortexFx>(true) == null)
            SetupSuctionWindEffect();

        initialized = true;
    }

    private Transform[] FindAll(string a, string b, string c)
    {
        return new[] { FindDeepChild(robotRoot, a), FindDeepChild(robotRoot, b), FindDeepChild(robotRoot, c) };
    }

    private static Quaternion[] CaptureRotations(Transform[] bones)
    {
        var rots = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            if (bones[i] != null) rots[i] = bones[i].localRotation;
        return rots;
    }

    /// <summary>
    /// Calcula, para cada garra y aleta, una bisagra radial: el eje perpendicular al tubo y a la
    /// dirección en que apunta el dedo. Girar un ángulo negativo sobre ella cierra el dedo hacia el eje
    /// del tubo y uno positivo lo abre. El eje se calcula una vez en el espacio del cabezal y luego se
    /// expresa en el espacio local de cada hueso de la cadena, para poder aplicarlo como
    /// <c>reposo * AngleAxis(θ, ejeLocal)</c> sin que el giro de un hueso deforme el eje del siguiente.
    /// </summary>
    /// <remarks>
    /// El eje del tubo es el eje Y local de Bone.002 (los huesos exportados apuntan por su eje Y).
    /// Bisagra = Y × r, donde r es la proyección radial (perpendicular al tubo) de la dirección del dedo;
    /// por la regla de la mano derecha, un giro positivo lleva Y hacia r, es decir, abre el dedo.
    /// </remarks>
    private void ComputeHingeAxes()
    {
        int n = clawBaseBones.Length;
        clawBaseHinges = new Vector3[n];
        clawMidHinges = new Vector3[n];
        clawTipHinges = new Vector3[n];
        clawRestOpenAngles = new float[n];

        for (int i = 0; i < n; i++)
        {
            Quaternion baseRot = clawBaseRestRots[i];
            Quaternion midRot = clawMidRestRots[i];
            Quaternion tipRot = clawTipRestRots[i];

            Vector3 restDir = baseRot * Vector3.up;                // Dirección del dedo en el cabezal.
            Vector3 hinge = RadialHinge(restDir);
            clawRestOpenAngles[i] = Vector3.Angle(Vector3.up, restDir);

            clawBaseHinges[i] = (Quaternion.Inverse(baseRot) * hinge).normalized;
            clawMidHinges[i] = (Quaternion.Inverse(baseRot * midRot) * hinge).normalized;
            clawTipHinges[i] = (Quaternion.Inverse(baseRot * midRot * tipRot) * hinge).normalized;
        }

        flapHinges = new Vector3[nozzleFlapBones.Length];
        for (int i = 0; i < nozzleFlapBones.Length; i++)
        {
            Quaternion flapRot = flapRestRots[i];
            flapHinges[i] = (Quaternion.Inverse(flapRot) * RadialHinge(flapRot * Vector3.up)).normalized;
        }
    }

    private static Vector3 RadialHinge(Vector3 restDir)
    {
        Vector3 radialOut = new Vector3(restDir.x, 0f, restDir.z);
        if (radialOut.sqrMagnitude < 0.0001f) radialOut = Vector3.right;
        return Vector3.Cross(Vector3.up, radialOut.normalized).normalized;
    }

    /// <summary>
    /// Registra las piezas inflables: la recámara (Cube), los dos tanques del tubo (Cylinder y
    /// Cylinder.001) y los dos anillos de la boquilla (Cylinder.002 y Cylinder.005). Se escalan las
    /// mallas y no los huesos: escalar un hueso desplazaría su malla hija, cuyo pivote no coincide con
    /// el del hueso, y provocaría que la pieza "saltara" fuera de su sitio.
    /// </summary>
    private void SetupInflatables()
    {
        inflatables = new InflatablePart[5];
        inflatableCount = 0;

        Vector3 headWorld = boneForearm != null ? boneForearm.position : robotRoot.position;
        Transform chamber = FindDeepChild(robotRoot, "Cube");
        Vector3 chamberCenter = chamber != null ? MeshCenterWorld(chamber) : robotRoot.position;
        // Dirección del tubo en el mundo: desde la recámara hacia el cabezal.
        Vector3 tubeDir = (headWorld - chamberCenter).sqrMagnitude > 1e-8f ? (headWorld - chamberCenter).normalized : transform.forward;
        float tubeLength = Mathf.Max(1e-4f, Vector3.Distance(headWorld, chamberCenter));

        AddInflatable(chamber, chamberRadialGrowth, chamberLengthGrowth, true, tubeDir, headWorld, tubeLength);
        AddInflatable(FindDeepChild(robotRoot, "Cylinder"), tankRadialGrowth, 0f, false, tubeDir, headWorld, tubeLength);
        AddInflatable(FindDeepChild(robotRoot, "Cylinder.001"), tankRadialGrowth * 0.85f, 0f, false, tubeDir, headWorld, tubeLength);
        AddInflatable(FindDeepChild(robotRoot, "Cylinder.002"), ringRadialGrowth, 0f, false, tubeDir, headWorld, tubeLength);
        AddInflatable(FindDeepChild(robotRoot, "Cylinder.005"), ringRadialGrowth, 0f, false, tubeDir, headWorld, tubeLength);
    }

    private void AddInflatable(Transform t, float radial, float length, bool isChamber, Vector3 tubeDirWorld, Vector3 headWorld, float tubeLength)
    {
        if (t == null) return;
        MeshFilter mf = t.GetComponent<MeshFilter>();
        Vector3 center = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.center : Vector3.zero;

        // Se elige como eje longitudinal el eje local de la malla más alineado con el tubo.
        Vector3 local = t.InverseTransformDirection(tubeDirWorld);
        float ax = Mathf.Abs(local.x), ay = Mathf.Abs(local.y), az = Mathf.Abs(local.z);
        int axis = ax >= ay && ax >= az ? 0 : (ay >= az ? 1 : 2);

        // Punto fijo del escalado: el centro de la malla, desplazado hacia la cara superior (vista del
        // jugador) en la recámara. Se usa InverseTransformVector porque la jerarquía tiene escalas
        // negativas y el signo del eje "arriba" en espacio de malla debe tenerlas en cuenta.
        Vector3 anchor = center;
        if (isChamber && mf != null && mf.sharedMesh != null)
        {
            Vector3 upLocal = t.InverseTransformVector(transform.up);
            float ux = Mathf.Abs(upLocal.x), uy = Mathf.Abs(upLocal.y), uz = Mathf.Abs(upLocal.z);
            int upAxis = ux >= uy && ux >= uz ? 0 : (uy >= uz ? 1 : 2);
            if (upAxis != axis)
            {
                Vector3 extents = mf.sharedMesh.bounds.extents;
                anchor[upAxis] += Mathf.Sign(upLocal[upAxis]) * extents[upAxis] * chamberTopAnchor;
            }
        }

        // Retardo de la onda: 0 en el cabezal y 1 en la recámara, según la distancia a lo largo del tubo.
        float delay = Mathf.Clamp01(Vector3.Distance(t.TransformPoint(center), headWorld) / tubeLength);

        inflatables[inflatableCount++] = new InflatablePart
        {
            transform = t,
            restPosition = t.localPosition,
            restScale = t.localScale,
            meshCenter = center,
            anchor = anchor,
            lengthAxis = axis,
            radialGrowth = radial,
            lengthGrowth = length,
            gulpDelay = delay,
            isChamber = isChamber
        };
    }

    private static Vector3 MeshCenterWorld(Transform t)
    {
        MeshFilter mf = t.GetComponent<MeshFilter>();
        return mf != null && mf.sharedMesh != null ? t.TransformPoint(mf.sharedMesh.bounds.center) : t.position;
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            InitializeReferences();
            if (!initialized) return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        bool vacuumActive = playerController == null || playerController.ActiveTool == ToolId.Vacuum;
        suctioning = vacuumActive && vacuum != null && vacuum.IsSuctioning;

        UpdateModeBlend(dt);
        UpdateFillState(dt);
        // Las garras se abren al aspirar (para dejar entrar los objetos) y también durante un instante
        // al disparar (para dejarlos salir). Al disparar se abren con una velocidad mayor y se cierran
        // después con la velocidad normal, de modo que el gesto se lee como un "escupir" rápido.
        bool ejecting = ejectHoldTime > 0f;
        if (ejecting) ejectHoldTime -= dt;
        float openTarget = suctioning || ejecting ? 1f : 0f;
        float openSpeed = ejecting && suctionOpen < 1f ? ejectClawSpeed : suctionClawSpeed;
        suctionOpen = Mathf.MoveTowards(suctionOpen, openTarget, dt * openSpeed);
        UpdateHook(dt);

        ApplyPose(Time.time);
        ApplyHookPlacement();
        UpdateSuctionWind(dt);
    }

    /// <summary>Avanza linealmente la mezcla de modo hacia la herramienta activa.</summary>
    private void UpdateModeBlend(float dt)
    {
        if (playerController == null) return;
        float target = playerController.ActiveTool == ToolId.Grapple ? 1f : 0f;
        grappleModeBlend = Mathf.MoveTowards(grappleModeBlend, target, dt * modeTransitionSpeed);
    }

    /// <summary>
    /// Actualiza la ocupación visual con un resorte amortiguado hacia la ocupación real del almacén.
    /// </summary>
    /// <remarks>
    /// Se integra el oscilador x'' = ω²(objetivo − x) − 2ζω·x', con ω = 2π·f (frecuencia natural) y ζ el
    /// amortiguamiento. Con ζ &lt; 1 la recámara sobrepasa ligeramente su tamaño final y se asienta, lo
    /// que transmite la sensación de un material elástico. Se usa Euler semi-implícito (primero la
    /// velocidad, luego la posición) por ser estable con pasos de frame variables.
    /// </remarks>
    private void UpdateFillState(float dt)
    {
        int stored = vacuum != null ? vacuum.StoredCount : 0;
        int capacity = vacuum != null ? Mathf.Max(1, vacuum.Capacity) : 1;

        if (stored > lastStoredCount) gulpTime = 0f;          // Se tragó un objeto: inicia la onda.
        else if (stored < lastStoredCount) deflateTime = 0f;  // Se disparó un objeto: rebote.
        lastStoredCount = stored;

        float target = Mathf.Clamp01((float)stored / capacity);
        float omega = 2f * Mathf.PI * inflateFrequency;
        float h = Mathf.Min(dt, 1f / 30f);  // Se limita el paso para evitar inestabilidad en picos.
        fillVelocity += (omega * omega * (target - fillValue) - 2f * inflateDamping * omega * fillVelocity) * h;
        fillValue += fillVelocity * h;

        if (gulpTime >= 0f) { gulpTime += dt; if (gulpTime > gulpDuration * 1.6f) gulpTime = -1f; }
        if (deflateTime >= 0f) { deflateTime += dt; if (deflateTime > 0.3f) deflateTime = -1f; }
    }

    private void OnEnable()
    {
        EventBus<GrappleFired>.Subscribe(OnGrappleFired);
        EventBus<GrappleReleased>.Subscribe(OnGrappleReleased);
        EventBus<ToolCooldownChanged>.Subscribe(OnToolCooldownChanged);
        EventBus<ObjectLaunched>.Subscribe(OnObjectLaunched);
    }

    private void OnDisable()
    {
        EventBus<GrappleFired>.Unsubscribe(OnGrappleFired);
        EventBus<GrappleReleased>.Unsubscribe(OnGrappleReleased);
        EventBus<ToolCooldownChanged>.Unsubscribe(OnToolCooldownChanged);
        EventBus<ObjectLaunched>.Unsubscribe(OnObjectLaunched);
    }

    /// <summary>
    /// Al disparar un objeto con la aspiradora las garras se abren durante <see cref="ejectOpenTime"/>
    /// para dejarlo pasar, igual que se abren al aspirar para dejarlo entrar.
    /// </summary>
    private void OnObjectLaunched(in ObjectLaunched evt) => ejectHoldTime = ejectOpenTime;

    /// <summary>
    /// Al disparar, el cabezal vuela hacia el punto apuntado. El tiempo de vuelo es t = d / v,
    /// limitado a [0,06 s; 0,2 s]: suficiente para leerse, pero sin retrasar la respuesta del tirón
    /// (la tracción física empieza en el mismo frame del disparo).
    /// </summary>
    private void OnGrappleFired(in GrappleFired evt)
    {
        if (grapple == null || boneForearm == null) return;
        Vector3 start = hookPhase == HookPhase.Docked ? boneForearm.position : hookWorldPos;
        hookWorldPos = start;
        releasePending = false;

        if (evt.Hit)
        {
            hookTarget = evt.Target;
            Vector3 toTarget = hookTarget - start;
            hookNormal = toTarget.sqrMagnitude > 1e-6f ? -toTarget.normalized : -transform.forward;
            hookPhase = HookPhase.Outbound;
            hookPhaseDuration = Mathf.Clamp(toTarget.magnitude / Mathf.Max(1f, hookFlightSpeed), 0.06f, 0.2f);
        }
        else
        {
            Vector3 dir = evt.Target - evt.Origin;
            float dist = Mathf.Min(dir.magnitude, missReachDistance);
            hookTarget = start + (dir.sqrMagnitude > 1e-6f ? dir.normalized : transform.forward) * dist;
            hookPhase = HookPhase.Miss;
            hookPhaseDuration = missDuration;
        }
        hookTime = 0f;
    }

    /// <summary>
    /// Al soltar, el cabezal se zafa y regresa. La duración de la retracción es exactamente el
    /// enfriamiento del gancho, de modo que el arma queda lista justo cuando vuelve a acoplarse.
    /// </summary>
    private void OnGrappleReleased(in GrappleReleased evt)
    {
        if (hookPhase == HookPhase.Outbound)
        {
            releasePending = true;   // Se completará al llegar, para no cortar el vuelo.
            return;
        }
        if (hookPhase == HookPhase.Attached) BeginRelease();
    }

    /// <summary>Si el enfriamiento se cancela (reaparición), el cabezal se acopla de inmediato.</summary>
    private void OnToolCooldownChanged(in ToolCooldownChanged evt)
    {
        if (evt.Remaining <= 0f && hookPhase == HookPhase.Releasing && hookTime < hookPhaseDuration * 0.9f)
            hookPhase = HookPhase.Docked;
    }

    private void BeginRelease()
    {
        hookPhase = HookPhase.Releasing;
        hookTime = 0f;
        hookDetachPoint = hookWorldPos;
        hookPhaseDuration = grapple != null ? Mathf.Max(0.3f, grapple.CooldownDuration) : 1f;
    }

    /// <summary>Avanza la máquina de estados del cabezal y las mezclas de las garras.</summary>
    private void UpdateHook(float dt)
    {
        hookTime += dt;
        float targetGrip = 0f;
        float targetFlare = 0f;

        switch (hookPhase)
        {
            case HookPhase.Outbound:
                targetFlare = 1f;   // Garras muy abiertas durante el vuelo, listas para morder.
                if (grapple != null && grapple.IsGrappling) hookTarget = grapple.AnchorPoint;
                if (hookTime >= hookPhaseDuration)
                {
                    hookPhase = HookPhase.Attached;
                    hookTime = 0f;
                    if (releasePending) BeginRelease();
                }
                break;

            case HookPhase.Attached:
                targetGrip = 1f;
                if (grapple != null && grapple.IsGrappling) hookTarget = grapple.AnchorPoint;
                else if (grapple != null) BeginRelease();
                break;

            case HookPhase.Releasing:
            {
                float u = hookTime / hookPhaseDuration;
                // Zafarse: las garras se abren de golpe y luego quedan entreabiertas en el regreso.
                targetFlare = u < unhookFraction ? 1f : 0.5f;
                targetGrip = 0f;
                if (u >= 1f) hookPhase = HookPhase.Docked;
                break;
            }

            case HookPhase.Miss:
                targetFlare = 1f;
                if (hookTime >= hookPhaseDuration) hookPhase = HookPhase.Docked;
                break;
        }

        // Cierre del agarre rápido (constante de tiempo gripCloseTime) y apertura algo más lenta.
        gripBlend = Mathf.MoveTowards(gripBlend, targetGrip, dt / Mathf.Max(0.01f, gripCloseTime));
        flareBlend = Mathf.MoveTowards(flareBlend, targetFlare, dt * 8f);
    }

    /// <summary>
    /// Fuerza el cabezal fuera del arma en una fase concreta (herramientas de editor).
    /// </summary>
    /// <param name="target">Punto de anclaje en el mundo.</param>
    /// <param name="grip">Cierre de agarre (0-1).</param>
    /// <param name="flare">Apertura extra (0-1).</param>
    public void PreviewHook(Vector3 target, float grip, float flare)
    {
        InitializeReferences();
        if (!initialized) return;
        hookPhase = HookPhase.Attached;
        hookTarget = target;
        hookNormal = (boneForearm.position - target).normalized;
        hookTime = 1f;
        gripBlend = grip;
        flareBlend = flare;
        ApplyPose(0f);
        ApplyHookPlacement();
        hookPhase = HookPhase.Docked;
    }

    /// <summary>
    /// Fuerza una pose concreta del modelo. Se usa desde herramientas de editor para revisar las
    /// poses sin entrar en juego.
    /// </summary>
    /// <param name="modeBlend">Mezcla de modo (0 = Aspiradora, 1 = Gancho).</param>
    /// <param name="fill">Ocupación del almacén (0-1).</param>
    /// <param name="anchorClamp">Cierre de agarre sobre el ancla (0-1).</param>
    /// <param name="gulpPhase">Fase normalizada de la onda al tragar (0-1); negativo = sin onda.</param>
    public void PreviewPose(float modeBlend, float fill, float anchorClamp, float gulpPhase)
    {
        InitializeReferences();
        if (!initialized) return;
        grappleModeBlend = modeBlend;
        fillValue = fill;
        fillVelocity = 0f;
        gulpTime = gulpPhase < 0f ? -1f : gulpPhase * gulpDuration;
        deflateTime = -1f;
        suctioning = false;
        ejectHoldTime = 0f;
        suctionOpen = 0f;
        gripBlend = anchorClamp;
        flareBlend = 0f;
        ApplyPose(0f);
    }

    /// <summary>Igual que <see cref="PreviewPose"/>, con la apertura por aspiración indicada.</summary>
    public void PreviewSuction(float suction)
    {
        InitializeReferences();
        if (!initialized) return;
        grappleModeBlend = 0f;
        suctionOpen = suction;
        gripBlend = 0f;
        flareBlend = 0f;
        suctioning = suction > 0.5f;
        ApplyPose(0f);
    }

    /// <summary>Aplica la pose completa a partir del estado actual.</summary>
    private void ApplyPose(float time)
    {
        if (robotRoot != null)
        {
            // Vibración muy leve al aspirar, expresada en el espacio del padre del modelo.
            Vector3 shake = Vector3.zero;
            if (suctioning)
            {
                shake = transform.InverseTransformVector(
                    transform.right * (Mathf.Sin(time * 55f) * 0.0012f) +
                    transform.up * (Mathf.Cos(time * 63f) * 0.0012f));
            }
            robotRoot.localPosition = robotRestLocalPos + shake;
            robotRoot.localRotation = robotRestLocalRot;
            robotRoot.localScale = robotRestLocalScale;
        }

        ApplyHead();
        ApplyClaws();
        ApplyFlaps();
        ApplyInflation(time);
    }

    /// <summary>
    /// Restaura el cabezal y añade un giro breve sobre el eje del tubo durante la transición de modo:
    /// θ = sin(π·b)·θmax, que vale 0 en ambos modos y es máximo a mitad de la transición.
    /// </summary>
    private void ApplyHead()
    {
        if (boneForearm == null) return;
        float twist = Mathf.Sin(grappleModeBlend * Mathf.PI) * transitionTwistDegrees;
        boneForearm.localPosition = forearmRestLocalPos;
        boneForearm.localRotation = forearmRestLocalRot * Quaternion.AngleAxis(twist, Vector3.up);
        boneForearm.localScale = forearmRestLocalScale;
    }

    /// <summary>
    /// Calcula la pose de las tres garras combinando cuatro señales:
    /// <list type="bullet">
    /// <item>la mezcla de modo (cerradas en Aspiradora, abiertas en Gancho), con desfase entre base,
    /// falange media y punta para que el gesto se lea como una mano que se abre;</item>
    /// <item>la aspiración: al aspirar se abren para dejar pasar los objetos hacia la boquilla;</item>
    /// <item>la apertura extra durante el vuelo del gancho y al zafarse;</item>
    /// <item>el agarre: al llegar a la superficie se cierran y las puntas se curvan para morderla.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// La apertura efectiva es o = max(o_modo, o_aspiración); el ángulo de cada falange se interpola
    /// entre la pose cerrada y la abierta con o, y después se suman la apertura extra (θ_flare·f) y el
    /// cierre de agarre (−θ_grip·g). Usar el máximo evita que dos causas de apertura se sumen y
    /// abran los dedos más allá de lo plausible.
    /// </remarks>
    private void ApplyClaws()
    {
        float b = grappleModeBlend;
        float suction = Mathf.SmoothStep(0f, 1f, suctionOpen) * suctionClawOpen;
        float baseOpen = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(b / 0.8f)), suction);
        float midOpen = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((b - 0.1f) / 0.8f)), suction);
        float tipOpen = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((b - 0.2f) / 0.8f)), suction);

        float grip = Mathf.SmoothStep(0f, 1f, gripBlend);
        float flare = Mathf.SmoothStep(0f, 1f, flareBlend);

        for (int i = 0; i < clawBaseBones.Length; i++)
        {
            float closedBase = -clawRestOpenAngles[i] * vacuumCloseFraction;
            float baseAngle = Mathf.Lerp(closedBase, grappleOpenExtraDegrees, baseOpen) + 14f * flare - 16f * grip;
            float midAngle = Mathf.Lerp(-vacuumMidCurlDegrees, 0f, midOpen) + 6f * flare - 12f * grip;
            float tipAngle = Mathf.Lerp(-vacuumTipCurlDegrees, 0f, tipOpen) + 8f * flare - 26f * grip;

            if (clawBaseBones[i] != null)
                clawBaseBones[i].localRotation = clawBaseRestRots[i] * Quaternion.AngleAxis(baseAngle, clawBaseHinges[i]);
            if (clawMidBones[i] != null)
                clawMidBones[i].localRotation = clawMidRestRots[i] * Quaternion.AngleAxis(midAngle, clawMidHinges[i]);
            if (clawTipBones[i] != null)
                clawTipBones[i].localRotation = clawTipRestRots[i] * Quaternion.AngleAxis(tipAngle, clawTipHinges[i]);
        }
    }

    /// <summary>Las aletas de la boquilla permanecen en reposo y se abren ligeramente mientras se aspira.</summary>
    private void ApplyFlaps()
    {
        float vacuumWeight = 1f - Mathf.SmoothStep(0f, 1f, grappleModeBlend);
        float angle = 14f * Mathf.SmoothStep(0f, 1f, suctionOpen) * vacuumWeight;
        for (int i = 0; i < nozzleFlapBones.Length; i++)
        {
            if (nozzleFlapBones[i] != null)
                nozzleFlapBones[i].localRotation = flapRestRots[i] * Quaternion.AngleAxis(angle, flapHinges[i]);
        }
    }

    /// <summary>
    /// Infla cada pieza alrededor del centro de su malla.
    /// </summary>
    /// <remarks>
    /// Para un vértice p de la malla, su posición en el padre es P = pos + R·(S ⊙ p). Para que un punto
    /// ancla a de la malla quede fijo al cambiar la escala de S a S', se despeja la nueva posición:
    /// pos' = pos + R·((S − S') ⊙ a). El ancla es el centro de la malla (en la recámara, desplazado hacia
    /// su cara superior), así la pieza crece en su sitio y el arma nunca se desplaza ni se levanta.
    /// La escala combina: ocupación (resorte), onda de tragado (gaussiana que avanza de la boquilla a la
    /// recámara, g(t) = A·exp(−((t − d·T)/σ)²)), rebote al disparar y vibración de presión.
    /// </remarks>
    private void ApplyInflation(float time)
    {
        float fill = Mathf.Max(0f, fillValue);
        float deflate = deflateTime >= 0f ? -Mathf.Sin(Mathf.Clamp01(deflateTime / 0.3f) * Mathf.PI) * deflateAmplitude : 0f;
        float wobble = Mathf.Sin(time * (9f + 10f * fill)) * pressureWobbleAmplitude * Mathf.Clamp01(fill);
        float travel = gulpDuration * 0.75f;
        float sigma = gulpDuration * 0.22f;

        for (int i = 0; i < inflatableCount; i++)
        {
            ref InflatablePart part = ref inflatables[i];
            if (part.transform == null) continue;

            float gulp = 0f;
            if (gulpTime >= 0f)
            {
                float x = (gulpTime - part.gulpDelay * travel) / sigma;
                gulp = gulpAmplitude * Mathf.Exp(-x * x);
            }

            float radial = 1f + part.radialGrowth * fill + gulp + (part.isChamber ? wobble + deflate : deflate * 0.5f);
            float length = 1f + part.lengthGrowth * fill + (part.isChamber ? gulp * 0.3f : 0f);

            Vector3 factors = new Vector3(radial, radial, radial);
            factors[part.lengthAxis] = length;
            Vector3 newScale = Vector3.Scale(part.restScale, factors);

            part.transform.localScale = newScale;
            part.transform.localPosition = part.restPosition +
                part.transform.localRotation * Vector3.Scale(part.restScale - newScale, part.anchor);
        }
    }

    /// <summary>
    /// Coloca el cabezal fuera del arma según la fase del gancho y dibuja el tubo que lo une al cuerpo.
    /// Se ejecuta después de la pose base para sobrescribir la posición del cabezal.
    /// </summary>
    /// <remarks>
    /// El cabezal se orienta con la rotación mínima que lleva su eje Y (el eje del tubo) a la dirección
    /// cuerpo → cabezal: q = FromToRotation(ŷ_actual, d̂)·q_actual. Para que las garras muerdan la
    /// superficie y no la atraviesen, el pivote se detiene a la distancia "clawReach" antes del ancla.
    /// En la retracción se usa una interpolación suave s(x) = x²(3 − 2x) entre el punto donde se zafó
    /// y la boquilla, que se mueve con el jugador, así que el cabezal siempre vuelve a su sitio.
    /// </remarks>
    private void ApplyHookPlacement()
    {
        if (boneForearm == null) return;
        Vector3 basePos = boneForearm.position;

        if (hookPhase == HookPhase.Docked)
        {
            hookWorldPos = basePos;
            if (vacuumTubeRenderer != null && vacuumTubeRenderer.enabled) vacuumTubeRenderer.enabled = false;
            return;
        }

        Vector3 bite = hookTarget + hookNormal * clawReach;   // Pivote del cabezal con las garras sobre el ancla.
        float tension = 1f;
        switch (hookPhase)
        {
            case HookPhase.Outbound:
            {
                float u = Mathf.Clamp01(hookTime / Mathf.Max(0.001f, hookPhaseDuration));
                hookWorldPos = Vector3.Lerp(basePos, bite, 1f - (1f - u) * (1f - u));   // Sale rápido y frena.
                tension = 0.5f + 0.5f * u;
                break;
            }
            case HookPhase.Attached:
            {
                // Pequeño golpe al morder: entra 3 cm en la superficie y se asienta.
                float jolt = Mathf.Exp(-hookTime * 25f) * 0.03f;
                hookWorldPos = bite - hookNormal * jolt;
                hookDetachPoint = hookWorldPos;
                break;
            }
            case HookPhase.Releasing:
            {
                float u = Mathf.Clamp01(hookTime / hookPhaseDuration);
                float travelEnd = 1f - dockFraction;
                if (u < unhookFraction)
                {
                    // Zafarse: tirón hacia atrás con un leve forcejeo lateral antes de soltarse.
                    float k = u / unhookFraction;
                    float wiggle = Mathf.Sin(k * Mathf.PI * 3f) * (1f - k) * 0.04f;
                    Vector3 side = Vector3.Cross(hookNormal, Vector3.up);
                    if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
                    hookWorldPos = hookDetachPoint + hookNormal * (unhookPushDistance * Mathf.SmoothStep(0f, 1f, k)) + side.normalized * wiggle;
                }
                else if (u < travelEnd)
                {
                    float k = (u - unhookFraction) / Mathf.Max(0.001f, travelEnd - unhookFraction);
                    Vector3 from = hookDetachPoint + hookNormal * unhookPushDistance;
                    hookWorldPos = Vector3.Lerp(from, basePos, k * k * (3f - 2f * k));
                    tension = 1f - 0.6f * Mathf.Sin(k * Mathf.PI);   // El tubo se afloja a mitad del regreso.
                }
                else
                {
                    hookWorldPos = basePos;   // Acoplado: las garras recuperan la pose del modo.
                }
                break;
            }
            case HookPhase.Miss:
            {
                float u = Mathf.Clamp01(hookTime / Mathf.Max(0.001f, hookPhaseDuration));
                float reach = u < 0.4f ? 1f - Mathf.Pow(1f - u / 0.4f, 2f) : 1f - Mathf.SmoothStep(0f, 1f, (u - 0.4f) / 0.6f);
                hookWorldPos = Vector3.Lerp(basePos, hookTarget, reach);
                tension = 0.6f;
                break;
            }
        }

        Vector3 delta = hookWorldPos - basePos;
        if (delta.sqrMagnitude < 0.0004f)
        {
            if (vacuumTubeRenderer != null && vacuumTubeRenderer.enabled) vacuumTubeRenderer.enabled = false;
            return;
        }

        Vector3 dir = hookPhase == HookPhase.Attached || hookPhase == HookPhase.Outbound
            ? (hookTarget - basePos).normalized
            : delta.normalized;
        boneForearm.rotation = Quaternion.FromToRotation(boneForearm.up, dir) * boneForearm.rotation;
        boneForearm.position = hookWorldPos;
        DrawDynamicVacuumTube(basePos, hookWorldPos, tension);
    }

    private void SetupNozzleTip()
    {
        GameObject tipObj = new GameObject("NozzleSuctionOrigin");
        tipObj.transform.SetParent(transform, false);
        tipObj.transform.localPosition = new Vector3(0.05f, -0.1f, 0.75f);
        tipObj.transform.localRotation = Quaternion.identity;
        nozzleTipTransform = tipObj.transform;
    }

    private void SetupVacuumTubeRenderer()
    {
        GameObject tubeObj = new GameObject("VacuumGrappleTube");
        tubeObj.transform.SetParent(transform, false);

        vacuumTubeRenderer = tubeObj.AddComponent<LineRenderer>();
        vacuumTubeRenderer.useWorldSpace = true;
        vacuumTubeRenderer.positionCount = TubeSegments;
        vacuumTubeRenderer.numCapVertices = 4;
        vacuumTubeRenderer.numCornerVertices = 4;
        vacuumTubeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        vacuumTubeRenderer.receiveShadows = false;
        vacuumTubeRenderer.textureMode = LineTextureMode.Tile;

        AnimationCurve widthCurve = new AnimationCurve();
        for (int i = 0; i <= 10; i++)
        {
            float t = i / 10f;
            float wave = (i % 2 == 0) ? 0.068f : 0.054f;
            widthCurve.AddKey(t, wave);
        }
        vacuumTubeRenderer.widthCurve = widthCurve;

        Shader tubeShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
        vacuumTubeMaterial = new Material(tubeShader);
        vacuumTubeMaterial.color = new Color(0.18f, 0.22f, 0.28f, 1f);
        if (vacuumTubeMaterial.HasProperty("_BaseColor"))
            vacuumTubeMaterial.SetColor("_BaseColor", new Color(0.18f, 0.22f, 0.28f, 1f));
        if (vacuumTubeMaterial.HasProperty("_Smoothness"))
            vacuumTubeMaterial.SetFloat("_Smoothness", 0.6f);

        vacuumTubeRenderer.sharedMaterial = vacuumTubeMaterial;
        vacuumTubeRenderer.enabled = false;
    }

    /// <summary>
    /// Crea el sistema de viento de absorción con rayas grises estilizadas (partículas estiradas + líneas de corriente grises).
    /// </summary>
    private void SetupSuctionWindEffect()
    {
        // Textura suave degradada para que cada partícula/raya gris tenga puntas afiladas de viento
        windStreakTexture = new Texture2D(64, 16, TextureFormat.RGBA32, false);
        windStreakTexture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 16; y++)
        {
            float ny = Mathf.Abs((y / 15f) * 2f - 1f);
            float radialAlpha = Mathf.SmoothStep(1f, 0f, ny);
            for (int x = 0; x < 64; x++)
            {
                float nx = Mathf.Abs((x / 63f) * 2f - 1f);
                float axialAlpha = Mathf.SmoothStep(1f, 0f, nx);
                float a = radialAlpha * axialAlpha;
                windStreakTexture.SetPixel(x, y, new Color(0.86f, 0.88f, 0.90f, a));
            }
        }
        windStreakTexture.Apply();

        GameObject windObj = new GameObject("VacuumSuctionWindFX");
        windObj.transform.SetParent(nozzleTipTransform, false);
        windObj.transform.localPosition = new Vector3(0f, 0f, 3.3f);
        windObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        suctionWindParticles = windObj.AddComponent<ParticleSystem>();
        suctionWindRenderer = windObj.GetComponent<ParticleSystemRenderer>();

        var main = suctionWindParticles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.26f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(14f, 20f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.085f);
        // Tonos puramente grises / plateados traslúcidos (sin azul)
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.62f, 0.64f, 0.67f, 0.55f),
            new Color(0.85f, 0.87f, 0.90f, 0.82f)
        );
        main.maxParticles = 120;

        var emission = suctionWindParticles.emission;
        emission.rateOverTime = 95f;

        var shape = suctionWindParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 1.15f;

        var velOverLife = suctionWindParticles.velocityOverLifetime;
        velOverLife.enabled = true;
        velOverLife.space = ParticleSystemSimulationSpace.Local;
        velOverLife.orbitalZ = new ParticleSystem.MinMaxCurve(5f, 10f);
        velOverLife.radial = new ParticleSystem.MinMaxCurve(-4.2f, -2.2f);

        var colOverLife = suctionWindParticles.colorOverLifetime;
        colOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.75f, 0.77f, 0.80f), 0f),
                new GradientColorKey(new Color(0.88f, 0.90f, 0.92f), 0.5f),
                new GradientColorKey(new Color(0.68f, 0.70f, 0.73f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.78f, 0.25f),
                new GradientAlphaKey(0.78f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colOverLife.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLife = suctionWindParticles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1.1f),
            new Keyframe(0.6f, 0.85f),
            new Keyframe(1f, 0.12f)
        );
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        suctionWindRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        suctionWindRenderer.velocityScale = 0.11f;
        suctionWindRenderer.lengthScale = 3.8f;
        suctionWindRenderer.shadowCastingMode = ShadowCastingMode.Off;
        suctionWindRenderer.receiveShadows = false;

        Shader spriteShader = Shader.Find("Sprites/Default");
        windParticleMaterial = new Material(spriteShader);
        windParticleMaterial.mainTexture = windStreakTexture;
        suctionWindRenderer.sharedMaterial = windParticleMaterial;

        suctionWindParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 2. Rayas grises continuas de corriente de aire (Streamlines) entrando en la boquilla
        windStreamlineMaterial = new Material(spriteShader);
        windStreamlineMaterial.mainTexture = windStreakTexture;

        windStreamlines = new LineRenderer[WindStreamlineCount];
        AnimationCurve streakWidth = new AnimationCurve(
            new Keyframe(0f, 0.002f),
            new Keyframe(0.3f, 0.022f),
            new Keyframe(0.8f, 0.015f),
            new Keyframe(1f, 0.002f)
        );

        Gradient streakGrad = new Gradient();
        streakGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.72f, 0.74f, 0.77f), 0f),
                new GradientColorKey(new Color(0.88f, 0.90f, 0.92f), 0.5f),
                new GradientColorKey(new Color(0.66f, 0.68f, 0.71f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.62f, 0.35f),
                new GradientAlphaKey(0.72f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        for (int i = 0; i < WindStreamlineCount; i++)
        {
            GameObject slObj = new GameObject($"WindGreyStreak_{i}");
            slObj.transform.SetParent(nozzleTipTransform, false);
            LineRenderer lr = slObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = WindStreamlinePoints;
            lr.widthCurve = streakWidth;
            lr.colorGradient = streakGrad;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = windStreamlineMaterial;
            lr.enabled = false;
            windStreamlines[i] = lr;
        }
    }

    private void DrawDynamicVacuumTube(Vector3 startWorld, Vector3 endWorld, float extension)
    {
        if (vacuumTubeRenderer == null) return;
        vacuumTubeRenderer.enabled = true;

        Vector3 delta = endWorld - startWorld;
        float dist = delta.magnitude;
        Vector3 dir = dist > 0.001f ? delta / dist : transform.forward;
        Vector3 right = Vector3.Cross(dir, Vector3.up);
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(dir, Vector3.right);
        right.Normalize();
        Vector3 up = Vector3.Cross(right, dir).normalized;

        float tensionSlack = (1f - extension) * 0.14f;
        float spiralFreq = Mathf.Clamp(dist * 3.5f, 6f, 40f);

        for (int i = 0; i < TubeSegments; i++)
        {
            float t = i / (float)(TubeSegments - 1);
            Vector3 p = Vector3.Lerp(startWorld, endWorld, t);

            float envelope = Mathf.Sin(t * Mathf.PI);
            float angle = t * spiralFreq - Time.time * 18f;
            Vector3 coilOffset = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * (0.018f + tensionSlack) * envelope;

            vacuumTubeRenderer.SetPosition(i, p + coilOffset);
        }
    }

    private void UpdateSuctionWind(float dt)
    {
        bool shouldPlayWind = playerController != null &&
                              playerController.ActiveTool == ToolId.Vacuum &&
                              vacuum != null &&
                              vacuum.IsSuctioning;

        if (suctionWindParticles != null)
        {
            if (shouldPlayWind)
            {
                if (!suctionWindParticles.isPlaying) suctionWindParticles.Play();
            }
            else
            {
                if (suctionWindParticles.isPlaying)
                {
                    suctionWindParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        if (windStreamlines != null)
        {
            for (int i = 0; i < windStreamlines.Length; i++)
            {
                LineRenderer lr = windStreamlines[i];
                if (lr == null) continue;

                if (!shouldPlayWind)
                {
                    if (lr.enabled) lr.enabled = false;
                    continue;
                }

                lr.enabled = true;
                // Cada línea de corriente gris viaja rápidamente desde adelante (z = 3.2m) hacia la boquilla (z = 0m)
                float cycle = Mathf.Repeat(Time.time * 3.6f + i * (1f / WindStreamlineCount), 1f);
                float baseAngle = i * (Mathf.PI * 2f / WindStreamlineCount) + Time.time * 4.5f;
                float streakLen = 1.15f;
                float headZ = Mathf.Lerp(3.2f, 0.05f, cycle);

                for (int p = 0; p < WindStreamlinePoints; p++)
                {
                    float frac = p / (float)(WindStreamlinePoints - 1);
                    float z = Mathf.Max(0f, headZ - frac * streakLen);
                    float coneNorm = z / 3.2f;
                    float radius = Mathf.Lerp(0.03f, 0.72f, coneNorm);
                    float twist = baseAngle + coneNorm * 2.2f;

                    Vector3 localPt = new Vector3(
                        Mathf.Cos(twist) * radius,
                        Mathf.Sin(twist) * radius,
                        z
                    );
                    lr.SetPosition(p, localPt);
                }
            }
        }
    }

    private static Transform FindDeepChild(Transform parent, string targetName)
    {
        if (parent == null) return null;
        if (parent.name == targetName) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindDeepChild(child, targetName);
            if (found != null) return found;
        }
        return null;
    }

    private void OnDestroy()
    {
        if (vacuumTubeMaterial != null) Destroy(vacuumTubeMaterial);
        if (windParticleMaterial != null) Destroy(windParticleMaterial);
        if (windStreamlineMaterial != null) Destroy(windStreamlineMaterial);
        if (windStreakTexture != null) Destroy(windStreakTexture);
    }
}
