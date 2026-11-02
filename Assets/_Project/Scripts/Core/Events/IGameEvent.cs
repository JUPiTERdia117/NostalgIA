// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: IGameEvent.cs
// Propósito: interfaz marcadora de los eventos que viajan por el bus.
// -----------------------------------------------------------------------------

namespace NostalgIA.Core.Events
{
    /// <summary>
    /// Marca un tipo como evento del juego.
    /// </summary>
    /// <remarks>
    /// Todos los eventos del proyecto son <c>readonly struct</c>. Al ser tipos por valor viajan
    /// por la pila y no generan asignaciones en el montón; al ser de solo lectura, un oyente no
    /// puede alterar la carga útil que reciben los oyentes siguientes. La restricción genérica
    /// <c>where T : struct, IGameEvent</c> del bus impide publicar clases por error.
    /// </remarks>
    public interface IGameEvent { }
}
