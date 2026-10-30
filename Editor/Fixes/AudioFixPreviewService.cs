using Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Audio;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    // Pārlasa atlasītos resursus un atkārtoti izmanto noteikumus; importa iestatījumus nemaina.
    internal sealed class AudioFixPreviewService
    {
        private readonly IAudioAnalyzer _analyzer;
        private readonly IAudioRuleEvaluator _rules;
        public AudioFixPreviewService(IAudioAnalyzer analyzer, IAudioRuleEvaluator rules)
        {
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public AudioFixPreview Prepare(AnalysisRunResult baseline, IEnumerable<RuleResult> selected)
        {
            if (baseline == null || string.IsNullOrEmpty(baseline.Context.SettingsJson))
                throw new InvalidOperationException("Run analysis before preparing an audio preview.");
            var findings = selected?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(selected));
            if (findings.Length == 0) throw new InvalidOperationException("Select at least one audio proposal.");
            var originalFindings = new HashSet<RuleResult>(baseline.AudioEvaluation.Findings);
            foreach (var finding in findings)
                if (finding == null || !originalFindings.Contains(finding) ||
                    finding.Proposal == null || string.IsNullOrEmpty(finding.Resource?.Guid))
                    throw new InvalidOperationException("The selected audio proposal does not belong to this analysis.");

            var expected = baseline.AudioCollection;
            var context = expected.Context;
            if (!context.SupportsStandalone || !context.HasMatchingImport ||
                context.TargetPlatform != baseline.Context.TargetPlatform)
                throw new InvalidOperationException("Build target is unsupported, changed or Unknown. Run analysis again.");
            var settings = baseline.Context.CopySettings();
            var current = _analyzer.Collect(findings.Select(f => f.Resource.Path).Distinct().ToArray(),
                context.TargetPlatform, AudioRuleRegistry.RequiresRuntimeSize(settings));
            if (!current.Context.HasMatchingImport || current.Context.TargetPlatform != context.TargetPlatform ||
                current.Context.ActivePlatform != context.ActivePlatform)
                throw new InvalidOperationException("Build target changed or is Unknown. Run analysis again.");
            var evaluation = _rules.Evaluate(current, settings);
            var assets = new List<AudioAssetPreview>();
            var previousClips = expected.Clips.ToDictionary(clip => (clip.Guid, (long?)clip.LocalId));
            var currentClips = current.Clips.ToDictionary(clip => (clip.Guid, (long?)clip.LocalId));
            var proposals = evaluation.Findings.ToLookup(f => (f.RuleId, f.Resource?.Identity));
            foreach (var group in findings.GroupBy(f => f.Resource.Identity))
            {
                var resource = group.First().Resource;
                previousClips.TryGetValue((resource.Guid, resource.LocalId), out var before);
                currentClips.TryGetValue((resource.Guid, resource.LocalId), out var now);
                if (now == null)
                    throw new InvalidOperationException("The audio resource is missing or its settings are no longer editable. Run analysis again: " + resource.Path);
                if (!SameClip(before, now))
                    throw new InvalidOperationException("Audio data or import settings changed. Apply or revert pending Inspector edits, then run analysis again: " + resource.Path);
                foreach (var finding in group)
                    if (!proposals[(finding.RuleId, resource.Identity)].Any(f => SameProposal(finding.Proposal, f.Proposal)))
                        throw new InvalidOperationException("The audio proposal is no longer available. Run analysis again: " + now.AssetPath);
                ValidateCombination(now, group.ToArray());
                assets.Add(new AudioAssetPreview(now, group));
            }
            return new AudioFixPreview(current.Context, assets);
        }

        private static bool SameClip(AudioFacts before, AudioFacts now) => before != null && now != null &&
            !string.IsNullOrEmpty(before.ImportVersion) && before.ImportVersion == now.ImportVersion &&
            !before.HasPendingImporterChanges && !now.HasPendingImporterChanges &&
            before.AssetPath == now.AssetPath && before.SampleSettings.Equals(now.SampleSettings) &&
            before.HasOverride == now.HasOverride && before.ForceToMono == now.ForceToMono && before.Ambisonic == now.Ambisonic &&
            before.Frequency == now.Frequency && before.Channels == now.Channels && before.Samples == now.Samples &&
            before.LengthSeconds == now.LengthSeconds && before.CurrentLoadType == now.CurrentLoadType;

        private static bool SameProposal(IChangeProposal before, IChangeProposal now)
        {
            if (before == null || now == null || before.GetType() != now.GetType()) return false;
            // Salīdzina tipizētās vērtības, nevis noapaļotu attēlošanas tekstu vai noteikuma ID.
            switch (before)
            {
                case AudioLoadTypeProposal load:
                    var nextLoad = (AudioLoadTypeProposal)now;
                    return load.Current == nextLoad.Current && load.Target == nextLoad.Target;
                case AudioCompressionProposal compression:
                    var nextCompression = (AudioCompressionProposal)now;
                    return compression.Current == nextCompression.Current && compression.Target == nextCompression.Target &&
                        compression.VorbisQuality == nextCompression.VorbisQuality;
                case AudioSampleRateProposal rate:
                    return rate.Current == ((AudioSampleRateProposal)now).Current && rate.Target == ((AudioSampleRateProposal)now).Target;
                case AudioMonoProposal _: return true;
                default: return false;
            }
        }

        private static void ValidateCombination(AudioFacts clip, IReadOnlyList<RuleResult> findings)
        {
            var loads = findings.Where(f => f.Proposal is AudioLoadTypeProposal).ToArray();
            if (loads.Select(f => ((AudioLoadTypeProposal)f.Proposal).Target).Distinct().Count() > 1)
                throw new InvalidOperationException(clip.AssetPath + ": " + string.Join(" and ", loads.Select(f => f.RuleId)) +
                    " propose different Load Type values. Select one proposal.");
            var formats = findings.Where(f => f.Proposal is AudioCompressionProposal).ToArray();
            if (formats.Select(f => ((AudioCompressionProposal)f.Proposal).Target).Distinct().Count() > 1)
                throw new InvalidOperationException(clip.AssetPath + ": selected proposals contain conflicting compression formats.");
            var format = formats.Length == 0 ? clip.SampleSettings.compressionFormat : ((AudioCompressionProposal)formats[0].Proposal).Target;
            if (findings.Any(f => f.Proposal is AudioSampleRateProposal) &&
                format != AudioCompressionFormat.PCM && format != AudioCompressionFormat.ADPCM)
                throw new InvalidOperationException(clip.AssetPath +
                    ": the selected compression change conflicts with Optimize Sample Rate. This tool supports that change only for PCM and ADPCM. Select compatible proposals.");
        }
    }

    // Pārbaudīti dati priekšskatījumam; šis objekts nepiešķir atļauju mainīt resursus.
    internal sealed class AudioFixPreview
    {
        public AudioAnalysisContext Context { get; }
        public IReadOnlyList<AudioAssetPreview> Assets { get; }
        public AudioFixPreview(AudioAnalysisContext context, IEnumerable<AudioAssetPreview> assets)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Assets = new List<AudioAssetPreview>(assets).AsReadOnly();
        }
    }

    // Priekšlikumus grupē tikai savstarpējai pārbaudei; saskarnē katram ir sava kartīte.
    internal sealed class AudioAssetPreview
    {
        public AudioFacts Clip { get; }
        public IReadOnlyList<RuleResult> Findings { get; }
        public AudioAssetPreview(AudioFacts clip, IEnumerable<RuleResult> findings)
        {
            Clip = clip ?? throw new ArgumentNullException(nameof(clip));
            Findings = new List<RuleResult>(findings).AsReadOnly();
        }
    }
}
