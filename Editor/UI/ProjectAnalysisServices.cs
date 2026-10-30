using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Scope;
using Judze.ProjectAnalysis.Editor.Analysis.Textures;
using Judze.ProjectAnalysis.Editor.Analysis.Textures.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Models;
using Judze.ProjectAnalysis.Editor.Analysis.Models.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Audio;
using Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules;
using Judze.ProjectAnalysis.Editor.Fixes;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Vienuviet savieno savācējus, noteikumus, labojumu izpildi un atcelšanas rūpnīcas.
    internal sealed class ProjectAnalysisServices
    {
        public IAnalysisScopeResolver Scope { get; }
        public AnalysisController Analysis { get; }
        public FixManager Fixes { get; }
        public AnalysisFixPreviewCoordinator Previews { get; }
        public FixRevertPlanner Reverts { get; }

        internal ProjectAnalysisServices(IAnalysisScopeResolver scope, AnalysisController analysis,
            FixManager fixes, AnalysisFixPreviewCoordinator previews, FixRevertPlanner reverts)
        {
            Scope = scope; Analysis = analysis; Fixes = fixes; Previews = previews; Reverts = reverts;
        }

        public static ProjectAnalysisServices CreateDefault(IFixOperationStore state)
        {
            var scope = new UnityAnalysisScopeResolver();
            var textures = new UnityTextureAnalyzer();
            var textureRules = new TextureRuleEvaluator();
            var models = new UnityModelAnalyzer();
            var modelRules = new ModelRuleEvaluator();
            var audio = new UnityAudioAnalyzer(new UnityAudioImportSizeReader());
            var audioRules = new AudioRuleEvaluator();
            var analysis = new AnalysisController(scope, textures, textureRules, models, modelRules, audio, audioRules,
                new UnityAnalysisProjectContextProvider());
            var textureExecutor = new UnityTextureFixExecutor();
            var modelExecutor = new UnityModelFixExecutor();
            var audioExecutor = new UnityAudioFixExecutor();
            var previews = new AnalysisFixPreviewCoordinator(new TextureFixPlanner(textureExecutor),
                new ModelFixPlanner(new ModelFixPreviewService(models, modelRules), modelExecutor),
                new AudioFixPlanner(new AudioFixPreviewService(audio, audioRules), audioExecutor));
            var reverts = new FixRevertPlanner(new IAssetRevertFactory[]
            {
                new UnityTextureRevertFactory(textureExecutor), new UnityModelRevertFactory(modelExecutor),
                new UnityAudioRevertFactory(audioExecutor)
            });
            return new ProjectAnalysisServices(scope, analysis,
                new FixManager(state, new AnalysisFixRechecker(analysis)), previews, reverts);
        }
    }
}
