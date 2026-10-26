// -----------------------------------------------------------------------------
// NostalgIA — Entrada (NostalgIA.Input)
// Archivo: DynamicTouchStick.cs
// Propósito: pad de movimiento táctil que aparece donde el dedo toca la mitad
// izquierda de la pantalla (TDD §7.5, tarea F1-A05).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace NostalgIA.Input.TouchControls
{
    /// <summary>
    /// Stick virtual dinámico: su base se coloca en el punto de contacto y escribe el valor en
    /// el stick izquierdo de un mando virtual (<c>&lt;Gamepad&gt;/leftStick</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hereda de <see cref="OnScreenControl"/>: el Input System crea un mando virtual y la acción
    /// <c>Move</c> lo lee igual que a un mando físico, sin código específico para móvil en la
    /// jugabilidad. <c>InputSchemeResolver</c> reconoce ese mando como esquema Táctil porque no es
    /// un dispositivo nativo.
    /// </para>
    /// <para>
    /// Geometría: con el origen <c>o</c> (punto de contacto inicial) y la posición actual
    /// <c>p</c>, el desplazamiento <c>d = p − o</c> se limita al radio <c>r</c>:
    /// <c>d' = d · min(1, r / |d|)</c>. El valor enviado es <c>d' / r</c>, un vector de magnitud
    /// ≤ 1 como el de un stick físico. Correr se activa cuando la componente vertical normalizada
    /// supera el umbral (GDD: «llevar el pad izquierdo hasta arriba»).
    /// </para>
    /// <para>
    /// El componente se coloca en una zona transparente que cubre la mitad izquierda del lienzo;
    /// solo atiende al primer dedo que la toca (<see cref="PointerEventData.pointerId"/>), de modo
    /// que un segundo dedo en la misma zona no mueve el pad.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DynamicTouchStick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("Ruta del control del mando virtual que recibe el valor.")]
        [InputControl(layout = "Vector2")]
        [SerializeField] private string stickControlPath = "<Gamepad>/leftStick";

        [Tooltip("Parámetros compartidos de los controles táctiles.")]
        [SerializeField] private TouchLayoutSettings settings;

        [Tooltip("Base visual del pad (hija de esta zona).")]
        [SerializeField] private RectTransform stickBase;

        [Tooltip("Perilla visual del pad (hija de la base).")]
        [SerializeField] private RectTransform knob;

        [Tooltip("Posición de reposo de la base, en coordenadas locales de la zona, cuando no hay dedo.")]
        [SerializeField] private Vector2 restPosition = new Vector2(260f, 260f);

        /// <summary>Zona de contacto (este mismo RectTransform).</summary>
        private RectTransform zone;

        /// <summary>Identificador del dedo que controla el pad; −1 si no hay ninguno.</summary>
        private int activePointerId = -1;

        /// <summary>Origen del gesto en coordenadas locales de la zona.</summary>
        private Vector2 origin;

        /// <summary>Lector de entrada para el correr virtual.</summary>
        private InputReader reader;

        /// <inheritdoc />
        protected override string controlPathInternal
        {
            get => stickControlPath;
            set => stickControlPath = value;
        }

        /// <summary>Radio del pad en unidades del lienzo.</summary>
        private float Radius => settings != null ? settings.StickRadius : 110f;

        /// <summary>Umbral vertical de correr.</summary>
        private float SprintThreshold => settings != null ? settings.SprintThreshold : 0.9f;

        /// <summary>Cachea referencias.</summary>
        private void Awake()
        {
            zone = (RectTransform)transform;
        }

        /// <summary>Activa el mando virtual y deja el pad en reposo.</summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            ServiceLocator.TryGet(out reader);
            ResetStick();
        }

        /// <summary>Libera el dedo y el correr virtual.</summary>
        protected override void OnDisable()
        {
            ResetStick();
            base.OnDisable();
        }

        /// <summary>Coloca la base bajo el dedo y comienza el gesto.</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId != -1) return;
            if (!TryGetLocalPoint(eventData, out Vector2 local)) return;

            activePointerId = eventData.pointerId;
            origin = local;
            if (stickBase != null) stickBase.localPosition = local;
            if (knob != null) knob.localPosition = Vector2.zero;
            SendValueToControl(Vector2.zero);
        }

        /// <summary>Calcula el desplazamiento limitado y lo envía al mando virtual.</summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId) return;
            if (!TryGetLocalPoint(eventData, out Vector2 local)) return;

            float radius = Radius;
            Vector2 offset = Vector2.ClampMagnitude(local - origin, radius);
            if (knob != null) knob.localPosition = offset;

            Vector2 value = offset / radius;
            SendValueToControl(value);

            // Correr: el pad se lleva al borde superior (componente vertical ≥ umbral).
            reader?.SetVirtualSprint(value.y >= SprintThreshold);
        }

        /// <summary>Termina el gesto y devuelve el pad a reposo.</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId) return;
            ResetStick();
        }

        /// <summary>Restablece el valor, el correr virtual y la posición visual.</summary>
        private void ResetStick()
        {
            activePointerId = -1;
            if (isActiveAndEnabled) SendValueToControl(Vector2.zero);
            reader?.SetVirtualSprint(false);
            if (stickBase != null) stickBase.localPosition = restPosition;
            if (knob != null) knob.localPosition = Vector2.zero;
        }

        /// <summary>Convierte la posición del puntero a coordenadas locales de la zona.</summary>
        private bool TryGetLocalPoint(PointerEventData eventData, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                zone, eventData.position, eventData.pressEventCamera, out local);
        }
    }
}
