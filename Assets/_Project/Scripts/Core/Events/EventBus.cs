// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: EventBus.cs
// Propósito: bus de eventos estático y genérico por tipo, sin asignaciones de
// memoria al publicar.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NostalgIA.Core.Events
{
    /// <summary>
    /// Bus estático por tipo de evento.
    /// </summary>
    /// <typeparam name="T">Evento a transportar; siempre un <c>struct</c> que implementa <see cref="IGameEvent"/>.</typeparam>
    /// <remarks>
    /// <para>
    /// Cada tipo de evento obtiene su propia clase estática cerrada (<c>EventBus&lt;PlayerDied&gt;</c>,
    /// <c>EventBus&lt;ObjectAbsorbed&gt;</c>, …). El compilador de C# crea una copia de los campos
    /// estáticos por cada argumento genérico, de modo que el despacho no necesita diccionarios ni
    /// comparaciones de tipo: localizar a los suscriptores cuesta un acceso a campo estático.
    /// </para>
    /// <para>
    /// Los suscriptores se guardan en un delegado <i>multicast</i>. Publicar invoca ese delegado
    /// pasando el evento por referencia de solo lectura (<c>in</c>): no se copia el <c>struct</c>
    /// por cada oyente ni se crea ningún objeto, por lo que <see cref="Raise"/> no asigna memoria.
    /// Las asignaciones ocurren solo al suscribirse o cancelar, operaciones que realizamos en
    /// <c>OnEnable</c>/<c>OnDisable</c> y nunca dentro del bucle de juego.
    /// </para>
    /// <para>
    /// Reglas de uso: suscribirse en <c>OnEnable</c> y cancelar en <c>OnDisable</c>;
    /// un oyente no publica eventos de su mismo tipo dentro del manejador.
    /// </para>
    /// </remarks>
    public static class EventBus<T> where T : struct, IGameEvent
    {
        /// <summary>Firma de los oyentes: reciben el evento por referencia de solo lectura.</summary>
        public delegate void Handler(in T evt);

        /// <summary>Delegado multicast con todos los oyentes registrados.</summary>
        private static Handler handlers;

        /// <summary>Indica si el bus ya registró su función de limpieza en <see cref="EventBusRegistry"/>.</summary>
        private static bool registeredForReset;

        /// <summary>Indica si hay al menos un oyente registrado.</summary>
        public static bool HasSubscribers => handlers != null;

        /// <summary>Suscribe un oyente. Llamar en <c>OnEnable</c>.</summary>
        /// <param name="handler">Método que recibirá los eventos.</param>
        public static void Subscribe(Handler handler)
        {
            if (handler == null) return;
            if (!registeredForReset)
            {
                // Primera suscripción de este tipo: se registra la limpieza para el reinicio de dominio.
                registeredForReset = true;
                EventBusRegistry.RegisterReset(Clear);
            }
            handlers += handler;
        }

        /// <summary>Cancela la suscripción. Llamar en <c>OnDisable</c>.</summary>
        /// <param name="handler">El mismo método usado en <see cref="Subscribe"/>.</param>
        public static void Unsubscribe(Handler handler)
        {
            if (handler == null) return;
            handlers -= handler;
        }

        /// <summary>Publica el evento a todos los oyentes registrados.</summary>
        /// <param name="evt">Evento a publicar, pasado por referencia de solo lectura.</param>
        /// <remarks>
        /// Se copia el delegado a una variable local antes de invocarlo: si un oyente cancela su
        /// suscripción durante la publicación, la invocación en curso no se altera (los delegados
        /// son inmutables), y no existe riesgo de referencia nula entre la comprobación y la llamada.
        /// </remarks>
        public static void Raise(in T evt)
        {
            Handler snapshot = handlers;
            snapshot?.Invoke(in evt);
        }

        /// <summary>Elimina todos los oyentes de este tipo de evento.</summary>
        public static void Clear() => handlers = null;
    }

    /// <summary>
    /// Registro de las funciones de limpieza de cada <see cref="EventBus{T}"/> cerrado.
    /// </summary>
    /// <remarks>
    /// Unity no ejecuta <c>RuntimeInitializeOnLoadMethod</c> sobre clases genéricas, por lo que
    /// cada bus se registra aquí en su primera suscripción. Al entrar a modo de juego con la
    /// recarga de dominio desactivada (<i>Enter Play Mode Options</i>) los campos estáticos
    /// conservarían oyentes de la sesión anterior; la limpieza en <c>SubsystemRegistration</c>
    /// lo evita.
    /// </remarks>
    public static class EventBusRegistry
    {
        /// <summary>Funciones de limpieza registradas; crece una vez por tipo de evento usado.</summary>
        private static readonly List<Action> Resets = new List<Action>(64);

        /// <summary>Registra la función de limpieza de un bus.</summary>
        /// <param name="reset">Acción que deja el bus sin oyentes.</param>
        internal static void RegisterReset(Action reset) => Resets.Add(reset);

        /// <summary>Elimina los oyentes de todos los buses usados hasta ahora.</summary>
        public static void ClearAll()
        {
            for (int i = 0; i < Resets.Count; i++) Resets[i]();
        }

        /// <summary>Limpieza automática antes de cargar la primera escena de cada sesión de juego.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => ClearAll();
    }
}
