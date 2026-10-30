namespace Judze.ProjectAnalysis.Editor.Analysis.Scope
{
    // Analīzei izvēlas visus projekta resursus vai vienu mapi.
    internal enum AnalysisScopeMode
    {
        AllProjectAssets,
        SelectedFolder
    }

    // Glabā izvēlēto režīmu un mapes ceļu resursu datubāzē.
    internal sealed class AnalysisScopeSelection
    {
        public AnalysisScopeMode Mode { get; }
        public string Path { get; }

        public AnalysisScopeSelection(AnalysisScopeMode mode, string path = "")
        {
            Mode = mode;
            Path = path ?? string.Empty;
        }
    }
}
