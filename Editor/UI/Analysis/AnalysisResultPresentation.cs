using System.Globalization;
using System.IO;
using System.Text;
using System;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Atradnes attēlošanas dati; vērtēšanas loģika paliek noteikumos.
    internal sealed class AnalysisResultRow
    {
        public bool CanUndo { get; set; }
        public bool IsResolved { get; set; }
        public bool HasAppliedFix { get; set; }
        public string Category { get; set; }
        public string Risk { get; set; }
        public string RuleId { get; set; }
        public string Resource { get; set; }
        public string Path { get; set; }
        public string Finding { get; set; }
        public string Details { get; set; }
        public RuleResult Source { get; set; }

        // Null nozīmē, ka šai atradnei nav atbalstīta priekšskatījuma.
        public FixPreviewRow Fix { get; set; }
    }

    internal sealed class AnalysisResultRowMapper
    {
        public AnalysisResultRow Create(RuleResult result, string category)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("A category is required.", nameof(category));

            var details = new StringBuilder(Describe(result));
            details.Append("\n\nRecommendation:\n").Append(result.Recommendation);
            if (result.Proposal != null)
                details.Append("\n\nProposed change:\n").Append(result.Proposal.Summary);
            if (result.Warnings.Count > 0)
            {
                details.Append("\n\nWarnings:");
                foreach (string warning in result.Warnings) details.Append("\n- ").Append(warning);
            }
            if (result.RelatedResources.Count > 0)
            {
                details.Append("\n\nRelated resources:");
                foreach (var resource in result.RelatedResources)
                {
                    details.Append("\n- ").Append(resource.DisplayName ?? resource.Path);
                    if (resource.LocalId.HasValue) details.Append(" (local ID: ").Append(resource.LocalId.Value).Append(')');
                }
            }
            return new AnalysisResultRow
            {
                Category = category,
                Risk = result.Priority.ToString(),
                RuleId = result.RuleId,
                Resource = result.IsProjectFinding ? "Project settings" : result.Resource.DisplayName ?? Path.GetFileName(result.Resource.Path),
                Path = result.Resource?.Path ?? string.Empty,
                Finding = result.Title,
                Details = details.ToString(),
                Source = result,
                Fix = null
            };
        }
        internal static string Describe(RuleResult result)
        {
            var details = new StringBuilder(result.Description);
            if (result.Measurements.Count > 0)
            {
                details.Append("\n\nMeasurements:");
                foreach (var measurement in result.Measurements)
                {
                    // Nezināmu vērtību neaizstāj ar nulli un nepārrēķina noteikuma prioritāti.
                    string value = measurement.Value.HasValue
                        ? measurement.Value.Value.ToString("0.######", CultureInfo.InvariantCulture)
                        : "Unknown";
                    details.Append("\n- ").Append(measurement.Name).Append(": ").Append(value);
                    if (measurement.Value.HasValue && !string.IsNullOrEmpty(measurement.Unit))
                        details.Append(' ').Append(measurement.Unit);
                }
            }
            return details.ToString();
        }
    }
}
