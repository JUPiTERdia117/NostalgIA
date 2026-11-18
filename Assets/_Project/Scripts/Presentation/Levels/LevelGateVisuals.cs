// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: LevelGateVisuals.cs
// Propósito: aspecto de la puerta entre niveles: vista en tiempo real del nivel
// siguiente, luz que se derrama hacia el nivel actual, destello al cruzar y
// disolución del nivel anterior.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Levels;
using NostalgIA.Presentation.VFX;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NostalgIA.Presentation.Levels
{
    /// <summary>Forma de colorear la luz de la puerta.</summary>
    public enum GateColorMode
    {
        /// <summary>Un solo color.</summary>
        Solid,
        /// <summary>Degradado vertical entre dos colores (arriba → abajo).</summary>
        Gradient
    }

    /// <summary>
    /// Presentación de la puerta de nivel. Escucha los eventos de <c>LevelGate</c> y no conoce la
    /// jugabilidad (separación simulación/presentación).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Vista del portal.</b> Una cámara dedicada se coloca, cada frame, en la pose del jugador
    /// transferida al marco de llegada (<see cref="PortalMath.Transfer"/>), con su plano cercano
    /// sobre ese marco (recorte oblicuo). Dibuja en una textura que la superficie de la puerta
    /// muestra en coordenadas de pantalla, de modo que la imagen encaja con la perspectiva del
    /// jugador. Solo se dibuja cuando la superficie es visible, el jugador está cerca y el nivel
    /// siguiente está despierto; a menor resolución en móvil y sin postprocesado (el de la cámara
    /// principal se aplica una sola vez a toda la imagen).
    /// </para>
    /// <para>
    /// <b>Iluminación prestada.</b> Antes de que la cámara del portal se dibuje se apagan los
    /// soles del nivel actual, se encienden los del nivel siguiente y se aplican sus ajustes de
    /// cielo, luz ambiental y niebla; al terminar se restaura todo. Son unas pocas escrituras de
    /// propiedades por frame y ninguna asignación de memoria.
    /// </para>
    /// <para>
    /// <b>Luz derramada.</b> El efecto de la referencia (la luz del otro mundo que sale por la
    /// puerta y dibuja su forma en el suelo) se obtiene con tres piezas baratas: un foco sin
    /// sombras con una <i>cookie</i> que reproduce exactamente la silueta de la abertura vista desde
    /// la fuente (la mancha de luz en el suelo tiene la forma de la puerta sin calcular sombras),
    /// un haz volumétrico aditivo y partículas de polvo luminoso.
    /// </para>
    /// <para>
    /// <b>Cruce.</b> En el frame del cruce se intercambia la iluminación (el jugador ya veía el
    /// nivel nuevo con su luz, así que no hay salto), se mezclan los perfiles de postprocesado y
    /// un destello breve marca el paso. Detrás del jugador queda la abertura de regreso, que
    /// muestra el nivel anterior y se disuelve en luz antes de que este se descargue.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(600)]
    [DisallowMultipleComponent]
    public sealed class LevelGateVisuals : MonoBehaviour
    {
        // Identificadores de propiedades de shader, cacheados una sola vez.
        private static readonly int PortalTexId = Shader.PropertyToID("_PortalTex");
        private static readonly int ColorAId = Shader.PropertyToID("_ColorA");
        private static readonly int ColorBId = Shader.PropertyToID("_ColorB");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int OpenId = Shader.PropertyToID("_Open");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int GradientYId = Shader.PropertyToID("_GradientY");

        [Header("Color de la luz")]
        [Tooltip("Color sólido o degradado vertical entre dos colores.")]
        [SerializeField] private GateColorMode colorMode = GateColorMode.Gradient;
        [Tooltip("Color superior (o único, en modo sólido).")]
        [SerializeField, ColorUsage(false, true)] private Color colorTop = new Color(1f, 0.86f, 0.55f, 1f);
        [Tooltip("Color inferior del degradado.")]
        [SerializeField, ColorUsage(false, true)] private Color colorBottom = new Color(1f, 0.6f, 0.78f, 1f);
        [Tooltip("Intensidad HDR del brillo de la abertura y los bordes (alimenta el Bloom).")]
        [SerializeField, Range(0f, 8f)] private float glowIntensity = 2.4f;
        [Tooltip("Fracción del brillo mientras el nivel siguiente aún no está listo (0-1).")]
        [SerializeField, Range(0f, 1f)] private float idleGlow = 0.55f;

        [Header("Luz sobre el nivel actual")]
        [Tooltip("Foco sin sombras colocado detrás de la abertura; recibe la cookie con su silueta.")]
        [SerializeField] private Light spillLight;
        [Tooltip("Intensidad del foco con la puerta abierta.")]
        [SerializeField, Range(0f, 30f)] private float spillIntensity = 7f;
        [Tooltip("Haz volumétrico aditivo.")]
        [SerializeField] private Renderer shaft;
        [Tooltip("Intensidad del haz.")]
        [SerializeField, Range(0f, 4f)] private float shaftIntensity = 0.55f;
        [Tooltip("Polvo luminoso dentro del haz.")]
        [SerializeField] private ParticleSystem dust;
        [Tooltip("Resolución de la cookie generada, en texels por lado.")]
        [SerializeField, Range(32, 256)] private int cookieResolution = 96;

        [Header("Piezas")]
        [Tooltip("Ancho y alto de la abertura, en m (igual que en LevelGate).")]
        [SerializeField] private Vector2 aperture = new Vector2(3.2f, 4.4f);
        [Tooltip("Superficie de la abertura (caja escalada al tamaño de la abertura).")]
        [SerializeField] private Renderer surface;
        [Tooltip("Bordes luminosos del marco.")]
        [SerializeField] private Renderer[] trims = new Renderer[0];
        [Tooltip("Abertura de regreso; se traslada al nivel siguiente y aparece al cruzar.")]
        [SerializeField] private Transform arrival;
        [Tooltip("Superficie de la abertura de regreso.")]
        [SerializeField] private Renderer arrivalSurface;
        [Tooltip("Bordes luminosos de la abertura de regreso.")]
        [SerializeField] private Renderer[] arrivalTrims = new Renderer[0];
        [Tooltip("Volume global del destello al cruzar (peso animado).")]
        [SerializeField] private Volume flashVolume;

        [Header("Vista del portal")]
        [Tooltip("Distancia máxima a la que se dibuja la vista del otro nivel, en m.")]
        [SerializeField, Min(5f)] private float renderDistance = 90f;
        [Tooltip("Escala de resolución de la vista en PC (fracción de la resolución de render del preset).")]
        [SerializeField, Range(0.25f, 1f)] private float resolutionScale = 0.8f;
        [Tooltip("Escala de resolución de la vista en móvil.")]
        [SerializeField, Range(0.25f, 1f)] private float mobileResolutionScale = 0.5f;
        [Tooltip("Plano lejano de la cámara del portal, en m (la niebla del otro nivel oculta el corte).")]
        [SerializeField, Min(50f)] private float farClip = 900f;

        [Header("Tiempos")]
        [Tooltip("Tiempo en que la luz se abre y deja ver el nivel siguiente, en s.")]
        [SerializeField, Min(0.05f)] private float openDuration = 1.2f;
        [Tooltip("Duración del destello al cruzar, en s.")]
        [SerializeField, Min(0.05f)] private float flashDuration = 0.9f;
        [Tooltip("Duración de la mezcla de perfiles de postprocesado al cruzar, en s.")]
        [SerializeField, Min(0.05f)] private float volumeCrossfade = 1f;

        [Header("Precalentado de shaders")]
        [Tooltip("Dibuja de forma invisible los materiales del nivel siguiente mientras duerme, para que sus shaders no se compilen todos de golpe al despertar.")]
        [SerializeField] private bool warmShaders = true;
        [Tooltip("Materiales que se precalientan por frame (1 reparte mejor los picos en teléfonos).")]
        [SerializeField, Range(1, 8)] private int warmupItemsPerFrame = 1;
        [Tooltip("Espera tras la precarga antes de empezar el precalentado, en s.")]
        [SerializeField, Min(0f)] private float warmupDelay = 1f;

        // Capa sin nombre en el proyecto: durante el precalentado la cámara del portal solo ve esta capa.
        private const int WarmLayer = 31;

        private MaterialPropertyBlock block;
        private Camera portalCamera;
        private UniversalAdditionalCameraData portalCameraData;
        private RenderTexture viewTexture;
        private Texture2D cookie;
        private Transform viewer;
        private Camera viewerCamera;

        private SceneLightingState currentState;
        private SceneLightingState borrowedState;
        private SceneLightingState sourceState;
        private SceneLightingState destinationState;
        private Transform exit;
        private bool prepared;
        private bool destinationAwake;
        private bool crossed;
        private bool closed;
        private bool borrowing;
        private bool forceRenderFrame;
        private float open;
        private float crossTime;
        private float fadeDuration = 1.8f;
        private float dissolve;
        private Vector3 baseSurfaceScale;
        private Vector3 baseArrivalScale;

        private LevelShaderWarmup warmup;
        private RenderTexture warmTexture;
        private bool warming;
        private bool warmRendering;
        private float warmupTimer;

        // ------------------------------------------------------------------ ciclo de vida
        /// <summary>Crea la cámara del portal y la cookie (una sola vez, al cargar el nivel).</summary>
        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (surface != null) baseSurfaceScale = surface.transform.localScale;
            if (arrivalSurface != null) baseArrivalScale = arrivalSurface.transform.localScale;
            if (arrival != null) arrival.gameObject.SetActive(false);
            if (flashVolume != null)
            {
                flashVolume.weight = 0f;
                flashVolume.enabled = false;
            }

            var cameraObject = new GameObject("PortalCamera");
            cameraObject.transform.SetParent(transform, false);
            portalCamera = cameraObject.AddComponent<Camera>();
            portalCamera.enabled = false;
            portalCamera.clearFlags = CameraClearFlags.Skybox;
            portalCamera.allowMSAA = false;
            portalCamera.useOcclusionCulling = true;
            portalCameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            portalCameraData.renderPostProcessing = false;
            portalCameraData.antialiasing = AntialiasingMode.None;
            portalCameraData.requiresDepthOption = CameraOverrideOption.Off;
            portalCameraData.requiresColorOption = CameraOverrideOption.Off;
            // Las sombras del sol del otro nivel solo en PC: en móvil se ahorra un mapa de sombras.
            portalCameraData.renderShadows = !Application.isMobilePlatform;

            BuildCookie();
            ApplyStaticColors();
        }

        /// <summary>Cámara del jugador (búsqueda única en la inicialización).</summary>
        private void Start()
        {
            viewerCamera = Camera.main;
            if (viewerCamera != null) viewer = viewerCamera.transform;
        }

        /// <summary>Suscripciones a eventos y a las devoluciones de llamada del pipeline.</summary>
        private void OnEnable()
        {
            EventBus<LevelGatePrepared>.Subscribe(OnPrepared);
            EventBus<LevelGateAwakeChanged>.Subscribe(OnAwakeChanged);
            EventBus<LevelGateCrossed>.Subscribe(OnCrossed);
            EventBus<LevelGateClosed>.Subscribe(OnClosed);
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        /// <summary>Cancela suscripciones y libera la textura de la vista.</summary>
        private void OnDisable()
        {
            EventBus<LevelGatePrepared>.Unsubscribe(OnPrepared);
            EventBus<LevelGateAwakeChanged>.Unsubscribe(OnAwakeChanged);
            EventBus<LevelGateCrossed>.Unsubscribe(OnCrossed);
            EventBus<LevelGateClosed>.Unsubscribe(OnClosed);
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            if (borrowing) EndBorrow();
            ReleaseViewTexture();
            ReleaseWarmTexture();
        }

        /// <summary>Libera la cookie y la abertura de regreso (que vive en la otra escena).</summary>
        private void OnDestroy()
        {
            if (cookie != null) Destroy(cookie);
            if (arrival != null && arrival.gameObject.scene != gameObject.scene) arrival.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        /// <summary>Permite ajustar los colores en el inspector durante el juego.</summary>
        private void OnValidate()
        {
            if (!Application.isPlaying || block == null) return;
            BuildCookie();
            ApplyStaticColors();
        }
#endif

        // ------------------------------------------------------------------ eventos
        /// <summary>
        /// El nivel siguiente llegó: se captura la iluminación de ambos niveles, se duerme la del
        /// siguiente y la abertura de regreso se lleva a su marco de llegada.
        /// </summary>
        private void OnPrepared(in LevelGatePrepared evt)
        {
            if (evt.Gate != transform) return;
            exit = evt.Exit;
            aperture = evt.Aperture;
            sourceState = SceneLightingState.Capture(evt.Source, flashVolume);
            destinationState = SceneLightingState.Capture(evt.Destination, null);
            destinationState.SetDormant();
            currentState = sourceState;
            borrowedState = destinationState;

            if (arrival != null && exit != null)
            {
                arrival.SetParent(null, false);
                SceneManager.MoveGameObjectToScene(arrival.gameObject, evt.Destination);
                arrival.SetPositionAndRotation(exit.position, exit.rotation);
                arrival.gameObject.SetActive(false);
            }

            // Las sondas de luz de ambas escenas se combinan en segundo plano.
            LightProbes.TetrahedralizeAsync();
            prepared = true;

            // Lista de materiales del nivel siguiente para precalentarlos mientras duerme.
            if (warmShaders)
            {
                warmup ??= new LevelShaderWarmup();
                warmup.Collect(evt.Destination);
                warming = warmup.Count > 0;
                warmupTimer = warmupDelay;
            }
        }

        /// <summary>El nivel siguiente despertó o volvió a dormir.</summary>
        private void OnAwakeChanged(in LevelGateAwakeChanged evt)
        {
            if (evt.Gate == transform) destinationAwake = evt.Awake;
        }

        /// <summary>
        /// Cruce: se intercambia la iluminación en este mismo frame (antes de dibujar) y empiezan
        /// el destello, la mezcla de perfiles y la disolución del nivel anterior.
        /// </summary>
        private void OnCrossed(in LevelGateCrossed evt)
        {
            if (evt.Gate != transform || destinationState == null) return;
            crossed = true;
            crossTime = 0f;
            fadeDuration = Mathf.Max(0.1f, evt.FadeDuration);
            forceRenderFrame = true;

            sourceState.SetSunsEnabled(false);
            sourceState.SetFlatAudioMuted(true);
            destinationState.SetSunsEnabled(true);
            destinationState.SetFlatAudioMuted(false);
            // La escena activa ya es la nueva: sus ajustes de cielo y niebla rigen la cámara principal.
            currentState = destinationState;
            borrowedState = sourceState;

            if (arrival != null) arrival.gameObject.SetActive(true);
            if (surface != null) surface.enabled = false;
            if (flashVolume != null)
            {
                flashVolume.weight = 0f;
                flashVolume.enabled = true;
            }
        }

        /// <summary>El nivel anterior se descarga: se sueltan sus referencias.</summary>
        private void OnClosed(in LevelGateClosed evt)
        {
            if (evt.Gate != transform) return;
            closed = true;
            destinationState?.SetVolumeBlend(1f);
            if (portalCamera != null) portalCamera.enabled = false;
            if (arrival != null) arrival.gameObject.SetActive(false);
            if (flashVolume != null) flashVolume.enabled = false;
            ReleaseViewTexture();
            sourceState = null;
        }

        // ------------------------------------------------------------------ frame
        /// <summary>
        /// Se ejecuta después del movimiento de la cámara y de la detección del cruce, por lo que la
        /// cámara del portal usa la pose final del jugador en este frame.
        /// </summary>
        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            float targetOpen = crossed || (prepared && destinationAwake) ? 1f : 0f;
            open = Mathf.MoveTowards(open, targetOpen, dt / openDuration);
            float glow = Mathf.Lerp(idleGlow, 1f, Mathf.SmoothStep(0f, 1f, open));

            if (crossed && !closed)
            {
                crossTime += dt;
                UpdateCrossEffects();
            }

            UpdateMaterials(glow);
            if (spillLight != null) spillLight.intensity = spillIntensity * glow * LightColorScale();
            UpdatePortalCamera();
        }

        /// <summary>
        /// Destello, mezcla de perfiles y disolución tras el cruce.
        /// <list type="bullet">
        /// <item>Destello: sube en 0,06 s y cae con 1 − smoothstep(0,06; T; t).</item>
        /// <item>Perfiles: u = t / T_mezcla; peso_anterior = 1 − u, peso_nuevo = u.</item>
        /// <item>Disolución: espera el 20 % de la duración y luego avanza con suavizado
        /// d = smoothstep(0, 1, (t − 0,2·T) / (0,8·T)).</item>
        /// </list>
        /// </summary>
        private void UpdateCrossEffects()
        {
            if (flashVolume != null)
            {
                float rise = Mathf.Clamp01(crossTime / 0.06f);
                float fall = 1f - Mathf.SmoothStep(0.06f, flashDuration, crossTime);
                flashVolume.weight = rise * fall;
                if (crossTime > flashDuration && flashVolume.enabled) flashVolume.enabled = false;
            }

            float u = Mathf.Clamp01(crossTime / volumeCrossfade);
            sourceState?.SetVolumeBlend(1f - u);
            destinationState?.SetVolumeBlend(u);

            float k = Mathf.Clamp01((crossTime - 0.2f * fadeDuration) / (0.8f * fadeDuration));
            dissolve = Mathf.SmoothStep(0f, 1f, k);
        }

        /// <summary>Escribe las propiedades por objeto (pocos renderers: el MPB está justificado).</summary>
        private void UpdateMaterials(float glow)
        {
            Color top = colorTop;
            Color bottom = colorMode == GateColorMode.Solid ? colorTop : colorBottom;
            Texture view = viewTexture != null ? viewTexture : (Texture)Texture2D.blackTexture;

            if (surface != null && surface.enabled)
            {
                surface.GetPropertyBlock(block);
                block.SetColor(ColorAId, top);
                block.SetColor(ColorBId, bottom);
                block.SetFloat(GlowIntensityId, glowIntensity * glow);
                block.SetFloat(OpenId, open);
                block.SetFloat(DissolveId, 0f);
                block.SetTexture(PortalTexId, view);
                surface.SetPropertyBlock(block);
            }

            Vector4 gateGradient = new Vector4(transform.position.y, transform.position.y + aperture.y, 0f, 0f);
            SetGlow(trims, top, bottom, glowIntensity * glow, gateGradient);
            SetGlow(shaft, top, bottom, shaftIntensity * glow, gateGradient);

            if (crossed && arrival != null && arrival.gameObject.activeSelf)
            {
                float fade = 1f - dissolve;
                Vector4 arrivalGradient = new Vector4(arrival.position.y, arrival.position.y + aperture.y, 0f, 0f);
                if (arrivalSurface != null)
                {
                    arrivalSurface.GetPropertyBlock(block);
                    block.SetColor(ColorAId, top);
                    block.SetColor(ColorBId, bottom);
                    block.SetFloat(GlowIntensityId, glowIntensity);
                    block.SetFloat(OpenId, 1f);
                    block.SetFloat(DissolveId, dissolve);
                    block.SetTexture(PortalTexId, view);
                    arrivalSurface.SetPropertyBlock(block);
                }
                SetGlow(arrivalTrims, top, bottom, glowIntensity * fade, arrivalGradient);
            }
        }

        /// <summary>Propiedades del shader de brillo en un grupo de renderers.</summary>
        private void SetGlow(Renderer[] renderers, Color top, Color bottom, float intensity, Vector4 gradient)
        {
            for (int i = 0; i < renderers.Length; i++) SetGlow(renderers[i], top, bottom, intensity, gradient);
        }

        /// <summary>Propiedades del shader de brillo en un renderer.</summary>
        private void SetGlow(Renderer target, Color top, Color bottom, float intensity, Vector4 gradient)
        {
            if (target == null) return;
            target.GetPropertyBlock(block);
            block.SetColor(ColorAId, top);
            block.SetColor(ColorBId, bottom);
            block.SetFloat(IntensityId, intensity);
            block.SetVector(GradientYId, gradient);
            target.SetPropertyBlock(block);
        }

        /// <summary>
        /// Coloca y habilita la cámara del portal. Antes del cruce mira del marco de la puerta al
        /// de llegada; después, del de llegada al de la puerta (el nivel anterior visto de espaldas).
        /// </summary>
        private void UpdatePortalCamera()
        {
            if (portalCamera == null) return;
            Transform from = null;
            Transform to = null;
            Renderer screen = null;
            Vector3 baseScale = Vector3.one;

            if (!crossed)
            {
                if (prepared && destinationAwake && exit != null)
                {
                    from = transform;
                    to = exit;
                    screen = surface;
                    baseScale = baseSurfaceScale;
                }
            }
            else if (!closed && dissolve < 1f && arrival != null)
            {
                from = arrival;
                to = transform;
                screen = arrivalSurface;
                baseScale = baseArrivalScale;
            }

            bool render = from != null && screen != null && viewer != null && viewerCamera != null;
            float side = 0f;
            if (render)
            {
                side = PortalMath.SignedDistance(from, viewer.position);
                float distance = Vector3.Distance(viewer.position, from.position);
                bool visible = screen.isVisible || forceRenderFrame;
                render = side > -0.3f && distance < renderDistance && visible;
            }
            forceRenderFrame = false;

            // Mientras el nivel siguiente duerme y la vista no se dibuja, la cámara del portal se
            // presta para precalentar sus shaders. Si la vista real arranca antes de terminar (o el
            // jugador cruza), el precalentado se corta: a partir de ahí el dibujo real hace lo mismo.
            warmRendering = false;
            if (warming)
            {
                if (!render && !crossed && !destinationAwake)
                {
                    WarmupStep();
                    return;
                }
                StopWarmup();
            }

            if (portalCamera.enabled != render) portalCamera.enabled = render;
            if (!render) return;

            EnsureViewTexture();
            Matrix4x4 transfer = PortalMath.Transfer(from, to);
            portalCamera.transform.SetPositionAndRotation(
                transfer.MultiplyPoint3x4(viewer.position),
                PortalMath.TransferRotation(from, to, viewer.rotation));
            portalCamera.fieldOfView = viewerCamera.fieldOfView;
            portalCamera.aspect = viewerCamera.aspect;
            portalCamera.nearClipPlane = viewerCamera.nearClipPlane;
            portalCamera.farClipPlane = farClip;
            portalCamera.cullingMask = viewerCamera.cullingMask;
            portalCamera.depth = viewerCamera.depth - 1f;
            portalCamera.ResetProjectionMatrix();
            PortalMath.ApplyObliqueClip(portalCamera, to, 0.02f);

            ThickenScreen(screen.transform, baseScale, side);
        }

        /// <summary>
        /// Evita que el plano cercano de la cámara del jugador recorte la superficie justo al cruzar:
        /// la caja de la superficie se engrosa hacia el lado contrario al jugador cuando este está a
        /// menos de 1 m. Como el shader muestrea la vista en coordenadas de pantalla, todas las caras
        /// de la caja muestran la misma imagen y el grosor no se nota.
        /// </summary>
        private static void ThickenScreen(Transform screen, Vector3 baseScale, float side)
        {
            const float Thick = 0.3f;
            bool near = Mathf.Abs(side) < 1f;
            float depth = near ? Thick : baseScale.z;
            float direction = side >= 0f ? -1f : 1f;
            Vector3 scale = new Vector3(baseScale.x, baseScale.y, depth);
            Vector3 position = screen.localPosition;
            position.z = near ? direction * depth * 0.5f : 0f;
            screen.localScale = scale;
            screen.localPosition = position;
        }

        // ------------------------------------------------------------------ precalentado de shaders
        /// <summary>
        /// Un paso del precalentado: coloca la cámara del portal en el marco de llegada, la limita a
        /// la capa de precalentado, apunta a la textura diminuta y dibuja la siguiente entrada.
        /// </summary>
        private void WarmupStep()
        {
            if (warmupTimer > 0f)
            {
                warmupTimer -= Time.deltaTime;
                if (portalCamera.enabled) portalCamera.enabled = false;
                return;
            }

            EnsureWarmTexture();
            Transform anchor = exit != null ? exit : transform;
            portalCamera.transform.SetPositionAndRotation(anchor.position + Vector3.up * 1.6f, anchor.rotation);
            portalCamera.targetTexture = warmTexture;
            portalCamera.cullingMask = 1 << WarmLayer;
            portalCamera.fieldOfView = 60f;
            portalCamera.aspect = 1f;
            portalCamera.nearClipPlane = 0.1f;
            portalCamera.farClipPlane = 50f;
            portalCamera.ResetProjectionMatrix();
            portalCamera.depth = (viewerCamera != null ? viewerCamera.depth : 0f) - 2f;
            if (!portalCamera.enabled) portalCamera.enabled = true;
            warmRendering = true;

            if (warmup.DrawNext(portalCamera, WarmLayer, viewerCamera, warmupItemsPerFrame)) StopWarmup();
        }

        /// <summary>
        /// Termina el precalentado: suelta la textura diminuta y deja creada la textura de la vista
        /// real, para que tampoco haya que reservar memoria en el frame en que la puerta despierta.
        /// </summary>
        private void StopWarmup()
        {
            warming = false;
            if (portalCamera == null) return;
            // Si este mismo frame todavía tenía un dibujo de precalentado pendiente, la cámara se
            // apaga en el siguiente paso de UpdatePortalCamera (que es quien decide si hay vista real).
            if (!warmRendering) portalCamera.enabled = false;
            portalCamera.targetTexture = null;
            ReleaseWarmTexture();
            EnsureViewTexture();
        }

        /// <summary>
        /// Textura de 32 × 32 con el mismo formato y profundidad que la vista real: el estado de la
        /// tubería gráfica depende del formato, no del tamaño, así que precalentar aquí sirve igual
        /// y casi no cuesta.
        /// </summary>
        private void EnsureWarmTexture()
        {
            if (warmTexture != null) return;
            RenderTextureFormat format = LdrEmissionFallback.PipelineIsLdr() ? RenderTextureFormat.Default : RenderTextureFormat.DefaultHDR;
            warmTexture = new RenderTexture(32, 32, 24, format) { name = "LevelGateWarmup" };
            warmTexture.Create();
        }

        /// <summary>Libera la textura del precalentado.</summary>
        private void ReleaseWarmTexture()
        {
            if (warmTexture == null) return;
            if (portalCamera != null && portalCamera.targetTexture == warmTexture) portalCamera.targetTexture = null;
            warmTexture.Release();
            Destroy(warmTexture);
            warmTexture = null;
        }

        // ------------------------------------------------------------------ iluminación prestada
        /// <summary>Justo antes de dibujar la cámara del portal se presta la iluminación del otro nivel.</summary>
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != portalCamera || borrowedState == null || currentState == null) return;
            borrowing = true;
            if (warmRendering)
            {
                // Precalentado: el sol del nivel siguiente sigue dormido (su objeto está apagado),
                // así que dejo encendido el del nivel actual para que las variantes con sombras
                // del sol sí se preparen; del nivel siguiente solo tomo cielo, ambiente y niebla.
                borrowedState.ApplyRenderSettings();
                return;
            }
            currentState.SetSunsEnabled(false);
            borrowedState.SetSunsEnabled(true);
            borrowedState.ApplyRenderSettings();
        }

        /// <summary>Al terminar la cámara del portal se restaura la iluminación del nivel actual.</summary>
        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != portalCamera || !borrowing) return;
            EndBorrow();
        }

        /// <summary>Restaura los soles y los ajustes del nivel actual.</summary>
        private void EndBorrow()
        {
            borrowing = false;
            borrowedState?.SetSunsEnabled(false);
            if (currentState == null) return;
            currentState.SetSunsEnabled(true);
            currentState.ApplyRenderSettings();
        }

        // ------------------------------------------------------------------ recursos
        /// <summary>
        /// Crea o redimensiona la textura de la vista. Se usa HDR solo si el pipeline lo admite
        /// (en calidad móvil, sin HDR, una textura de 32 bits ahorra la mitad de ancho de banda).
        /// </summary>
        private void EnsureViewTexture()
        {
            // Fracción de la resolución de render del preset (no de la pantalla real).
            float scale = Application.isMobilePlatform ? mobileResolutionScale : resolutionScale;
            Vector2Int size = GameGraphics.SecondaryViewSize(scale);
            int width = size.x;
            int height = size.y;
            if (viewTexture != null && viewTexture.width == width && viewTexture.height == height)
            {
                // La textura pudo crearse al terminar el precalentado sin quedar asignada a la cámara.
                if (portalCamera.targetTexture != viewTexture) portalCamera.targetTexture = viewTexture;
                return;
            }

            ReleaseViewTexture();
            RenderTextureFormat format = LdrEmissionFallback.PipelineIsLdr() ? RenderTextureFormat.Default : RenderTextureFormat.DefaultHDR;
            viewTexture = new RenderTexture(width, height, 24, format) { name = "LevelGateView" };
            viewTexture.Create();
            portalCamera.targetTexture = viewTexture;
        }

        /// <summary>Libera la textura de la vista.</summary>
        private void ReleaseViewTexture()
        {
            if (viewTexture == null) return;
            if (portalCamera != null) portalCamera.targetTexture = null;
            viewTexture.Release();
            Destroy(viewTexture);
            viewTexture = null;
        }

        /// <summary>Colores que no cambian cada frame: foco y partículas.</summary>
        private void ApplyStaticColors()
        {
            Color top = colorTop;
            Color bottom = colorMode == GateColorMode.Solid ? colorTop : colorBottom;
            if (spillLight != null)
            {
                Color average = (top + bottom) * 0.5f;
                float max = Mathf.Max(0.0001f, Mathf.Max(average.r, Mathf.Max(average.g, average.b)));
                spillLight.color = average / max;
            }
            if (dust != null)
            {
                ParticleSystem.MainModule main = dust.main;
                main.startColor = new ParticleSystem.MinMaxGradient(top, bottom);
            }
        }

        /// <summary>Factor de intensidad que compensa el color normalizado del foco y la cookie (×2).</summary>
        private float LightColorScale()
        {
            Color bottom = colorMode == GateColorMode.Solid ? colorTop : colorBottom;
            Color average = (colorTop + bottom) * 0.5f;
            return 2f * Mathf.Max(average.r, Mathf.Max(average.g, average.b));
        }

        /// <summary>
        /// Genera la cookie del foco: para cada texel se reconstruye la dirección del rayo dentro del
        /// cono, d = R·normalize((2u − 1)·tan(α/2), (2v − 1)·tan(α/2), 1), y se intersecta con el
        /// plano de la puerta (z_local = 0): t = −S_z / d_z, P = S + t·d. El texel vale 1 si P cae
        /// dentro de la abertura, con un borde suave de 6 cm (smoothstep sobre la distancia al
        /// borde). Así la mancha de luz en el suelo tiene la silueta exacta de la puerta, como la
        /// proyectaría la luz del otro mundo, sin calcular sombras.
        /// </summary>
        /// <remarks>
        /// El color del degradado se codifica como c / c̄ · 0,5 (relativo al color medio del foco):
        /// con cookies en color (PC) la mancha muestra el degradado; con cookies en escala de grises
        /// (móvil) queda la silueta con el color medio. La textura se crea una vez al cargar el nivel.
        /// </remarks>
        private void BuildCookie()
        {
            if (spillLight == null || spillLight.type != LightType.Spot) return;
            int size = cookieResolution;
            if (cookie == null || cookie.width != size)
            {
                if (cookie != null) Destroy(cookie);
                cookie = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                {
                    name = "LevelGateCookie",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            Color top = colorTop;
            Color bottom = colorMode == GateColorMode.Solid ? colorTop : colorBottom;
            Color average = (top + bottom) * 0.5f;
            const float Soft = 0.06f;
            float tanHalf = Mathf.Tan(spillLight.spotAngle * 0.5f * Mathf.Deg2Rad);
            Vector3 sourceLocal = transform.InverseTransformPoint(spillLight.transform.position);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    Vector3 lightDirection = new Vector3(u * tanHalf, v * tanHalf, 1f).normalized;
                    Vector3 local = transform.InverseTransformDirection(spillLight.transform.rotation * lightDirection);
                    float value = 0f;
                    float gradient = 0.5f;
                    if (local.z > 1e-4f && sourceLocal.z < 0f)
                    {
                        float t = -sourceLocal.z / local.z;
                        Vector3 hit = sourceLocal + local * t;
                        float edge = Mathf.Min(aperture.x * 0.5f - Mathf.Abs(hit.x), Mathf.Min(hit.y, aperture.y - hit.y));
                        value = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-Soft, Soft, edge));
                        gradient = Mathf.Clamp01(hit.y / Mathf.Max(0.01f, aperture.y));
                    }

                    Color tint = Color.Lerp(bottom, top, gradient);
                    float r = Mathf.Clamp01(tint.r / Mathf.Max(0.0001f, average.r) * 0.5f) * value;
                    float g = Mathf.Clamp01(tint.g / Mathf.Max(0.0001f, average.g) * 0.5f) * value;
                    float b = Mathf.Clamp01(tint.b / Mathf.Max(0.0001f, average.b) * 0.5f) * value;
                    pixels[y * size + x] = new Color(r, g, b, value);
                }
            }

            cookie.SetPixels32(pixels);
            cookie.Apply(false, false);
            spillLight.cookie = cookie;
        }
    }
}
