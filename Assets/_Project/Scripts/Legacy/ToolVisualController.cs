using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Tools;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Controla las animaciones procedurales del modelo de la herramienta (robot.fbx).
/// </summary>
/// <remarks>
/// <para>
/// <b>Recámara.</b> Los tanques (Cylinder y Cylinder.001) y los anillos de entrada se inflan en
/// función de la capacidad ocupada (<c>almacenados / capacidad</c>). El inflado se aplica a las
/// mallas y no a los huesos, y se compensa la posición para que cada malla crezca alrededor del
/// centro de sus propios límites: así la recámara se hincha "en su sitio" y el arma no se
/// desplaza. El valor visual sigue al objetivo con un resorte subamortiguado
/// (x'' = k·(objetivo − x) − c·x'), que da el rebote gelatinoso al tragar y al disparar.
/// </para>
/// <para>
/// <b>Garras.</b> Siempre visibles. En modo aspiradora se pliegan hacia el eje (cerradas sobre la
/// boquilla); en modo gancho se abren hacia fuera con las puntas curvadas, listas para agarrar.
/// Al anclar se cierran sobre el punto de agarre. La transición interpola tres poses por
/// articulación, con un pequeño desfase entre base, falange media y punta.
/// </para>
/// <para>
/// Corrección de la Fase 1 (rama experimental): antes el cuerpo "Cube" subía hasta 1,8 m al
/// llenarse (desplazamiento de 8,5 unidades en el espacio escalado del modelo), por eso el arma
/// "se levantaba" al absorber; y el cabezal entero se escondía 7 m detrás de la cámara en modo
/// aspiradora, por eso las garras desaparecían.
/// </para>
/// </remarks>
public class ToolVisualController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private ToolController playerController;
    [SerializeField] private VacuumTool vacuum;
    [SerializeField] private GrappleTool grapple;
    [SerializeField] private Transform robotRoot;

    [Header("Velocidades de Animación")]
    [SerializeField] private float modeTransitionSpeed = 3.5f;
    [SerializeField] private float clawLaunchTime = 0.055f;
    [SerializeField] private float clawRetractSpeed = 28f;

    [Header("Recámara (inflado según la capacidad ocupada)")]
    [Tooltip("Escala radial de los tanques con la aspiradora llena.")]
    [SerializeField, Range(1f, 2.5f)] private float chamberMaxRadial = 1.75f;
    [Tooltip("Escala axial de los tanques con la aspiradora llena.")]
    [SerializeField, Range(1f, 1.5f)] private float chamberMaxAxial = 1.15f;
    [Tooltip("Escala radial de los anillos de entrada con la aspiradora llena.")]
    [SerializeField, Range(1f, 1.6f)] private float ringMaxRadial = 1.3f;
    [Tooltip("Escala del cuerpo (vientre del arma) con la aspiradora llena; crece alrededor de su centro.")]
    [SerializeField, Range(1f, 1.5f)] private float bodyMaxRadial = 1.18f;
    [Tooltip("Exponente de la curva de llenado (< 1: el primer objeto ya se nota).")]
    [SerializeField, Range(0.3f, 1f)] private float fillCurveExponent = 0.6f;
    [Tooltip("Rigidez del resorte del inflado (k).")]
    [SerializeField, Min(1f)] private float inflateStiffness = 90f;
    [Tooltip("Amortiguamiento del resorte del inflado (c). ζ = c / (2·√k) < 1 produce rebote.")]
    [SerializeField, Min(0f)] private float inflateDamping = 10f;
    [Tooltip("Impulso de velocidad del resorte al tragar un objeto.")]
    [SerializeField] private float swallowImpulse = 2.2f;
    [Tooltip("Impulso (negativo) del resorte al disparar un objeto.")]
    [SerializeField] private float launchImpulse = -2.6f;
    [Tooltip("Amplitud de la onda de tragado que recorre anillos y tanques.")]
    [SerializeField, Range(0f, 0.4f)] private float swallowWaveAmplitude = 0.14f;

    [Header("Garras (grados sobre la bisagra radial; + abre hacia fuera, − cierra hacia el eje)")]
    [SerializeField] private Vector3 clawClosedAngles = new Vector3(-16f, -22f, -34f);
    [SerializeField] private Vector3 clawOpenAngles = new Vector3(40f, -8f, -32f);
    [SerializeField] private Vector3 clawGripAngles = new Vector3(-4f, -20f, -38f);

    // Nodos del rig de robot.fbx
    private Transform boneRoot;         // Bone
    private Transform boneArm;          // Bone.001
    private Transform boneForearm;      // Bone.002 (cabezal de garras y aletas)
    private Transform boneCompartmentA; // Bone.003 (Cylinder - tanque principal trasero)
    private Transform boneCompartmentB; // Bone.004 (Cylinder.001 - tanque secundario delantero)

    // Meshes de anillos y cuerpo en la raíz del modelo
    private Transform tubeRingA;        // Cylinder.002
    private Transform tubeRingB;        // Cylinder.005
    private Transform mainCube;         // Cube (cámara posterior)

    // Cadena de garras: 3 dedos articulados (Bone.009->014->015, Bone.010->012->013, Bone.011->016->017)
    private Transform[] clawBaseBones;
    private Transform[] clawMidBones;
    private Transform[] clawTipBones;
    private Transform[] nozzleFlapBones; // Bone.006, Bone.007, Bone.008

    // Poses originales capturadas al iniciar
    private Vector3 robotDefaultLocalPos;
    private Quaternion robotDefaultLocalRot;
    private Vector3 robotDefaultLocalScale;

    private Vector3 forearmDefaultLocalPos;
    private Quaternion forearmDefaultLocalRot;
    private Vector3 forearmDefaultLocalScale;

    /// <summary>Pose de reposo de una malla inflable y el centro de sus límites (espacio de malla).</summary>
    private struct InflatableMesh
    {
        public Transform transform;
        public Vector3 defaultPosition;
        public Vector3 defaultScale;
        public Quaternion defaultRotation;
        public Vector3 boundsCenter;
    }

    // Mallas de la recámara: tanque trasero, tanque delantero, anillo trasero y anillo de entrada.
    private InflatableMesh tankRear;
    private InflatableMesh tankFront;
    private InflatableMesh ringRear;
    private InflatableMesh ringFront;
    private InflatableMesh body;

    private Quaternion[] clawBaseDefaultRots;
    private Quaternion[] clawMidDefaultRots;
    private Quaternion[] clawTipDefaultRots;
    private Quaternion[] flapDefaultRots;

    // Ejes de bisagra radial para plegado simétrico de las 3 garras a 120°
    private Vector3[] baseRadialHingeAxes;
    private Vector3[] midRadialHingeAxes;
    private Vector3[] tipRadialHingeAxes;
    private Vector3[] flapRadialHingeAxes;

    // Estado continuo de transición de modo (0 = Aspiradora, 1 = Gancho)
    private float grappleModeBlend = 0f;

    // Estado del disparo físico del gancho y tubo elástico
    private float grappleExtension = 0f;
    private Vector3 currentHookWorldPos;
    private Vector3 lastValidAnchorPos;

    // Estado del inflado: valor del resorte, su velocidad y la onda de tragado (-1 = inactiva).
    private float inflateValue = 0f;
    private float inflateVelocity = 0f;
    private float swallowWaveTimer = -1f;
    private int lastStoredCount = 0;

#if UNITY_EDITOR
    // Vista previa para ajustar la animación en el editor (−1 = desactivada).
    [Header("Vista previa (solo editor)")]
    [Tooltip("Fuerza el llenado visual (0–1) para revisar el inflado sin absorber objetos.")]
    [SerializeField, Range(-1f, 1f)] private float previewFill = -1f;
    [Tooltip("Fuerza la mezcla de modo (0 = aspiradora, 1 = gancho).")]
    [SerializeField, Range(-1f, 1f)] private float previewModeBlend = -1f;
#endif

    // Tubo de aspiradora extensible al lanzar el gancho
    private LineRenderer vacuumTubeRenderer;
    private Material vacuumTubeMaterial;
    private const int TubeSegments = 20;

    // Efecto de viento al aspirar: partículas de rayas grises + líneas aerodinámicas grises en espiral
    private ParticleSystem suctionWindParticles;
    private ParticleSystemRenderer suctionWindRenderer;
    private Material windParticleMaterial;
    private Texture2D windStreakTexture;
    private Transform nozzleTipTransform;

    private const int WindStreamlineCount = 6;
    private const int WindStreamlinePoints = 14;
    private LineRenderer[] windStreamlines;
    private Material windStreamlineMaterial;

    private bool initialized = false;

    private void Awake()
    {
        InitializeReferences();
    }

    private void Start()
    {
        if (!initialized) InitializeReferences();
    }

    private void InitializeReferences()
    {
        if (initialized) return;

        if (playerController == null) playerController = GetComponentInParent<ToolController>();
        if (vacuum == null) vacuum = GetComponent<VacuumTool>() ?? GetComponentInChildren<VacuumTool>(true);
        if (grapple == null) grapple = GetComponent<GrappleTool>() ?? GetComponentInChildren<GrappleTool>(true);

        if (robotRoot == null)
        {
            Transform found = transform.Find("robot");
            if (found == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.ToLower().Contains("robot"))
                    {
                        found = child;
                        break;
                    }
                }
            }
            robotRoot = found;
        }

        if (robotRoot == null) return;

        robotDefaultLocalPos = robotRoot.localPosition;
        robotDefaultLocalRot = robotRoot.localRotation;
        robotDefaultLocalScale = robotRoot.localScale;

        boneRoot = FindDeepChild(robotRoot, "Bone");
        boneArm = FindDeepChild(robotRoot, "Bone.001");
        boneForearm = FindDeepChild(robotRoot, "Bone.002");
        boneCompartmentA = FindDeepChild(robotRoot, "Bone.003");
        boneCompartmentB = FindDeepChild(robotRoot, "Bone.004");

        tubeRingA = FindDeepChild(robotRoot, "Cylinder.002");
        tubeRingB = FindDeepChild(robotRoot, "Cylinder.005");
        mainCube = FindDeepChild(robotRoot, "Cube");

        // Re-emparentar Bone.004 (Cylinder.001 - compartimiento secundario) a Bone.001 para que el tanque
        // permanezca en el cuerpo del arma cuando Bone.002 (el cabezal completo con garras y placas) se lance con el gancho
        if (boneCompartmentB != null && boneArm != null && boneCompartmentB.parent == boneForearm)
        {
            boneCompartmentB.SetParent(boneArm, true);
        }

        clawBaseBones = new Transform[]
        {
            FindDeepChild(robotRoot, "Bone.009"),
            FindDeepChild(robotRoot, "Bone.010"),
            FindDeepChild(robotRoot, "Bone.011")
        };

        clawMidBones = new Transform[]
        {
            FindDeepChild(robotRoot, "Bone.014"),
            FindDeepChild(robotRoot, "Bone.012"),
            FindDeepChild(robotRoot, "Bone.016")
        };

        clawTipBones = new Transform[]
        {
            FindDeepChild(robotRoot, "Bone.015"),
            FindDeepChild(robotRoot, "Bone.013"),
            FindDeepChild(robotRoot, "Bone.017")
        };

        nozzleFlapBones = new Transform[]
        {
            FindDeepChild(robotRoot, "Bone.006"),
            FindDeepChild(robotRoot, "Bone.007"),
            FindDeepChild(robotRoot, "Bone.008")
        };

        if (boneForearm != null)
        {
            forearmDefaultLocalPos = boneForearm.localPosition;
            forearmDefaultLocalRot = boneForearm.localRotation;
            forearmDefaultLocalScale = boneForearm.localScale;
        }

        tankRear = CaptureInflatable(FindDeepChild(robotRoot, "Cylinder"));
        tankFront = CaptureInflatable(FindDeepChild(robotRoot, "Cylinder.001"));
        ringRear = CaptureInflatable(tubeRingA);
        ringFront = CaptureInflatable(tubeRingB);
        body = CaptureInflatable(mainCube);

        clawBaseDefaultRots = CaptureRotations(clawBaseBones);
        clawMidDefaultRots = CaptureRotations(clawMidBones);
        clawTipDefaultRots = CaptureRotations(clawTipBones);
        flapDefaultRots = CaptureRotations(nozzleFlapBones);

        ComputeRadialHingeAxes();

        if (playerController != null)
        {
            grappleModeBlend = playerController.ActiveTool == ToolId.Grapple ? 1f : 0f;
        }

        SetupNozzleTip();
        SetupVacuumTubeRenderer();
        // Si la herramienta ya tiene el vórtice de partículas de la Fase 1 (SuctionVortexFx, con
        // material como asset), no creamos el viento heredado: este generaba en ejecución una
        // textura, dos materiales y seis LineRenderer actualizados cada frame (auditoría QA).
        if (GetComponentInChildren<NostalgIA.Presentation.VFX.SuctionVortexFx>(true) == null)
            SetupSuctionWindEffect();

        initialized = true;
    }

    private void ComputeRadialHingeAxes()
    {
        baseRadialHingeAxes = new Vector3[clawBaseBones.Length];
        midRadialHingeAxes = new Vector3[clawBaseBones.Length];
        tipRadialHingeAxes = new Vector3[clawBaseBones.Length];

        for (int i = 0; i < clawBaseBones.Length; i++)
        {
            Quaternion baseRot = clawBaseDefaultRots[i];
            Quaternion midRot = i < clawMidDefaultRots.Length ? clawMidDefaultRots[i] : Quaternion.identity;
            Quaternion tipRot = i < clawTipDefaultRots.Length ? clawTipDefaultRots[i] : Quaternion.identity;

            Vector3 restDir = baseRot * Vector3.up;
            Vector3 radialOut = new Vector3(restDir.x, 0f, restDir.z);
            if (radialOut.sqrMagnitude < 0.0001f) radialOut = Vector3.right;
            radialOut.Normalize();

            Vector3 hingeInForearm = Vector3.Cross(Vector3.up, radialOut).normalized;
            baseRadialHingeAxes[i] = hingeInForearm;
            // La rotación de cada falange se aplica a la derecha de su rotación de reposo, así que
            // el eje debe expresarse en el marco propio de esa falange: inversa de la cadena acumulada.
            midRadialHingeAxes[i] = (Quaternion.Inverse(baseRot * midRot) * hingeInForearm).normalized;
            tipRadialHingeAxes[i] = (Quaternion.Inverse(baseRot * midRot * tipRot) * hingeInForearm).normalized;
        }

        flapRadialHingeAxes = new Vector3[nozzleFlapBones.Length];
        for (int i = 0; i < nozzleFlapBones.Length; i++)
        {
            Quaternion flapRot = flapDefaultRots[i];
            Vector3 restDir = flapRot * Vector3.up;
            Vector3 radialOut = new Vector3(restDir.x, 0f, restDir.z);
            if (radialOut.sqrMagnitude < 0.0001f) radialOut = Vector3.right;
            radialOut.Normalize();
            flapRadialHingeAxes[i] = Vector3.Cross(Vector3.up, radialOut).normalized;
        }
    }

    private Quaternion[] CaptureRotations(Transform[] bones)
    {
        Quaternion[] rots = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null) rots[i] = bones[i].localRotation;
        }
        return rots;
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

    private void LateUpdate()
    {
        if (!initialized)
        {
            InitializeReferences();
            if (!initialized) return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        UpdateModeTransition(dt);
        UpdateCompartmentInflation(dt);
        UpdateClawsAndGrappleLaunch(dt);
        UpdateSuctionWind(dt);
    }

    private void UpdateModeTransition(float dt)
    {
        if (playerController == null) return;

        ToolId currentTool = playerController.ActiveTool;
        float targetBlend = (currentTool == ToolId.Grapple) ? 1f : 0f;
        grappleModeBlend = Mathf.MoveTowards(grappleModeBlend, targetBlend, dt * modeTransitionSpeed);
#if UNITY_EDITOR
        if (previewModeBlend >= 0f) grappleModeBlend = previewModeBlend;
#endif

        if (robotRoot != null)
        {
            Vector3 suctionShake = Vector3.zero;
            if (vacuum != null && vacuum.IsSuctioning && currentTool == ToolId.Vacuum)
            {
                suctionShake = new Vector3(
                    Mathf.Sin(Time.time * 55f) * 0.006f,
                    Mathf.Cos(Time.time * 63f) * 0.006f,
                    Mathf.Sin(Time.time * 40f) * 0.009f
                );
            }

            robotRoot.localPosition = robotDefaultLocalPos + suctionShake;
            robotRoot.localRotation = robotDefaultLocalRot;
            robotRoot.localScale = robotDefaultLocalScale;
        }
    }

    /// <summary>Guarda la pose de reposo de una malla inflable y el centro de sus límites.</summary>
    private static InflatableMesh CaptureInflatable(Transform t)
    {
        var data = new InflatableMesh { transform = t };
        if (t == null) return data;
        data.defaultPosition = t.localPosition;
        data.defaultScale = t.localScale;
        data.defaultRotation = t.localRotation;
        MeshFilter filter = t.GetComponent<MeshFilter>();
        data.boundsCenter = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.center : Vector3.zero;
        return data;
    }

    /// <summary>
    /// Escala una malla inflable por eje (<paramref name="factors"/>, en el espacio de la malla)
    /// manteniendo fijo el centro de sus límites. Un vértice v se coloca en p + R·(s∘v); para que
    /// el centro c no se mueva al pasar de s₀ a s, la posición se corrige con p = p₀ + R·((s₀ − s)∘c).
    /// </summary>
    private static void ApplyInflation(in InflatableMesh mesh, Vector3 factors)
    {
        if (mesh.transform == null) return;
        Vector3 s0 = mesh.defaultScale;
        Vector3 s = Vector3.Scale(s0, factors);
        Vector3 c = mesh.boundsCenter;
        Vector3 delta = new Vector3((s0.x - s.x) * c.x, (s0.y - s.y) * c.y, (s0.z - s.z) * c.z);
        mesh.transform.localScale = s;
        mesh.transform.localPosition = mesh.defaultPosition + mesh.defaultRotation * delta;
    }

    /// <summary>Pulso gaussiano de la onda de tragado centrado en <paramref name="center"/>.</summary>
    private float WavePulse(float center, float width)
    {
        if (swallowWaveTimer < 0f) return 0f;
        float d = (swallowWaveTimer - center) / width;
        return Mathf.Exp(-d * d) * swallowWaveAmplitude;
    }

    /// <summary>
    /// Infla la recámara según la capacidad ocupada, con resorte y onda de tragado que viaja de la
    /// boquilla (anillo de entrada) al tanque trasero.
    /// </summary>
    private void UpdateCompartmentInflation(float dt)
    {
        if (vacuum == null) return;

        int stored = vacuum.StoredCount;
        int capacity = Mathf.Max(1, vacuum.Capacity);

        // Impulsos del resorte al cambiar el contenido: rebote al tragar, contracción al disparar.
        if (stored > lastStoredCount)
        {
            inflateVelocity += swallowImpulse;
            swallowWaveTimer = 0f;
        }
        else if (stored < lastStoredCount)
        {
            inflateVelocity += launchImpulse;
        }
        lastStoredCount = stored;

        // Objetivo: fracción ocupada con curva f^γ (γ < 1 hace visible el primer objeto).
        float target = Mathf.Pow(Mathf.Clamp01((float)stored / capacity), fillCurveExponent);
#if UNITY_EDITOR
        if (previewFill >= 0f) target = Mathf.Pow(previewFill, fillCurveExponent);
#endif

        // Resorte con Euler semiimplícito; el paso se limita para que sea estable a pocos FPS.
        float step = Mathf.Min(dt, 1f / 30f);
        inflateVelocity += (inflateStiffness * (target - inflateValue) - inflateDamping * inflateVelocity) * step;
        inflateValue += inflateVelocity * step;

        if (swallowWaveTimer >= 0f)
        {
            swallowWaveTimer += dt;
            if (swallowWaveTimer > 0.9f) swallowWaveTimer = -1f;
        }

        // El valor del resorte puede salir de [0, 1] durante el rebote; se limita para no invertir mallas.
        float fill = Mathf.Clamp(inflateValue, -0.15f, 1.15f);
        float wobble = fill > 0.02f ? Mathf.Sin(Time.time * (9f + 10f * fill)) * 0.012f * fill : 0f;

        // Onda: anillo de entrada → anillo trasero → tanque delantero → tanque trasero.
        float ringFrontRadial = 1f + (ringMaxRadial - 1f) * fill + WavePulse(0.08f, 0.07f);
        float ringRearRadial = 1f + (ringMaxRadial - 1f) * fill + WavePulse(0.18f, 0.08f);
        float frontRadial = 1f + (chamberMaxRadial - 1f) * 0.9f * fill + WavePulse(0.32f, 0.1f) + wobble;
        float rearRadial = 1f + (chamberMaxRadial - 1f) * fill + WavePulse(0.5f, 0.12f) + wobble;
        float axial = 1f + (chamberMaxAxial - 1f) * fill;

        float bodyRadial = 1f + (bodyMaxRadial - 1f) * fill + WavePulse(0.65f, 0.14f) * 0.5f + wobble * 0.5f;

        // Cilindros: ejes x, y radiales y z axial. Cuerpo (Cube): x axial (cañón), y, z radiales.
        ApplyInflation(ringFront, new Vector3(ringFrontRadial, ringFrontRadial, 1f));
        ApplyInflation(ringRear, new Vector3(ringRearRadial, ringRearRadial, 1f));
        ApplyInflation(tankFront, new Vector3(frontRadial, frontRadial, axial));
        ApplyInflation(tankRear, new Vector3(rearRadial, rearRadial, axial));
        ApplyInflation(body, new Vector3(1f + (bodyRadial - 1f) * 0.35f, bodyRadial, bodyRadial));
    }

    private void UpdateClawsAndGrappleLaunch(float dt)
    {
        bool isGrapplingNow = grapple != null && grapple.IsGrappling && grapple.AnchorPoint != Vector3.zero;

        if (isGrapplingNow)
        {
            lastValidAnchorPos = grapple.AnchorPoint;
            grappleExtension = Mathf.MoveTowards(grappleExtension, 1f, dt / Mathf.Max(0.01f, clawLaunchTime));
        }
        else
        {
            grappleExtension = Mathf.MoveTowards(grappleExtension, 0f, dt * clawRetractSpeed);
        }

        // Pose de las garras: cerrada (aspiradora) → abierta (gancho) → agarre (anclado). La base
        // se mueve primero y la punta al final (desfase de 0,08 y 0,16), lo que da una apertura
        // escalonada más orgánica que mover las tres articulaciones a la vez.
        float baseBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(grappleModeBlend * 1.15f));
        float midBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((grappleModeBlend - 0.08f) / 0.92f));
        float tipBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((grappleModeBlend - 0.16f) / 0.84f));
        float gripBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((grappleExtension - 0.55f) / 0.45f));

        Vector3 modePose = new Vector3(
            Mathf.Lerp(clawClosedAngles.x, clawOpenAngles.x, baseBlend),
            Mathf.Lerp(clawClosedAngles.y, clawOpenAngles.y, midBlend),
            Mathf.Lerp(clawClosedAngles.z, clawOpenAngles.z, tipBlend));
        Vector3 pose = Vector3.Lerp(modePose, clawGripAngles, gripBlend);

        for (int i = 0; i < clawBaseBones.Length; i++)
        {
            if (clawBaseBones[i] != null)
                clawBaseBones[i].localRotation = Quaternion.AngleAxis(pose.x, baseRadialHingeAxes[i]) * clawBaseDefaultRots[i];

            if (i < clawMidBones.Length && clawMidBones[i] != null)
                clawMidBones[i].localRotation = clawMidDefaultRots[i] * Quaternion.AngleAxis(pose.y, midRadialHingeAxes[i]);

            if (i < clawTipBones.Length && clawTipBones[i] != null)
                clawTipBones[i].localRotation = clawTipDefaultRots[i] * Quaternion.AngleAxis(pose.z, tipRadialHingeAxes[i]);
        }

        float vacuumOpen = 1f - Mathf.SmoothStep(0f, 1f, grappleModeBlend);
        float suctionBonus = (vacuum != null && vacuum.IsSuctioning) ? 14f : 0f;
        for (int i = 0; i < nozzleFlapBones.Length; i++)
        {
            if (nozzleFlapBones[i] != null)
            {
                float flapAngle = (20f + suctionBonus) * vacuumOpen;
                nozzleFlapBones[i].localRotation = Quaternion.AngleAxis(flapAngle, flapRadialHingeAxes[i]) * flapDefaultRots[i];
            }
        }

        // Lanzamiento físico del cabezal completo (boneForearm / Bone.002) hacia el objetivo y estiramiento del tubo
        if (boneForearm != null)
        {
            // El cabezal permanece en su sitio en ambos modos (las garras siempre se ven); solo
            // se desplaza durante el lanzamiento del gancho.
            boneForearm.localPosition = forearmDefaultLocalPos;
            boneForearm.localRotation = forearmDefaultLocalRot;
            boneForearm.localScale = forearmDefaultLocalScale;

            Vector3 baseWorldPos = boneForearm.position;

            if (grappleExtension > 0.001f && lastValidAnchorPos != Vector3.zero)
            {
                currentHookWorldPos = Vector3.Lerp(baseWorldPos, lastValidAnchorPos, grappleExtension);
                boneForearm.position = currentHookWorldPos;

                Vector3 toAnchor = lastValidAnchorPos - baseWorldPos;
                if (toAnchor.sqrMagnitude > 0.001f)
                {
                    // Orientación exacta del cabezal de garras (Bone.002) apuntando hacia el ancla
                    boneForearm.rotation = Quaternion.LookRotation(toAnchor.normalized, transform.up) * Quaternion.Euler(0f, -90f, -90f);
                }

                DrawDynamicVacuumTube(baseWorldPos, currentHookWorldPos, grappleExtension);
            }
            else
            {
                if (vacuumTubeRenderer != null && vacuumTubeRenderer.enabled)
                {
                    vacuumTubeRenderer.enabled = false;
                }
            }
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
