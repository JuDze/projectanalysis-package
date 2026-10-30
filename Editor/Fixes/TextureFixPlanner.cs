using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Textures;
using Judze.ProjectAnalysis.Editor.Analysis.Textures.Rules;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    // Tipizēti apstrādātāji tikai apvieno priekšlikumus; tie nemaina Unity resursus.
    internal sealed class TextureFixPlanner
    {
        private readonly ITextureFixExecutor _executor;
        private sealed class Changes
        {
            public int? Size;
            public bool? Mipmaps;
            public bool? Readable, Streaming;
        }
        private readonly Dictionary<Type, Action<IChangeProposal, Changes>> _handlers =
            new Dictionary<Type, Action<IChangeProposal, Changes>>
            {
                { typeof(TextureResizeProposal), (p, c) => Set(ref c.Size, ((TextureResizeProposal)p).ProposedMaxSize) },
                { typeof(TextureMipmapsProposal), (p, c) => Set(ref c.Mipmaps, false) },
                { typeof(TextureReadWriteProposal), (p, c) => Set(ref c.Readable, false) },
                { typeof(TextureStreamingProposal), (p, c) => Set(ref c.Streaming, true) }
            };

        public TextureFixPlanner(ITextureFixExecutor executor) { _executor = executor; }
        public TextureFixPlan Prepare(TextureAnalysisRunResult baseline, IEnumerable<RuleResult> selected)
        {
            if (baseline == null || string.IsNullOrEmpty(baseline.SettingsJson))
                throw new InvalidOperationException("Run analysis before preparing fixes.");
            RequireContext(baseline.Context, _executor.ReadContext(baseline.Context.TargetPlatform));
            var findings = selected?.Distinct().ToList() ?? throw new ArgumentNullException(nameof(selected));
            if (findings.Count == 0) throw new InvalidOperationException("Select at least one fix.");
            var originalFindings = new HashSet<RuleResult>(baseline.Evaluation.Findings);
            foreach (var finding in findings)
                if (finding == null || !originalFindings.Contains(finding) || finding.Resource == null || finding.Proposal == null ||
                    string.IsNullOrEmpty(finding.Resource.Guid))
                    throw new InvalidOperationException("The selected fix does not belong to this analysis.");
            var assets = new List<TextureAssetFixPlan>();
            var textures = baseline.Textures.ToDictionary(texture => texture.Guid, StringComparer.Ordinal);
            foreach (var group in findings.GroupBy(f => f.Resource.Guid))
            {
                textures.TryGetValue(group.Key, out var facts);
                var before = _executor.Capture(group.Key);
                if (facts == null || string.IsNullOrEmpty(facts.ImportVersion) || facts.ImportVersion != before.ImportVersion)
                    throw new InvalidOperationException("Texture settings have changed. Run analysis again: " + before.Path);
                var changes = new Changes();
                foreach (var finding in group)
                {
                    if (!_handlers.TryGetValue(finding.Proposal.GetType(), out var handler))
                        throw new NotSupportedException("This proposal has no supported fix handler.");
                    handler(finding.Proposal, changes);
                }
                var asset = new TextureAssetFixPlan(before, group, changes.Size, changes.Mipmaps, changes.Readable, changes.Streaming);
                _executor.Validate(asset, baseline.Context);
                assets.Add(asset);
            }
            return new TextureFixPlan(baseline, assets);
        }

        public IEnumerable<IAssetFixOperation> CreateOperations(TextureFixPlan plan) =>
            plan.Assets.Select(asset => new TextureFixOperation(asset, plan.Baseline, _executor));

        public static void RequireContext(TextureAnalysisContext expected, TextureAnalysisContext actual)
        {
            if (!expected.ImportedDataMatchesTarget || !actual.ImportedDataMatchesTarget ||
                expected.TargetPlatform != actual.TargetPlatform || expected.ActiveBuildTarget != actual.ActiveBuildTarget ||
                expected.ImportSettingsPlatform != actual.ImportSettingsPlatform || expected.QualityLevelIndex != actual.QualityLevelIndex ||
                expected.QualityLevelName != actual.QualityLevelName || expected.GlobalStreamingMipmapsActive != actual.GlobalStreamingMipmapsActive)
                throw new InvalidOperationException("Project analysis context changed. Run analysis and review the fixes again.");
        }

        private static void Set<T>(ref T? destination, T value) where T : struct
        {
            if (destination.HasValue && !EqualityComparer<T>.Default.Equals(destination.Value, value))
                throw new InvalidOperationException("Selected fixes contain conflicting values.");
            destination = value;
        }
    }
}
