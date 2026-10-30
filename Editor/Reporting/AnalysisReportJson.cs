using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;

namespace Judze.ProjectAnalysis.Editor.Reporting
{
    // Serializācija ir nodalīta no datu sagatavošanas un saskarnes.
    internal static class AnalysisReportJson
    {
        // Iekšējais momentuzņēmums saglabā atkārtoto pārbaudi Unity saskarnei.
        public static string Write(AnalysisReport report) => JsonUtility.ToJson(CopyForSerialization(report), true);

        public static string WriteForWeb(AnalysisReport report)
        {
            var copy = CopyForSerialization(report) ?? throw new ArgumentNullException(nameof(report));
            // Atsevišķajā eksporta struktūrā Recheck lauka nav, arī ne ar null vērtību.
            return JsonUtility.ToJson(new WebReport
            {
                SchemaVersion = copy.SchemaVersion, Id = copy.Id, Type = copy.Type, CreatedUtc = copy.CreatedUtc,
                Summary = copy.Summary, Analysis = copy.Analysis,
                Operations = copy.Operations.Select(operation => new WebReportOperation
                {
                    Id = operation.Id, Action = operation.Action, StartedUtc = operation.StartedUtc,
                    FinishedUtc = operation.FinishedUtc, Message = operation.Message, Assets = operation.Assets
                }).ToList()
            }, true);
        }

        private static AnalysisReport CopyForSerialization(AnalysisReport report)
        {
            // Eksportē atsevišķu kopiju; atlasīšana nemaina saglabāto pārskatu vai Undo datus.
            var copy = JsonUtility.FromJson<AnalysisReport>(JsonUtility.ToJson(report));
            RemoveUnsupportedRisks(copy?.Analysis);
            // Kopsavilkumu pārrēķina pēc atlases, lai tas sakristu ar eksportētajām atradnēm.
            if (copy != null) copy.Summary = ReportSummaryBuilder.Build(copy.Analysis?.Findings);
            return copy;
        }

        public static string WriteSnapshot(AnalysisReportSnapshot snapshot)
        {
            var copy = JsonUtility.FromJson<AnalysisReportSnapshot>(JsonUtility.ToJson(snapshot));
            RemoveUnsupportedRisks(copy);
            return JsonUtility.ToJson(copy);
        }
        public static string WriteSettings(Settings.RuleSettings settings) => JsonUtility.ToJson(settings, true);

        public static AnalysisReport Read(string json)
        {
            var report = ReadDocument<AnalysisReport>(json);
            RemoveEmptyRechecks(report);
            RemoveUnsupportedRisks(report?.Analysis);
            if (report == null || report.SchemaVersion != 1 || string.IsNullOrWhiteSpace(report.Id) ||
                (report.Type != "Analysis" && report.Type != "Analysis and fixes") || !IsTime(report.CreatedUtc) ||
                !ValidSnapshot(report.Analysis) || !All(report.Operations, ValidOperation))
                throw new InvalidDataException("The report snapshot is invalid or unsupported.");
            // Iepriekš saglabāts vai trūkstošs kopsavilkums neaizstāj atradņu faktiskos skaitītājus.
            report.Summary = ReportSummaryBuilder.Build(report.Analysis.Findings);
            return report;
        }

        // JsonUtility var izveidot tukšu objektu arī tad, ja eksportā Recheck lauka nav.
        private static void RemoveEmptyRechecks(AnalysisReport report)
        {
            foreach (var operation in report?.Operations ?? Enumerable.Empty<ReportOperation>())
            {
                var recheck = operation?.Recheck;
                if (recheck != null && string.IsNullOrEmpty(recheck.Status) &&
                    string.IsNullOrEmpty(recheck.TimeUtc) && string.IsNullOrEmpty(recheck.Message) &&
                    (recheck.Counts == null || recheck.Counts.Count == 0) &&
                    (recheck.BeforeKeys == null || recheck.BeforeKeys.Count == 0) &&
                    (recheck.AfterKeys == null || recheck.AfterKeys.Count == 0))
                    operation.Recheck = null;
            }
        }

        public static AnalysisReportSnapshot ReadSnapshot(string json)
        {
            var snapshot = ReadDocument<AnalysisReportSnapshot>(json);
            RemoveUnsupportedRisks(snapshot);
            if (!ValidSnapshot(snapshot))
                throw new InvalidDataException("The original analysis snapshot is unavailable. Run analysis again.");
            return snapshot;
        }

        // Summary, JSON un datnes izmanto vienu atradņu kopu ar trim atļautajiem riskiem.
        private static void RemoveUnsupportedRisks(AnalysisReportSnapshot snapshot) =>
            snapshot?.Findings?.RemoveAll(finding => finding != null &&
                finding.Risk != nameof(RiskLevel.Low) && finding.Risk != nameof(RiskLevel.Medium) &&
                finding.Risk != nameof(RiskLevel.High));

        // Deserializācijas veiksme vēl negarantē, ka pārskatu var droši attēlot vai eksportēt.
        private static T ReadDocument<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("The report JSON is empty.");
            try { return JsonUtility.FromJson<T>(json); }
            catch (ArgumentException error) { throw new InvalidDataException("The report JSON is invalid.", error); }
        }

        private static bool ValidSnapshot(AnalysisReportSnapshot snapshot) => snapshot != null &&
            !string.IsNullOrWhiteSpace(snapshot.SessionId) && IsTime(snapshot.AnalyzedUtc) && snapshot.Settings != null &&
            All(snapshot.Findings, finding =>
                finding != null && !string.IsNullOrWhiteSpace(finding.RuleId) &&
                (finding.IsProjectFinding || ValidResource(finding.Resource)) &&
                All(finding.RelatedResources, ValidResource) && All(finding.Measurements, value => value != null));

        private static bool ValidResource(ReportResource resource) => resource != null && !string.IsNullOrWhiteSpace(resource.Path);

        private static bool ValidOperation(ReportOperation operation) => operation != null &&
            !string.IsNullOrWhiteSpace(operation.Id) && IsTime(operation.StartedUtc) &&
            All(operation.Assets, asset => asset != null && !string.IsNullOrWhiteSpace(asset.Guid) &&
                !string.IsNullOrWhiteSpace(asset.Path) && asset.RuleIds != null && asset.Checks != null &&
                (asset.Status == "Completed" || asset.Status == "Not applied" || asset.Status == "Failed") &&
                asset.RevertsOperationIds != null && All(asset.Changes, change => change != null)) &&
            // Eksportētais pārskats ir derīgs arī bez Unity iekšējās atkārtotās pārbaudes.
            (operation.Recheck == null || ValidRecheck(operation.Recheck));

        private static bool ValidRecheck(ReportRecheck recheck) => !string.IsNullOrWhiteSpace(recheck.Status) &&
            (string.IsNullOrEmpty(recheck.TimeUtc) || IsTime(recheck.TimeUtc)) &&
            recheck.BeforeKeys != null && recheck.AfterKeys != null &&
            All(recheck.Counts, count => count != null && !string.IsNullOrWhiteSpace(count.Name) && count.Value >= 0);

        private static bool All<T>(IEnumerable<T> values, Func<T, bool> valid) => values != null && values.All(valid);
        private static bool IsTime(string value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _);

        // Kopīgie pārskata dati nemainās; tikai operācijas eksportē bez Recheck.
        [Serializable]
        private sealed class WebReport
        {
            public int SchemaVersion;
            public string Id, Type, CreatedUtc;
            public ReportSummary Summary;
            public AnalysisReportSnapshot Analysis;
            public List<WebReportOperation> Operations;
        }

        [Serializable]
        private sealed class WebReportOperation
        {
            public string Id, Action, StartedUtc, FinishedUtc, Message;
            public List<ReportAssetChanges> Assets;
        }
    }
}
