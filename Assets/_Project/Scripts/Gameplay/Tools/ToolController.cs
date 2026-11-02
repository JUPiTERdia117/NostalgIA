// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: ToolController.cs
// Propósito: herramienta activa, resolución contextual de acciones y
// enfriamiento compartido.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Services;
using NostalgIA.Core.State;
using NostalgIA.Gameplay.Aim;
using NostalgIA.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace NostalgIA.Gameplay.Tools
{
    /// <summary>
    /// Controlador de herramientas del dron.
    /// </summary>
    /// <remarks>
    /// <para>Responsabilidades:</para>
    /// <list type="bullet">
    /// <item>Mantener <b>una sola herramienta activa</b> y procesar <c>SwitchTool</c>.</item>
    /// <item>Traducir las acciones lógicas a <see cref="ToolInput"/> según la herramienta activa
    /// la aspiradora atiende <c>Absorb</c> y <c>Fire</c>; el gancho, <c>Grapple</c>.
    /// Esto resuelve que el clic derecho y RT tengan significados distintos según la herramienta.</item>
    /// <item>Administrar el <b>enfriamiento compartido</b>: tras soltar el gancho, ninguna de las
    /// dos herramientas acepta acciones hasta que el ducto se retrae (<i>Cooldown</i>).
    /// Cambiar de herramienta sí está permitido durante el enfriamiento.</item>
    /// <item>Publicar <see cref="ToolSwitched"/> y <see cref="ToolCooldownChanged"/> (solo al
    /// iniciar y terminar; el valor intermedio se lee en <see cref="IToolState"/>).</item>
    /// </list>
    /// <para>
    /// Reutiliza el GUID del antiguo <c>PlayerController</c> para conservar la referencia del
    /// prefab; sus campos de vida pasaron a <c>PlayerHealth</c>.
    /// </para>
    /// <para>
    /// Las pulsaciones llegan como eventos de <see cref="InputReader"/> (antes de <c>Update</c>) y
    /// se guardan en indicadores que se consumen una vez por frame, de modo que una pulsación
    /// nunca se procesa dos veces ni se pierde entre frames.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ToolController : MonoBehaviour, IToolState
    {
        [Tooltip("Aspiradora Lanzadora.")]
        [SerializeField] private VacuumTool vacuum;

        [Tooltip("Gancho.")]
        [FormerlySerializedAs("graple")]
        [SerializeField] private GrappleTool grapple;

        [Tooltip("Servicio de puntería del jugador. Si falta, se busca en el mismo objeto.")]
        [SerializeField] private AimService aim;

        [Tooltip("Herramienta seleccionada al iniciar la estación.")]
        [SerializeField] private ToolId initialTool = ToolId.Vacuum;

        private ITool activeTool;
        private ToolId activeToolId;
        private InputReader input;

        private float cooldownRemaining;
        private float cooldownDuration;

        private bool switchRequested;
        private bool firePressed;
        private bool grapplePressed;

        /// <inheritdoc />
        public ToolId ActiveTool => activeToolId;

        /// <inheritdoc />
        public float CooldownRemaining => cooldownRemaining;

        /// <inheritdoc />
        public float CooldownDuration => cooldownDuration;

        /// <inheritdoc />
        public bool IsOnCooldown => cooldownRemaining > 0f;

        /// <summary>Aspiradora.</summary>
        public VacuumTool Vacuum => vacuum;

        /// <summary>Gancho.</summary>
        public GrappleTool Grapple => grapple;

        /// <summary>Resuelve referencias.</summary>
        private void Awake()
        {
            if (vacuum == null) vacuum = GetComponentInChildren<VacuumTool>(true);
            if (grapple == null) grapple = GetComponentInChildren<GrappleTool>(true);
            if (aim == null) aim = GetComponent<AimService>();
        }

        /// <summary>Registro, suscripciones y herramienta inicial.</summary>
        private void OnEnable()
        {
            ServiceLocator.Register<IToolState>(this);
            ServiceLocator.Register(this);
            if (ServiceLocator.TryGet(out input))
            {
                input.SwitchToolPressed += OnSwitchToolPressed;
                input.FirePressed += OnFirePressed;
                input.GrapplePressed += OnGrapplePressed;
            }
            EventBus<GrappleReleased>.Subscribe(OnGrappleReleased);
        }

        /// <summary>Cancela suscripciones y acciones en curso.</summary>
        private void OnDisable()
        {
            if (input != null)
            {
                input.SwitchToolPressed -= OnSwitchToolPressed;
                input.FirePressed -= OnFirePressed;
                input.GrapplePressed -= OnGrapplePressed;
            }
            EventBus<GrappleReleased>.Unsubscribe(OnGrappleReleased);
            activeTool?.Unequip();
            ServiceLocator.Unregister<IToolState>(this);
            ServiceLocator.Unregister(this);
        }

        /// <summary>Equipa la herramienta inicial (después de que los demás componentes despierten).</summary>
        private void Start() => Select(initialTool, true);

        /// <summary>Consume las pulsaciones del frame y actualiza la herramienta activa.</summary>
        private void Update()
        {
            float dt = Time.deltaTime;

            if (switchRequested)
            {
                switchRequested = false;
                Select(activeToolId == ToolId.Vacuum ? ToolId.Grapple : ToolId.Vacuum, false);
            }

            if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= dt;
                if (cooldownRemaining <= 0f)
                {
                    cooldownRemaining = 0f;
                    EventBus<ToolCooldownChanged>.Raise(new ToolCooldownChanged(0f, 0f));
                }
            }

            ToolInput toolInput = BuildInput();
            firePressed = false;
            grapplePressed = false;

            if (activeTool == null || dt <= 0f) return;
            if (aim != null)
            {
                aim.SetActiveTool(activeToolId, activeTool.Range);
                activeTool.Tick(in toolInput, in aim.Current, dt);
            }
            else
            {
                AimSample none = default;
                activeTool.Tick(in toolInput, in none, dt);
            }
        }

        /// <summary>
        /// Construye la entrada filtrada: durante el enfriamiento no pasa ninguna acción, y cada
        /// herramienta recibe solo las suyas.
        /// </summary>
        private ToolInput BuildInput()
        {
            if (IsOnCooldown || input == null) return ToolInput.None;
            return activeToolId == ToolId.Vacuum
                ? new ToolInput(input.AbsorbHeld, firePressed, false)
                : new ToolInput(false, false, grapplePressed);
        }

        /// <summary>Selecciona una herramienta, desequipando la anterior.</summary>
        /// <param name="tool">Herramienta a activar.</param>
        /// <param name="force">Publica el evento aunque sea la misma (al iniciar).</param>
        public void Select(ToolId tool, bool force)
        {
            if (!force && tool == activeToolId && activeTool != null) return;

            activeTool?.Unequip();
            activeToolId = tool;
            activeTool = tool == ToolId.Vacuum ? (ITool)vacuum : grapple;
            activeTool?.Equip();
            if (aim != null && activeTool != null) aim.SetActiveTool(tool, activeTool.Range);
            EventBus<ToolSwitched>.Raise(new ToolSwitched(tool));
        }

        /// <summary>Inicia el enfriamiento compartido.</summary>
        /// <param name="duration">Duración en segundos.</param>
        public void StartCooldown(float duration)
        {
            if (duration <= 0f) return;
            cooldownDuration = duration;
            cooldownRemaining = duration;
            EventBus<ToolCooldownChanged>.Raise(new ToolCooldownChanged(duration, 1f));
        }

        /// <summary>
        /// Restablece las herramientas para la reaparición: suelta el gancho, detiene la succión
        /// y anula el enfriamiento.
        /// </summary>
        public void ResetTools()
        {
            vacuum?.ResetTool();
            grapple?.ResetTool();
            firePressed = false;
            grapplePressed = false;
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = 0f;
                EventBus<ToolCooldownChanged>.Raise(new ToolCooldownChanged(0f, 0f));
            }
        }

        // ------------------------------------------------------------------ eventos
        private void OnSwitchToolPressed() => switchRequested = true;
        private void OnFirePressed() => firePressed = true;
        private void OnGrapplePressed() => grapplePressed = true;

        /// <summary>Al soltar el gancho empieza el enfriamiento compartido.</summary>
        private void OnGrappleReleased(in GrappleReleased evt)
        {
            if (grapple != null) StartCooldown(grapple.CooldownDuration);
        }
    }
}
