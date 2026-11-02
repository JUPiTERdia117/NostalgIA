// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: PoolService.cs
// Propósito: servicio de pools de GameObjects sobre UnityEngine.Pool.ObjectPool.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace NostalgIA.Core.Pooling
{
    /// <summary>
    /// Administra un pool por <see cref="PoolDefinition"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Usamos <see cref="ObjectPool{T}"/> del motor (pila interna con capacidad reservada) para
    /// que tomar y devolver objetos sea O(1) y no asigne memoria una vez alcanzado el tamaño de
    /// trabajo. Las funciones de creación se construyen una vez por pool.
    /// </para>
    /// <para>
    /// Todos los objetos cuelgan de un contenedor persistente (<c>DontDestroyOnLoad</c>) y se
    /// mueven en coordenadas de mundo: el contenedor está en el origen sin rotación ni escala,
    /// por lo que su transformación es la identidad. Así sobreviven a los cambios de escena y el
    /// pool nunca guarda referencias a objetos destruidos.
    /// </para>
    /// </remarks>
    public sealed class PoolService
    {
        /// <summary>Pools por definición.</summary>
        private readonly Dictionary<PoolDefinition, ObjectPool<PooledObject>> pools =
            new Dictionary<PoolDefinition, ObjectPool<PooledObject>>(16);

        /// <summary>Objetos fuera del pool, para poder devolverlos todos al cambiar de escena.</summary>
        private readonly List<PooledObject> active = new List<PooledObject>(128);

        /// <summary>Contenedor persistente de los objetos.</summary>
        private readonly Transform container;

        /// <summary>Crea el servicio con su contenedor persistente.</summary>
        /// <param name="containerParent">Padre del contenedor (normalmente la raíz persistente de servicios).</param>
        public PoolService(Transform containerParent)
        {
            var root = new GameObject("[Pools]");
            container = root.transform;
            container.SetParent(containerParent, false);
            container.localPosition = Vector3.zero;
            container.localRotation = Quaternion.identity;
            container.localScale = Vector3.one;
        }

        /// <summary>Cantidad de objetos actualmente en uso.</summary>
        public int ActiveCount => active.Count;

        /// <summary>Crea el pool (si no existe) e instancia su tamaño inicial.</summary>
        /// <param name="definition">Definición del pool.</param>
        public void Prewarm(PoolDefinition definition)
        {
            ObjectPool<PooledObject> pool = GetOrCreatePool(definition);
            if (pool == null) return;

            int missing = definition.InitialSize - pool.CountInactive;
            if (missing <= 0) return;

            // Se toman y devuelven en dos pasadas para que el pool cree objetos nuevos en lugar de
            // reutilizar el mismo. El arreglo temporal solo existe durante la carga.
            var temp = new PooledObject[missing];
            for (int i = 0; i < missing; i++) temp[i] = pool.Get();
            for (int i = 0; i < missing; i++) pool.Release(temp[i]);
        }

        /// <summary>Toma un objeto del pool, lo coloca y lo activa.</summary>
        /// <param name="definition">Definición del pool.</param>
        /// <param name="position">Posición en el mundo.</param>
        /// <param name="rotation">Rotación en el mundo.</param>
        /// <returns>El objeto activo, o <c>null</c> si la definición no es válida.</returns>
        public PooledObject Spawn(PoolDefinition definition, Vector3 position, Quaternion rotation)
        {
            ObjectPool<PooledObject> pool = GetOrCreatePool(definition);
            if (pool == null) return null;

            PooledObject item = pool.Get();
            // Se coloca antes de activar para que la física no registre un salto desde la posición anterior.
            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            active.Add(item);
            item.NotifySpawned();
            return item;
        }

        /// <summary>Devuelve un objeto a su pool.</summary>
        /// <param name="item">Objeto a devolver.</param>
        public void Release(PooledObject item)
        {
            if (item == null || !item.IsSpawned) return;
            item.NotifyDespawned();
            int index = active.IndexOf(item);
            if (index >= 0)
            {
                // Eliminación por intercambio con el último: O(1) y sin desplazar el arreglo.
                int last = active.Count - 1;
                active[index] = active[last];
                active.RemoveAt(last);
            }

            if (item.Definition != null && pools.TryGetValue(item.Definition, out ObjectPool<PooledObject> pool))
                pool.Release(item);
            else
                Object.Destroy(item.gameObject);
        }

        /// <summary>Devuelve todos los objetos activos (al descargar una escena).</summary>
        public void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count) continue;
                PooledObject item = active[i];
                if (item == null) { active.RemoveAt(i); continue; }
                Release(item);
            }
        }

        /// <summary>Obtiene o construye el pool de una definición.</summary>
        private ObjectPool<PooledObject> GetOrCreatePool(PoolDefinition definition)
        {
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogWarning("[PoolService] Definición de pool nula o sin prefab.");
                return null;
            }

            if (pools.TryGetValue(definition, out ObjectPool<PooledObject> pool)) return pool;

            PooledObject prefab = definition.Prefab;
            pool = new ObjectPool<PooledObject>(
                createFunc: () =>
                {
                    PooledObject created = Object.Instantiate(prefab, container);
                    created.gameObject.SetActive(false);
                    created.Bind(this, definition);
                    return created;
                },
                actionOnGet: null,
                actionOnRelease: released =>
                {
                    released.gameObject.SetActive(false);
                    released.transform.SetParent(container, true);
                },
                actionOnDestroy: destroyed =>
                {
                    if (destroyed != null) Object.Destroy(destroyed.gameObject);
                },
                collectionCheck: false,
                defaultCapacity: Mathf.Max(1, definition.InitialSize),
                maxSize: definition.MaxSize);

            pools.Add(definition, pool);
            return pool;
        }
    }
}
