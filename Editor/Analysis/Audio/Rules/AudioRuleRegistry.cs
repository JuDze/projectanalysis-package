using System;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules
{
    internal static class AudioRuleRegistry
    {
        // RAM novērtējumam vajadzīgie dati jāvāc arī tad, ja AUD005 ir izslēgts.
        public static bool RequiresRuntimeSize(RuleSettings settings) => settings.Aud005.Enabled ||
            settings.Aud001.Enabled && settings.Aud001.Goal == AudioOptimizationGoal.ReduceRamUsage;

        public static RuleEngine<AudioFacts, AudioAnalysisContext> Create(RuleSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return new RuleEngine<AudioFacts, AudioAnalysisContext>(new IRule<AudioFacts, AudioAnalysisContext>[]
            {
                new Aud001LoadTypeRule(settings.Aud001, settings.Aud005.RuntimeSizeThresholdBytes, settings.Aud005.RuntimeSizeHighThresholdBytes), new Aud002CompressionRule(settings.Aud002),
                new Aud003SampleRateRule(settings.Aud003), new Aud004MonoRule(settings.Aud004), new Aud005StreamingRule(settings.Aud005)
            });
        }
    }
}
