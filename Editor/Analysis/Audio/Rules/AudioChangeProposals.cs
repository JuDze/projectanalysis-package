using System.Globalization;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using UnityEditor;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules
{
    // Priekšlikumi apraksta izmaiņas; piemērošana un lietotāja apstiprinājums ir atsevišķs posms.
    internal sealed class AudioLoadTypeProposal : IChangeProposal
    {
        public RuleResource Resource { get; }
        public AudioClipLoadType Current { get; }
        public AudioClipLoadType Target { get; }
        public string Summary => $"Standalone Load Type: {AudioRulePolicy.LoadTypeName(Current)} → {AudioRulePolicy.LoadTypeName(Target)}. Other import settings remain unchanged.";
        public AudioLoadTypeProposal(RuleResource resource, AudioClipLoadType current, AudioClipLoadType target)
        { Resource = resource; Current = current; Target = target; }
    }

    internal sealed class AudioCompressionProposal : IChangeProposal
    {
        public RuleResource Resource { get; }
        public AudioCompressionFormat Current { get; }
        public AudioCompressionFormat Target { get; }
        public float? VorbisQuality { get; }
        public string Summary => $"Standalone Compression Format: {Current} → {Target}." +
            (VorbisQuality.HasValue ? " Vorbis quality: " + VorbisQuality.Value.ToString("0.###", CultureInfo.InvariantCulture) + "." : "") +
            " Load Type and sample-rate settings remain unchanged.";
        public AudioCompressionProposal(RuleResource resource, AudioCompressionFormat current,
            AudioCompressionFormat target, float? vorbisQuality)
        { Resource = resource; Current = current; Target = target; VorbisQuality = vorbisQuality; }
    }

    internal sealed class AudioSampleRateProposal : IChangeProposal
    {
        public RuleResource Resource { get; }
        public AudioSampleRateSetting Current { get; }
        public AudioSampleRateSetting Target => AudioSampleRateSetting.OptimizeSampleRate;
        public string Summary => $"Standalone Sample Rate Setting: {Current} → Optimize Sample Rate. Unity selects the resulting frequency; no fixed output frequency is requested.";
        public AudioSampleRateProposal(RuleResource resource, AudioSampleRateSetting current)
        { Resource = resource; Current = current; }
    }

    internal sealed class AudioMonoProposal : IChangeProposal
    {
        public RuleResource Resource { get; }
        public bool Current => false;
        public bool Target => true;
        public string Summary => "Force To Mono: Off → On. This changes every use of the clip across platforms; confirm that channel separation is unnecessary.";
        public AudioMonoProposal(RuleResource resource) { Resource = resource; }
    }
}
