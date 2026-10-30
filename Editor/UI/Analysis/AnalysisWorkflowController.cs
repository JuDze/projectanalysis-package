using System;
using System.Linq;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Scope;
using Judze.ProjectAnalysis.Editor.Fixes;
using Judze.ProjectAnalysis.Editor.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Piesaista analīzes un labojumu darbības skatam; noteikumu loģika paliek analīzes modulī.
    internal sealed class AnalysisWorkflowController : IDisposable
    {
        private readonly VisualElement _root;
        private readonly AnalysisTabWindow _view;
        private readonly ProjectAnalysisServices _services;
        private readonly IFixSessionStore _fixState;
        private readonly Func<RuleSettings> _savedSettings;
        private readonly Func<BuildTarget> _readTarget;
        private readonly Func<string, string, string, bool> _confirm;
        private readonly ReportWorkflowController _reports;

        public AnalysisWorkflowController(VisualElement root, AnalysisTabWindow view,
            ProjectAnalysisServices services, IFixSessionStore fixState, Func<RuleSettings> savedSettings,
            Func<BuildTarget> readTarget, Func<string, string, string, bool> confirm, ReportWorkflowController reports)
        {
            _root = root; _view = view; _services = services; _fixState = fixState;
            _savedSettings = savedSettings; _readTarget = readTarget; _confirm = confirm; _reports = reports;
            _view.SingleFixRequested += PreviewSingle;
            _view.FixApplyRequested += ExecuteFixPlan;
            _view.FixSelectionApplyRequested += FixSelected;
            _view.RevertRequested += Revert;
            _view.AnalysisRequested += Run;
            _view.ScopeCheckRequested += CheckScope;
            _view.OutputCleared += _reports.ClearBaseline;
            _view.CreateReportRequested += _reports.CreateReport;
            _reports.AttachAnalysis(view);
            RefreshUndoAvailability();
        }

        public void Dispose()
        {
            // Pārbūvējot logu, iepriekšējie apstrādātāji vairs nedrīkst reaģēt uz notikumiem.
            _view.SingleFixRequested -= PreviewSingle;
            _view.FixApplyRequested -= ExecuteFixPlan;
            _view.FixSelectionApplyRequested -= FixSelected;
            _view.RevertRequested -= Revert;
            _view.AnalysisRequested -= Run;
            _view.ScopeCheckRequested -= CheckScope;
            _view.OutputCleared -= _reports.ClearBaseline;
            _view.CreateReportRequested -= _reports.CreateReport;
        }

        private void PreviewSingle(AnalysisRunResult run, IReadOnlyList<RuleResult> selected)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Project Analysis", "Preparing fix preview...", 0f);
                var preview = _services.Previews.Prepare(run, selected,
                    JsonUtility.ToJson(_savedSettings()));
                _view.ShowFixPreview(preview);
                EditorUtility.ClearProgressBar();
                if (preview.Plan == null) { _view.ShowFixMessage(preview.ApplyUnavailableReason); return; }
                // Apstiprinājums attiecas tikai uz nospiestās rindas objektu
                string changes = DescribeFixChanges(preview);
                if (_confirm("Apply fix", "Are you sure you want to apply this fix?\n\n" + changes, "Apply fix"))
                    _view.ConfirmPreparedFixes(preview.Plan.Id);
            }
            catch (Exception error) { _view.ShowFixMessage(error.Message); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private void ExecuteFixPlan(FixPlan plan, string confirmedId)
        {
            try
            {
                _view.BeginFixes();
                _root.SetEnabled(false);
                EditorUtility.DisplayProgressBar("Project Analysis", plan.Kind == FixOperationKind.Revert ? "Reverting asset fixes..." : "Applying confirmed asset changes...", 0f);
                var result = _services.Fixes.Apply(plan, confirmedId, JsonUtility.ToJson(_savedSettings()), _fixState.Id);
                RefreshUndoAvailability();
                _view.ShowFixResult(result);
                // Nepilnīgi saglabāts labojumu stāvoklis nedrīkst radīt kļūdainu pārskata tipu.
                if (result.StateError != null) _view.SetReportAvailable(false);
            }
            catch (Exception error)
            {
                _view.ShowFixMessage("Changes could not be completed: " + error.Message);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _root.SetEnabled(true);
                _view.EndAnalysis();
                RefreshUndoAvailability();
            }
        }

        private void FixSelected(AnalysisRunResult run, IReadOnlyList<RuleResult> selected)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Project Analysis", "Checking selected fixes...", 0f);
                var preview = _services.Previews.PrepareAvailable(run, selected, JsonUtility.ToJson(_savedSettings()));
                _view.ShowFixPreview(preview);
                EditorUtility.ClearProgressBar();
                if (preview.Plan == null) { _view.ShowFixMessage(preview.ApplyUnavailableReason); return; }
                // Dialoga apstiprinājums attiecas tikai uz tikko pārbaudīto plānu.
                string changes = DescribeFixChanges(preview);
                if (_confirm("Fix selected",
                    $"Apply {preview.Rows.Count} selected proposal(s) to {preview.Plan.Assets.Count} asset file(s)?\n\n" + changes, "Apply fixes"))
                    _view.ConfirmPreparedFixes(preview.Plan.Id);
            }
            catch (Exception error) { _view.ShowFixMessage(error.Message); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private void Revert(string guid)
        {
            try
            {
                var pending = PendingFixes.Pending(_fixState.LoadAll(), _fixState.Id);
                var selected = pending.Where(e => guid == null || e.Asset.Guid == guid).ToArray();
                if (selected.Length == 0) { RefreshUndoAvailability(); return; }
                var baseline = _view.LastRun;
                if (baseline == null)
                {
                    // Pēc loga aizvēršanas atcelšanai izmanto saglabātās operācijas analīzes kontekstu.
                    var source = selected.Last().Operation;
                    var settings = JsonUtility.FromJson<RuleSettings>(source.SettingsJson);
                    var scope = new ResolvedAnalysisScope(source.ScopePaths, Array.Empty<string>());
                    baseline = _services.Analysis.RunResolved(scope, settings, (BuildTarget)Enum.Parse(typeof(BuildTarget), source.Target));
                }
                var plan = _services.Reverts.Prepare(baseline, pending, guid);
                string changes = string.Join("\n\n", plan.Assets.Select(a =>
                {
                    var record = a.CreateRecord();
                    return record.Path + "\n" + string.Join("\n", record.Changes.Select(c => c.Property + ": " + c.Before + " → " + c.Intended));
                }));
                if (_confirm(guid == null ? "Revert all fixes" : "Undo resource fixes",
                    $"Revert fixes for {plan.Assets.Count} asset file(s) made since the last Run Analysis?\nAll rules affecting each selected file are included.\n\n" + changes,
                    "Revert fixes")) ExecuteFixPlan(plan, plan.Id);
            }
            catch (Exception error) { _view.ShowFixMessage("Fixes could not be reverted: " + error.Message); }
        }

        private void Run(AnalysisScopeSelection selection)
        {
            _reports.ClearBaseline();
            _view.SetReportAvailable(false);
            try
            {
                // Palaišana izmanto tikai apstiprināto iestatījumu kopiju.
                var settings = _savedSettings();
                BuildTarget target = _readTarget();
                _fixState.Start();
                _view.BeginAnalysis(selection, target);
                if (!settings.HasEnabledRules)
                {
                    _view.ShowNoEnabledRules();
                    return;
                }
                EditorUtility.DisplayProgressBar("Project Analysis",
                    "Collecting texture, model and audio data and evaluating rules...", 0f);
                var run = _services.Analysis.Run(selection, settings, target);
                _view.ShowAnalysisResult(run);
                _reports.Capture(run, selection);
                _view.SetReportAvailable(true);
            }
            catch (Exception exception)
            {
                _view.ShowAnalysisError(exception.Message);
                Debug.LogWarning($"Project Analysis: {exception.Message}");
            }
            finally
            {
                // Atjauno saskarni arī tad, ja datu iegūšana neizdevās.
                EditorUtility.ClearProgressBar();
                _view.EndAnalysis();
            }
        }

        private void CheckScope(AnalysisScopeSelection selection)
        {
            try
            {
                var scope = _services.Scope.Resolve(selection);

                _view.ShowScopeSummary($"Asset files: {scope.AssetPaths.Count}");
            }
            catch (Exception exception)
            {
                _view.ShowScopeSummary(exception.Message);

                Debug.LogWarning(
                    $"Project Analysis: {exception.Message}");
            }
        }

        private void RefreshUndoAvailability()
        {
            if (_view == null || _fixState == null) return;
            try
            {
                var pending = PendingFixes.Pending(_fixState.LoadAll(), _fixState.Id);
                _view.SetPendingFixes(pending);
            }
            catch (Exception error)
            {
                _view.SetPendingFixes(Array.Empty<FixRevertEntry>());
                _view.ShowFixMessage("Undo availability could not be loaded: " + error.Message);
            }
        }

        // Dialogā rāda tikai datnes ceļu un parametru maiņu; brīdinājumi paliek priekšskatījumā.
        private static string DescribeFixChanges(AnalysisFixPreview preview) =>
            string.Join("\n\n", preview.Rows.GroupBy(row => row.Path).Select(asset =>
                asset.Key + "\n" + string.Join("\n", asset.SelectMany(row => row.PropertyChanges)
                    .Select(change => change.PropertyName + ": " + change.CurrentValue + " → " + change.ProposedValue)
                    .Distinct())));

    }
}
