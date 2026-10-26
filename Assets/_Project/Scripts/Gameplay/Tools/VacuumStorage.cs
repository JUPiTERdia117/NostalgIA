// -----------------------------------------------------------------------------
// NostalgIA — Jugabilidad (NostalgIA.Gameplay)
// Archivo: VacuumStorage.cs
// Propósito: almacén FIFO de la aspiradora (GDD 2.3 A, TDD §6.3, tarea F1-A09).
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace NostalgIA.Gameplay.Tools
{
    /// <summary>
    /// Almacén FIFO: el primer objeto absorbido es el primero en dispararse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El prototipo usaba una pila (LIFO), contraria al GDD. Una <see cref="Queue{T}"/> con
    /// capacidad reservada ofrece encolar, desencolar y consultar el frente en O(1) sin asignar
    /// memoria mientras no se supere la capacidad inicial.
    /// </para>
    /// <para>
    /// Si la salida está bloqueada, el objeto permanece al frente de la cola y no se consume:
    /// por eso el disparo primero consulta con <see cref="TryPeek"/> y solo retira con
    /// <see cref="Dequeue"/> tras comprobar la salida.
    /// </para>
    /// </remarks>
    public sealed class VacuumStorage
    {
        private readonly Queue<Absorbable> queue;

        /// <summary>Objetos almacenados.</summary>
        public int Count => queue.Count;

        /// <summary>Capacidad actual.</summary>
        public int Capacity { get; private set; }

        /// <summary>Indica si no admite más objetos.</summary>
        public bool IsFull => queue.Count >= Capacity;

        /// <summary>Llenado normalizado: <c>n / capacidad</c>.</summary>
        public float Fill01 => Capacity > 0 ? (float)queue.Count / Capacity : 0f;

        /// <summary>Crea el almacén con su capacidad inicial.</summary>
        /// <param name="capacity">Capacidad (mínimo 1).</param>
        public VacuumStorage(int capacity)
        {
            Capacity = Mathf.Max(1, capacity);
            queue = new Queue<Absorbable>(Capacity);
        }

        /// <summary>Encola un objeto si hay espacio.</summary>
        /// <returns><c>false</c> si el almacén está lleno o el objeto es nulo.</returns>
        public bool TryStore(Absorbable item)
        {
            if (item == null || queue.Count >= Capacity) return false;
            queue.Enqueue(item);
            return true;
        }

        /// <summary>Consulta el frente sin retirarlo.</summary>
        public bool TryPeek(out Absorbable item) => queue.TryPeek(out item);

        /// <summary>Retira el frente.</summary>
        public Absorbable Dequeue() => queue.Dequeue();

        /// <summary>Aplica una mejora de capacidad sin perder objetos almacenados.</summary>
        /// <param name="capacity">Capacidad nueva; nunca menor que los objetos ya guardados.</param>
        public void SetCapacity(int capacity) => Capacity = Mathf.Max(Mathf.Max(1, capacity), queue.Count);

        /// <summary>
        /// Descarta del frente los objetos destruidos externamente (por ejemplo, por una recarga
        /// de escena), para que <see cref="TryPeek"/> nunca devuelva una referencia muerta.
        /// </summary>
        public void PurgeDestroyedFront()
        {
            while (queue.Count > 0 && queue.Peek() == null) queue.Dequeue();
        }
    }
}
