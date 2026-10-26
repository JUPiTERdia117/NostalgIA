// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: ReticlePresenter.cs
// Propósito: retícula central con sus cuatro estados y el enfriamiento de las
// herramientas (GDD 5.1, TDD §6.2 y §12).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Services;
using NostalgIA.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace NostalgIA.Presentation.UI
{
    /// <summary>
    /// Cambia color y escala de la retícula según <see cref="AimTargetChanged"/> y muestra el
    /// enfriamiento compartido como un anillo radial.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Estados (TDD §6.2): Ninguno (blanco), Absorbible (cian), Anclaje (naranja) y Fuera de
    /// alcance (gris translúcido). Además del color, cada estado cambia la escala, para que la
    /// retícula sea legible sin depender solo del color (accesibilidad, TDD §12).
    /// </para>
    /// <para>
    /// Los colores se escriben solo cuando cambia el estado, porque asignar
    /// <see cref="Graphic.color"/> marca el lienzo para reconstrucción. El anillo de enfriamiento
    /// lee <see cref="IToolState"/> cada frame únicamente mientras dura el enfriamiento.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ReticlePresenter : MonoBehaviour
    {
        [Tooltip("Marcas de la retícula que se colorean según el objetivo.")]
        [SerializeField] private Graphic[] marks = new Graphic[0];

        [Tooltip("Transform que se escala según el objetivo.")]
        [SerializeField] private RectTransform scaledRoot;

        [Tooltip("Anillo radial (Image con relleno radial) que muestra el enfriamiento restante.")]
        [SerializeField] private Image cooldownRing;

        [Header("Colores")]
        [SerializeField] private Color noneColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color absorbableColor = new Color(0f, 0.9f, 1f, 1f);
        [SerializeField] private Color anchorColor = new Color(1f, 0.55f, 0.12f, 1f);
        [SerializeField] private Color outOfRangeColor = new Color(0.6f, 0.6f, 0.6f, 0.45f);

        [Header("Escalas")]
        [SerializeField, Min(0.1f)] private float noneScale = 1f;
        [SerializeField, Min(0.1f)] private float targetScale = 1.35f;
        [SerializeField, Min(0.1f)] private float outOfRangeScale = 0.85f;

        private IToolState toolState;
        private bool cooldownVisible;

        /// <summary>Estado inicial y suscripciones.</summary>
        private void OnEnable()
        {
            EventBus<AimTargetChanged>.Subscribe(OnAimTargetChanged);
            EventBus<ToolCooldownChanged>.Subscribe(OnCooldownChanged);
            Apply(AimTargetKind.None);
            SetCooldownVisible(false);
        }

        /// <summary>Cancela suscripciones.</summary>
        private void OnDisable()
        {
            EventBus<AimTargetChanged>.Unsubscribe(OnAimTargetChanged);
            EventBus<ToolCooldownChanged>.Unsubscribe(OnCooldownChanged);
        }

        private void OnAimTargetChanged(in AimTargetChanged evt) => Apply(evt.Current);

        private void OnCooldownChanged(in ToolCooldownChanged evt) => SetCooldownVisible(evt.Remaining > 0f);

        /// <summary>Actualiza el anillo: relleno = restante / duración.</summary>
        private void LateUpdate()
        {
            if (!cooldownVisible || cooldownRing == null) return;
            if (toolState == null && !ServiceLocator.TryGet(out toolState)) return;
            float duration = toolState.CooldownDuration;
            cooldownRing.fillAmount = duration > 0f ? Mathf.Clamp01(toolState.CooldownRemaining / duration) : 0f;
        }

        /// <summary>Muestra u oculta el anillo de enfriamiento.</summary>
        private void SetCooldownVisible(bool visible)
        {
            cooldownVisible = visible;
            if (cooldownRing != null)
            {
                cooldownRing.enabled = visible;
                if (!visible) cooldownRing.fillAmount = 0f;
            }
        }

        /// <summary>Aplica color y escala del estado.</summary>
        private void Apply(AimTargetKind kind)
        {
            Color color;
            float scale;
            switch (kind)
            {
                case AimTargetKind.Absorbable: color = absorbableColor; scale = targetScale; break;
                case AimTargetKind.Anchor: color = anchorColor; scale = targetScale; break;
                case AimTargetKind.OutOfRange: color = outOfRangeColor; scale = outOfRangeScale; break;
                default: color = noneColor; scale = noneScale; break;
            }

            for (int i = 0; i < marks.Length; i++)
            {
                if (marks[i] != null) marks[i].color = color;
            }
            if (scaledRoot != null) scaledRoot.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
