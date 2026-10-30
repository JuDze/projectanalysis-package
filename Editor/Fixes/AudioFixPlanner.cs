using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal sealed class AudioFixPlanner
    {
        private readonly AudioFixPreviewService _review;
        private readonly IAudioFixExecutor _executor;
        private sealed class Changes
        {
            public AudioImporterSampleSettings Settings;
            public bool Platform;
            public bool? Mono;
        }
        private readonly Dictionary<Type, Action<Changes, IChangeProposal>> _handlers =
            new Dictionary<Type, Action<Changes, IChangeProposal>>
        {
            { typeof(AudioLoadTypeProposal), (c, p) => { c.Platform = true; c.Settings.loadType = ((AudioLoadTypeProposal)p).Target; } },
            { typeof(AudioCompressionProposal), (c, p) =>
                {
                    var value = (AudioCompressionProposal)p; c.Platform = true; c.Settings.compressionFormat = value.Target;
                    if (value.VorbisQuality.HasValue) c.Settings.quality = value.VorbisQuality.Value;
                }
            },
            { typeof(AudioSampleRateProposal), (c, p) => { c.Platform = true; c.Settings.sampleRateSetting = ((AudioSampleRateProposal)p).Target; } },
            { typeof(AudioMonoProposal), (c, p) => c.Mono = true }
        };
        public AudioFixPlanner(AudioFixPreviewService review, IAudioFixExecutor executor)
        { _review = review ?? throw new ArgumentNullException(nameof(review)); _executor = executor ?? throw new ArgumentNullException(nameof(executor)); }
        public AudioFixPlan Prepare(AnalysisRunResult run, IEnumerable<RuleResult> selected)
        {
            var preview = _review.Prepare(run, selected);
            var assets = new List<AudioAssetFixPlan>();
            foreach (var item in preview.Assets)
            {
                var before = _executor.Capture(item.Clip.Guid);
                if (before.ImportVersion != item.Clip.ImportVersion || before.Path != item.Clip.AssetPath ||
                    before.Effective == null || !before.Effective.Matches(item.Clip.SampleSettings) ||
                    before.HasOverride != item.Clip.HasOverride || before.ForceToMono != item.Clip.ForceToMono)
                    throw new InvalidOperationException("Audio settings changed while preparing the plan. Run analysis again.");
                var changes = new Changes { Settings = before.Effective.ToSettings() };
                foreach (var finding in item.Findings)
                {
                    if (!_handlers.TryGetValue(finding.Proposal.GetType(), out var handler))
                        throw new NotSupportedException("This audio proposal has no supported fix handler.");
                    handler(changes, finding.Proposal);
                }
                var asset = new AudioAssetFixPlan(before, item, changes.Settings, changes.Platform, changes.Mono);
                _executor.Validate(asset, run.Context.TargetPlatform); assets.Add(asset);
            }
            return new AudioFixPlan(run, preview, assets);
        }
        public void ValidateContext(AudioFixPlan plan) => _review.Prepare(plan.Baseline, plan.Assets.SelectMany(a => a.Findings));
        public IEnumerable<IAssetFixOperation> CreateOperations(AudioFixPlan plan) =>
            plan.Assets.Select(asset => new AudioFixOperation(asset, plan.Baseline, _executor, _review));
    }
}
