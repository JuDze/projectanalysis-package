using System;
using UnityEditor;
using Judze.ProjectAnalysis.Editor.Analysis.Audio;
using Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Models;
using Judze.ProjectAnalysis.Editor.Analysis.Models.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Scope;
using Judze.ProjectAnalysis.Editor.Analysis.Textures;
using Judze.ProjectAnalysis.Editor.Analysis.Textures.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Execution
{
    // Augstākā līmeņa vadība izmanto savācēju un izvērtētāju saskarnes.
    internal sealed class AnalysisController
    {
        private readonly IAnalysisScopeResolver _scope;
        private readonly IAnalysisProjectContextProvider _projectContext;
        private readonly ITextureAnalyzer _textures;
        private readonly ITextureRuleEvaluator _textureRules;
        private readonly IModelAnalyzer _models;
        private readonly IModelRuleEvaluator _modelRules;
        private readonly IAudioAnalyzer _audio;
        private readonly IAudioRuleEvaluator _audioRules;

        public AnalysisController(IAnalysisScopeResolver scope, ITextureAnalyzer textures, ITextureRuleEvaluator textureRules,
            IModelAnalyzer models, IModelRuleEvaluator modelRules, IAudioAnalyzer audio, IAudioRuleEvaluator audioRules,
            IAnalysisProjectContextProvider projectContext)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
            _textures = textures ?? throw new ArgumentNullException(nameof(textures));
            _textureRules = textureRules ?? throw new ArgumentNullException(nameof(textureRules));
            _models = models ?? throw new ArgumentNullException(nameof(models));
            _modelRules = modelRules ?? throw new ArgumentNullException(nameof(modelRules));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _audioRules = audioRules ?? throw new ArgumentNullException(nameof(audioRules));
        }

        public AnalysisRunResult Run(AnalysisScopeSelection selection, RuleSettings settings, BuildTarget targetPlatform)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            // Tvērumu nosaka vienreiz; visas resursu grupas saņem vienu datņu kopu.
            return RunResolved(_scope.Resolve(selection), settings, targetPlatform);
        }

        public AnalysisRunResult RunResolved(ResolvedAnalysisScope scope, RuleSettings settings, BuildTarget targetPlatform)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return Execute(new AnalysisRunContext(scope, settings, targetPlatform, _projectContext.Capture()));
        }

        // Atkārtotā pārbaude atkārtoti izmanto sākotnējo kontekstu, nevis kādas grupas rezultātu.
        public AnalysisRunResult RunResolved(AnalysisRunContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            // Atkārtota pārbaude saglabā atlasi un iestatījumus, bet pārlasa pašreizējo projekta stāvokli.
            return Execute(new AnalysisRunContext(context.Scope, context.CopySettings(),
                context.TargetPlatform, _projectContext.Capture()));
        }

        private AnalysisRunResult Execute(AnalysisRunContext context)
        {
            var settings = context.CopySettings()
                ?? throw new InvalidOperationException("The analysis run has no saved settings snapshot.");
            var targetPlatform = context.TargetPlatform;
            // Katra grupa saņem savu iestatījumu kopiju; vienas grupas izmaiņas neietekmē nākamo.
            var textureRun = TextureAnalysisRunResult.Empty(context);
            if (AnalysisBuildTarget.IsKnown(targetPlatform) && settings.HasEnabledTextureRules)
            {
                try
                {
                    if (!AnalysisBuildTarget.IsStandalone(targetPlatform))
                        textureRun = TextureAnalysisRunResult.Empty(context, new RuleEvaluation(diagnostics: new[]
                        {
                            new RuleDiagnostic("TEXTURE_ANALYSIS", null,
                                $"Texture analysis does not yet support {targetPlatform} import settings.")
                        }));
                    else
                    {
                        var textures = _textures.Collect(context.Scope.AssetPaths,
                            new TextureAnalysisContext(targetPlatform, context.Project));
                        if (textures.Context.TargetPlatform != targetPlatform)
                            throw new InvalidOperationException("Texture collection does not match the analysis build target.");
                        var textureSettings = context.CopySettings();
                        var textureEvaluation = _textureRules.Evaluate(textures, textureSettings);
                        textureRun = new TextureAnalysisRunResult(context.Scope, textures.Context, textures.Textures.Count,
                            textureEvaluation, textures.Textures, textureSettings);
                    }
                }
                catch (Exception exception)
                {
                    textureRun = TextureAnalysisRunResult.Empty(context, new RuleEvaluation(diagnostics: new[]
                    { new RuleDiagnostic("TEXTURE_ANALYSIS", null, "Texture analysis failed: " + exception.Message) }));
                }
            }
            var models = new ModelCollectionResult(null,
                new ModelAnalysisContext(targetPlatform, context.Project.ActiveBuildTarget));
            var modelEvaluation = new RuleEvaluation();
            if (AnalysisBuildTarget.IsKnown(targetPlatform) && settings.HasEnabledModelRules)
            {
                try
                {
                    models = _models.Collect(context.Scope.AssetPaths, targetPlatform, settings.Mod004.Enabled);
                    if (models.Context.TargetPlatform != targetPlatform)
                        throw new InvalidOperationException("Model collection does not match the analysis build target.");
                    modelEvaluation = _modelRules.Evaluate(models, context.CopySettings());
                }
                catch (Exception exception)
                {
                    modelEvaluation = new RuleEvaluation(diagnostics: new[]
                    { new RuleDiagnostic("MODEL_ANALYSIS", null, "Model analysis failed: " + exception.Message) });
                }
            }
            var audio = new AudioCollectionResult(null,
                new AudioAnalysisContext(targetPlatform, context.Project.ActiveBuildTarget));
            var audioEvaluation = new RuleEvaluation();
            if (AnalysisBuildTarget.IsKnown(targetPlatform) && settings.HasEnabledAudioRules)
            {
                try
                {
                    audio = _audio.Collect(context.Scope.AssetPaths, targetPlatform, AudioRuleRegistry.RequiresRuntimeSize(settings));
                    if (audio.Context.TargetPlatform != targetPlatform)
                        throw new InvalidOperationException("Audio collection does not match the analysis build target.");
                    audioEvaluation = _audioRules.Evaluate(audio, context.CopySettings());
                }
                catch (Exception exception)
                {
                    // Vienas grupas kļūda neatceļ citu grupu rezultātus vai jau piemērotus TEX labojumus.
                    audioEvaluation = new RuleEvaluation(diagnostics: new[]
                    { new RuleDiagnostic("AUDIO_ANALYSIS", null, "Audio analysis failed: " + exception.Message) });
                }
            }
            return new AnalysisRunResult(textureRun, models, modelEvaluation, audio, audioEvaluation, context);
        }
    }
}
