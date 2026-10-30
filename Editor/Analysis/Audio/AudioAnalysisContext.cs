using UnityEditor;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio
{
    internal sealed class AudioAnalysisContext
    {
        public BuildTarget TargetPlatform { get; }
        public BuildTarget ActivePlatform { get; }
        // Vienāda platformu grupa pati par sevi neapstiprina izvēlētās būves importu.
        public bool HasMatchingImport => AnalysisBuildTarget.IsKnown(TargetPlatform) && TargetPlatform == ActivePlatform;
        public bool SupportsStandalone => AnalysisBuildTarget.IsStandalone(TargetPlatform);

        public AudioAnalysisContext(BuildTarget targetPlatform, BuildTarget activePlatform)
        { TargetPlatform = targetPlatform; ActivePlatform = activePlatform; }
    }
}
