using UnityEditor;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Analysis.Execution
{
    // Nolasa kopīgos Unity iestatījumus, neielādējot projekta resursus.
    internal sealed class UnityAnalysisProjectContextProvider : IAnalysisProjectContextProvider
    {
        public AnalysisProjectContext Capture()
        {
            int index = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            return new AnalysisProjectContext(EditorUserBuildSettings.activeBuildTarget, index,
                index >= 0 && index < names.Length ? names[index] : string.Empty,
                QualitySettings.streamingMipmapsActive);
        }
    }
}
