using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Analysis.Execution
{
    // Projekta stāvoklis nepieder nevienam atsevišķam resursu analizatoram.
    internal sealed class AnalysisProjectContext
    {
        public BuildTarget ActiveBuildTarget { get; }
        public int QualityLevelIndex { get; }
        public string QualityLevelName { get; }
        public bool GlobalStreamingMipmapsActive { get; }

        public AnalysisProjectContext(BuildTarget activeBuildTarget, int qualityLevelIndex,
            string qualityLevelName, bool globalStreamingMipmapsActive)
        {
            ActiveBuildTarget = activeBuildTarget;
            QualityLevelIndex = qualityLevelIndex;
            QualityLevelName = qualityLevelName;
            GlobalStreamingMipmapsActive = globalStreamingMipmapsActive;
        }
    }
}
