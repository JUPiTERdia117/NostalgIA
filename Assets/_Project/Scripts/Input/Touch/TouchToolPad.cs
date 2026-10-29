// -----------------------------------------------------------------------------
// NostalgIA — Entrada (NostalgIA.Input)
// Archivo: TouchToolPad.cs
// Propósito: pad derecho de herramienta cuyo gesto cambia según la herramienta
// activa (GDD 2.3, TDD §7.5, tarea F1-A05).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Services;
using NostalgIA.Core.State;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NostalgIA.Input.TouchControls
{
    /// <summary>
    /// Botón contextual de herramienta para pantallas táctiles.
    /// </summary>
    /// <remarks>
    /// <para>Comportamiento (TDD §7.5):</para>
    /// <list type="bullet">
    /// <item><b>Aspiradora:</b> mantener más de <c>holdThreshold</c> segundos = <c>Absorb</c> hasta soltar;
    /// un toque más corto = <c>Fire</c> al soltar. Mientras el dedo sigue abajo, el pad también es un
    /// stick de vista: arrastrar mueve la perilla y gira la cámara, así puedo seguir absorbiendo
    /// mientras apunto a los objetos de al lado. Si arrastro antes del umbral, la succión empieza en
    /// ese momento (arrastrar nunca cuenta como toque para disparar).</item>
    /// <item><b>Gancho:</b> tocar = <c>Grapple</c> (volver a tocar cancela). Aquí el pad es solo un botón:
    /// ni gira la cámara ni mueve la perilla.</item>
    /// </list>
    /// <para>
    /// La herramienta activa se conoce por el evento <see cref="ToolSwitched"/>, por lo que este
    /// componente de entrada no depende del ensamblado de jugabilidad. Además la vuelvo a leer de
    /// <see cref="IToolState"/> al activarse y al reaparecer, para que el icono y el gesto nunca se
    /// queden con la herramienta equivocada si se perdió algún evento.
    /// </para>
    /// <para>
    /// Las pulsaciones se miden con <see cref="Time.unscaledTime"/> para que una pausa breve de la
    /// escala de tiempo no convierta un toque en «mantener».
    /// </para>
    /// </remarks>
    public sealed class TouchToolPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("Parámetros compartidos de los controles táctiles.")]
        [SerializeField] private TouchLayoutSettings settings;

        [Tooltip("Imagen del icono del pad (opcional).")]
        [SerializeField] private Image icon;

        [Tooltip("Icono mostrado con la aspiradora activa (opcional).")]
        [SerializeField] private Sprite vacuumIcon;

        [Tooltip("Icono mostrado con el gancho activo (opcional).")]
        [SerializeField] private Sprite grappleIcon;

        [Tooltip("Perilla que se desplaza con el dedo en modo aspiradora. Si se deja vacía se usa el icono.")]
        [SerializeField] private RectTransform knob;

        [Tooltip("Fracción del radio que el dedo puede moverse y seguir contando como toque (disparo).")]
        [SerializeField, Range(0f, 0.5f)] private float tapTolerance = 0.18f;

        /// <summary>Herramienta activa según el último <see cref="ToolSwitched"/>.</summary>
        private ToolId activeTool = ToolId.Vacuum;

        /// <summary>Dedo que mantiene el pad; −1 si no hay ninguno.</summary>
        private int activePointerId = -1;

        /// <summary>Instante en que empezó la pulsación.</summary>
        private float pressedAt;

        /// <summary>Indica si la pulsación ya se convirtió en succión.</summary>
        private bool holding;

        /// <summary>Punto donde cayó el dedo, en coordenadas locales del pad.</summary>
        private Vector2 origin;

        /// <summary>Posición de reposo de la perilla.</summary>
        private Vector2 knobRest;

        /// <summary>Lector de entrada.</summary>
        private InputReader reader;

        /// <summary>Umbral de mantener.</summary>
        private float HoldThreshold => settings != null ? settings.HoldThreshold : 0.2f;

        /// <summary>Radio del stick, el mismo que el del pad de movimiento.</summary>
        private float Radius => settings != null ? settings.StickRadius : 110f;

        /// <summary>Perilla efectiva: la asignada o, si falta, el icono.</summary>
        private RectTransform Knob => knob != null ? knob : (icon != null ? icon.rectTransform : null);

        /// <summary>Guarda la posición de reposo de la perilla.</summary>
        private void Awake()
        {
            if (Knob != null) knobRest = Knob.anchoredPosition;
        }

        /// <summary>Suscripción y estado inicial.</summary>
        private void OnEnable()
        {
            ServiceLocator.TryGet(out reader);
            EventBus<ToolSwitched>.Subscribe(OnToolSwitched);
            EventBus<PlayerRespawned>.Subscribe(OnPlayerRespawned);
            CancelPress();
            SyncWithToolState();
        }

        /// <summary>Cancela la pulsación y la suscripción.</summary>
        private void OnDisable()
        {
            EventBus<ToolSwitched>.Unsubscribe(OnToolSwitched);
            EventBus<PlayerRespawned>.Unsubscribe(OnPlayerRespawned);
            CancelPress();
        }

        /// <summary>Registra el inicio de la pulsación; con el gancho dispara de inmediato.</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId != -1) return;
            activePointerId = eventData.pointerId;
            pressedAt = Time.unscaledTime;
            holding = false;
            TryGetLocalPoint(eventData, out origin);

            if (activeTool == ToolId.Grapple) reader?.InjectGrapple();
        }

        /// <summary>
        /// Con la aspiradora, el arrastre mueve la perilla y gira la cámara como un stick:
        /// desplazamiento = clamp(dedo − origen, radio) y valor = desplazamiento / radio (−1 a 1).
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId || activeTool != ToolId.Vacuum) return;
            if (!TryGetLocalPoint(eventData, out Vector2 local)) return;

            float radius = Radius;
            Vector2 offset = Vector2.ClampMagnitude(local - origin, radius);
            if (Knob != null) Knob.anchoredPosition = knobRest + offset;

            Vector2 value = offset / radius;
            reader?.SetVirtualLookStick(value);

            // Si ya me salí de la tolerancia de toque, el gesto es "apuntar mientras absorbo".
            if (!holding && value.magnitude > tapTolerance) StartAbsorb();
        }

        /// <summary>Convierte la pulsación en succión al superar el umbral (solo aspiradora).</summary>
        private void Update()
        {
            if (activePointerId == -1 || holding || activeTool != ToolId.Vacuum) return;
            if (Time.unscaledTime - pressedAt < HoldThreshold) return;
            StartAbsorb();
        }

        /// <summary>Al soltar: termina la succión o, si fue un toque corto, dispara.</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId) return;
            bool wasHolding = holding;
            bool isVacuum = activeTool == ToolId.Vacuum;
            CancelPress();
            if (isVacuum && !wasHolding) reader?.InjectFire();
        }

        /// <summary>Empieza la succión virtual.</summary>
        private void StartAbsorb()
        {
            holding = true;
            reader?.SetVirtualAbsorb(true);
        }

        /// <summary>Termina cualquier pulsación en curso y regresa la perilla al centro.</summary>
        private void CancelPress()
        {
            if (holding) reader?.SetVirtualAbsorb(false);
            reader?.SetVirtualLookStick(Vector2.zero);
            holding = false;
            activePointerId = -1;
            if (Knob != null) Knob.anchoredPosition = knobRest;
        }

        /// <summary>Actualiza el modo del pad al cambiar de herramienta.</summary>
        private void OnToolSwitched(in ToolSwitched evt)
        {
            CancelPress();
            activeTool = evt.Tool;
            RefreshIcon();
        }

        /// <summary>Al reaparecer suelto cualquier pulsación y vuelvo a leer la herramienta real.</summary>
        private void OnPlayerRespawned(in PlayerRespawned evt)
        {
            CancelPress();
            SyncWithToolState();
        }

        /// <summary>Toma la herramienta activa del estado de herramientas, si está registrado.</summary>
        private void SyncWithToolState()
        {
            if (ServiceLocator.TryGet(out IToolState tools)) activeTool = tools.ActiveTool;
            RefreshIcon();
        }

        /// <summary>Muestra el icono de la herramienta activa.</summary>
        private void RefreshIcon()
        {
            if (icon == null) return;
            Sprite sprite = activeTool == ToolId.Vacuum ? vacuumIcon : grappleIcon;
            if (sprite != null) icon.sprite = sprite;
        }

        /// <summary>Convierte la posición del dedo a coordenadas locales del pad.</summary>
        private bool TryGetLocalPoint(PointerEventData eventData, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform, eventData.position, eventData.pressEventCamera, out local);
        }
    }
}
