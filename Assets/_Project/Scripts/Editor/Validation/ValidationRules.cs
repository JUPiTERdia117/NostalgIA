// -----------------------------------------------------------------------------
// NostalgIA — Herramientas de editor (NostalgIA.Editor)
// Archivo: ValidationRules.cs
// Propósito: nombres públicos de las reglas del validador, compartidos por el
// propio validador, el reporte y las pruebas automatizadas.
// -----------------------------------------------------------------------------

namespace NostalgIA.EditorTools.Validation
{
    /// <summary>
    /// Nombres de las reglas evaluadas por <see cref="ProjectValidator"/>.
    /// </summary>
    /// <remarks>
    /// En esta revisión se decidió exponer los nombres como constantes públicas para que las
    /// pruebas automatizadas puedan filtrar los hallazgos por regla sin depender del texto de
    /// los mensajes, que puede cambiar.
    /// </remarks>
    public static class ValidationRules
    {
        /// <summary>Lectura de escenas y prefabs como texto YAML.</summary>
        public const string AssetReadability = "Lectura de activos";
        /// <summary>Serialización de texto forzada en el proyecto.</summary>
        public const string Serialization = "Serialización de texto";
        /// <summary>Restricción 1 del curso: prohibición de Unity Terrain.</summary>
        public const string Terrain = "Sin Terrain";
        /// <summary>Referencias a scripts inexistentes.</summary>
        public const string MissingScripts = "Sin scripts faltantes";
        /// <summary>Instancias de prefabs cuyo asset de origen no existe.</summary>
        public const string MissingPrefabs = "Sin prefabs faltantes";
        /// <summary>Material gris por defecto de ProBuilder en escenas de Estación.</summary>
        public const string ProBuilderMaterial = "Sin material por defecto de ProBuilder en Estaciones";
        /// <summary>Escenas incluidas en el build.</summary>
        public const string BuildScenes = "Build Settings";
        /// <summary>Nombres de las capas físicas (TDD §13).</summary>
        public const string Layers = "Capas físicas (TDD §13)";
        /// <summary>Matriz de colisiones entre capas (TDD §13).</summary>
        public const string CollisionMatrix = "Matriz de colisiones (TDD §13)";
        /// <summary>Reglas de dependencia entre ensamblados (TDD §2.1).</summary>
        public const string Assemblies = "Dependencias entre ensamblados (TDD §2.1)";
    }
}
