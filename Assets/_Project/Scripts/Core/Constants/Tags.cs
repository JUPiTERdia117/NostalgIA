// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: Tags.cs
// Propósito: nombres de etiquetas (Tags) centralizados para evitar cadenas mágicas.
// -----------------------------------------------------------------------------

namespace NostalgIA.Core
{
    /// <summary>
    /// Etiquetas de GameObject utilizadas por el proyecto.
    /// </summary>
    /// <remarks>
    /// En este módulo se decidió reunir las etiquetas en constantes para que un error de
    /// escritura se detecte en compilación y no en tiempo de juego. Las comparaciones deben
    /// hacerse con <c>GameObject.CompareTag(Tags.Player)</c>, que no genera asignaciones de
    /// memoria, en lugar de comparar <c>gameObject.tag</c>, que crea una cadena nueva en cada
    /// lectura.
    /// </remarks>
    public static class Tags
    {
        /// <summary>Etiqueta por defecto de Unity.</summary>
        public const string Untagged = "Untagged";
        /// <summary>Etiqueta integrada de Unity asignada a la raíz del jugador.</summary>
        public const string Player = "Player";
        /// <summary>Etiqueta de los enemigos del prototipo.</summary>
        public const string Enemy = "Enemy";
        /// <summary>Etiqueta de las superficies de lava electrónica del prototipo.</summary>
        public const string Lava = "Lava";
    }
}
