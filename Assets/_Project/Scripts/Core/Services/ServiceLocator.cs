// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: ServiceLocator.cs
// Propósito: registro mínimo de servicios e interfaces de estado de solo lectura
// (TDD §4.1, tarea F1-A01).
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NostalgIA.Core.Services
{
    /// <summary>
    /// Localizador de servicios mínimo: una instancia por tipo de contrato.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lo usamos para dos fines del TDD: (1) acceder a servicios persistentes
    /// (<c>GameStateMachine</c>, <c>PoolService</c>, <c>InputReader</c>) y (2) exponer el estado
    /// continuo de alta frecuencia mediante interfaces de solo lectura
    /// (<c>IPlayerMotionState</c>, <c>IVacuumState</c>, <c>IToolState</c>) sin publicar eventos por frame.
    /// </para>
    /// <para>
    /// La implementación usa una clase estática genérica por contrato (<see cref="Slot{T}"/>): el
    /// compilador reserva un campo estático distinto para cada <c>T</c>, de modo que
    /// <see cref="TryGet{T}"/> es un acceso a campo, sin diccionarios, sin <i>hashing</i> de tipos
    /// y sin <i>boxing</i>. Recomendamos aun así cachear la referencia en <c>OnEnable</c>.
    /// </para>
    /// </remarks>
    public static class ServiceLocator
    {
        /// <summary>Funciones que vacían cada ranura usada, para el reinicio sin recarga de dominio.</summary>
        private static readonly List<Action> Clears = new List<Action>(32);

        /// <summary>Ranura de almacenamiento para el contrato <typeparamref name="T"/>.</summary>
        private static class Slot<T> where T : class
        {
            /// <summary>Instancia registrada (o <c>null</c>).</summary>
            public static T Instance;

            /// <summary>Indica si la ranura ya registró su función de limpieza.</summary>
            public static bool Tracked;

            /// <summary>Vacía la ranura.</summary>
            public static void Clear() => Instance = null;
        }

        /// <summary>Registra (o reemplaza) la instancia que atiende el contrato <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">Interfaz o clase del contrato.</typeparam>
        /// <param name="instance">Instancia que se registra.</param>
        public static void Register<T>(T instance) where T : class
        {
            if (!Slot<T>.Tracked)
            {
                Slot<T>.Tracked = true;
                Clears.Add(Slot<T>.Clear);
            }
            Slot<T>.Instance = instance;
        }

        /// <summary>
        /// Elimina el registro solo si la instancia indicada es la registrada. Así un objeto que
        /// se destruye tarde no borra el registro de su reemplazo.
        /// </summary>
        /// <typeparam name="T">Contrato.</typeparam>
        /// <param name="instance">Instancia que deja de prestar el servicio.</param>
        public static void Unregister<T>(T instance) where T : class
        {
            if (ReferenceEquals(Slot<T>.Instance, instance)) Slot<T>.Instance = null;
        }

        /// <summary>Intenta obtener la instancia registrada.</summary>
        /// <typeparam name="T">Contrato.</typeparam>
        /// <param name="instance">Instancia registrada o <c>null</c>.</param>
        /// <returns><c>true</c> si hay una instancia registrada y, si es un objeto de Unity, sigue viva.</returns>
        public static bool TryGet<T>(out T instance) where T : class
        {
            instance = Slot<T>.Instance;
            // Un UnityEngine.Object destruido compara igual a null con el operador sobrecargado;
            // por eso se comprueba también ese caso y se limpia la ranura.
            if (instance is UnityEngine.Object unityObject && unityObject == null)
            {
                Slot<T>.Instance = null;
                instance = null;
            }
            return instance != null;
        }

        /// <summary>Obtiene la instancia registrada o <c>null</c>.</summary>
        /// <typeparam name="T">Contrato.</typeparam>
        public static T Get<T>() where T : class
        {
            TryGet(out T instance);
            return instance;
        }

        /// <summary>Vacía todas las ranuras usadas.</summary>
        public static void ClearAll()
        {
            for (int i = 0; i < Clears.Count; i++) Clears[i]();
        }

        /// <summary>Limpieza automática antes de cada sesión de juego (Enter Play Mode sin recarga de dominio).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => ClearAll();
    }
}
