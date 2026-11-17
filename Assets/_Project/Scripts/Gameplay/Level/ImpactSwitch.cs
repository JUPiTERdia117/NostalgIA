// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: ImpactSwitch.cs
// Propósito: interruptor que se activa al golpearlo con un objeto disparado y
// mueve un mecanismo (por ejemplo, abre una puerta).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Damage;
using NostalgIA.Core.Lifecycle;
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Interruptor de impacto: cuando un objeto lanzado lo golpea con fuerza suficiente, se
    /// activa y desplaza el mecanismo enlazado hasta su posición abierta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Absorbable</c> avisa a cualquier <see cref="IImpactReceiver"/> que golpea, con la fuerza
    /// estimada F = m·a. Este componente solo compara esa fuerza con <see cref="minForce"/>.
    /// </para>
    /// <para>
    /// El color del interruptor cambia con un <see cref="MaterialPropertyBlock"/>: no se crea ningún
    /// material nuevo. Al reaparecer el jugador, el interruptor y el mecanismo vuelven a su estado
    /// inicial para que el acertijo se pueda repetir.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ImpactSwitch : MonoBehaviour, IImpactReceiver, IResettable
    {
        [Tooltip("Fuerza mínima del impacto para activarlo, en newtons.")]
        [SerializeField, Min(0f)] private float minForce = 150f;
        [Tooltip("Mecanismo que se mueve al activarse (puerta, plataforma…).")]
        [SerializeField] private Transform mechanism;
        [Tooltip("Desplazamiento del mecanismo al abrirse, en coordenadas locales de su padre.")]
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 4f, 0f);
        [Tooltip("Velocidad del mecanismo, en metros por segundo.")]
        [SerializeField, Min(0.1f)] private float speed = 3f;
        [Tooltip("Color del interruptor antes y después de activarse.")]
        [SerializeField] private Color idleColor = new Color(1f, 0.35f, 0.25f);
        [SerializeField] private Color activeColor = new Color(0.25f, 1f, 0.45f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Renderer switchRenderer;
        private MaterialPropertyBlock block;
        private Vector3 closedPosition;
        private bool active;

        /// <summary>Indica si ya se activó.</summary>
        public bool IsActive => active;

        private void Awake()
        {
            switchRenderer = GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            if (mechanism != null) closedPosition = mechanism.localPosition;
            ResettableRegistry.Register(this);
            ApplyColor();
        }

        private void OnDestroy() => ResettableRegistry.Unregister(this);

        /// <inheritdoc />
        public void ReceiveImpact(Vector3 point, float force)
        {
            if (active || force < minForce) return;
            active = true;
            ApplyColor();
        }

        /// <summary>Lleva el mecanismo hacia su posición abierta o cerrada.</summary>
        private void Update()
        {
            if (mechanism == null) return;
            Vector3 target = active ? closedPosition + openOffset : closedPosition;
            mechanism.localPosition = Vector3.MoveTowards(mechanism.localPosition, target, speed * Time.deltaTime);
        }

        /// <inheritdoc />
        public void ResetState()
        {
            active = false;
            if (mechanism != null) mechanism.localPosition = closedPosition;
            ApplyColor();
        }

        /// <summary>Pinta el interruptor según su estado (URP usa _BaseColor; el resto, _Color).</summary>
        private void ApplyColor()
        {
            if (switchRenderer == null) return;
            Color color = active ? activeColor : idleColor;
            switchRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            switchRenderer.SetPropertyBlock(block);
        }
    }
}
