using System.Collections.Generic;

namespace Judze.ProjectAnalysis.Editor.Analysis.Textures
{
    internal interface ITextureAnalyzer
    {
        // Nolasa tekstūru datus norādītajos resursu ceļos.
        TextureCollectionResult Collect(IReadOnlyList<string> assetPaths, TextureAnalysisContext context);
    }
}
