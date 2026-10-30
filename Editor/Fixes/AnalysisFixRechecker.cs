using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal interface IFixRechecker
    {
        AnalysisRunResult Run(AnalysisRunResult baseline);
    }

    internal sealed class AnalysisFixRechecker : IFixRechecker
    {
        private readonly AnalysisController _analysis;
        public AnalysisFixRechecker(AnalysisController analysis)
        { _analysis = analysis ?? throw new ArgumentNullException(nameof(analysis)); }
        public AnalysisRunResult Run(AnalysisRunResult baseline) => _analysis.RunResolved(
            (baseline ?? throw new ArgumentNullException(nameof(baseline))).Context);
    }

    internal static class AnalysisFixRecheckComparison
    {
        public static void Fill(FixRecheckRecord record, FixPlan plan, AnalysisRunResult after)
        {
            var before = plan.Baseline;
            var categories = plan.Assets.Select(a => a.Category).Distinct().ToArray();
            var oldFindings = new List<RuleResult>();
            var newFindings = new List<RuleResult>();
            var diagnostics = new List<RuleDiagnostic>(after.Context.Diagnostics);
            bool incomplete = before.Context.Diagnostics.Count > 0 || after.Context.Diagnostics.Count > 0;
            if (categories.Contains("Models"))
            {
                oldFindings.AddRange(before.ModelEvaluation.Findings); newFindings.AddRange(after.ModelEvaluation.Findings);
                diagnostics.AddRange(after.ModelEvaluation.Diagnostics);
                incomplete |= before.ModelEvaluation.Diagnostics.Count > 0 || after.ModelEvaluation.Diagnostics.Count > 0;
                var afterModels = new HashSet<string>(after.ModelCollection.Models.Select(m => m.Guid), StringComparer.Ordinal);
                incomplete |= before.ModelCollection.Models.Any(m => !afterModels.Contains(m.Guid));
                var afterMeshes = new HashSet<string>(after.ModelCollection.Models.SelectMany(m => m.Meshes).Select(m => m.Guid + ":" + m.LocalId));
                incomplete |= before.ModelCollection.Models.SelectMany(m => m.Meshes).Any(m => !afterMeshes.Contains(m.Guid + ":" + m.LocalId));
                if (before.ModelCollection.Context.TargetPlatform != after.ModelCollection.Context.TargetPlatform ||
                    after.ModelCollection.Context.TargetPlatform != after.ModelCollection.Context.ActivePlatform ||
                    !AnalysisBuildTarget.IsKnown(after.ModelCollection.Context.TargetPlatform))
                    throw new InvalidOperationException("Build target changed during the repeated model analysis.");
            }
            if (categories.Contains("Audio"))
            {
                oldFindings.AddRange(before.AudioEvaluation.Findings); newFindings.AddRange(after.AudioEvaluation.Findings);
                diagnostics.AddRange(after.AudioEvaluation.Diagnostics);
                incomplete |= before.AudioEvaluation.Diagnostics.Count > 0 || after.AudioEvaluation.Diagnostics.Count > 0;
                var afterClips = new HashSet<string>(after.AudioCollection.Clips.Select(c => c.Guid + ":" + c.LocalId));
                incomplete |= before.AudioCollection.Clips.Any(c => !afterClips.Contains(c.Guid + ":" + c.LocalId));
                if (before.AudioCollection.Context.TargetPlatform != after.AudioCollection.Context.TargetPlatform ||
                    !after.AudioCollection.Context.HasMatchingImport || !after.AudioCollection.Context.SupportsStandalone)
                    throw new InvalidOperationException("Build target changed during the repeated audio analysis.");
            }
            if (categories.Contains("Textures"))
            {
                TextureFixPlanner.RequireContext(before.TextureRun.Context, after.TextureRun.Context);
                oldFindings.AddRange(before.TextureRun.Evaluation.Findings);
                newFindings.AddRange(after.TextureRun.Evaluation.Findings);
                diagnostics.AddRange(after.TextureRun.Evaluation.Diagnostics);
                var afterTextures = new HashSet<string>(after.TextureRun.Textures.Select(t => t.Guid), StringComparer.Ordinal);
                incomplete |= before.TextureRun.Evaluation.Diagnostics.Count > 0 || after.TextureRun.Evaluation.Diagnostics.Count > 0 ||
                    before.TextureRun.Textures.Any(t => !afterTextures.Contains(t.Guid));
            }
            FixRecheckComparison.FillResults(record, oldFindings, newFindings, diagnostics, incomplete,
                "Repeated analysis used the original scope and saved settings. Compared resource groups: " + string.Join(", ", categories) + ".");
        }
    }
}
