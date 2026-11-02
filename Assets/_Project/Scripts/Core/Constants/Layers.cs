// -----------------------------------------------------------------------------
// NostalgIA — Núcleo (NostalgIA.Core)
// Archivo: Layers.cs
// Propósito: fuente única de verdad para los índices, nombres y máscaras de las
// capas físicas del proyecto.
// -----------------------------------------------------------------------------

using System.Collections.Generic;

namespace NostalgIA.Core
{
    /// <summary>
    /// Índices de las capas físicas del proyecto.
    /// </summary>
    /// <remarks>
    /// En este módulo se decidió identificar las capas por <b>índice</b> y no por nombre.
    /// Los índices son lo que Unity serializa en escenas y prefabs; si una capa se renombra
    /// (como ocurrió con «Poolable» → «Absorbable» y «Graple» → «Grapple»), el código basado
    /// en índices sigue funcionando sin cambios. Además, se evita llamar a
    /// <c>LayerMask.NameToLayer</c> o <c>LayerMask.GetMask</c>, que comparan cadenas y no deben
    /// usarse en tiempo de juego.
    /// </remarks>
    public static class Layers
    {
        /// <summary>Geometría general del nivel.</summary>
        public const int Default = 0;
        /// <summary>Capa integrada de Unity para efectos transparentes.</summary>
        public const int TransparentFX = 1;
        /// <summary>Capa integrada de Unity ignorada por los raycasts por defecto.</summary>
        public const int IgnoreRaycast = 2;
        /// <summary>Paredes que no admiten anclaje del gancho.</summary>
        public const int Wall = 3;
        /// <summary>Volúmenes de agua (Estación 2: Verano).</summary>
        public const int Water = 4;
        /// <summary>Capa integrada de Unity para la interfaz.</summary>
        public const int UI = 5;
        /// <summary>Cápsula del jugador.</summary>
        public const int Player = 6;
        /// <summary>Objetos que la Aspiradora Lanzadora puede absorber (antes «Poolable»).</summary>
        public const int Absorbable = 7;
        /// <summary>Superficies válidas para el anclaje del gancho (antes «Graple»).</summary>
        public const int Grapple = 8;
        /// <summary>Enemigos del bestiario digital.</summary>
        public const int Enemy = 9;
        /// <summary>Peligros y superficies de muerte instantánea.</summary>
        public const int Hazard = 10;
        /// <summary>Objetos absorbibles mientras viajan lanzados por el jugador.</summary>
        public const int PlayerProjectile = 11;
        /// <summary>Proyectiles disparados por enemigos.</summary>
        public const int EnemyProjectile = 12;
        /// <summary>Fragmentos puramente visuales que no deben bloquear al jugador.</summary>
        public const int Debris = 13;
        /// <summary>Volúmenes lógicos (checkpoints, zonas de tutorial, vacío).</summary>
        public const int Trigger = 14;

        /// <summary>
        /// Nombres esperados de cada capa, tal como deben aparecer en Project Settings › Tags and Layers.
        /// </summary>
        public static class Names
        {
            public const string Default = "Default";
            public const string TransparentFX = "TransparentFX";
            public const string IgnoreRaycast = "Ignore Raycast";
            public const string Wall = "Wall";
            public const string Water = "Water";
            public const string UI = "UI";
            public const string Player = "Player";
            public const string Absorbable = "Absorbable";
            public const string Grapple = "Grapple";
            public const string Enemy = "Enemy";
            public const string Hazard = "Hazard";
            public const string PlayerProjectile = "PlayerProjectile";
            public const string EnemyProjectile = "EnemyProjectile";
            public const string Debris = "Debris";
            public const string Trigger = "Trigger";
        }

        /// <summary>
        /// Máscaras de bits precalculadas en tiempo de compilación.
        /// </summary>
        /// <remarks>
        /// Una máscara de capas es un entero de 32 bits donde el bit <c>i</c> vale 1 si la capa
        /// <c>i</c> participa en la consulta: <c>mascara = 1 &lt;&lt; indice</c>. Al declararlas como
        /// <c>const</c>, el compilador las sustituye por su valor numérico y su uso en el bucle de
        /// juego no tiene costo ni genera asignaciones de memoria.
        /// </remarks>
        public static class Masks
        {
            public const int Default = 1 << Layers.Default;
            public const int Wall = 1 << Layers.Wall;
            public const int Water = 1 << Layers.Water;
            public const int Player = 1 << Layers.Player;
            public const int Absorbable = 1 << Layers.Absorbable;
            public const int Grapple = 1 << Layers.Grapple;
            public const int Enemy = 1 << Layers.Enemy;
            public const int Hazard = 1 << Layers.Hazard;
            public const int PlayerProjectile = 1 << Layers.PlayerProjectile;
            public const int EnemyProjectile = 1 << Layers.EnemyProjectile;
            public const int Debris = 1 << Layers.Debris;
            public const int Trigger = 1 << Layers.Trigger;

            /// <summary>
            /// Geometría sólida que bloquea la puntería y el lanzamiento de objetos:
            /// nivel general, paredes y superficies de anclaje.
            /// </summary>
            public const int SolidGeometry = Default | Wall | Grapple;
        }

        /// <summary>
        /// Tabla de nombres esperados indexada por capa (32 posiciones). Las posiciones que el
        /// proyecto no utiliza contienen <c>null</c>. Sirve para comprobar la configuración de
        /// Tags and Layers.
        /// </summary>
        /// <remarks>
        /// El arreglo se crea una única vez al cargar la clase y se expone como
        /// <see cref="IReadOnlyList{T}"/> para que ningún sistema pueda modificar la tabla por
        /// accidente. Leerlo a través de la interfaz no genera asignaciones de memoria.
        /// </remarks>
        public static IReadOnlyList<string> ExpectedNames => ExpectedNamesTable;

        /// <summary>Almacenamiento privado de <see cref="ExpectedNames"/>.</summary>
        private static readonly string[] ExpectedNamesTable = BuildExpectedNames();

        /// <summary>
        /// Indica si la capa <paramref name="layer"/> está incluida en <paramref name="mask"/>.
        /// </summary>
        /// <param name="mask">Máscara de capas (por ejemplo, el valor de un <c>LayerMask</c>).</param>
        /// <param name="layer">Índice de capa entre 0 y 31.</param>
        /// <returns><c>true</c> si el bit correspondiente a la capa está activo.</returns>
        /// <remarks>Se aplica una operación AND de bits: <c>(mascara &amp; (1 &lt;&lt; capa)) ≠ 0</c>.</remarks>
        public static bool Contains(int mask, int layer) => (mask & (1 << layer)) != 0;

        /// <summary>Construye la tabla de nombres esperados a partir de las constantes.</summary>
        private static string[] BuildExpectedNames()
        {
            var names = new string[32];
            names[Default] = Names.Default;
            names[TransparentFX] = Names.TransparentFX;
            names[IgnoreRaycast] = Names.IgnoreRaycast;
            names[Wall] = Names.Wall;
            names[Water] = Names.Water;
            names[UI] = Names.UI;
            names[Player] = Names.Player;
            names[Absorbable] = Names.Absorbable;
            names[Grapple] = Names.Grapple;
            names[Enemy] = Names.Enemy;
            names[Hazard] = Names.Hazard;
            names[PlayerProjectile] = Names.PlayerProjectile;
            names[EnemyProjectile] = Names.EnemyProjectile;
            names[Debris] = Names.Debris;
            names[Trigger] = Names.Trigger;
            return names;
        }
    }
}
