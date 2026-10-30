using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Reporting
{
    // Pārskats satur tikai datus, nevis Unity objektus vai izpildāmas labojumu komandas.
    [Serializable]
    internal sealed class AnalysisReport
    {
        public int SchemaVersion = 1;
        public string Id, Type, CreatedUtc;
        public ReportSummary Summary = new ReportSummary();
        public AnalysisReportSnapshot Analysis;
        public List<ReportOperation> Operations = new List<ReportOperation>();
    }

    // Skaitītāji attiecas uz atradnēm, nevis unikāliem resursiem vai labojumiem.
    [Serializable]
    internal sealed class ReportSummary
    {
        public ReportRiskCounts Textures = new ReportRiskCounts();
        public ReportRiskCounts Models = new ReportRiskCounts();
        public ReportRiskCounts Audio = new ReportRiskCounts();
        public ReportTotals Totals = new ReportTotals();
    }

    [Serializable]
    internal sealed class ReportRiskCounts
    {
        public int Low, Medium, High;
    }

    [Serializable]
    internal sealed class ReportTotals
    {
        public int Low, Medium, High, Findings;
    }

    [Serializable]
    internal sealed class AnalysisReportSnapshot
    {
        public string SessionId, AnalyzedUtc, ProjectName, UnityVersion;
        public string BuildTarget, Platform, QualityLevel, Scope;
        public int EnabledRuleCount, ResourceCount, TextureCount, ModelCount, MeshCount, AudioCount;
        public RuleSettings Settings;
        public List<ReportFinding> Findings = new List<ReportFinding>();
    }

    [Serializable]
    internal sealed class ReportResource
    {
        public string Category, Guid, LocalId, Path, Name;
    }

    [Serializable]
    internal sealed class ReportFinding
    {
        public string Category, RuleId, Risk, Title;
        public bool IsProjectFinding;
        public ReportResource Resource;
        public List<ReportResource> RelatedResources = new List<ReportResource>();
        public List<ReportMeasurement> Measurements = new List<ReportMeasurement>();
    }

    [Serializable]
    internal sealed class ReportMeasurement
    {
        public string Name, Value, Unit;
    }

    [Serializable]
    internal sealed class ReportOperation
    {
        public string Id, Action, StartedUtc, FinishedUtc, Message;
        public List<ReportAssetChanges> Assets = new List<ReportAssetChanges>();
        public ReportRecheck Recheck;
    }

    [Serializable]
    internal sealed class ReportAssetChanges
    {
        public string Guid, Path, Category, Status, Message;
        public bool IsActiveFix;
        public List<string> RuleIds = new List<string>();
        public List<string> RevertsOperationIds = new List<string>();
        public List<string> Checks = new List<string>();
        public List<ReportValueChange> Changes = new List<ReportValueChange>();
    }

    [Serializable]
    internal sealed class ReportValueChange
    {
        public string Property, Before, Intended, Actual;
        public bool HasActual;
    }

    [Serializable]
    internal sealed class ReportRecheck
    {
        public string Status, TimeUtc, Message;
        // Ja pilns salīdzinājums nav pieejams, saraksts ir tukšs, nevis aizpildīts ar nullēm.
        public List<ReportCount> Counts = new List<ReportCount>();
        public List<string> BeforeKeys = new List<string>();
        public List<string> AfterKeys = new List<string>();
    }

    [Serializable]
    internal sealed class ReportCount
    {
        public string Name;
        public int Value;
    }
}
