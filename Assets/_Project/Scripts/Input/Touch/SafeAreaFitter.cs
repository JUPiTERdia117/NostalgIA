// -----------------------------------------------------------------------------
// NostalgIA — Entrada (NostalgIA.Input)
// Archivo: SafeAreaFitter.cs
// Propósito: ajustar un panel al área segura de la pantalla (recortes, bordes
// redondeados).
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Input.TouchControls
{
    /// <summary>
    /// Ajusta las anclas del <see cref="RectTransform"/> para que coincida con
    /// <see cref="Screen.safeArea"/>.
    /// </summary>
    /// <remarks>
    /// Las anclas se expresan en fracciones de la pantalla:
    /// <c>anchorMin = safeArea.min / (ancho, alto)</c> y <c>anchorMax = safeArea.max / (ancho, alto)</c>,
    /// con desplazamientos nulos. Así los controles hijos, anclados a las esquinas del panel,
    /// quedan fuera de los recortes de cámara en cualquier teléfono. El área segura solo se
    /// recalcula cuando cambia (rotación o cambio de resolución); la comparación cada frame es
    /// de dos <c>Rect</c> y dos enteros, sin asignaciones.
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>Panel ajustado.</summary>
        private RectTransform panel;

        /// <summary>Último área segura aplicada.</summary>
        private Rect appliedArea;

        /// <summary>Última resolución aplicada.</summary>
        private Vector2Int appliedResolution;

        /// <summary>Aplica el área segura al activarse.</summary>
        private void OnEnable()
        {
            panel = (RectTransform)transform;
            Apply(true);
        }

        /// <summary>Comprueba cambios de área segura o resolución.</summary>
        private void Update() => Apply(false);

        /// <summary>Recalcula las anclas si el área segura cambió.</summary>
        /// <param name="force">Aplica aunque no haya cambios.</param>
        private void Apply(bool force)
        {
            Rect area = Screen.safeArea;
            var resolution = new Vector2Int(Screen.width, Screen.height);
            if (!force && area == appliedArea && resolution == appliedResolution) return;
            if (resolution.x <= 0 || resolution.y <= 0) return;

            appliedArea = area;
            appliedResolution = resolution;

            Vector2 min = area.position;
            Vector2 max = area.position + area.size;
            min.x /= resolution.x;
            min.y /= resolution.y;
            max.x /= resolution.x;
            max.y /= resolution.y;

            panel.anchorMin = min;
            panel.anchorMax = max;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
        }
    }
}
