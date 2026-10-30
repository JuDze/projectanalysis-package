using System.Collections.Generic;
using System.IO;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;

namespace Judze.ProjectAnalysis.Editor.Reporting
{
    // Veido kopsavilkumu tikai no pārskata atradnēm; neiegūst Unity datus un nemaina avotu.
    internal static class ReportSummaryBuilder
    {
        public static ReportSummary Build(IEnumerable<ReportFinding> findings)
        {
            var summary = new ReportSummary();
            foreach (var finding in findings ?? Enumerable.Empty<ReportFinding>())
            {
                if (finding == null ||
                    (finding.Risk != nameof(RiskLevel.Low) && finding.Risk != nameof(RiskLevel.Medium) &&
                     finding.Risk != nameof(RiskLevel.High)))
                    continue;

                ReportRiskCounts counts;
                switch (finding.Category)
                {
                    case "Textures": counts = summary.Textures; break;
                    case "Models": counts = summary.Models; break;
                    case "Audio": counts = summary.Audio; break;
                    // Nepazīstama kategorija nedrīkst radīt nepilnīgu kopsavilkumu.
                    default: throw new InvalidDataException("The report finding category is unsupported.");
                }
                switch (finding.Risk)
                {
                    case nameof(RiskLevel.Low): counts.Low++; break;
                    case nameof(RiskLevel.Medium): counts.Medium++; break;
                    case nameof(RiskLevel.High): counts.High++; break;
                }
            }

            summary.Totals.Low = summary.Textures.Low + summary.Models.Low + summary.Audio.Low;
            summary.Totals.Medium = summary.Textures.Medium + summary.Models.Medium + summary.Audio.Medium;
            summary.Totals.High = summary.Textures.High + summary.Models.High + summary.Audio.High;
            summary.Totals.Findings = summary.Totals.Low + summary.Totals.Medium + summary.Totals.High;
            return summary;
        }
    }
}
