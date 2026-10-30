using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal sealed class FixOperationResult
    {
        public FixOperationRecord Operation { get; }
        public AnalysisRunResult RecheckedAnalysis { get; }
        public string StateError { get; }
        public FixOperationResult(FixOperationRecord operation, AnalysisRunResult analysis, string stateError)
        { Operation = operation; RecheckedAnalysis = analysis; StateError = stateError; }
    }

    // Vada secību, izmantojot šaurus līgumus; pats nepiekļūst Unity importētājiem.
    internal sealed class FixManager
    {
        private readonly IFixOperationStore _operations;
        private readonly IFixRechecker _rechecker;
        private readonly HashSet<string> _usedPlans = new HashSet<string>(StringComparer.Ordinal);
        private bool _running;
        public FixManager(IFixOperationStore operations, IFixRechecker rechecker)
        { _operations = operations; _rechecker = rechecker; }

        public FixOperationResult Apply(FixPlan plan, string confirmedPlanId, string currentSettingsJson, string sessionId = null)
        {
            if (plan == null || plan.Assets.Count == 0 || string.IsNullOrEmpty(confirmedPlanId) || confirmedPlanId != plan.Id)
                throw new InvalidOperationException("Explicit confirmation of this fix plan is required.");
            if (_running || _usedPlans.Contains(plan.Id))
                throw new InvalidOperationException("This plan is already running or has been used. Review a new plan.");
            if (plan.Kind == FixOperationKind.Apply && plan.Baseline.Context.SettingsJson != currentSettingsJson)
                throw new InvalidOperationException("Saved rule settings changed. Run analysis again.");
            _running = true;
            _usedPlans.Add(plan.Id);
            try
            {
                // Visu kopu pārbauda pirms pirmās izmaiņas; bojāts labojumu stāvoklis netiek aizstāts ar tukšu.
                plan.ValidateBeforeApply();
                _operations.LoadAll();
                var operation = new FixOperationRecord
                {
                    Version = 2, Id = Guid.NewGuid().ToString("N"), PlanId = plan.Id, SessionId = sessionId,
                    Action = plan.Kind == FixOperationKind.Revert ? "Revert fixes" : "Apply fixes",
                    StartedUtc = DateTimeOffset.UtcNow.ToString("O"), State = FixOperationStatus.InProgress, Message = plan.Notes,
                    Target = plan.Baseline.Context.TargetPlatform.ToString(), SettingsJson = plan.Baseline.Context.SettingsJson,
                    ScopePaths = plan.Baseline.Context.Scope.AssetPaths.ToList(),
                    Assets = plan.Assets.Select(asset => asset.CreateRecord()).ToList()
                };
                // Ja šis ieraksts neizdodas, neviens resurss vēl nav mainīts.
                _operations.Save(operation);
                string stateError = null;
                bool attempted = false;
                for (int i = 0; i < plan.Assets.Count; i++)
                {
                    var asset = plan.Assets[i];
                    var record = operation.Assets[i];
                    try
                    {
                        asset.Validate();
                        record.State = FixAssetStatus.Applying;
                        _operations.Save(operation);
                    }
                    catch (Exception error)
                    {
                        record.State = FixAssetStatus.NotApplied; record.Message = error.Message;
                        operation.Message = "Stopped before changing " + asset.Path + ": " + error.Message;
                        break;
                    }
                    attempted = true;
                    bool failed = false;
                    try
                    {
                        asset.Apply();
                        asset.RecordActual(record);
                        record.State = FixAssetStatus.Completed;
                    }
                    catch (Exception error)
                    {
                        failed = true;
                        record.Message = "Apply failed: " + error.Message;
                        try
                        {
                            asset.Restore();
                            asset.RecordActual(record);
                            record.State = FixAssetStatus.RestoredAfterFailure;
                        }
                        catch (Exception restoreError)
                        {
                            record.State = FixAssetStatus.RecoveryRequired;
                            record.Message += " Restoration failed: " + restoreError.Message;
                            try { asset.RecordActual(record); }
                            catch { record.After = null; record.ModelAfter = null; record.AudioAfter = null; foreach (var change in record.Changes) change.Actual = null; }
                        }
                        operation.Message = record.Message;
                    }
                    try { _operations.Save(operation); }
                    catch (Exception error) { stateError = error.Message; break; }
                    // Iepriekš pabeigtos resursus saglabā; pēc pirmās kļūdas grupu neturpina.
                    if (failed) break;
                }
                int completed = operation.Assets.Count(a => a.State == FixAssetStatus.Completed);
                operation.State = completed == operation.Assets.Count ? FixOperationStatus.Completed : completed > 0 ? FixOperationStatus.PartiallyCompleted : FixOperationStatus.Failed;
                AnalysisRunResult rechecked = null;
                if (attempted)
                {
                    operation.Recheck.TimeUtc = DateTimeOffset.UtcNow.ToString("O");
                    try
                    {
                        rechecked = _rechecker.Run(plan.Baseline);
                        AnalysisFixRecheckComparison.Fill(operation.Recheck, plan, rechecked);
                    }
                    catch (Exception error)
                    {
                        // Nederīgs salīdzināšanas konteksts nedrīkst aizstāt saskarnes pēdējo derīgo analīzi.
                        rechecked = null;
                        operation.Recheck.HasCounts = false;
                        operation.Recheck.State = FixRecheckState.Failed; operation.Recheck.Message = error.Message;
                    }
                }
                operation.Recheck.State = FixRecheckStatus.ForOperation(operation);
                operation.FinishedUtc = DateTimeOffset.UtcNow.ToString("O");
                try { _operations.Save(operation); }
                catch (Exception error) { stateError = error.Message; }
                return new FixOperationResult(operation, rechecked, stateError);
            }
            finally { _running = false; }
        }
    }
}
