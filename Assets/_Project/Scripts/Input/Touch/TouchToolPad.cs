// -----------------------------------------------------------------------------
// NostalgIA — Entrada (NostalgIA.Input)
// Archivo: TouchToolPad.cs
// Propósito: pad derecho de herramienta cuyo gesto cambia según la herramienta
// activa (GDD 2.3, TDD §7.5, tarea F1-A05).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Services;
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
    /// un toque más corto = <c>Fire</c> al soltar.</item>
    /// <item><b>Gancho:</b> tocar = <c>Grapple</c> (volver a tocar cancela).</item>
    /// </list>
    /// <para>
    /// La herramienta activa se conoce por el evento <see cref="ToolSwitched"/>, por lo que este
    /// componente de entrada no depende del ensamblado de jugabilidad. Los valores se inyectan en
    /// <see cref="InputReader"/>, que los presenta a la jugabilidad igual que los de una acción.
    /// </para>
    /// <para>
    /// Las pulsaciones se miden con <see cref="Time.unscaledTime"/> para que una pausa breve de la
    /// escala de tiempo no convierta un toque en «mantener».
    /// </para>
    /// </remarks>
    public sealed class TouchToolPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("Parámetros compartidos de los controles táctiles.")]
        [SerializeField] private TouchLayoutSettings settings;

        [Tooltip("Imagen del icono del pad (opcional).")]
        [SerializeField] private Image icon;

        [Tooltip("Icono mostrado con la aspiradora activa (opcional).")]
        [SerializeField] private Sprite vacuumIcon;

        [Tooltip("Icono mostrado con el gancho activo (opcional).")]
        [SerializeField] private Sprite grappleIcon;

        /// <summary>Herramienta activa según el último <see cref="ToolSwitched"/>.</summary>
        private ToolId activeTool = ToolId.Vacuum;

        /// <summary>Dedo que mantiene el pad; −1 si no hay ninguno.</summary>
        private int activePointerId = -1;

        /// <summary>Instante en que empezó la pulsación.</summary>
        private float pressedAt;

        /// <summary>Indica si la pulsación ya superó el umbral y se convirtió en succión.</summary>
        private bool holding;

        /// <summary>Lector de entrada.</summary>
        private InputReader reader;

        /// <summary>Umbral de mantener.</summary>
        private float HoldThreshold => settings != null ? settings.HoldThreshold : 0.2f;

        /// <summary>Suscripción y estado inicial.</summary>
        private void OnEnable()
        {
            ServiceLocator.TryGet(out reader);
            EventBus<ToolSwitched>.Subscribe(OnToolSwitched);
            CancelPress();
            RefreshIcon();
        }

        /// <summary>Cancela la pulsación y la suscripción.</summary>
        private void OnDisable()
        {
            EventBus<ToolSwitched>.Unsubscribe(OnToolSwitched);
            CancelPress();
        }

        /// <summary>Registra el inicio de la pulsación; con el gancho dispara de inmediato.</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId != -1) return;
            activePointerId = eventData.pointerId;
            pressedAt = Time.unscaledTime;
            holding = false;

            if (activeTool == ToolId.Grapple) reader?.InjectGrapple();
        }

        /// <summary>Convierte la pulsación en succión al superar el umbral (solo aspiradora).</summary>
        private void Update()
        {
            if (activePointerId == -1 || holding || activeTool != ToolId.Vacuum) return;
            if (Time.unscaledTime - pressedAt < HoldThreshold) return;
            holding = true;
            reader?.SetVirtualAbsorb(true);
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

        /// <summary>Termina cualquier pulsación en curso.</summary>
        private void CancelPress()
        {
            if (holding) reader?.SetVirtualAbsorb(false);
            holding = false;
            activePointerId = -1;
        }

        /// <summary>Actualiza el modo del pad al cambiar de herramienta.</summary>
        private void OnToolSwitched(in ToolSwitched evt)
        {
            CancelPress();
            activeTool = evt.Tool;
            RefreshIcon();
        }

        /// <summary>Muestra el icono de la herramienta activa.</summary>
        private void RefreshIcon()
        {
            if (icon == null) return;
            Sprite sprite = activeTool == ToolId.Vacuum ? vacuumIcon : grappleIcon;
            if (sprite != null) icon.sprite = sprite;
        }
    }
}
