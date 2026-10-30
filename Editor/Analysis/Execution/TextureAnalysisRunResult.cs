using System;
using UnityEngine;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Settings;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Scope;
using Judze.ProjectAnalysis.Editor.Analysis.Textures;

namespace Judze.ProjectAnalysis.Editor.Analysis.Execution
{
    // Palaišanas dati saglabā faktus un iestatījumu kopiju atkārtotai pārbaudei.
    internal sealed class TextureAnalysisRunResult
    {
        public ResolvedAnalysisScope Scope { get; }
        public IReadOnlyList<TextureFacts> Textures { get; }
        public string SettingsJson { get; }
        public RuleSettings CopySettings() => JsonUtility.FromJson<RuleSettings>(SettingsJson);
        public TextureAnalysisContext Context { get; }
        public int CollectedTextureCount { get; }
        public RuleEvaluation Evaluation { get; }

        public static TextureAnalysisRunResult Empty(AnalysisRunContext context, RuleEvaluation evaluation = null) =>
            new TextureAnalysisRunResult(context.Scope, new TextureAnalysisContext(context.TargetPlatform, context.Project),
                0, evaluation ?? new RuleEvaluation(), settings: context.CopySettings());

        public TextureAnalysisRunResult(ResolvedAnalysisScope scope, TextureAnalysisContext context,
            int collectedTextureCount, RuleEvaluation evaluation,
            IEnumerable<TextureFacts> textures = null, RuleSettings settings = null)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            if (collectedTextureCount < 0) throw new ArgumentOutOfRangeException(nameof(collectedTextureCount));
            CollectedTextureCount = collectedTextureCount;
            Textures = new List<TextureFacts>(textures ?? Array.Empty<TextureFacts>()).AsReadOnly();
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            SettingsJson = settings == null ? null : JsonUtility.ToJson(settings);
            Evaluation = evaluation ?? throw new ArgumentNullException(nameof(evaluation));
        }
    }
}
