using System;
using System.Collections.Generic;

namespace Judze.ProjectAnalysis.Editor.Analysis.Rules
{
    internal sealed class RuleDiagnostic
    {
        public string RuleId { get; }
        public string AssetPath { get; }
        public string Message { get; }
        public RuleDiagnostic(string ruleId, string assetPath, string message)
        { RuleId = ruleId; AssetPath = assetPath; Message = message; }
    }

    // Datu trūkums un izpildes kļūdas ir nodalītas no resursu atradnēm.
    internal sealed class RuleEvaluation
    {
        public IReadOnlyList<RuleResult> Findings { get; }
        public IReadOnlyList<RuleDiagnostic> Diagnostics { get; }
        public RuleEvaluation(IEnumerable<RuleResult> findings = null, IEnumerable<RuleDiagnostic> diagnostics = null)
        {
            Findings = new List<RuleResult>(findings ?? Array.Empty<RuleResult>()).AsReadOnly();
            Diagnostics = new List<RuleDiagnostic>(diagnostics ?? Array.Empty<RuleDiagnostic>()).AsReadOnly();
        }
    }
}
