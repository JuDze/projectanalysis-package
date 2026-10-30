using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Textures.Rules
{
    internal sealed class TextureRuleEvaluator : ITextureRuleEvaluator
    {
        // Katrs konstruktors nokopē tikai savus parametrus; melnraksta izmaiņas izpildi neietekmē.
        public static RuleEngine<TextureFacts, TextureAnalysisContext> Create(RuleSettings savedSettings)
        {
            if (savedSettings == null) throw new ArgumentNullException(nameof(savedSettings));
            return new RuleEngine<TextureFacts, TextureAnalysisContext>(new IRule<TextureFacts, TextureAnalysisContext>[]
            {
                new Tex001ResolutionRule(savedSettings.Tex001),
                new Tex002ReadWriteRule(savedSettings.Tex002),
                new Tex003SpriteMipmapsRule(savedSettings.Tex003),
                new Tex004StreamingRule(savedSettings.Tex004)
            });
        }

        public RuleEvaluation Evaluate(TextureCollectionResult collection, RuleSettings savedSettings)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (savedSettings == null) throw new ArgumentNullException(nameof(savedSettings));
            var result = Create(savedSettings).Evaluate(collection.Textures, collection.Context);
            var diagnostics = new List<RuleDiagnostic>(result.Diagnostics);
            diagnostics.AddRange(collection.Diagnostics);
            return new RuleEvaluation(result.Findings, diagnostics);
        }
    }
}
