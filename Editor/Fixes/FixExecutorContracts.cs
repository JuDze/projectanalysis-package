using Judze.ProjectAnalysis.Editor.Analysis.Textures;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    // Unity operācijas ir nodalītas no plānošanas, stāvokļa glabāšanas un izpildes secības.
    internal interface ITextureFixExecutor
    {
        TextureAnalysisContext ReadContext(BuildTarget target);
        TextureImporterSnapshot Capture(string guid);
        void Validate(TextureAssetFixPlan asset, TextureAnalysisContext context);
        void Apply(TextureAssetFixPlan asset);
        void Restore(TextureAssetFixPlan asset);
    }

    // Izpildītājs maina tikai apstiprinātās viena importētāja vērtības.
    internal interface IModelFixExecutor
    {
        ModelImporterSnapshot Capture(string guid);
        void Validate(ModelAssetFixPlan asset, BuildTarget target);
        void Apply(ModelAssetFixPlan asset);
        void Restore(ModelAssetFixPlan asset);
    }

    internal interface IAudioFixExecutor
    {
        AudioImporterSnapshot Capture(string guid);
        void Validate(AudioAssetFixPlan asset, BuildTarget target);
        void Apply(AudioAssetFixPlan asset);
        void Restore(AudioAssetFixPlan asset);
    }
}
