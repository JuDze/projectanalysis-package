using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Attēlo sagatavoto pārskatu saturu; datu sagatavošana un serializācija notiek atsevišķi.
    internal sealed class ReportsTabView
    {
        private readonly VisualElement _root;
        private readonly ReportsListView _list;
        private readonly Label _placeholder;
        private readonly TabView _detailsTabs;
        private readonly ScrollView _summaryScroll;
        private readonly VisualElement _findings;
        private readonly VisualElement _fixes;
        private readonly VisualElement _changes;
        private readonly VisualElement _rechecks;
        private readonly Button _saveReport;
        private readonly Button _deleteReport;
        private readonly Button _sendReport;
        private bool _uploading;
        private readonly Label _footerStatus;
        public event Action<ReportPreviewRow> SaveRequested;
        public event Action<ReportPreviewRow> DeleteRequested;
        public event Action<ReportPreviewRow> SendRequested;
        public event Action CancelUploadRequested;

        public ReportsTabView(
            VisualElement root,
            IEnumerable<ReportPreviewRow> rows)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _placeholder = Require<Label>("reports-details-placeholder");
            _detailsTabs = Require<TabView>("reports-details-tabs");
            _summaryScroll = Require<ScrollView>("reports-summary-scroll");
            _findings = Require<VisualElement>("reports-finding-items");
            _fixes = Require<VisualElement>("reports-fixes-content");
            _changes = Require<VisualElement>("reports-change-items");
            _rechecks = Require<VisualElement>("reports-recheck-items");

            // Pogas nodod atlasīto momentuzņēmumu; datņu darbības veic atsevišķas komponentes.
            _saveReport = Require<Button>("save-report-button");
            _deleteReport = Require<Button>("delete-report-button");
            _footerStatus = Require<Label>("reports-footer-status");
            _sendReport = Require<Button>("send-report-to-server-button");
            _list = new ReportsListView(root, rows);
            _saveReport.clicked += () => { if (!_uploading && _list.SelectedReport != null) SaveRequested?.Invoke(_list.SelectedReport); };
            _deleteReport.clicked += () => { if (!_uploading && _list.SelectedReport != null) DeleteRequested?.Invoke(_list.SelectedReport); };
            _sendReport.clicked += () =>
            {
                if (_uploading) CancelUploadRequested?.Invoke();
                else if (_list.SelectedReport != null) SendRequested?.Invoke(_list.SelectedReport);
            };
            _list.SelectionChanged += ShowReport;
            ShowReport(null);
        }

        public void SetRows(IEnumerable<ReportPreviewRow> rows) { _list.SetRows(rows); RefreshActions(); }
        public void SetUploading(bool uploading) { _uploading = uploading; RefreshActions(); }

        private void RefreshActions()
        {
            bool selected = _list.SelectedReport != null;
            _saveReport.SetEnabled(selected && !_uploading);
            _deleteReport.SetEnabled(selected && !_uploading);
            // Atcelšana paliek pieejama arī tad, ja saraksta filtrs paslēpj atlasīto pārskatu.
            _sendReport.SetEnabled(selected || _uploading);
            _sendReport.text = _uploading ? "Cancel upload" : "Send to Web";
            _sendReport.tooltip = _uploading ? "Cancel the current report upload"
                : "Send the selected report using the current Web Connection values";
        }
        public void ShowMessage(string message) => _footerStatus.text = message;
        public bool SelectReport(string id)
        {
            bool selected = _list.SelectReport(id);
            if (selected)
            {
                _detailsTabs.activeTab = Require<Tab>("reports-summary-tab");
                _summaryScroll.scrollOffset = Vector2.zero;
            }
            return selected;
        }

        private void ShowReport(ReportPreviewRow report)
        {
            bool hasReport = report != null;
            RefreshActions();
            _placeholder.EnableInClassList("pa-hidden", hasReport);
            _detailsTabs.EnableInClassList("pa-hidden", !hasReport);
            _summaryScroll.scrollOffset = Vector2.zero;
            _findings.Clear();
            _changes.Clear();
            _rechecks.Clear();

            if (!hasReport)
                return;

            SetLabel("reports-detail-id", report.Id);
            SetLabel("reports-detail-title", report.Title);
            SetLabel("reports-detail-created", FormatTime(report.Created));
            SetLabel("reports-detail-analyzed", FormatTime(report.Analyzed));
            SetLabel("reports-detail-project", report.ProjectName);
            SetLabel("reports-detail-type", report.Type);
            SetLabel("reports-detail-platform", report.Platform);
            SetLabel("reports-detail-quality", report.QualityLevel);
            SetLabel("reports-detail-scope", report.Scope);
            SetLabel("reports-detail-resources", report.ResourceCount.ToString());
            SetLabel("reports-detail-rules", report.EnabledRuleCount.ToString());
            SetLabel("reports-settings-snapshot", report.SettingsJson);
            SetLabel("reports-detail-findings", report.Findings.Count.ToString());
            SetLabel("reports-risk-summary", FormatRiskSummary(report.Findings));
            SetLabel("reports-category-summary", FormatCategorySummary(report.Findings));

            Require<Label>("reports-no-findings").EnableInClassList(
                "pa-hidden", report.Findings.Count != 0);
            foreach (var finding in report.Findings)
                _findings.Add(CreateFinding(finding));

            // Platforma un kvalitātes līmenis pieder pārskata saglabātajam kontekstam.
            // To attēlošanai netiek nolasīti vai mainīti pašreizējie projekta iestatījumi.
            bool hasFixes = report.Fixes != null;
            _fixes.EnableInClassList("pa-hidden", !hasFixes);
            if (!hasFixes)
                return;

            SetLabel("reports-fixes-status", report.Fixes.Status);
            int completed = report.Fixes.Changes.Count(change => change.Status == "Completed");
            SetLabel("reports-fixes-completed", $"{completed} of {report.Fixes.Changes.Count}");
            foreach (var change in report.Fixes.Changes)
                _changes.Add(CreateChange(change));
            foreach (var recheck in report.Fixes.Rechecks)
                _rechecks.Add(CreateRecheck(recheck));
        }

        private static VisualElement CreateFinding(ReportFindingPreview finding)
        {
            var card = new Foldout
            {
                text = $"{finding.Risk} — {finding.ResourceName} — {finding.RuleId}",
                value = true
            };
            card.AddToClassList("reports-card");
            card.Add(CreateMetadataRow("Category", finding.Category));
            card.Add(CreateMetadataRow("Resource", finding.ResourcePath));
            card.Add(CreateMetadataRow("Rule", $"{finding.RuleId} — {finding.RuleName}"));
            card.Add(CreateMetadataRow("Finding", finding.Finding));
            if (!string.IsNullOrEmpty(finding.RelatedResources)) card.Add(CreateMetadataRow("Related resources", finding.RelatedResources));
            return card;
        }

        private static VisualElement CreateChange(ReportChangePreview change)
        {
            var card = new Foldout
            {
                text = $"{change.ResourceName} — {change.PropertyName}",
                value = true
            };
            card.AddToClassList("reports-card");
            card.Add(CreateMetadataRow("Operation", change.OperationId));
            card.Add(CreateMetadataRow("Action", change.Action));
            card.Add(CreateMetadataRow("Time", FormatTime(change.Time)));
            card.Add(CreateMetadataRow("Resource", change.ResourcePath));
            card.Add(CreateMetadataRow("Rule", change.RuleId));
            card.Add(CreateMetadataRow("Property", change.PropertyName));
            card.Add(CreateValueRow(true, "Before", "Intended value", "After"));
            card.Add(CreateValueRow(false, change.Before, change.IntendedValue, change.After));
            card.Add(CreateMetadataRow("Status", change.Status));
            if (change.Action == "Apply fixes" && change.Status == "Completed")
                card.Add(CreateMetadataRow("Active fix", change.IsActiveFix ? "Yes" : "No"));
            if (!string.IsNullOrEmpty(change.Message))
                card.Add(CreateMetadataRow("Message", change.Message));
            return card;
        }

        private static VisualElement CreateRecheck(ReportRecheckPreview recheck)
        {
            var card = new Foldout { text = recheck.Action + " — " + recheck.Status, value = false };
            card.AddToClassList("reports-card");
            card.Add(CreateMetadataRow("Operation", recheck.OperationId));
            card.Add(CreateMetadataRow("Recheck", recheck.Status));
            card.Add(CreateMetadataRow("Time", recheck.Time.HasValue ? FormatTime(recheck.Time.Value) : "Not recorded"));
            card.Add(CreateMetadataRow("Scope", recheck.Scope));
            card.Add(CreateMetadataRow("Details", recheck.Message));
            // Katras operācijas salīdzinājums ir atsevišķs; skaitītājus starp operācijām nesummē.
            if (recheck.BeforeCount.HasValue && recheck.AfterCount.HasValue)
            {
                card.Add(CreateMetadataRow("Findings before", FormatCount(recheck.BeforeCount)));
                card.Add(CreateMetadataRow("Findings after", FormatCount(recheck.AfterCount)));
                card.Add(CreateMetadataRow("Resolved", FormatCount(recheck.Resolved)));
                card.Add(CreateMetadataRow("Remaining", FormatCount(recheck.Remaining)));
                card.Add(CreateMetadataRow("New", FormatCount(recheck.New)));
            }
            else card.Add(CreateMetadataRow("Comparison counts", "Not available"));
            return card;
        }

        private static VisualElement CreateMetadataRow(string key, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("reports-meta-row");
            var keyLabel = new Label(key);
            keyLabel.AddToClassList("reports-meta-key");
            var valueLabel = new Label(value ?? "Not recorded");
            valueLabel.AddToClassList("reports-meta-value");
            row.Add(keyLabel);
            row.Add(valueLabel);
            return row;
        }

        private static VisualElement CreateValueRow(bool heading, params string[] values)
        {
            var row = new VisualElement();
            row.AddToClassList("reports-value-row");
            row.EnableInClassList("reports-value-heading", heading);
            foreach (string value in values)
            {
                var label = new Label(value ?? "Unknown");
                label.AddToClassList("reports-value-cell");
                row.Add(label);
            }
            return row;
        }

        private static string FormatRiskSummary(IReadOnlyList<ReportFindingPreview> findings)
        {
            if (findings.Count == 0)
                return "No findings";
            return string.Join("  •  ", findings.GroupBy(finding => finding.Risk)
                .Select(group => $"{group.Key}: {group.Count()}"));
        }

        private static string FormatCategorySummary(IReadOnlyList<ReportFindingPreview> findings)
        {
            string[] categories = { "Textures", "Models", "Audio" };
            return string.Join("  •  ", categories.Select(category =>
                $"{category}: {findings.Count(finding => finding.Category == category)}"));
        }

        private void SetLabel(string name, string value)
        {
            Require<Label>(name).text = value ?? "Not recorded";
        }

        private static string FormatTime(DateTimeOffset time)
        {
            return time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'zzz");
        }

        private static string FormatCount(int? count)
        {
            return count?.ToString() ?? "Not recorded";
        }

        private T Require<T>(string name) where T : VisualElement
        {
            return _root.Q<T>(name)
                ?? throw new InvalidOperationException($"Missing UI element '{name}'.");
        }
    }
}
