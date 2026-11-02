// -----------------------------------------------------------------------------
// NostalgIA — Presentación (NostalgIA.Presentation)
// Archivo: DebrisBurstEmitter.cs
// Propósito: estallido de fragmentos en pool al romperse un objeto
// (sustituye a CreatePrimitive + Destroy del prototipo).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Events;
using NostalgIA.Core.Pooling;
using NostalgIA.Core.Services;
using UnityEngine;

namespace NostalgIA.Presentation.VFX
{
    /// <summary>
    /// Escucha <see cref="BreakableDestroyed"/> y emite fragmentos físicos tomados de un pool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El prototipo creaba cada fragmento con <c>GameObject.CreatePrimitive</c> y lo destruía a
    /// los 1,2–2,2 s, lo que asignaba memoria y provocaba picos del recolector en cada rotura.
    /// Ahora los fragmentos se precrean en <c>PoolService</c> y vuelven al pool solos al expirar
    /// (<see cref="PooledObject.AutoReleaseAfter"/>).
    /// </para>
    /// <para>
    /// Cinemática de cada fragmento (valores del prototipo): posición inicial dentro de una esfera
    /// de 0,25 m; impulso <c>J = ‖û + 0,6·ŷ‖⁻¹(û + 0,6·ŷ)·U(4, 8)</c> N·s con <c>û</c> aleatorio en
    /// la esfera unitaria (sesgo hacia arriba), y velocidad angular aleatoria de hasta 12 rad/s.
    /// Los fragmentos están en la capa <c>Debris</c>, que no colisiona con el jugador, los
    /// enemigos ni los proyectiles.
    /// </para>
    /// </remarks>
    public sealed class DebrisBurstEmitter : MonoBehaviour
    {
        [Tooltip("Pool de fragmentos (prefab con PooledObject y DebrisFragment).")]
        [SerializeField] private PoolDefinition debrisPool;

        [Tooltip("Límite de fragmentos por estallido (protege el presupuesto en móvil).")]
        [SerializeField, Range(1, 32)] private int maxFragmentsPerBurst = 8;

        [Tooltip("Vida mínima y máxima de cada fragmento, en segundos.")]
        [SerializeField] private Vector2 lifetimeRange = new Vector2(1.2f, 2.2f);

        private PoolService pools;

        /// <summary>Suscripción.</summary>
        private void OnEnable() => EventBus<BreakableDestroyed>.Subscribe(OnBreakableDestroyed);

        /// <summary>Cancela la suscripción.</summary>
        private void OnDisable() => EventBus<BreakableDestroyed>.Unsubscribe(OnBreakableDestroyed);

        /// <summary>Emite el estallido.</summary>
        private void OnBreakableDestroyed(in BreakableDestroyed evt)
        {
            if (debrisPool == null) return;
            if (pools == null && !ServiceLocator.TryGet(out pools)) return;

            int count = Mathf.Min(evt.FragmentCount, maxFragmentsPerBurst);
            float sizeFactor = Mathf.Clamp(evt.Size, 0.5f, 2f);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = evt.Position + Random.insideUnitSphere * 0.25f;
                PooledObject item = pools.Spawn(debrisPool, position, Random.rotationUniform);
                if (item == null) return;

                item.AutoReleaseAfter = Random.Range(lifetimeRange.x, lifetimeRange.y);
                item.transform.localScale = Vector3.one * (Random.Range(0.12f, 0.28f) * sizeFactor);

                if (item.TryGetComponent(out DebrisFragment fragment))
                {
                    Vector3 impulse = (Random.onUnitSphere + Vector3.up * 0.6f).normalized * Random.Range(4f, 8f);
                    fragment.Launch(evt.Material, impulse, Random.insideUnitSphere * 12f);
                }
            }
        }
    }
}
