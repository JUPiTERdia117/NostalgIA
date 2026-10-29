// -----------------------------------------------------------------------------
// NostalgIA — Lógica de juego / Nivel
// Archivo: PromptZone.cs
// Propósito: Volumen de activación física (Trigger) para avisos, instrucciones
// y guías en pantalla. Completamente desacoplado y reutilizable en cualquier nivel.
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Player;
using UnityEngine;

namespace NostalgIA.Gameplay.Level
{
    /// <summary>
    /// Volumen de colisión trigger que notifica al HUD cuando el jugador entra o sale,
    /// mostrando u ocultando mensajes y controles configurados desde el Inspector.
    /// Reutilizable en cualquier nivel (Tutorial, Dreamcore, Invierno, etc.).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("NostalgIA/Gameplay/Prompt Zone")]
    public class PromptZone : MonoBehaviour
    {
        [Header("Modo")]
        [Tooltip("Activate: Muestra el aviso al entrar.\nComplete: Oculta el aviso al entrar.\nActivateAndCompleteOnExit: Muestra al entrar y oculta al salir.")]
        [SerializeField] private PromptZoneMode mode = PromptZoneMode.Activate;

        [Tooltip("Si se marca, el aviso solo se dispara la primera vez que el jugador entra.")]
        [SerializeField] private bool triggerOnce;

        [Header("Contenido")]
        [Tooltip("Título o encabezado principal del aviso.")]
        [SerializeField] private string title;

        [Tooltip("Descripción detallada o instrucción (opcional).")]
        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Tooltip("Controles o teclas recomendadas (opcional; ej. [Espacio] Saltar).")]
        [SerializeField] private string controls;

        [Header("Visualización en Scene View")]
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.35f);
        [SerializeField] private bool showAlways = true;

        private bool hasTriggered;
        private Collider cachedCollider;

        public PromptZoneMode Mode => mode;
        public string Title => title;
        public string Description => description;
        public string Controls => controls;

        private void Awake()
        {
            cachedCollider = GetComponent<Collider>();
            if (cachedCollider != null) cachedCollider.isTrigger = true;
        }

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnce && hasTriggered) return;
            if (!IsPlayer(other)) return;

            hasTriggered = true;

            if (mode == PromptZoneMode.Complete)
            {
                EventBus<PromptTriggered>.Raise(new PromptTriggered(null, null, null, mode, hide: true));
            }
            else
            {
                EventBus<PromptTriggered>.Raise(new PromptTriggered(title, description, controls, mode, hide: false));
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (mode != PromptZoneMode.ActivateAndCompleteOnExit) return;
            if (!IsPlayer(other)) return;

            EventBus<PromptTriggered>.Raise(new PromptTriggered(null, null, null, mode, hide: true));
        }

        private static bool IsPlayer(Collider other)
        {
            if (other == null) return false;
            if (other.CompareTag("Player")) return true;
            return other.GetComponentInParent<PlayerMotor>() != null;
        }

        private void OnDrawGizmos()
        {
            if (!showAlways) return;
            DrawGizmosWithColor(gizmoColor);
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmosWithColor(new Color(1f, 0.85f, 0.2f, 0.8f));
        }

        private void DrawGizmosWithColor(Color color)
        {
            Collider col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            if (col is BoxCollider box)
            {
                Gizmos.color = color;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * 2.2f));
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.color = color;
                Gizmos.DrawSphere(sphere.center, sphere.radius);
                Gizmos.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * 2.2f));
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
    }

    [System.Obsolete("Usa PromptZone.")]
    public sealed class TutorialPromptZone : PromptZone { }
}
