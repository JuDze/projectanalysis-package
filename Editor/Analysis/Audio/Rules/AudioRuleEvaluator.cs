using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules
{
    internal sealed class AudioRuleEvaluator : IAudioRuleEvaluator
    {
        public RuleEvaluation Evaluate(AudioCollectionResult collection, RuleSettings savedSettings)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (savedSettings == null) throw new ArgumentNullException(nameof(savedSettings));
            if (!collection.Context.SupportsStandalone)
                return new RuleEvaluation(diagnostics: collection.Diagnostics.Concat(new[]
                    { new RuleDiagnostic("AUDIO_ANALYSIS", null, "Audio rules require Standalone settings.") }));
            var result = AudioRuleRegistry.Create(savedSettings).Evaluate(collection.Clips, collection.Context);
            // Salīdzina viena lauka priekšlikumus, nevis noteikumu identifikatorus. Dzinējs paliek nemainīts.
            var alternatives = result.Findings.Where(f => f.Proposal is AudioLoadTypeProposal)
                .GroupBy(f => ((AudioLoadTypeProposal)f.Proposal).Resource.Identity)
                .Where(g => g.Select(f => ((AudioLoadTypeProposal)f.Proposal).Target).Distinct().Count() > 1)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(f => f.RuleId)));
            var findings = new List<RuleResult>();
            foreach (var finding in result.Findings)
            {
                if (finding.Proposal is AudioLoadTypeProposal proposal && alternatives.TryGetValue(proposal.Resource.Identity, out string rules))
                {
                    string warning = $"Alternative Load Type proposals ({rules}) affect this clip. Choose one target mode; these changes must not be applied as a sequence.";
                    findings.Add(new RuleResult(finding.RuleId, finding.Resource, finding.Priority, finding.Title,
                        finding.Description, finding.Recommendation, finding.Measurements,
                        finding.Warnings.Concat(new[] { warning }), finding.Proposal, finding.RelatedResources));
                }
                else findings.Add(finding);
            }
            return new RuleEvaluation(findings, collection.Diagnostics.Concat(result.Diagnostics));
        }
    }
}
