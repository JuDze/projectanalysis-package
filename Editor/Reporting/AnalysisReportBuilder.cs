using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Fixes;

namespace Judze.ProjectAnalysis.Editor.Reporting
{
    // Saņem jau iegūtus faktus un labojumu stāvokli; neveic jaunu analīzi vai resursu izmaiņas.
    internal sealed class AnalysisReportBuilder
    {
        public AnalysisReportSnapshot Capture(AnalysisRunResult run, string sessionId, string projectName,
            string unityVersion, string scope, DateTimeOffset analyzedAt)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (string.IsNullOrEmpty(sessionId)) throw new ArgumentException("An analysis session is required.", nameof(sessionId));
            var textureRun = run.TextureRun;
            var settings = run.Context.CopySettings() ?? throw new InvalidOperationException("The analysis settings snapshot is unavailable.");
            var snapshot = new AnalysisReportSnapshot
            {
                SessionId = sessionId, AnalyzedUtc = analyzedAt.ToUniversalTime().ToString("O"), ProjectName = projectName,
                UnityVersion = unityVersion, BuildTarget = run.Context.TargetPlatform.ToString(),
                Platform = AnalysisBuildTarget.DisplayName(run.Context.TargetPlatform),
                QualityLevel = run.Context.Project.QualityLevelName,
                Scope = scope,
                Settings = settings, TextureCount = textureRun.Textures.Count, ModelCount = run.ModelCollection.Models.Count,
                MeshCount = run.ModelCollection.MeshCount, AudioCount = run.AudioCollection.Clips.Count,
                // Skaits attiecas uz ieslēgtiem noteikumiem, nevis garantēti veiksmīgām pārbaudēm.
                EnabledRuleCount = new[] { settings.Tex001.Enabled, settings.Tex002.Enabled, settings.Tex003.Enabled, settings.Tex004.Enabled,
                    settings.Mod001.Enabled, settings.Mod002.Enabled, settings.Mod003.Enabled, settings.Mod004.Enabled,
                    settings.Aud001.Enabled, settings.Aud002.Enabled, settings.Aud003.Enabled, settings.Aud004.Enabled, settings.Aud005.Enabled }.Count(enabled => enabled)
            };
            // Saglabā kopskaitu, nevis visu pārbaudīto datņu sarakstu; resursu dati paliek atradnēs un labojumos.
            snapshot.ResourceCount = textureRun.Textures.Select(t => string.IsNullOrEmpty(t.Guid) ? t.AssetPath : t.Guid)
                .Concat(run.ModelCollection.Models.Select(m => string.IsNullOrEmpty(m.Guid) ? m.AssetPath : m.Guid))
                .Concat(run.AudioCollection.Clips.Select(c => string.IsNullOrEmpty(c.Guid) ? c.AssetPath : c.Guid))
                .Distinct().Count();
            AddEvaluation(snapshot, "Textures", textureRun.Evaluation);
            AddEvaluation(snapshot, "Models", run.ModelEvaluation);
            AddEvaluation(snapshot, "Audio", run.AudioEvaluation);
            return snapshot;
        }

        public AnalysisReport Create(AnalysisReportSnapshot snapshot, IEnumerable<FixOperationRecord> fixOperations, DateTimeOffset createdAt)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (fixOperations == null) throw new ArgumentNullException(nameof(fixOperations));
            var operations = fixOperations.Where(o => o.SessionId == snapshot.SessionId)
                .OrderBy(o => o.StartedUtc, StringComparer.Ordinal).ThenBy(o => o.Id, StringComparer.Ordinal).ToArray();
            // Izmanto to pašu atcelto resursu uzskaiti kā Analysis cilnes Undo.
            var pending = PendingFixes.Pending(operations, snapshot.SessionId);
            var active = new HashSet<string>(pending.Select(e => e.Operation.Id + ":" + e.Asset.Guid), StringComparer.Ordinal);
            var report = new AnalysisReport
            {
                Id = Guid.NewGuid().ToString("N"), CreatedUtc = createdAt.ToUniversalTime().ToString("O"), Analysis = snapshot,
                Summary = ReportSummaryBuilder.Build(snapshot.Findings),
                Type = pending.Count > 0 ? "Analysis and fixes" : "Analysis"
            };
            // Ja visi labojumi atcelti, pārskatā paliek tikai sākotnējā analīze.
            if (pending.Count > 0)
                report.Operations = operations.Select(o => Operation(o, active)).ToList();
            return report;
        }

        private static void AddEvaluation(AnalysisReportSnapshot snapshot, string category, RuleEvaluation evaluation)
        {
            snapshot.Findings.AddRange(evaluation.Findings.Select(f => new ReportFinding
            {
                Category = category, RuleId = f.RuleId, Risk = f.Priority.ToString(), Title = f.Title,
                IsProjectFinding = f.IsProjectFinding,
                Resource = f.Resource == null ? null : Resource(category, f.Resource.Guid, f.Resource.Path,
                    f.Resource.LocalId?.ToString(CultureInfo.InvariantCulture), f.Resource.DisplayName),
                RelatedResources = f.RelatedResources.Select(r => Resource(category, r.Guid, r.Path,
                    r.LocalId?.ToString(CultureInfo.InvariantCulture), r.DisplayName)).ToList(),
                Measurements = f.Measurements.Select(m => new ReportMeasurement
                {
                    Name = m.Name, Value = m.Value?.ToString("R", CultureInfo.InvariantCulture) ?? "Unknown", Unit = m.Unit
                }).ToList()
            }));
        }

        private static ReportResource Resource(string category, string guid, string path, string localId = null, string name = null) =>
            new ReportResource { Category = category, Guid = guid, Path = path, LocalId = localId, Name = name ?? Path.GetFileName(path) };

        private static ReportOperation Operation(FixOperationRecord source, HashSet<string> active) => new ReportOperation
        {
            Id = source.Id, Action = source.Action, StartedUtc = source.StartedUtc, FinishedUtc = source.FinishedUtc,
            Message = source.Message, Recheck = Recheck(source),
            Assets = source.Assets.Select(a => new ReportAssetChanges
            {
                Guid = a.Guid, Path = a.Path, Category = a.Category, Status = FixStatusText.ToReportText(a.State), Message = AssetMessage(a),
                IsActiveFix = source.Action != "Revert fixes" && active.Contains(source.Id + ":" + a.Guid),
                RuleIds = a.RuleIds.ToList(), RevertsOperationIds = (a.RevertsOperationIds ?? new List<string>()).ToList(),
                Checks = (a.ModelChecks ?? new List<string>()).Concat(a.AudioChecks ?? new List<string>()).ToList(),
                Changes = a.Changes.Select(c => new ReportValueChange
                {
                    Property = c.Property, Before = c.Before, Intended = c.Intended,
                    Actual = c.Actual ?? "Unknown", HasActual = c.Actual != null
                }).ToList()
            }).ToList()
        };

        // Atjaunošanas un pārtraukuma informācija paliek skaidrojumā, nevis rada papildu statusus.
        private static string AssetMessage(FixAssetRecord asset)
        {
            string details;
            switch (asset.State)
            {
                case FixAssetStatus.Completed: case FixAssetStatus.NotApplied: case FixAssetStatus.Failed: return asset.Message;
                case FixAssetStatus.NotStarted: details = "Processing stopped before this resource was changed."; break;
                case FixAssetStatus.RestoredAfterFailure: details = "Original values were restored after the failed attempt."; break;
                case FixAssetStatus.RecoveryRequired: details = "Original values could not be restored. Inspect the resource."; break;
                case FixAssetStatus.Applying: case FixAssetStatus.UnknownAfterInterruption:
                    details = "The operation was interrupted. The final resource state could not be confirmed."; break;
                default: details = "The resource result could not be confirmed."; break;
            }
            return string.IsNullOrWhiteSpace(asset.Message) ? details : asset.Message + "\n" + details;
        }

        private static ReportRecheck Recheck(FixOperationRecord operation)
        {
            var source = operation.Recheck;
            var result = new ReportRecheck
            {
                Status = FixStatusText.ToText(FixRecheckStatus.ForOperation(operation)), TimeUtc = source.TimeUtc, Message = source.Message,
                BeforeKeys = source.BeforeKeys.ToList(), AfterKeys = source.AfterKeys.ToList()
            };
            if (source.HasCounts)
                result.Counts.AddRange(new[]
                {
                    new ReportCount { Name = "Before", Value = source.BeforeCount }, new ReportCount { Name = "After", Value = source.AfterCount },
                    new ReportCount { Name = "Resolved", Value = source.Resolved }, new ReportCount { Name = "Remaining", Value = source.Remaining },
                    new ReportCount { Name = "New", Value = source.New }
                });
            return result;
        }
    }
}
