// -----------------------------------------------------------------------------
// NostalgIA — Herramientas de editor (NostalgIA.Editor)
// Archivo: ValidationBuildGate.cs
// Propósito: ejecutar el validador del proyecto antes de cada build y detenerlo si
// se incumple una regla obligatoria (por ejemplo, la prohibición de Terrain).
// -----------------------------------------------------------------------------

using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace NostalgIA.EditorTools.Validation
{
    /// <summary>
    /// Paso previo al build que ejecuta <see cref="ProjectValidator"/> y lanza
    /// <see cref="BuildFailedException"/> si hay errores.
    /// </summary>
    /// <remarks>
    /// En este módulo se decidió no confiar en la ejecución manual del validador: Unity invoca
    /// automáticamente toda clase que implementa <see cref="IPreprocessBuildWithReport"/> antes de
    /// compilar el juego, por lo que ningún build de entrega puede generarse si incumple las
    /// restricciones de la asignatura. El reporte queda guardado en <c>Docs/Bitacora_Tecnica/</c>
    /// como evidencia de cada build.
    /// </remarks>
    public sealed class ValidationBuildGate : IPreprocessBuildWithReport
    {
        /// <summary>
        /// Orden de ejecución entre los pasos previos al build. Se usa 0 para validar antes que
        /// cualquier otro procesamiento.
        /// </summary>
        public int callbackOrder => 0;

        /// <summary>Ejecuta la validación y detiene el build si hay errores.</summary>
        /// <param name="report">Información del build en curso, provista por Unity.</param>
        public void OnPreprocessBuild(BuildReport report)
        {
            ValidationReport result = ProjectValidator.Run();
            string reportPath = ProjectValidator.WriteReport(result);
            ProjectValidator.LogSummary(result, reportPath);

            if (result.ErrorCount > 0)
            {
                throw new BuildFailedException(
                    $"[NostalgIA] Build detenido: la validación encontró {result.ErrorCount} error(es). Revisar {reportPath}.");
            }
        }
    }
}
