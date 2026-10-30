using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Pārvalda tikai pārskatu saraksta atlasi, meklēšanu un filtrēšanu.
    internal sealed class ReportsListView
    {
        private readonly List<ReportPreviewRow> _allRows;
        private readonly List<ReportPreviewRow> _visibleRows = new List<ReportPreviewRow>();
        private readonly ToolbarSearchField _search;
        private readonly DropdownField _typeFilter;
        private readonly MultiColumnListView _table;
        private readonly Label _emptyMessage;
        private readonly Label _summary;
        private ReportPreviewRow _selectedReport;
        private bool _updatingRows;

        // Null nozīmē, ka redzamajā sarakstā nav atlasīta pārskata.
        public event Action<ReportPreviewRow> SelectionChanged;
        public ReportPreviewRow SelectedReport => _selectedReport;

        public ReportsListView(VisualElement root, IEnumerable<ReportPreviewRow> rows)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            _allRows = rows.OrderByDescending(row => row.Created)
                .ThenBy(row => row.Id, StringComparer.Ordinal).ToList();
            _search = Require<ToolbarSearchField>(root, "reports-search");
            _typeFilter = Require<DropdownField>(root, "reports-type-filter");
            _table = Require<MultiColumnListView>(root, "reports-table");
            _emptyMessage = Require<Label>(root, "reports-empty-message");
            _summary = Require<Label>(root, "reports-list-summary");

            _typeFilter.choices = new List<string>
            {
                "All types", "Analysis", "Analysis and fixes"
            };
            _typeFilter.SetValueWithoutNotify("All types");
            BindColumn("created", row => row.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                row => $"{row.Created.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz} · {row.Id}");
            BindColumn("title", row => row.Title, row => $"{row.Id}\n{row.Title}\n{row.ProjectName}");
            BindColumn("type", row => row.Type);
            BindColumn("resources", row => row.ResourceCount.ToString(),
                row => $"Resources checked in the original analysis: {row.ResourceCount}");
            BindColumn("findings", row => row.Findings.Count.ToString(),
                row => $"Findings in the original analysis: {row.Findings.Count}");

            _table.selectionType = SelectionType.Single;
            _table.itemsSource = _visibleRows;
            _table.selectionChanged += _ =>
            {
                // Saraksta pārbūves pagaidu indeksus neuzskatām par lietotāja izvēli.
                if (!_updatingRows)
                    SetSelectedReport(_table.selectedItem as ReportPreviewRow);
            };
            _search.RegisterValueChangedCallback(_ => ApplyFilters());
            _typeFilter.RegisterValueChangedCallback(_ => ApplyFilters());
            Require<Button>(root, "clear-reports-filters-button").clicked += () =>
            {
                _search.SetValueWithoutNotify(string.Empty);
                _typeFilter.SetValueWithoutNotify("All types");
                ApplyFilters();
            };
            ApplyFilters();
        }

        public void SetRows(IEnumerable<ReportPreviewRow> rows)
        {
            _allRows.Clear();
            _allRows.AddRange(rows.OrderByDescending(row => row.Created).ThenBy(row => row.Id, StringComparer.Ordinal));
            ApplyFilters();
        }

        public bool SelectReport(string id)
        {
            if (!_allRows.Any(row => row.Id == id)) return false;
            // Jaunais pārskats ir jāatver arī tad, ja iepriekšējais filtrs to paslēptu.
            _search.SetValueWithoutNotify(string.Empty);
            _typeFilter.SetValueWithoutNotify("All types");
            ApplyFilters();
            _table.SetSelection(_visibleRows.FindIndex(row => row.Id == id));
            return true;
        }

        private void ApplyFilters()
        {
            string selectedId = _selectedReport?.Id;
            string query = (_search.value ?? string.Empty).Trim();
            ReportPreviewRow nextSelection = null;
            _updatingRows = true;
            try
            {
                _table.SetSelectionWithoutNotify(Array.Empty<int>());
                _visibleRows.Clear();
                foreach (var row in _allRows)
                {
                    bool typeMatches = _typeFilter.value == "All types" || row.Type == _typeFilter.value;
                    bool textMatches = query.Length == 0 || Contains(row.Id, query) ||
                        Contains(row.Title, query) || Contains(row.ProjectName, query);
                    if (typeMatches && textMatches)
                        _visibleRows.Add(row);
                }

                _table.RefreshItems();
                int index = _visibleRows.FindIndex(row =>
                    string.Equals(row.Id, selectedId, StringComparison.Ordinal));
                if (index >= 0)
                {
                    _table.SetSelectionWithoutNotify(new[] { index });
                    nextSelection = _visibleRows[index];
                }
            }
            finally
            {
                _updatingRows = false;
            }

            _summary.text = $"Showing {_visibleRows.Count} of {_allRows.Count} reports";
            _emptyMessage.text = _allRows.Count == 0
                ? "No reports available."
                : "No reports match the current filters.";
            _emptyMessage.EnableInClassList("pa-hidden", _visibleRows.Count > 0);
            SetSelectedReport(nextSelection);
        }

        private void SetSelectedReport(ReportPreviewRow report)
        {
            bool changed = !string.Equals(_selectedReport?.Id, report?.Id, StringComparison.Ordinal);
            _selectedReport = report;
            // Ja atlase nemainās, saglabājam atvērto detaļu cilni un ritinājumu.
            if (changed)
                SelectionChanged?.Invoke(report);
        }

        private static bool Contains(string text, string query) =>
            !string.IsNullOrEmpty(text) && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private void BindColumn(string name, Func<ReportPreviewRow, string> text,
            Func<ReportPreviewRow, string> tooltip = null)
        {
            var column = _table.columns[name];
            column.makeCell = () =>
            {
                var label = new Label();
                label.AddToClassList("reports-cell");
                return label;
            };
            column.bindCell = (element, index) =>
            {
                var label = (Label)element;
                var row = _visibleRows[index];
                label.text = text(row);
                label.tooltip = tooltip == null ? label.text : tooltip(row);
            };
            column.unbindCell = (element, _) =>
            {
                var label = (Label)element;
                label.text = string.Empty;
                label.tooltip = string.Empty;
            };
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UI element '{name}'.");
    }
}
