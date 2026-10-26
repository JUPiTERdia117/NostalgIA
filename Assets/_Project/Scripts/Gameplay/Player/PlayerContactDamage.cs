// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: PlayerContactDamage.cs
// Propósito: aplicar al jugador el daño de los peligros que toca (enemigos,
// lava electrónica) (TDD §5.2, tarea F1-A11).
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using NostalgIA.Core;
using NostalgIA.Core.Damage;
using UnityEngine;

namespace NostalgIA.Gameplay.Player
{
    /// <summary>
    /// Traduce los contactos del <see cref="CharacterController"/> y de los triggers en golpes
    /// para <see cref="PlayerHealth"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>OnControllerColliderHit</c> se invoca por cada collider tocado en cada <c>Move</c>,
    /// incluido el suelo, es decir, varias veces por frame. Buscar componentes en cada llamada
    /// sería costoso, así que en este módulo se decidió resolver cada collider una sola vez y
    /// guardar el resultado en un diccionario indexado por <see cref="Object.GetInstanceID"/>:
    /// las consultas siguientes son O(1) y no asignan memoria. Los colliders que no son
    /// peligrosos (el suelo) se guardan con valor <c>null</c>.
    /// </para>
    /// <para>
    /// Además de <see cref="IContactHazard"/>, se respetan las etiquetas del prototipo:
    /// <c>Lava</c> (muerte instantánea) y <c>Enemy</c> (un corazón), para objetos que aún no
    /// implementan el contrato.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerContactDamage : MonoBehaviour
    {
        /// <summary>Peligro genérico para objetos con la etiqueta <c>Lava</c>.</summary>
        private sealed class TagLavaHazard : IContactHazard
        {
            public bool TryGetContactDamage(Vector3 contactPoint, out DamageInfo info)
            {
                info = DamageInfo.Instakill(contactPoint);
                return true;
            }
        }

        /// <summary>Peligro genérico para objetos con la etiqueta <c>Enemy</c>.</summary>
        private sealed class TagEnemyHazard : IContactHazard
        {
            public bool TryGetContactDamage(Vector3 contactPoint, out DamageInfo info)
            {
                info = DamageInfo.Normal(1, contactPoint);
                return true;
            }
        }

        private static readonly IContactHazard LavaByTag = new TagLavaHazard();
        private static readonly IContactHazard EnemyByTag = new TagEnemyHazard();

        /// <summary>Caché collider → peligro (o <c>null</c> si no es peligroso).</summary>
        private readonly Dictionary<int, IContactHazard> cache = new Dictionary<int, IContactHazard>(256);

        private PlayerHealth health;

        /// <summary>Cachea la vida.</summary>
        private void Awake() => health = GetComponent<PlayerHealth>();

        /// <summary>Contacto por movimiento del CharacterController.</summary>
        private void OnControllerColliderHit(ControllerColliderHit hit) => Evaluate(hit.collider, hit.point);

        /// <summary>Entrada a un trigger peligroso.</summary>
        private void OnTriggerEnter(Collider other) => Evaluate(other, other.ClosestPoint(transform.position));

        /// <summary>Permanencia en un trigger peligroso (por ejemplo, tras la invulnerabilidad).</summary>
        private void OnTriggerStay(Collider other) => Evaluate(other, transform.position);

        /// <summary>Resuelve el peligro del collider (con caché) y aplica el golpe.</summary>
        private void Evaluate(Collider other, Vector3 point)
        {
            if (other == null || health.IsDead) return;

            int id = other.GetInstanceID();
            if (!cache.TryGetValue(id, out IContactHazard hazard))
            {
                hazard = Resolve(other);
                cache[id] = hazard;
            }

            if (hazard == null) return;
            // Un peligro destruido (enemigo eliminado) compara igual a null como objeto de Unity.
            if (hazard is Object unityObject && unityObject == null)
            {
                cache[id] = null;
                return;
            }

            if (hazard.TryGetContactDamage(point, out DamageInfo info)) health.TakeDamage(in info);
        }

        /// <summary>Busca el peligro asociado a un collider (solo la primera vez).</summary>
        private static IContactHazard Resolve(Collider other)
        {
            IContactHazard hazard = other.GetComponentInParent<IContactHazard>();
            if (hazard != null) return hazard;
            if (other.CompareTag(Tags.Lava)) return LavaByTag;
            if (other.CompareTag(Tags.Enemy)) return EnemyByTag;
            return null;
        }

        /// <summary>Vacía la caché (al cambiar de escena los identificadores dejan de ser válidos).</summary>
        public void ClearCache() => cache.Clear();
    }
}
