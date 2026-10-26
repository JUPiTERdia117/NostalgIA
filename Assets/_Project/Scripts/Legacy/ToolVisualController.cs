using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Controla todas las animaciones y efectos visuales del modelo 3D de la herramienta (robot.fbx):
/// 1. Modo Aspirar: Garras completamente retraídas sobre el cañón, efecto de viento con rayas grises
///    convergiendo hacia la boquilla, e inflado progresivo y muy notorio del compartimiento con onda
///    peristáltica al tragar cada objeto y vibración de presión según su llenado.
/// 2. Modo Gancho: Garras desplegadas y activas; al disparar el gancho, el cabezal físico completo (Bone.002)
///    sale disparado hacia el objetivo estirando dinámicamente el tubo de aspiradora, y se retrae rápido al soltar.
/// 3. Transición continua, fluida y coherente al alternar de modo con [Q] sin ocultar el arma.
/// </summary>
public class ToolVisualController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private ObjectVacum vacuum;
    [SerializeField] private GrapleController grapple;
    [SerializeField] private Transform robotRoot;

    [Header("Velocidades de Animación")]
    [SerializeField] private float modeTransitionSpeed = 3.5f;
    [SerializeField] private float clawLaunchTime = 0.055f;
    [SerializeField] private float clawRetractSpeed = 28f;
    [SerializeField] private float compartmentInflateSpeed = 9f;
    [SerializeField] private float maxCompartmentInflation = 2.45f;

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

    private Vector3 compADefaultScale;
    private Vector3 compBDefaultScale;
    private Vector3 ringADefaultScale;
    private Vector3 ringBDefaultScale;
    private Vector3 mainCubeDefaultScale;
    private Vector3 mainCubeDefaultPos;

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

    // Estado del inflado progresivo del compartimiento
    private float currentVisualFill = 0f;
    private float swallowWaveTimer = -1f; // Onda peristáltica que recorre el arma de adelante hacia atrás al tragar
    private float launchDeflatePulse = 0f;
    private int lastStoredCount = 0;

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

        if (playerController == null) playerController = GetComponentInParent<PlayerController>();
        if (vacuum == null) vacuum = GetComponent<ObjectVacum>() ?? GetComponentInChildren<ObjectVacum>(true);
        if (grapple == null) grapple = GetComponent<GrapleController>() ?? GetComponentInChildren<GrapleController>(true);

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

        if (boneCompartmentA != null) compADefaultScale = boneCompartmentA.localScale;
        if (boneCompartmentB != null) compBDefaultScale = boneCompartmentB.localScale;
        if (tubeRingA != null) ringADefaultScale = tubeRingA.localScale;
        if (tubeRingB != null) ringBDefaultScale = tubeRingB.localScale;
        if (mainCube != null)
        {
            mainCubeDefaultScale = mainCube.localScale;
            mainCubeDefaultPos = mainCube.localPosition;
        }

        clawBaseDefaultRots = CaptureRotations(clawBaseBones);
        clawMidDefaultRots = CaptureRotations(clawMidBones);
        clawTipDefaultRots = CaptureRotations(clawTipBones);
        flapDefaultRots = CaptureRotations(nozzleFlapBones);

        ComputeRadialHingeAxes();

        if (playerController != null)
        {
            grappleModeBlend = playerController.SelectedTool == PlayerController.Tool.Grapple ? 1f : 0f;
        }

        SetupNozzleTip();
        SetupVacuumTubeRenderer();
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
            midRadialHingeAxes[i] = (Quaternion.Inverse(baseRot) * hingeInForearm).normalized;
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

        PlayerController.Tool currentTool = playerController.SelectedTool;
        float targetBlend = (currentTool == PlayerController.Tool.Grapple) ? 1f : 0f;
        grappleModeBlend = Mathf.MoveTowards(grappleModeBlend, targetBlend, dt * modeTransitionSpeed);

        if (robotRoot != null)
        {
            Vector3 suctionShake = Vector3.zero;
            if (vacuum != null && vacuum.vacuumActive && currentTool == PlayerController.Tool.Vacuum)
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

    /// <summary>
    /// Infla de manera muy visible y progresiva todos los compartimientos y anillos de la aspiradora
    /// a medida que absorbe objetos, incluyendo una onda peristáltica ("tragado") y vibración de presión.
    /// </summary>
    private void UpdateCompartmentInflation(float dt)
    {
        if (vacuum == null) return;

        int stored = vacuum.StoredCount;
        int capacity = Mathf.Max(1, vacuum.ObjectCapacity);

        if (stored > lastStoredCount)
        {
            // Iniciar onda de tragado que recorre desde la boquilla hasta el tanque trasero
            swallowWaveTimer = 0f;
        }
        else if (stored < lastStoredCount)
        {
            // Impulso elástico de desinflado al disparar un objeto
            launchDeflatePulse = 1f;
        }
        lastStoredCount = stored;

        if (swallowWaveTimer >= 0f)
        {
            swallowWaveTimer += dt * 3.8f;
            if (swallowWaveTimer > 1.3f) swallowWaveTimer = -1f;
        }
        launchDeflatePulse = Mathf.MoveTowards(launchDeflatePulse, 0f, dt * 6f);

        // Curva no lineal (exponente 0.55) + escalón inicial para que desde el 1er objeto se note claramente cómo se infla
        float rawRatio = Mathf.Clamp01((float)stored / capacity);
        float targetVisualFill = stored > 0
            ? Mathf.Clamp01(0.18f + 0.82f * Mathf.Pow(rawRatio, 0.58f))
            : 0f;

        currentVisualFill = Mathf.Lerp(currentVisualFill, targetVisualFill, dt * compartmentInflateSpeed);

        // Vibración elástica de presión interna cuando el tanque contiene objetos
        float pressureWobble = currentVisualFill > 0.01f
            ? Mathf.Sin(Time.time * (10f + currentVisualFill * 18f)) * 0.045f * currentVisualFill
            : 0f;

        float deflateKick = -Mathf.Sin(launchDeflatePulse * Mathf.PI) * 0.18f;

        // Onda peristáltica en 3 etapas: 1) Anillos delanteros -> 2) Cilindro frontal -> 3) Cilindro trasero y Cuerpo Cube
        float waveFront = swallowWaveTimer >= 0f ? Mathf.Exp(-Mathf.Pow((swallowWaveTimer - 0.18f) * 5.5f, 2f)) * 0.42f : 0f;
        float waveMid   = swallowWaveTimer >= 0f ? Mathf.Exp(-Mathf.Pow((swallowWaveTimer - 0.52f) * 4.8f, 2f)) * 0.48f : 0f;
        float waveRear  = swallowWaveTimer >= 0f ? Mathf.Exp(-Mathf.Pow((swallowWaveTimer - 0.88f) * 4.2f, 2f)) * 0.52f : 0f;

        // 1. Anillos de entrada (Cylinder.005 y Cylinder.002)
        if (tubeRingB != null)
        {
            float s = 1f + currentVisualFill * 0.35f + waveFront;
            tubeRingB.localScale = new Vector3(ringBDefaultScale.x * s, ringBDefaultScale.y * s, ringBDefaultScale.z);
        }
        if (tubeRingA != null)
        {
            float s = 1f + currentVisualFill * 0.45f + (waveFront * 0.5f + waveMid * 0.5f);
            tubeRingA.localScale = new Vector3(ringADefaultScale.x * s, ringADefaultScale.y * s, ringADefaultScale.z);
        }

        // 2. Compartimiento delantero (Bone.004 / Cylinder.001)
        if (boneCompartmentB != null)
        {
            float radialB = Mathf.Lerp(1f, maxCompartmentInflation * 0.92f, currentVisualFill) + waveMid + pressureWobble + deflateKick;
            float axialB = Mathf.Lerp(1f, 1f + (maxCompartmentInflation - 1f) * 0.42f, currentVisualFill) + waveMid * 0.35f;
            boneCompartmentB.localScale = new Vector3(
                compBDefaultScale.x * axialB,
                compBDefaultScale.y * radialB,
                compBDefaultScale.z * radialB
            );
        }

        // 3. Compartimiento principal trasero (Bone.003 / Cylinder)
        if (boneCompartmentA != null)
        {
            float radialA = Mathf.Lerp(1f, maxCompartmentInflation, currentVisualFill) + waveRear + pressureWobble + deflateKick;
            float axialA = Mathf.Lerp(1f, 1f + (maxCompartmentInflation - 1f) * 0.55f, currentVisualFill) + waveRear * 0.4f;
            boneCompartmentA.localScale = new Vector3(
                compADefaultScale.x * axialA,
                compADefaultScale.y * radialA,
                compADefaultScale.z * radialA
            );
        }

        // 4. Cámara principal / vientre del arma (Cube) — muy visible en primera persona
        if (mainCube != null)
        {
            float bellyRadial = Mathf.Lerp(1f, 1.78f, currentVisualFill) + waveRear * 0.65f + pressureWobble * 1.2f + deflateKick * 0.6f;
            float bellyLength = Mathf.Lerp(1f, 1.28f, currentVisualFill) + waveRear * 0.25f;
            mainCube.localScale = new Vector3(
                mainCubeDefaultScale.x * bellyLength,
                mainCubeDefaultScale.y * bellyRadial,
                mainCubeDefaultScale.z * bellyRadial
            );
            // Ligero abombamiento superior/lateral al inflarse para que sobresalga claramente en la vista del jugador
            mainCube.localPosition = mainCubeDefaultPos + new Vector3(0f, currentVisualFill * 8.5f, 0f);
        }
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

        // Articulación escalonada y continua de las 3 garras al cambiar entre Aspiradora y Gancho
        float baseBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(grappleModeBlend * 1.15f));
        float midBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((grappleModeBlend - 0.08f) / 0.92f));
        float tipBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((grappleModeBlend - 0.16f) / 0.84f));
        float anchorClamp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((grappleExtension - 0.55f) / 0.45f));

        for (int i = 0; i < clawBaseBones.Length; i++)
        {
            if (clawBaseBones[i] != null)
            {
                float activeAngle = Mathf.Lerp(0f, -22f, anchorClamp);
                float baseAngle = Mathf.Lerp(105f, activeAngle, baseBlend);
                clawBaseBones[i].localRotation = Quaternion.AngleAxis(baseAngle, baseRadialHingeAxes[i]) * clawBaseDefaultRots[i];
            }

            if (i < clawMidBones.Length && clawMidBones[i] != null)
            {
                float activeMidAngle = Mathf.Lerp(0f, -15f, anchorClamp);
                float midAngle = Mathf.Lerp(20f, activeMidAngle, midBlend);
                clawMidBones[i].localRotation = clawMidDefaultRots[i] * Quaternion.AngleAxis(midAngle, midRadialHingeAxes[i]);
            }

            if (i < clawTipBones.Length && clawTipBones[i] != null)
            {
                float activeTipAngle = Mathf.Lerp(0f, -20f, anchorClamp);
                float tipAngle = Mathf.Lerp(85f, activeTipAngle, tipBlend);
                clawTipBones[i].localRotation = clawTipDefaultRots[i] * Quaternion.AngleAxis(tipAngle, tipRadialHingeAxes[i]);
            }
        }

        float vacuumOpen = 1f - Mathf.SmoothStep(0f, 1f, grappleModeBlend);
        float suctionBonus = (vacuum != null && vacuum.vacuumActive) ? 14f : 0f;
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
            float slideBlend = Mathf.SmoothStep(0f, 1f, grappleModeBlend);
            Vector3 retractedLocalPos = forearmDefaultLocalPos + new Vector3(0f, -0.34f, 0f);
            Vector3 baseModeLocalPos = Vector3.Lerp(retractedLocalPos, forearmDefaultLocalPos, slideBlend);

            boneForearm.localPosition = baseModeLocalPos;
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
                              playerController.SelectedTool == PlayerController.Tool.Vacuum &&
                              vacuum != null &&
                              vacuum.vacuumActive;

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
