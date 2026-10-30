using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Textures.Rules
{
    internal interface ITextureRuleEvaluator
    {
        // Izvērtē savāktos faktus ar saglabātajiem iestatījumiem.
        RuleEvaluation Evaluate(TextureCollectionResult collection, RuleSettings savedSettings);
    }
}
