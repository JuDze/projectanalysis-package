using System;

namespace Judze.ProjectAnalysis.Editor.Analysis.Rules
{
    // Prioritāte nosaka pārskatīšanas secību, nevis izmērītu veiktspējas zudumu.
    internal enum RiskLevel { Low, Medium, High }

    // Kopīgs robežu salīdzinājums; katrs noteikums pats nosaka atradnes nosacījumu.
    internal static class RiskThresholds
    {
        public static RiskLevel Evaluate(long value, long medium, long high) =>
            value > high ? RiskLevel.High : value > medium ? RiskLevel.Medium : RiskLevel.Low;

        public static RiskLevel? Evaluate(double? value, double medium, double high) =>
            !value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value) ? (RiskLevel?)null :
            value.Value > high ? RiskLevel.High : value.Value > medium ? RiskLevel.Medium : RiskLevel.Low;

        public static void Validate(long medium, long high)
        {
            if (medium <= 0 || high <= medium)
                throw new ArgumentOutOfRangeException(nameof(high), "Thresholds must be positive and High must be greater than Medium.");
        }

        public static void Validate(double medium, double high)
        {
            if (double.IsNaN(medium) || double.IsInfinity(medium) || double.IsNaN(high) || double.IsInfinity(high) || medium <= 0 || high <= medium)
                throw new ArgumentException("Thresholds must be finite and positive, and High must be greater than Medium.");
        }
    }
}
