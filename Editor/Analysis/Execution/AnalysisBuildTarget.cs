using System;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Analysis.Execution
{
    // Vienoti atšķir zināmu mērķi no trūkstoša konteksta un sagatavo tā nosaukumu.
    internal static class AnalysisBuildTarget
    {
        // Tekstūras izmanto platformas nosaukumu, audio — BuildTargetGroup vērtību.
        public const string StandaloneImporterName = "Standalone";
        public const BuildTargetGroup StandaloneGroup = BuildTargetGroup.Standalone;

        public static bool IsKnown(BuildTarget target) =>
            target != BuildTarget.NoTarget && Enum.IsDefined(typeof(BuildTarget), target);

        public static BuildTarget Normalize(BuildTarget target) =>
            IsKnown(target) ? target : BuildTarget.NoTarget;

        public static bool IsStandalone(BuildTarget target) => target == BuildTarget.StandaloneWindows64 ||
            target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneOSX ||
            target == BuildTarget.StandaloneLinux64;

        public static string DisplayName(BuildTarget target)
        {
            if (!IsKnown(target)) return "Unknown";
            switch (target)
            {
                case BuildTarget.StandaloneWindows64: return "Windows (64-bit)";
                case BuildTarget.StandaloneWindows: return "Windows (32-bit)";
                case BuildTarget.StandaloneOSX: return "macOS";
                case BuildTarget.StandaloneLinux64: return "Linux (64-bit)";
                default: return target.ToString();
            }
        }
    }
}
