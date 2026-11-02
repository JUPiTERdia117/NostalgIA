// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: IResettable.cs
// Propósito: reinicio de objetos de la estación al reaparecer el jugador sin
// recargar la escena.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace NostalgIA.Core.Lifecycle
{
    /// <summary>Objeto de la estación que vuelve a su estado inicial cuando el jugador reaparece.</summary>
    public interface IResettable
    {
        /// <summary>Restablece el estado inicial.</summary>
        void ResetState();
    }

    /// <summary>
    /// Registro de los <see cref="IResettable"/> activos. <c>RespawnService</c> lo recorre al reaparecer.
    /// </summary>
    /// <remarks>
    /// Los objetos se registran en <c>Awake</c> y se retiran en <c>OnDestroy</c> (y no en
    /// <c>OnEnable</c>/<c>OnDisable</c>), porque un objeto desactivado —por ejemplo, un
    /// absorbible roto— es precisamente el que debe reactivarse al reiniciar.
    /// La lista se reserva con capacidad inicial para que el registro de una estación típica no
    /// redimensione el arreglo interno durante la carga.
    /// </remarks>
    public static class ResettableRegistry
    {
        /// <summary>Objetos registrados.</summary>
        private static readonly List<IResettable> Items = new List<IResettable>(256);

        /// <summary>Cantidad de objetos registrados.</summary>
        public static int Count => Items.Count;

        /// <summary>Registra un objeto (se ignoran duplicados).</summary>
        public static void Register(IResettable item)
        {
            if (item != null && !Items.Contains(item)) Items.Add(item);
        }

        /// <summary>Retira un objeto.</summary>
        public static void Unregister(IResettable item)
        {
            if (item != null) Items.Remove(item);
        }

        /// <summary>
        /// Restablece todos los objetos registrados.
        /// </summary>
        /// <remarks>
        /// Se recorre de atrás hacia adelante para que, si un objeto se retira del registro durante
        /// su propio reinicio, los índices pendientes sigan siendo válidos.
        /// </remarks>
        public static void ResetAll()
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (i >= Items.Count) continue;
                IResettable item = Items[i];
                if (item is Object unityObject && unityObject == null)
                {
                    Items.RemoveAt(i);
                    continue;
                }
                item.ResetState();
            }
        }

        /// <summary>Vacía el registro.</summary>
        public static void Clear() => Items.Clear();

        /// <summary>Limpieza automática antes de cada sesión de juego.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Clear();
    }
}
