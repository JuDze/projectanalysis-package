using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Judze.ProjectAnalysis.Editor.Fixes;
using UnityEngine.UIElements;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Scope;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Savieno palaišanas darbības un noteikumu atradnes vienā skatā.
    public sealed class AnalysisTabWindow
    {
        private readonly Label _targetLabel;
        private readonly ResourceScopeView _scopeView;
        private readonly VisualElement _scopeRoot;
        private readonly Button _runButton;
        private readonly Button _createReportButton;
        private bool _reportAvailable;
        private readonly AnalysisResultsView _resultsPanel;
        private readonly FixPreviewView _preview;
        private readonly Label _status;
        private readonly AnalysisResultRowMapper _mapper = new AnalysisResultRowMapper();
        private readonly TextureFixPreviewMapper _fixPreviewMapper = new TextureFixPreviewMapper();
        private readonly ModelFixPreviewMapper _modelPreviewMapper = new ModelFixPreviewMapper();
        private readonly AudioFixPreviewMapper _audioPreviewMapper = new AudioFixPreviewMapper();
        private bool _isRunning;
        internal bool IsRunning => _isRunning;
        private Action _refreshFixActions;
        private readonly HashSet<string> _undoableGuids = new HashSet<string>();
        private readonly HashSet<string> _appliedFixKeys = new HashSet<string>();
        private readonly Dictionary<string, int> _rowPositions = new Dictionary<string, int>();
        private readonly Dictionary<string, AnalysisResultRow> _knownRows = new Dictionary<string, AnalysisResultRow>();
        internal AnalysisRunResult LastRun => _lastRun;
        private AnalysisRunResult _lastRun;
        private FixPlan _preparedPlan;
        private readonly Label _fixStatus;
        private bool _hasOutput;
        private bool _isStale;
        private string _lastRunDescription = string.Empty;
        private string _baseRunDescription = string.Empty;
        private string _completionText = string.Empty;

        internal event Action<AnalysisScopeSelection> ScopeCheckRequested;
        internal event Action<AnalysisScopeSelection> AnalysisRequested;
        internal event Action<AnalysisRunResult, IReadOnlyList<RuleResult>> SingleFixRequested;
        internal event Action<FixPlan, string> FixApplyRequested;
        internal event Action<AnalysisRunResult, IReadOnlyList<RuleResult>> FixSelectionApplyRequested;
        internal event Action<string> RevertRequested;
        internal event Action CreateReportRequested;
        internal event Action OutputCleared;

        internal void ShowScopeSummary(string message) => _scopeView.ShowSummary(message);

        public void SetTargetPlatform(string platformName)
        {
            _targetLabel.text = $"Platform: {platformName}";
        }

        public AnalysisTabWindow(VisualElement root, Action onConfigureRules, VisualTreeAsset fixChangeTemplate,
            VisualTreeAsset propertyChangeTemplate)
        {
            _targetLabel = root.Q<Label>("target-platform-label");
            _runButton = root.Q<Button>("run-analysis-button");
            _createReportButton = root.Q<Button>("create-report-button");
            _createReportButton.clicked += () =>
            {
                if (_reportAvailable && !_isRunning) CreateReportRequested?.Invoke();
            };
            _status = root.Q<Label>("analysis-status");
            _fixStatus = root.Q<Label>("analysis-fix-status");
            root.Q<Button>("configure-rules-button").clicked += onConfigureRules;
            _scopeRoot = root.Q("resource-scope");
            _scopeView = new ResourceScopeView(_scopeRoot);
            _scopeView.CheckRequested += selection => ScopeCheckRequested?.Invoke(selection);
            _scopeView.SelectionChanged += MarkResultsOutdated;
            _runButton.clicked += () =>
            {
                if (!_isRunning) AnalysisRequested?.Invoke(_scopeView.GetSelection());
            };

            _resultsPanel = new AnalysisResultsView(root.Q("results-view"), Array.Empty<AnalysisResultRow>());
            _preview = new FixPreviewView(root, fixChangeTemplate, propertyChangeTemplate);
            _resultsPanel.ResultsCleared += ResetOutput;
            _resultsPanel.FixRequested += row => RequestFix(new[] { row.Source });
            var details = root.Q<Label>("result-details");
            var fixSelected = root.Q<Button>("fix-selected-button");
            var revertAll = root.Q<Button>("revert-all-fixes-button");
            var clearSelection = root.Q<Button>("clear-fix-selection-button");
            var selectAll = root.Q<Button>("select-all-fixes-button");
            var selectionSummary = root.Q<Label>("result-fix-selection-summary");
            string emptyDetailsText = details.text;

            void RefreshFixSelection()
            {
                int count = _resultsPanel.SelectedFixCount;
                selectionSummary.text = $"Selected fixes: {count}" +
                    $" | Hidden by filters: {_resultsPanel.HiddenSelectedFixCount}";
                fixSelected.SetEnabled(count > 0 && !_isStale && !_isRunning);
                revertAll.SetEnabled(_undoableGuids.Count > 0 && !_isRunning);
                clearSelection.SetEnabled(count > 0 && !_isRunning);
                selectAll.SetEnabled(_resultsPanel.CanSelectAllVisibleFixes && !_isRunning);
            }

            _refreshFixActions = RefreshFixSelection;
            _resultsPanel.UndoRequested += guid => { if (!_isRunning) RevertRequested?.Invoke(guid); };
            revertAll.clicked += () => { if (!_isRunning && _undoableGuids.Count > 0) RevertRequested?.Invoke(null); };
            _resultsPanel.ResultSelected += result =>
            {
                // Ja mainās atvērtais rezultāts vai filtri, iepriekšējais apstiprinājums vairs neder.
                _preview.Hide();
                details.text = result == null ? emptyDetailsText :
                    $"{result.RuleId} · {result.Finding}\n\n" +
                    $"Resource: {(string.IsNullOrEmpty(result.Path) ? result.Resource : result.Path)}\n" +
                    $"Risk: {result.Risk}\n\n{result.Details}";
            };
            _resultsPanel.FixSelectionChanged += () =>
            {
                _preview.Hide();
                RefreshFixSelection();
            };
            _resultsPanel.ViewChanged += RefreshFixSelection;
            selectAll.clicked += _resultsPanel.SelectAllVisibleFixes;
            clearSelection.clicked += _resultsPanel.ClearFixSelection;
            fixSelected.clicked += () => RequestFix(_resultsPanel.SelectedFindings, true);
            RefreshFixSelection();
            ResetOutput();
            EndAnalysis();
        }

        internal void BeginAnalysis(AnalysisScopeSelection selection, BuildTarget target)
        {
            SetReportAvailable(false);
            _lastRun = null;
            _knownRows.Clear();
            _rowPositions.Clear();
            _undoableGuids.Clear();
            _appliedFixKeys.Clear();
            _preparedPlan = null;
            _fixStatus.text = string.Empty;
            _isRunning = true;
            _hasOutput = false;
            _isStale = false;
            string scope = selection.Mode == AnalysisScopeMode.AllProjectAssets
                ? "Assets and Packages" : selection.Path;
            _baseRunDescription = $"Last run scope: {scope} · Target: {AnalysisBuildTarget.DisplayName(target)}";
            _lastRunDescription = _baseRunDescription;
            _completionText = string.Empty;
            _resultsPanel.SetResults(Array.Empty<AnalysisResultRow>(), false);
            _preview.Hide();
            _status.text = "Analysing...";
            _runButton.SetEnabled(false);
            _scopeRoot.SetEnabled(false);
        }


        internal void ShowAnalysisResult(AnalysisRunResult run)
        {
            var textures = run.TextureRun;
            _lastRun = run;
            _isStale = false;
            _preparedPlan = null;
            var rows = new List<AnalysisResultRow>();
            foreach (var finding in textures.Evaluation.Findings)
            {
                var row = _mapper.Create(finding, "Textures");
                // Priekšskatījumu sagatavo no tā paša pabeigtās analīzes rezultāta.
                row.Fix = _fixPreviewMapper.Create(finding, textures.Context);
                rows.Add(row);
            }
            foreach (var finding in run.ModelEvaluation.Findings)
            {
                var row = _mapper.Create(finding, "Models");
                row.Fix = _modelPreviewMapper.Create(finding, run.ModelCollection);
                rows.Add(row);
            }
            foreach (var finding in run.AudioEvaluation.Findings)
            {
                var row = _mapper.Create(finding, "Audio");
                row.Fix = _audioPreviewMapper.Create(finding, run.AudioCollection);
                rows.Add(row);
            }
            // Saglabā atrisinātās rindas, lai pēc atkārtotās pārbaudes būtu pieejama resursa atcelšana.
            foreach (var row in rows)
            {
                string key = RowKey(row);
                if (!_rowPositions.ContainsKey(key)) _rowPositions.Add(key, _rowPositions.Count);
                _knownRows[key] = row;
            }
            var currentKeys = new HashSet<string>(rows.Select(RowKey));
            var diagnostics = run.Evaluation.Diagnostics;
            foreach (var previous in _knownRows.Values)
            {
                if (currentKeys.Contains(RowKey(previous)) || previous.Source?.Resource == null || !_undoableGuids.Contains(previous.Source.Resource.Guid)) continue;
                bool verified = !diagnostics.Any(d => string.IsNullOrEmpty(d.AssetPath) || d.AssetPath == previous.Path);
                rows.Add(new AnalysisResultRow
                {
                    Category = previous.Category, RuleId = previous.RuleId, Resource = previous.Resource, Path = previous.Path,
                    Source = previous.Source, Risk = previous.Risk, IsResolved = verified,
                    Finding = previous.Source.Title,
                    Details = (verified ? "The finding is absent from the repeated analysis." : "The repeated analysis is incomplete; resolution is not confirmed.") +
                        "\nUndo restores all fixes for this asset file since the last Run Analysis.\n\nPrevious finding:\n" + previous.Details
                });
            }
            foreach (var row in rows)
            {
                row.CanUndo = row.Source?.Resource != null && _undoableGuids.Contains(row.Source.Resource.Guid);
                row.HasAppliedFix = row.CanUndo && (row.IsResolved || _appliedFixKeys.Contains(row.RuleId + ":" + row.Source.Resource.Guid));
            }
            // Pēc labojuma saglabā rindas vietu, atlasi un riska filtra atbilstību.
            _resultsPanel.SetResults(rows.OrderBy(row => _rowPositions[RowKey(row)]), preserveSelection: true);
            ShowScopeSummary($"Asset files: {run.Context.Scope.AssetPaths.Count}\n");
            // Atkārtota pārbaude aizstāj kopsavilkumu, nevis pievieno vēl vienu tā kopiju.
            _completionText = $"Textures: {textures.CollectedTextureCount} | Models: {run.ModelCollection.Models.Count} | Meshes: {run.ModelCollection.MeshCount} | Audio clips: {run.AudioCollection.Clips.Count} | Findings: {rows.Count}";
            _lastRunDescription = _baseRunDescription + $" · Quality Level: {run.Context.Project.QualityLevelName}";
            _hasOutput = true;
            RefreshStatus();
        }

        internal void ShowAnalysisError(string message)
        {
            // Kļūda neaizstāj nezināmus datus ar veiksmīgu tukšu analīzi.
            _resultsPanel.SetResults(Array.Empty<AnalysisResultRow>());
            _completionText = "Analysis failed: " + message;
            _hasOutput = true;
            RefreshStatus();
        }

        internal void ShowNoEnabledRules()
        {
            _resultsPanel.SetResults(Array.Empty<AnalysisResultRow>());
            _completionText = "No texture, model or audio rules are enabled.";
            _hasOutput = true;
            RefreshStatus();
        }

        internal void EndAnalysis()
        {
            _isRunning = false;
            _scopeRoot.SetEnabled(true);
            _runButton.SetEnabled(true);
            _refreshFixActions?.Invoke();
            RefreshReportAvailability();
        }

        internal void SetReportAvailable(bool available)
        {
            _reportAvailable = available;
            RefreshReportAvailability();
        }

        private void RefreshReportAvailability() => _createReportButton.SetEnabled(_reportAvailable && !_isRunning);

        internal void MarkResultsOutdated()
        {
            if (!_hasOutput || _isRunning) return;
            _isStale = true;
            _preparedPlan = null;
            _preview.Hide();
            _resultsPanel.ClearFixSelection();
            RefreshStatus();
        }

        private void RequestFix(IReadOnlyList<RuleResult> findings, bool applySelected = false)
        {
            if (_isRunning || _isStale || _lastRun == null)
            {
                ShowFixMessage("Run analysis again before preparing fixes.");
                return;
            }
            _preparedPlan = null;
            _preview.Hide();
            if (applySelected) FixSelectionApplyRequested?.Invoke(_lastRun, findings);
            else SingleFixRequested?.Invoke(_lastRun, findings);
        }

        internal void ShowFixPreview(AnalysisFixPreview preview)
        {
            _preparedPlan = preview.Plan;
            _fixStatus.text = preview.Plan?.Notes ?? string.Empty;
            _preview.Show(preview.Rows, preview.ApplyUnavailableReason);
        }

        internal void ConfirmPreparedFixes(string id)
        {
            if (_preparedPlan == null || _isStale || _isRunning || _preparedPlan.Id != id) return;
            var plan = _preparedPlan; _preparedPlan = null;
            FixApplyRequested?.Invoke(plan, id);
        }

        internal void SetPendingFixes(IEnumerable<FixRevertEntry> pending)
        {
            _undoableGuids.Clear();
            _appliedFixKeys.Clear();
            foreach (var entry in pending)
            {
                _undoableGuids.Add(entry.Asset.Guid);
                foreach (string ruleId in entry.Asset.RuleIds)
                    _appliedFixKeys.Add(ruleId + ":" + entry.Asset.Guid);
            }
            _resultsPanel.SetFixState(_undoableGuids, _appliedFixKeys);
            _refreshFixActions?.Invoke();
        }

        private static string RowKey(AnalysisResultRow row) => row.RuleId + ":" + (row.Source?.Resource?.Identity ?? "project");

        internal void ShowFixMessage(string message) { _fixStatus.text = message; }

        internal void BeginFixes()
        {
            _isRunning = true;
            RefreshReportAvailability();
            _preview.Hide();
            _fixStatus.text = "Applying confirmed asset changes...";
            _refreshFixActions?.Invoke();
        }

        internal void ShowFixResult(FixOperationResult result)
        {
            if (result.RecheckedAnalysis != null) ShowAnalysisResult(result.RecheckedAnalysis);
            else { _isStale = true; _resultsPanel.ClearFixSelection(); RefreshStatus(); }
            var operation = result.Operation;
            int completed = operation.Assets.Count(a => a.State == FixAssetStatus.Completed);
            string action = operation.Action == "Revert fixes" ? "reverted" : "applied";
            ShowFixMessage($"{FixStatusText.ToText(operation.State)}: {completed} of {operation.Assets.Count} asset(s) {action}. " +
                $"Recheck: {FixStatusText.ToText(operation.Recheck.State)}.\n" + operation.Message +
                (result.StateError == null ? "\nFix data is available for Undo and reports." : "\nFix state could not be saved: " + result.StateError));
        }

        private void RefreshStatus()
        {
            _status.text = _completionText + "\n" + _lastRunDescription +
                (_isStale ? "\nSelection or saved settings changed. Run analysis again to update results." : "");
        }

        private void ResetOutput()
        {
            SetReportAvailable(false);
            OutputCleared?.Invoke();
            _knownRows.Clear();
            _rowPositions.Clear();
            _lastRun = null;
            _preparedPlan = null;
            _fixStatus.text = string.Empty;
            _hasOutput = false;
            _isStale = false;
            _lastRunDescription = string.Empty;
            _completionText = string.Empty;
            _preview.Hide();
            _status.text = "Run analysis to see results overview.";
        }
    }
}
