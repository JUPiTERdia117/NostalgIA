// -----------------------------------------------------------------------------
// NostalgIA — Herramientas de editor (NostalgIA.Editor)
// Archivo: ValidationReport.cs
// Propósito: modelo de datos del resultado de la validación del proyecto y su
// exportación a Markdown como evidencia para la bitácora técnica.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;

namespace NostalgIA.EditorTools.Validation
{
    /// <summary>Gravedad de un hallazgo de validación.</summary>
    public enum ValidationSeverity
    {
        /// <summary>Dato informativo; no requiere acción.</summary>
        Info = 0,
        /// <summary>Debe corregirse antes de la entrega, pero no bloquea el build.</summary>
        Warning = 1,
        /// <summary>Incumple una regla obligatoria; bloquea el build.</summary>
        Error = 2
    }

    /// <summary>
    /// Hallazgo individual de la validación: qué regla se evaluó, sobre qué activo y con qué resultado.
    /// </summary>
    public readonly struct ValidationIssue
    {
        /// <summary>Gravedad del hallazgo.</summary>
        public readonly ValidationSeverity Severity;
        /// <summary>Nombre corto de la regla evaluada (por ejemplo, «Sin Terrain»).</summary>
        public readonly string Rule;
        /// <summary>Ruta del activo afectado; vacía si el hallazgo es global.</summary>
        public readonly string AssetPath;
        /// <summary>Descripción del hallazgo y, cuando aplica, de la corrección esperada.</summary>
        public readonly string Message;

        /// <summary>Crea un hallazgo inmutable.</summary>
        public ValidationIssue(ValidationSeverity severity, string rule, string assetPath, string message)
        {
            Severity = severity;
            Rule = rule;
            AssetPath = assetPath ?? string.Empty;
            Message = message;
        }
    }

    /// <summary>
    /// Resultado completo de una ejecución del validador.
    /// </summary>
    /// <remarks>
    /// En este módulo se decidió separar el resultado de la lógica de validación para poder
    /// mostrarlo en consola, escribirlo en <c>Docs/Bitacora_Tecnica/</c> y usarlo para detener el
    /// build, sin repetir código.
    /// </remarks>
    public sealed class ValidationReport
    {
        private readonly List<ValidationIssue> issues = new List<ValidationIssue>(64);
        private readonly List<string> passedRules = new List<string>(16);

        /// <summary>Momento en que se generó el reporte (hora local).</summary>
        public DateTime GeneratedAt { get; } = DateTime.Now;

        /// <summary>Hallazgos registrados, en orden de detección.</summary>
        public IReadOnlyList<ValidationIssue> Issues => issues;

        /// <summary>Reglas evaluadas que no produjeron errores ni advertencias.</summary>
        public IReadOnlyList<string> PassedRules => passedRules;

        /// <summary>Número de hallazgos con gravedad <see cref="ValidationSeverity.Error"/>.</summary>
        public int ErrorCount { get; private set; }

        /// <summary>Número de hallazgos con gravedad <see cref="ValidationSeverity.Warning"/>.</summary>
        public int WarningCount { get; private set; }

        /// <summary>Registra un hallazgo y actualiza los contadores.</summary>
        public void Add(ValidationSeverity severity, string rule, string assetPath, string message)
        {
            issues.Add(new ValidationIssue(severity, rule, assetPath, message));
            if (severity == ValidationSeverity.Error) ErrorCount++;
            else if (severity == ValidationSeverity.Warning) WarningCount++;
        }

        /// <summary>Registra una regla que se cumplió por completo.</summary>
        public void Pass(string rule) => passedRules.Add(rule);

        /// <summary>
        /// Exporta el reporte como documento Markdown en español, con resumen y tabla de hallazgos.
        /// </summary>
        /// <param name="unityVersion">Versión del editor que ejecutó la validación.</param>
        /// <returns>Texto Markdown listo para guardarse en la bitácora técnica.</returns>
        public string ToMarkdown(string unityVersion)
        {
            var sb = new StringBuilder(4096);
            sb.AppendLine("# Reporte de validación del proyecto — NostalgIA");
            sb.AppendLine();
            sb.AppendLine($"**Fecha:** {GeneratedAt:yyyy-MM-dd HH:mm}  ");
            sb.AppendLine($"**Unity:** {unityVersion}  ");
            sb.AppendLine($"**Resultado:** {(ErrorCount == 0 ? "APROBADO" : "RECHAZADO")} — {ErrorCount} error(es), {WarningCount} advertencia(s)");
            sb.AppendLine();
            sb.AppendLine("Reporte generado por el validador del proyecto (menú *NostalgIA › Validar proyecto* y paso previo a cada build). Las reglas provienen de `Docs/Requisitos.md` y `Docs/TDD_Arquitectura.md` (§15).");
            sb.AppendLine();

            sb.AppendLine("## Reglas cumplidas");
            sb.AppendLine();
            if (passedRules.Count == 0)
            {
                sb.AppendLine("Ninguna regla se cumplió sin observaciones.");
            }
            else
            {
                for (int i = 0; i < passedRules.Count; i++)
                {
                    sb.AppendLine($"- {passedRules[i]}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("## Hallazgos");
            sb.AppendLine();
            if (issues.Count == 0)
            {
                sb.AppendLine("Sin hallazgos.");
                return sb.ToString();
            }

            sb.AppendLine("| Gravedad | Regla | Activo | Detalle |");
            sb.AppendLine("| :--- | :--- | :--- | :--- |");
            // Se listan primero los errores, luego las advertencias y al final la información,
            // para que lo que bloquea el build sea lo primero que se lea.
            AppendRows(sb, ValidationSeverity.Error);
            AppendRows(sb, ValidationSeverity.Warning);
            AppendRows(sb, ValidationSeverity.Info);
            return sb.ToString();
        }

        /// <summary>Agrega a la tabla las filas de una gravedad concreta.</summary>
        private void AppendRows(StringBuilder sb, ValidationSeverity severity)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                ValidationIssue issue = issues[i];
                if (issue.Severity != severity) continue;
                string asset = string.IsNullOrEmpty(issue.AssetPath) ? "—" : $"`{Escape(issue.AssetPath)}`";
                sb.AppendLine($"| {SeverityLabel(issue.Severity)} | {Escape(issue.Rule)} | {asset} | {Escape(issue.Message)} |");
            }
        }

        /// <summary>Etiqueta en español para cada gravedad.</summary>
        public static string SeverityLabel(ValidationSeverity severity)
        {
            switch (severity)
            {
                case ValidationSeverity.Error: return "Error";
                case ValidationSeverity.Warning: return "Advertencia";
                default: return "Información";
            }
        }

        /// <summary>Escapa las barras verticales para no romper la tabla Markdown.</summary>
        private static string Escape(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.Replace("|", "\\|");
    }
}
