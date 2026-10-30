using System.Collections.Generic;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Analysis.Models
{
    internal interface IModelAnalyzer
    {
        ModelCollectionResult Collect(IReadOnlyList<string> assetPaths, BuildTarget targetPlatform, bool collectLodContext = true);
    }
}
