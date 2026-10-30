using UnityEditor;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;

namespace Judze.ProjectAnalysis.Editor.Analysis.Textures
{
    // Kopīgais projekta stāvoklis vienam datu iegūšanas pieprasījumam.
    internal sealed class TextureAnalysisContext
    {
        public BuildTarget TargetPlatform { get; }
        public BuildTarget ActiveBuildTarget { get; }
        public string ImportSettingsPlatform { get; }
        public int QualityLevelIndex { get; }
        public string QualityLevelName { get; }
        public bool GlobalStreamingMipmapsActive { get; }

        // Vienāda platformu grupa vēl nenozīmē vienādu faktisko importa rezultātu.
        public bool ImportedDataMatchesTarget => AnalysisBuildTarget.IsKnown(TargetPlatform) && TargetPlatform == ActiveBuildTarget;

        public TextureAnalysisContext(BuildTarget targetPlatform, AnalysisProjectContext project)
            : this(targetPlatform, project.ActiveBuildTarget,
                AnalysisBuildTarget.IsStandalone(targetPlatform) ? AnalysisBuildTarget.StandaloneImporterName : null,
                project.QualityLevelIndex, project.QualityLevelName, project.GlobalStreamingMipmapsActive) { }

        public TextureAnalysisContext(
            BuildTarget targetPlatform, BuildTarget activeBuildTarget, string importSettingsPlatform,
            int qualityLevelIndex, string qualityLevelName, bool globalStreamingMipmapsActive)
        {
            TargetPlatform = targetPlatform;
            ActiveBuildTarget = activeBuildTarget;
            ImportSettingsPlatform = importSettingsPlatform;
            QualityLevelIndex = qualityLevelIndex;
            QualityLevelName = qualityLevelName;
            GlobalStreamingMipmapsActive = globalStreamingMipmapsActive;
        }
    }
}
