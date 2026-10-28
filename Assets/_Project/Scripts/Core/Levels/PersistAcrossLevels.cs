// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: PersistAcrossLevels.cs
// Propósito: marcar objetos raíz que deben acompañar al jugador al cambiar de nivel.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace NostalgIA.Core.Levels
{
    /// <summary>
    /// Marca un objeto raíz de la escena (por ejemplo, el <c>EventSystem</c>) para que la puerta
    /// de nivel lo traslade a la escena siguiente antes de descargar la actual.
    /// </summary>
    /// <remarks>
    /// El jugador se traslada siempre, sin necesidad de este marcador. Se prefirió un marcador
    /// explícito a buscar tipos concretos para que el núcleo y la jugabilidad no dependan de
    /// ensamblados de interfaz (UGUI) y para que cada nivel decida qué conserva.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PersistAcrossLevels : MonoBehaviour
    {
    }
}
