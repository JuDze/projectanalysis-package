using System.Collections.Generic;

namespace Judze.ProjectAnalysis.Editor.Analysis.Scope
{
    // Sagatavotie resursu datņu ceļi un atlases paziņojumi.
    internal sealed class ResolvedAnalysisScope
    {
        public IReadOnlyList<string> AssetPaths { get; }
        public IReadOnlyList<string> Messages { get; }

        public ResolvedAnalysisScope(IEnumerable<string> assetPaths, IEnumerable<string> messages)
        {
            AssetPaths = new List<string>(assetPaths).AsReadOnly();
            Messages = new List<string>(messages).AsReadOnly();
        }
    }
}
