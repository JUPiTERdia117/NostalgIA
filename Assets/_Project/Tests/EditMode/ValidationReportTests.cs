// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de modo de edición (NostalgIA.Tests.EditMode)
// Archivo: ValidationReportTests.cs
// Propósito: verificar el modelo del reporte de validación y su exportación.
// -----------------------------------------------------------------------------

using NostalgIA.EditorTools.Validation;
using NUnit.Framework;

namespace NostalgIA.Tests.EditMode
{
    /// <summary>
    /// Pruebas unitarias de <see cref="ValidationReport"/>: contadores, veredicto y formato Markdown.
    /// </summary>
    public sealed class ValidationReportTests
    {
        private const string UnityVersion = "6000.3.7f1";

        /// <summary>Un reporte sin hallazgos se considera aprobado.</summary>
        [Test]
        public void ReporteVacio_SeApruebaSinErrores()
        {
            var report = new ValidationReport();

            Assert.AreEqual(0, report.ErrorCount);
            Assert.AreEqual(0, report.WarningCount);
            StringAssert.Contains("APROBADO", report.ToMarkdown(UnityVersion));
        }

        /// <summary>Cada gravedad incrementa solo su propio contador.</summary>
        [Test]
        public void Add_ActualizaElContadorDeCadaGravedad()
        {
            var report = new ValidationReport();
            report.Add(ValidationSeverity.Error, "Regla A", "Assets/a.unity", "error");
            report.Add(ValidationSeverity.Warning, "Regla B", "Assets/b.unity", "advertencia");
            report.Add(ValidationSeverity.Warning, "Regla B", "Assets/c.unity", "advertencia");
            report.Add(ValidationSeverity.Info, "Regla C", string.Empty, "información");

            Assert.AreEqual(1, report.ErrorCount);
            Assert.AreEqual(2, report.WarningCount);
            Assert.AreEqual(4, report.Issues.Count);
        }

        /// <summary>Con al menos un error el reporte se rechaza y los errores se listan antes que las advertencias.</summary>
        [Test]
        public void ToMarkdown_ConErrores_SeRechazaYListaErroresPrimero()
        {
            var report = new ValidationReport();
            report.Add(ValidationSeverity.Warning, "Regla B", "Assets/b.unity", "advertencia");
            report.Add(ValidationSeverity.Error, "Regla A", "Assets/a.unity", "error");

            string markdown = report.ToMarkdown(UnityVersion);

            StringAssert.Contains("RECHAZADO", markdown);
            int errorRow = markdown.IndexOf("| Error |", System.StringComparison.Ordinal);
            int warningRow = markdown.IndexOf("| Advertencia |", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(errorRow, 0);
            Assert.Less(errorRow, warningRow, "Los errores deben aparecer antes que las advertencias.");
        }

        /// <summary>Las barras verticales de los mensajes se escapan para no romper la tabla.</summary>
        [Test]
        public void ToMarkdown_EscapaBarrasVerticales()
        {
            var report = new ValidationReport();
            report.Add(ValidationSeverity.Error, "Regla", "Assets/x.unity", "valor a|b");

            StringAssert.Contains("valor a\\|b", report.ToMarkdown(UnityVersion));
        }

        /// <summary>Las reglas cumplidas se listan en su propia sección.</summary>
        [Test]
        public void Pass_RegistraLasReglasCumplidas()
        {
            var report = new ValidationReport();
            report.Pass(ValidationRules.Terrain);

            Assert.AreEqual(1, report.PassedRules.Count);
            StringAssert.Contains(ValidationRules.Terrain, report.ToMarkdown(UnityVersion));
        }
    }
}
