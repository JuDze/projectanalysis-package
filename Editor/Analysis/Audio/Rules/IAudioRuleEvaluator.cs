using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules
{
    internal interface IAudioRuleEvaluator
    {
        RuleEvaluation Evaluate(AudioCollectionResult collection, RuleSettings savedSettings);
    }
}
