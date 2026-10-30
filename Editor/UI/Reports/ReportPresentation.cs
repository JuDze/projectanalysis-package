using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Reporting;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Atradnes kopsavilkumā norāda noteikumu, risku un saistītos resursus.
    internal sealed class ReportFindingPreview
    {
        public string Category { get; set; }
        public string Risk { get; set; }
        public string ResourceName { get; set; }
        public string ResourcePath { get; set; }
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string Finding { get; set; }
        public string RelatedResources { get; set; }
    }

    // Plānotā un faktiskā vērtība ir nodalīta, lai parādītu arī neveiksmīgas izmaiņas.
    internal sealed class ReportChangePreview
    {
        public string OperationId { get; set; }
        public string Action { get; set; }
        public DateTimeOffset Time { get; set; }
        public string ResourceName { get; set; }
        public string ResourcePath { get; set; }
        public string RuleId { get; set; }
        public string PropertyName { get; set; }
        public string Before { get; set; }
        public string IntendedValue { get; set; }
        public string After { get; set; }
        public string Status { get; set; }
        public bool IsActiveFix { get; set; }
        public string Message { get; set; }
    }

    // Neveiktai pārbaudei laiks un skaitītāji paliek tukši, nevis tiek aizstāti ar nullēm.
    internal sealed class ReportRecheckPreview
    {
        public string OperationId { get; set; }
        public string Action { get; set; }
        public string Status { get; set; }
        public string Scope { get; set; }
        public string Message { get; set; }
        public DateTimeOffset? Time { get; set; }
        public int? BeforeCount { get; set; }
        public int? AfterCount { get; set; }
        public int? Resolved { get; set; }
        public int? Remaining { get; set; }
        public int? New { get; set; }
    }

    // Labojumu kopsavilkums ir pieejams tikai analīzes un labojumu pārskatiem.
    internal sealed class ReportFixesPreview
    {
        public string Status { get; set; }
        public IReadOnlyList<ReportChangePreview> Changes { get; set; } =
            Array.Empty<ReportChangePreview>();
        public IReadOnlyList<ReportRecheckPreview> Rechecks { get; set; } = Array.Empty<ReportRecheckPreview>();
    }

    // Saskarnes dati tiek veidoti no pārskata momentuzņēmuma.
    internal sealed class ReportPreviewRow
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string ProjectName { get; set; }
        public string Type { get; set; }
        public string Platform { get; set; }
        public string QualityLevel { get; set; }
        public string Scope { get; set; }
        public string Json { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset Analyzed { get; set; }
        public string SettingsJson { get; set; }
        public int ResourceCount { get; set; }
        public int EnabledRuleCount { get; set; }
        public IReadOnlyList<ReportFindingPreview> Findings { get; set; } =
            Array.Empty<ReportFindingPreview>();
        public ReportFixesPreview Fixes { get; set; }
    }

    // Kopsavilkums un eksportējamais JSON izmanto vienu sagatavoto pārskatu.
    internal static class ReportRowMapper
    {
        public static ReportPreviewRow Create(string json)
        {
            // Arī agrāk saglabātus pārskatus pārbauda un sagatavo eksportam šeit; datnes rakstītājs JSON vairs nepārveido.
            var report = AnalysisReportJson.Read(json);
            var analysis = report.Analysis;
            return new ReportPreviewRow
            {
                Id = report.Id, Type = report.Type, Title = report.Type + " — " + analysis.ProjectName,
                ProjectName = analysis.ProjectName, Platform = analysis.Platform, QualityLevel = analysis.QualityLevel,
                Scope = analysis.Scope, Created = DateTimeOffset.Parse(report.CreatedUtc), Analyzed = DateTimeOffset.Parse(analysis.AnalyzedUtc),
                ResourceCount = analysis.ResourceCount, EnabledRuleCount = analysis.EnabledRuleCount, Json = AnalysisReportJson.WriteForWeb(report),
                SettingsJson = AnalysisReportJson.WriteSettings(analysis.Settings),
                Findings = analysis.Findings.Select(f => new ReportFindingPreview
                {
                    Category = f.Category, Risk = f.Risk, RuleId = f.RuleId, RuleName = f.Title, Finding = f.Title,
                    ResourceName = f.IsProjectFinding ? "Project settings" : f.Resource.Name,
                    ResourcePath = f.IsProjectFinding ? "Project settings" : f.Resource.Path,
                    RelatedResources = string.Join("\n", f.RelatedResources.Select(r => r.Path + (string.IsNullOrEmpty(r.LocalId) ? "" : " · " + r.LocalId)))
                }).ToArray(),
                Fixes = report.Type == "Analysis and fixes" ? Fixes(report) : null
            };
        }

        private static ReportFixesPreview Fixes(AnalysisReport report) => new ReportFixesPreview
        {
            Status = report.Operations.SelectMany(o => o.Assets).Where(a => a.IsActiveFix).Select(a => a.Guid).Distinct().Count() +
                " asset file(s) with active fixes · " + report.Operations.Count + " operation(s)",
            Changes = report.Operations.SelectMany(o => o.Assets.SelectMany(a => a.Changes.Select(c => new ReportChangePreview
            {
                OperationId = o.Id, Action = o.Action, Time = DateTimeOffset.Parse(o.StartedUtc),
                ResourceName = Path.GetFileName(a.Path), ResourcePath = a.Path, RuleId = string.Join(", ", a.RuleIds),
                PropertyName = c.Property, Before = c.Before, IntendedValue = c.Intended, After = c.HasActual ? c.Actual : null,
                Status = a.Status, IsActiveFix = a.IsActiveFix,
                Message = string.Join("\n", new[] { o.Message, a.Message }.Concat(a.Checks).Where(m => !string.IsNullOrEmpty(m)))
            }))).ToArray(),
            Rechecks = report.Operations.Where(o => o.Recheck != null).Select(o => new ReportRecheckPreview
            {
                OperationId = o.Id, Action = o.Action, Status = o.Recheck.Status,
                Time = string.IsNullOrEmpty(o.Recheck.TimeUtc) ? (DateTimeOffset?)null : DateTimeOffset.Parse(o.Recheck.TimeUtc),
                Scope = "Original resolved scope · Compared categories: " + string.Join(", ", o.Assets.Select(a => a.Category).Distinct()),
                Message = o.Recheck.Message,
                BeforeCount = Count(o.Recheck, "Before"), AfterCount = Count(o.Recheck, "After"), Resolved = Count(o.Recheck, "Resolved"),
                Remaining = Count(o.Recheck, "Remaining"), New = Count(o.Recheck, "New")
            }).ToArray()
        };

        private static int? Count(ReportRecheck recheck, string name) => recheck.Counts.FirstOrDefault(c => c.Name == name)?.Value;
    }
}
