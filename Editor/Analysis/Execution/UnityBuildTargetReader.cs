using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Analysis.Execution
{
    // Nolasa projekta būves mērķi; kļūdas gadījumā neizvēlas noklusējuma platformu.
    internal static class UnityBuildTargetReader
    {
        public static BuildTarget Read()
        {
            try { return AnalysisBuildTarget.Normalize(EditorUserBuildSettings.activeBuildTarget); }
            catch (System.Exception) { return BuildTarget.NoTarget; }
        }
    }
}
