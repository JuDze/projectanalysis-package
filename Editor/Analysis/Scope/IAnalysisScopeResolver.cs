namespace Judze.ProjectAnalysis.Editor.Analysis.Scope
{
    // Sagatavo analīzes resursu kopu neatkarīgi no saskarnes.
    internal interface IAnalysisScopeResolver
    {
        ResolvedAnalysisScope Resolve(AnalysisScopeSelection selection);
    }
}
