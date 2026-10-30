using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Models.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Models;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    // Atkārtoti iegūst faktus un izmanto esošos noteikumus; šeit nav importa izmaiņu.
    internal sealed class ModelFixPreviewService
    {
        private readonly IModelAnalyzer _analyzer;
        private readonly IModelRuleEvaluator _rules;
        public ModelFixPreviewService(IModelAnalyzer analyzer, IModelRuleEvaluator rules)
        {
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public ModelFixPreview Prepare(AnalysisRunResult baseline, IEnumerable<RuleResult> selected) => Prepare(baseline, selected, true);

        public void ValidateAsset(AnalysisRunResult baseline, IEnumerable<RuleResult> selected) => Prepare(baseline, selected, false);

        private ModelFixPreview Prepare(AnalysisRunResult baseline, IEnumerable<RuleResult> selected, bool checkWholeContext)
        {
            if (baseline == null || string.IsNullOrEmpty(baseline.Context.SettingsJson))
                throw new InvalidOperationException("Run analysis before preparing a model preview.");
            var findings = selected?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(selected));
            if (findings.Length == 0) throw new InvalidOperationException("Select at least one model proposal.");
            var originalFindings = new HashSet<RuleResult>(baseline.ModelEvaluation.Findings);
            foreach (var finding in findings)
                if (finding == null || !originalFindings.Contains(finding) ||
                    finding.Proposal == null || string.IsNullOrEmpty(finding.Resource?.Guid))
                    throw new InvalidOperationException("The selected model proposal does not belong to this analysis.");

            var settings = baseline.Context.CopySettings();
            var expected = baseline.ModelCollection;
            var current = _analyzer.Collect(baseline.Context.Scope.AssetPaths, expected.Context.TargetPlatform, settings.Mod004.Enabled);
            if (!AnalysisBuildTarget.IsKnown(expected.Context.TargetPlatform) ||
                expected.Context.TargetPlatform != expected.Context.ActivePlatform ||
                current.Context.TargetPlatform != expected.Context.TargetPlatform ||
                current.Context.ActivePlatform != expected.Context.ActivePlatform ||
                expected.Context.TargetPlatform != baseline.Context.TargetPlatform)
                throw new InvalidOperationException("Build target changed or is Unknown. Run analysis again.");
            if (settings.Mod004.Enabled && ((checkWholeContext && (string.IsNullOrEmpty(expected.Context.LodContextVersion) ||
                expected.Context.LodContextVersion != current.Context.LodContextVersion)) ||
                expected.Context.LodContextComplete != current.Context.LodContextComplete))
                throw new InvalidOperationException("Model or prefab LOD context changed. Run analysis again.");

            var evaluation = _rules.Evaluate(current, settings);
            var assets = new List<ModelAssetPreview>();
            var previousModels = expected.Models.ToDictionary(model => model.Guid, StringComparer.Ordinal);
            var currentModels = current.Models.ToDictionary(model => model.Guid, StringComparer.Ordinal);
            var proposals = new HashSet<(string, string, Type)>(evaluation.Findings
                .Select(f => (f.RuleId, f.Resource?.Identity, f.Proposal?.GetType())));
            foreach (var group in findings.GroupBy(f => f.Resource.Guid))
            {
                previousModels.TryGetValue(group.Key, out var before);
                currentModels.TryGetValue(group.Key, out var now);
                if (now == null)
                    throw new InvalidOperationException("The model resource is missing or its settings are no longer editable. Run analysis again: " + group.First().Resource.Path);
                if (before == null || !now.HasModelImporter ||
                    string.IsNullOrEmpty(before.ImportVersion) || before.ImportVersion != now.ImportVersion ||
                    before.AssetPath != now.AssetPath || before.HasPendingImporterChanges || now.HasPendingImporterChanges ||
                    before.ReadWriteEnabled != now.ReadWriteEnabled || before.GenerateMeshLods != now.GenerateMeshLods ||
                    before.MaximumMeshLod != now.MaximumMeshLod || before.DiscardOddLodLevels != now.DiscardOddLodLevels ||
                    !SameMeshes(before.Meshes, now.Meshes))
                    throw new InvalidOperationException("Model data or importer settings changed. Apply or revert pending Inspector edits, then run analysis again: " + group.First().Resource.Path);
                foreach (var finding in group)
                    if (!proposals.Contains((finding.RuleId, finding.Resource.Identity, finding.Proposal.GetType())))
                        throw new InvalidOperationException("The model proposal is no longer available. Run analysis again: " + now.AssetPath);
                assets.Add(new ModelAssetPreview(now, group));
            }
            return new ModelFixPreview(current.Context, assets);
        }

        private static bool SameMeshes(IReadOnlyList<MeshFacts> before, IReadOnlyList<MeshFacts> now)
        {
            if (before.Count != now.Count) return false;
            var currentMeshes = now.ToDictionary(mesh => (mesh.Guid, mesh.LocalId));
            foreach (var mesh in before)
            {
                currentMeshes.TryGetValue((mesh.Guid, mesh.LocalId), out var other);
                if (other == null || mesh.Name != other.Name || mesh.VertexCount != other.VertexCount ||
                    mesh.TriangleCount != other.TriangleCount || mesh.TriangleTopologyOnly != other.TriangleTopologyOnly ||
                    mesh.LodCount != other.LodCount ||
                    !mesh.LodGroupPaths.OrderBy(p => p, StringComparer.Ordinal)
                        .SequenceEqual(other.LodGroupPaths.OrderBy(p => p, StringComparer.Ordinal))) return false;
            }
            return true;
        }
    }

    // Pārbaudīti priekšskatījuma dati; tie nepiešķir atļauju mainīt resursus.
    internal sealed class ModelFixPreview
    {
        public ModelAnalysisContext Context { get; }
        public IReadOnlyList<ModelAssetPreview> Assets { get; }
        public ModelFixPreview(ModelAnalysisContext context, IEnumerable<ModelAssetPreview> assets)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Assets = new List<ModelAssetPreview>(assets).AsReadOnly();
        }
    }

    internal sealed class ModelAssetPreview
    {
        public ModelFacts Model { get; }
        public IReadOnlyList<RuleResult> Findings { get; }
        public ModelAssetPreview(ModelFacts model, IEnumerable<RuleResult> findings)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Findings = new List<RuleResult>(findings).AsReadOnly();
        }
    }
}
