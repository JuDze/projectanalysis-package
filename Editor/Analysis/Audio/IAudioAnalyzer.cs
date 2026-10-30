using System.Collections.Generic;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio
{
    internal interface IAudioAnalyzer
    {
        AudioCollectionResult Collect(IReadOnlyList<string> paths, BuildTarget targetPlatform, bool collectRuntimeSize);
    }
}
