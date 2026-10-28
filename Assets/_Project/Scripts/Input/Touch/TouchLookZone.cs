// -----------------------------------------------------------------------------
// NostalgIA — Entrada (NostalgIA.Input)
// Archivo: TouchLookZone.cs
// Propósito: zona de arrastre para mirar en la mitad derecha de la pantalla
// (TDD §7.5, tarea F1-A05).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Services;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NostalgIA.Input.TouchControls
{
    /// <summary>
    /// Convierte el arrastre de un dedo en la mitad derecha en giro de cámara.
    /// </summary>
    /// <remarks>
    /// <para>
    /// En este módulo se decidió usar los eventos de puntero de uGUI sobre una imagen transparente
    /// que cubre la mitad derecha, en lugar de leer <c>EnhancedTouch</c> directamente: los botones
    /// del HUD están por encima en el orden de dibujo y capturan sus propios toques, de modo que la
    /// condición «arrastre fuera de botones» del TDD se cumple sin lógica de exclusión adicional.
    /// </para>
    /// <para>
    /// El delta del puntero (píxeles de pantalla en este frame) se entrega a
    /// <see cref="InputReader.AddTouchLookDelta"/>, que lo normaliza por la altura de pantalla y lo
    /// suma a las demás fuentes de mirada. <see cref="PointerEventData"/> lo reutiliza el
    /// EventSystem entre frames, por lo que el gesto no asigna memoria.
    /// </para>
    /// </remarks>
    public sealed class TouchLookZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>Dedo que controla la mirada; −1 si no hay ninguno.</summary>
        private int activePointerId = -1;

        /// <summary>Lector de entrada.</summary>
        private InputReader reader;

        /// <summary>Obtiene el lector registrado.</summary>
        private void OnEnable()
        {
            ServiceLocator.TryGet(out reader);
            activePointerId = -1;
        }

        /// <summary>Libera el dedo.</summary>
        private void OnDisable() => activePointerId = -1;

        /// <summary>Toma el primer dedo que toca la zona.</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId == -1) activePointerId = eventData.pointerId;
        }

        /// <summary>Entrega el desplazamiento del dedo al lector.</summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId) return;
            reader?.AddTouchLookDelta(eventData.delta);
        }

        /// <summary>Suelta el dedo.</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId) activePointerId = -1;
        }
    }
}
