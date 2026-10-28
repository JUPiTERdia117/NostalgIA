// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: AIMonitorTarget.cs
// Propósito: sección de monitor en la pared de la arena central (IA) que se
// activa al derrotar una oleada de enemigos y se destruye al dispararle un
// objeto con la aspiradora.
// -----------------------------------------------------------------------------

using NostalgIA.Core;
using NostalgIA.Core.Damage;
using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Tools;
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Monitor blindado de la IA en la pared del centro del laberinto.
    /// Permanece protegido durante cada oleada; cuando la oleada cae, abre su compuerta
    /// y enciende su pantalla para que el jugador le dispare un objeto absorbido.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class AIMonitorTarget : MonoBehaviour, IDamageable, IImpactReceiver
    {
        [Header("Identidad")]
        [Tooltip("Índice de este monitor dentro del combate contra la IA (0, 1 o 2).")]
        [SerializeField] private int monitorIndex;

        [Header("Referencias visuales")]
        [Tooltip("Renderer de la pantalla del monitor cuyo color cambia según el estado.")]
        [SerializeField] private Renderer screenRenderer;
        [Tooltip("Compuerta o blindaje que se desplaza al quedar vulnerable el monitor.")]
        [SerializeField] private Transform shutterTransform;
        [Tooltip("Desplazamiento local de la compuerta cuando el monitor se abre.")]
        [SerializeField] private Vector3 shutterOpenLocalOffset = new Vector3(0f, 3.4f, 0f);
        [Tooltip("Indicador luminoso o baliza que se enciende mientras el monitor es vulnerable.")]
        [SerializeField] private GameObject vulnerableBeacon;

        [Header("Colores de estado")]
        [Tooltip("Color de la pantalla mientras está bloqueada esperando a que termine la oleada.")]
        [SerializeField] private Color lockedColor = new Color(0.08f, 0.12f, 0.18f, 1f);
        [Tooltip("Color base de la pantalla cuando se activa y puede recibir disparos.")]
        [SerializeField] private Color vulnerableColorA = new Color(0.0f, 0.95f, 1.0f, 1f);
        [Tooltip("Color alterno del pulso cuando está vulnerable para atraer la mirada del jugador.")]
        [SerializeField] private Color vulnerableColorB = new Color(1.0f, 0.32f, 0.08f, 1f);
        [Tooltip("Color de la pantalla rota tras ser golpeada por un proyectil.")]
        [SerializeField] private Color destroyedColor = new Color(0.03f, 0.03f, 0.04f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock propBlock;
        private Vector3 shutterClosedLocalPos;
        private Vector3 shutterTargetLocalPos;
        private bool isVulnerable;
        private bool isDestroyed;

        /// <summary>Indica si el monitor está expuesto y listo para recibir un disparo.</summary>
        public bool IsVulnerable => isVulnerable && !isDestroyed;

        /// <summary>Indica si el monitor ya fue destruido por el jugador.</summary>
        public bool IsDestroyed => isDestroyed;

        /// <inheritdoc />
        public bool IsDead => isDestroyed;

        /// <summary>Índice del monitor (0..2).</summary>
        public int MonitorIndex => monitorIndex;

        /// <summary>Configura referencias desde el constructor de escena.</summary>
        public void Configure(int index, Renderer screen, Transform shutter, Vector3 openOffset, GameObject beacon)
        {
            monitorIndex = index;
            screenRenderer = screen;
            shutterTransform = shutter;
            shutterOpenLocalOffset = openOffset;
            vulnerableBeacon = beacon;
        }

        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
            if (shutterTransform != null)
            {
                shutterClosedLocalPos = shutterTransform.localPosition;
                shutterTargetLocalPos = shutterClosedLocalPos;
            }
            SetVulnerable(false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (shutterTransform != null)
            {
                shutterTransform.localPosition = Vector3.MoveTowards(
                    shutterTransform.localPosition,
                    shutterTargetLocalPos,
                    6.5f * dt);
            }

            if (isVulnerable && !isDestroyed && screenRenderer != null)
            {
                // Aquí usé una onda senoidal rápida para alternar entre cian y naranja de alerta,
                // de modo que el jugador identifique enseguida qué sección de la pared debe golpear.
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7.5f);
                ApplyScreenColor(Color.Lerp(vulnerableColorA, vulnerableColorB, pulse));
            }
        }

        /// <summary>Activa o bloquea la vulnerabilidad del monitor.</summary>
        public void SetVulnerable(bool vulnerable)
        {
            if (isDestroyed) return;
            isVulnerable = vulnerable;

            if (shutterTransform != null)
            {
                shutterTargetLocalPos = vulnerable
                    ? shutterClosedLocalPos + shutterOpenLocalOffset
                    : shutterClosedLocalPos;
            }

            if (vulnerableBeacon != null)
            {
                vulnerableBeacon.SetActive(vulnerable);
            }

            ApplyScreenColor(vulnerable ? vulnerableColorA : lockedColor);
        }

        /// <inheritdoc />
        public void ReceiveImpact(Vector3 point, float force)
        {
            if (!IsVulnerable) return;
            ShatterMonitor(point);
        }

        /// <inheritdoc />
        public void TakeDamage(in DamageInfo info)
        {
            if (!IsVulnerable) return;
            ShatterMonitor(info.SourcePosition);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsVulnerable) return;

            Absorbable absorbable = collision.gameObject.GetComponentInParent<Absorbable>();
            if (absorbable != null && !absorbable.IsDestroyed)
            {
                // Acepto tanto objetos recién disparados (IsThrown) como objetos lanzados con velocidad
                // suficiente que choquen contra el cristal del monitor.
                if (absorbable.IsThrown || collision.relativeVelocity.sqrMagnitude >= 2.2f)
                {
                    Vector3 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                    absorbable.BreakApart(hitPoint);
                    ShatterMonitor(hitPoint);
                    return;
                }
            }

            if (collision.gameObject.layer == Layers.PlayerProjectile)
            {
                Vector3 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                ShatterMonitor(hitPoint);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsVulnerable) return;

            Absorbable absorbable = other.GetComponentInParent<Absorbable>();
            if (absorbable != null && !absorbable.IsDestroyed)
            {
                Rigidbody rb = absorbable.Body;
                if (absorbable.IsThrown || (rb != null && rb.linearVelocity.sqrMagnitude >= 3.0f))
                {
                    Vector3 hitPoint = other.ClosestPoint(transform.position);
                    absorbable.BreakApart(hitPoint);
                    ShatterMonitor(hitPoint);
                }
            }
        }

        private void ShatterMonitor(Vector3 hitPoint)
        {
            if (isDestroyed) return;
            isDestroyed = true;
            isVulnerable = false;

            if (vulnerableBeacon != null) vulnerableBeacon.SetActive(false);
            ApplyScreenColor(destroyedColor);

            // Inclino ligeramente el monitor roto y emito fragmentos usando el sistema de rotura existente.
            transform.localRotation *= Quaternion.Euler(7f, 0f, -5f);
            Material mat = screenRenderer != null ? screenRenderer.sharedMaterial : null;
            EventBus<BreakableDestroyed>.Raise(new BreakableDestroyed(hitPoint, 10, mat, 2.2f));
        }

        private void ApplyScreenColor(Color color)
        {
            if (screenRenderer == null) return;
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            screenRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorId, color);
            propBlock.SetColor(ColorId, color);
            screenRenderer.SetPropertyBlock(propBlock);
        }
    }
}
