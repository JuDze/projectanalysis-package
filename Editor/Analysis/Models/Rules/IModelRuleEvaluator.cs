using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Models.Rules
{
    internal interface IModelRuleEvaluator
    {
        RuleEvaluation Evaluate(ModelCollectionResult collection, RuleSettings savedSettings);
    }
}
