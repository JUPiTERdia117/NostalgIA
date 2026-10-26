// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de modo de edición (NostalgIA.Tests.EditMode)
// Archivo: ProjectValidatorTests.cs
// Propósito: ejecutar el validador sobre el proyecto real como prueba de
// aceptación de la Fase 0 (Docs/Roadmap_Fases.md §3, criterios de aceptación).
// -----------------------------------------------------------------------------

using System.Text;
using NostalgIA.EditorTools.Validation;
using NUnit.Framework;

namespace NostalgIA.Tests.EditMode
{
    /// <summary>
    /// Pruebas de integración de <see cref="ProjectValidator"/> sobre el estado actual del proyecto.
    /// </summary>
    /// <remarks>
    /// La validación completa se ejecuta una sola vez para toda la clase
    /// (<see cref="OneTimeSetUpAttribute"/>), porque recorre todas las escenas y prefabs y tarda
    /// varios segundos; cada prueba consulta el mismo reporte.
    /// </remarks>
    public sealed class ProjectValidatorTests
    {
        private ValidationReport report;

        /// <summary>Ejecuta el validador una vez antes de las pruebas de la clase.</summary>
        [OneTimeSetUp]
        public void EjecutarValidacion()
        {
            report = ProjectValidator.Run();
        }

        /// <summary>Criterio de aceptación de la Fase 0: el validador no encuentra errores.</summary>
        [Test]
        public void Proyecto_NoTieneErroresDeValidacion()
        {
            Assert.AreEqual(0, report.ErrorCount, DescribeIssues(ValidationSeverity.Error));
        }

        /// <summary>Restricción 1 del curso: no existe ningún componente ni asset de Terrain.</summary>
        [Test]
        public void Proyecto_NoUsaTerrain()
        {
            AssertNoIssues(ValidationRules.Terrain);
        }

        /// <summary>Ninguna escena ni prefab referencia scripts o prefabs inexistentes.</summary>
        [Test]
        public void Proyecto_NoTieneReferenciasRotas()
        {
            AssertNoIssues(ValidationRules.MissingScripts);
            AssertNoIssues(ValidationRules.MissingPrefabs);
        }

        /// <summary>La lista de escenas del build cumple las reglas de la Fase 0.</summary>
        [Test]
        public void BuildSettings_CumplenLasReglas()
        {
            AssertNoIssues(ValidationRules.BuildScenes);
        }

        /// <summary>Capas y matriz de colisiones según el TDD (§13).</summary>
        [Test]
        public void CapasYColisiones_CumplenElTDD()
        {
            AssertNoIssues(ValidationRules.Layers);
            AssertNoIssues(ValidationRules.CollisionMatrix);
        }

        /// <summary>Los ensamblados existen y respetan sus reglas de dependencia (TDD §2.1).</summary>
        [Test]
        public void Ensamblados_RespetanLasDependencias()
        {
            AssertNoIssues(ValidationRules.Assemblies);
        }

        /// <summary>El conteo de apariciones no cuenta coincidencias superpuestas.</summary>
        [Test]
        public void CountOccurrences_CuentaSinSuperposicion()
        {
            Assert.AreEqual(2, ProjectValidator.CountOccurrences("aaaa", "aa"));
            Assert.AreEqual(3, ProjectValidator.CountOccurrences("x-x-x", "x"));
            Assert.AreEqual(0, ProjectValidator.CountOccurrences("abc", "z"));
            Assert.AreEqual(0, ProjectValidator.CountOccurrences(string.Empty, "z"));
            Assert.AreEqual(0, ProjectValidator.CountOccurrences("abc", string.Empty));
        }

        /// <summary>Falla si existe al menos un hallazgo de advertencia o error para la regla indicada.</summary>
        private void AssertNoIssues(string rule)
        {
            var details = new StringBuilder();
            int count = 0;
            foreach (ValidationIssue issue in report.Issues)
            {
                if (issue.Rule != rule || issue.Severity == ValidationSeverity.Info) continue;
                count++;
                details.AppendLine($"{issue.AssetPath}: {issue.Message}");
            }
            Assert.AreEqual(0, count, $"Regla «{rule}»:\n{details}");
        }

        /// <summary>Lista los hallazgos de una gravedad para mostrarlos en el mensaje de la prueba.</summary>
        private string DescribeIssues(ValidationSeverity severity)
        {
            var details = new StringBuilder();
            foreach (ValidationIssue issue in report.Issues)
            {
                if (issue.Severity != severity) continue;
                details.AppendLine($"[{issue.Rule}] {issue.AssetPath}: {issue.Message}");
            }
            return details.ToString();
        }
    }
}
