using System;
using System.Linq;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Scope;
using Judze.ProjectAnalysis.Editor.Settings;
using UnityEditor;
using UnityEngine;
using Judze.ProjectAnalysis.Editor.Analysis.Audio;
using Judze.ProjectAnalysis.Editor.Analysis.Models;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;

namespace Judze.ProjectAnalysis.Editor.Analysis.Execution
{
    // Kopīgais konteksts pastāv pirms resursu grupu izpildes un saglabā nemainīgu iestatījumu kopiju.
    internal sealed class AnalysisRunContext
    {
        public ResolvedAnalysisScope Scope { get; }
        public BuildTarget TargetPlatform { get; }
        public AnalysisProjectContext Project { get; }
        public IReadOnlyList<RuleDiagnostic> Diagnostics { get; }
        public string SettingsJson { get; }
        public RuleSettings CopySettings() => JsonUtility.FromJson<RuleSettings>(SettingsJson);

        public AnalysisRunContext(ResolvedAnalysisScope scope, RuleSettings settings, BuildTarget targetPlatform,
            AnalysisProjectContext project)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            TargetPlatform = AnalysisBuildTarget.Normalize(targetPlatform);
            Project = project ?? throw new ArgumentNullException(nameof(project));
            var diagnostics = scope.Messages.Select(message => new RuleDiagnostic("SCOPE", null, message)).ToList();
            if (!AnalysisBuildTarget.IsKnown(TargetPlatform))
                diagnostics.Add(new RuleDiagnostic("PLATFORM", null,
                    "Build target is Unknown. Platform-dependent analysis was skipped."));
            Diagnostics = diagnostics.AsReadOnly();
            SettingsJson = settings == null ? null : JsonUtility.ToJson(settings);
        }
    }

    // Resursu grupu rezultāti izmanto vienu palaišanas kontekstu.
    internal sealed class AnalysisRunResult
    {
        public AnalysisRunContext Context { get; }
        public TextureAnalysisRunResult TextureRun { get; }
        public ModelCollectionResult ModelCollection { get; }
        public RuleEvaluation ModelEvaluation { get; }
        public AudioCollectionResult AudioCollection { get; }
        public RuleEvaluation AudioEvaluation { get; }
        public RuleEvaluation Evaluation { get; }

        public AnalysisRunResult(TextureAnalysisRunResult textureRun, ModelCollectionResult modelCollection,
            RuleEvaluation modelEvaluation, AudioCollectionResult audioCollection, RuleEvaluation audioEvaluation,
            AnalysisRunContext context)
        {
            TextureRun = textureRun ?? throw new ArgumentNullException(nameof(textureRun));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            ModelCollection = modelCollection ?? throw new ArgumentNullException(nameof(modelCollection));
            ModelEvaluation = modelEvaluation ?? throw new ArgumentNullException(nameof(modelEvaluation));
            AudioCollection = audioCollection ?? throw new ArgumentNullException(nameof(audioCollection));
            AudioEvaluation = audioEvaluation ?? throw new ArgumentNullException(nameof(audioEvaluation));
            Evaluation = new RuleEvaluation(textureRun.Evaluation.Findings.Concat(modelEvaluation.Findings).Concat(audioEvaluation.Findings),
                textureRun.Evaluation.Diagnostics.Concat(modelEvaluation.Diagnostics).Concat(audioEvaluation.Diagnostics).Concat(context.Diagnostics));
        }

    }
}
