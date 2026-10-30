using System;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Models.Rules
{
    internal sealed class ModelRuleEvaluator : IModelRuleEvaluator
    {
        public static RuleEngine<ModelFacts, ModelAnalysisContext> Create(RuleSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return new RuleEngine<ModelFacts, ModelAnalysisContext>(new IRule<ModelFacts, ModelAnalysisContext>[]
            {
                new Mod001VertexCountRule(settings.Mod001), new Mod002TriangleCountRule(settings.Mod002),
                new Mod003ReadWriteRule(settings.Mod003),
                new Mod004MeshLodRule(settings.Mod004, settings.Mod002.TriangleThreshold, settings.Mod002.TriangleHighThreshold)
            });
        }
        public RuleEvaluation Evaluate(ModelCollectionResult collection, RuleSettings settings)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var evaluation = Create(settings).Evaluate(collection.Models, collection.Context);
            return new RuleEvaluation(evaluation.Findings, collection.Diagnostics.Concat(evaluation.Diagnostics));
        }
    }
}
