using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Judze.ProjectAnalysis.Editor.UI
{

    // Attēlo analīzes rezultātus un filtrē tos pēc kategorijas, riska un teksta.
    // Skats pats neiegūst projekta datus un neizpilda analīzes noteikumus.

    internal sealed class AnalysisResultsView
    {
        private readonly List<AnalysisResultRow> _allRows;
        private readonly Dictionary<AnalysisResultRow, string> _searchableText = new Dictionary<AnalysisResultRow, string>();
        private readonly List<AnalysisResultRow> _visibleRows = new List<AnalysisResultRow>();
        private readonly HashSet<string> _selectedFixIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<ToolbarToggle> _categoryButtons = new List<ToolbarToggle>();
        private readonly ToolbarSearchField _search;
        private readonly DropdownField _riskFilter;
        private readonly MultiColumnListView _table;
        private readonly Button _clearResultsButton;
        private readonly Label _summary;
        private readonly Label _emptyMessage;
        private string _category = "All";
        private bool _hasOutput;

        // Null vērtība paziņo, ka atlasītā rinda vairs nav pieejama.
        public event Action<AnalysisResultRow> ResultSelected;
        public event Action<AnalysisResultRow> FixRequested;
        public event Action<string> UndoRequested;
        public event Action FixSelectionChanged;
        public event Action ViewChanged;
        public event Action ResultsCleared;

        // Atzīmes saglabājam arī filtriem paslēptajām rindām un vienmēr parādām to skaitu.
        public int SelectedFixCount => _selectedFixIds.Count;

        public IReadOnlyList<Judze.ProjectAnalysis.Editor.Analysis.Rules.RuleResult> SelectedFindings => _allRows
            .Where(row => row.Source != null && row.Fix != null && _selectedFixIds.Contains(row.Fix.Id))
            .Select(row => row.Source).ToList();

        public int HiddenSelectedFixCount => _selectedFixIds.Count - _visibleRows.Count(
            row => row.Fix != null && _selectedFixIds.Contains(row.Fix.Id));

        public AnalysisResultsView(VisualElement root, IEnumerable<AnalysisResultRow> rows)
        {
            _allRows = rows.ToList();
            CacheSearchText();
            _hasOutput = _allRows.Count > 0;
            _search = root.Q<ToolbarSearchField>("result-search");
            _riskFilter = root.Q<DropdownField>("risk-filter");
            _table = root.Q<MultiColumnListView>("results-table");
            _emptyMessage = root.Q<Label>("results-empty-message");
            _summary = root.Q<Label>("results-summary");
            _clearResultsButton = root.Q<Button>("clear-results-button");
            _clearResultsButton.clicked += ClearResults;

            // Kolonnu nosaukumus, platumus un secību nosaka UXML; C# piesaista šūnu datus.
            BindFixSelectionColumn();
            BindColumn("risk", row => row.Risk);
            BindColumn("resource", row => row.Resource);
            BindColumn("finding", row => row.Finding);
            BindFixActionColumn();
            BindUndoColumn();
            _table.itemsSource = _visibleRows;
            _table.selectionChanged += _ =>
                ResultSelected?.Invoke(_table.selectedItem as AnalysisResultRow);

            // Filtru vērtības nav atkarīgas no UI Builder rediģējamajiem pogu uzrakstiem.
            BindCategory(root, "category-all", "All");
            BindCategory(root, "category-textures", "Textures");
            BindCategory(root, "category-models", "Models");
            BindCategory(root, "category-audio", "Audio");

            _search.RegisterValueChangedCallback(_ => ApplyFilters());
            _riskFilter.RegisterValueChangedCallback(_ => ApplyFilters());
            ApplyFilters();
        }

        private void BindCategory(VisualElement root, string name, string category)
        {
            var button = root.Q<ToolbarToggle>(name);
            button.SetValueWithoutNotify(category == _category);
            _categoryButtons.Add(button);
            button.RegisterValueChangedCallback(_ =>
            {
                _category = category;
                // Vienlaikus aktīva ir viena kategorija; novēršam atkārtotus notikumus.
                foreach (var categoryButton in _categoryButtons)
                    categoryButton.SetValueWithoutNotify(categoryButton == button);
                ApplyFilters();
            });
        }

        public bool CanSelectAllVisibleFixes => _visibleRows.Any(
            row => row.Fix != null && !_selectedFixIds.Contains(row.Fix.Id));

        public void SelectAllVisibleFixes()
        {
            bool changed = false;
            // Atlasām visas filtrētā saraksta labojamās rindas, arī ārpus redzamās ritināšanas zonas.
            // Iepriekš atzīmētie, ar filtriem paslēptie labojumi paliek atlasīti.
            foreach (var row in _visibleRows)
            {
                if (row.Fix != null)
                    changed |= _selectedFixIds.Add(row.Fix.Id);
            }
            if (!changed)
                return;

            _table.RefreshItems();
            FixSelectionChanged?.Invoke();
        }

        public void ClearFixSelection()
        {
            if (_selectedFixIds.Count == 0)
                return;
            _selectedFixIds.Clear();
            _table.RefreshItems();
            FixSelectionChanged?.Invoke();
        }

        private void BindFixSelectionColumn()
        {
            var column = _table.columns["selected"];
            column.makeCell = () =>
            {
                var toggle = new Toggle();
                toggle.AddToClassList("analysis-fix-toggle");
                // Savs atzīmes simbols ļauj aizstāt Editor tēmas tumšo fona attēlu.
                var check = new Label("✓") { pickingMode = PickingMode.Ignore };
                check.AddToClassList("analysis-fix-check");
                toggle.Q<VisualElement>(className: "unity-toggle__checkmark").Add(check);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    var row = toggle.userData as AnalysisResultRow;
                    if (row?.Fix == null)
                        return;
                    bool changed = evt.newValue
                        ? _selectedFixIds.Add(row.Fix.Id)
                        : _selectedFixIds.Remove(row.Fix.Id);
                    if (changed)
                        FixSelectionChanged?.Invoke();
                });

                // Labojuma atzīme nemaina atvērto rezultāta rindu.
                toggle.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                toggle.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
                toggle.RegisterCallback<PointerCancelEvent>(evt => evt.StopPropagation());
                toggle.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (evt.button >= 0)
                        evt.StopPropagation();
                });
                return toggle;
            };
            column.bindCell = (element, index) =>
            {
                var toggle = (Toggle)element;
                var row = _visibleRows[index];
                toggle.userData = row;
                toggle.SetValueWithoutNotify(row.Fix != null && _selectedFixIds.Contains(row.Fix.Id));
                toggle.SetEnabled(row.Fix != null);
                toggle.tooltip = row.Fix == null
                    ? "No supported fix preview is available. Review the result details."
                    : $"Include {row.RuleId}: {row.Resource} in the fix preview";
            };
            column.unbindCell = (element, _) =>
            {
                var toggle = (Toggle)element;
                toggle.userData = null;
                toggle.SetValueWithoutNotify(false);
                toggle.tooltip = string.Empty;
            };
        }

        private void BindFixActionColumn()
        {
            var column = _table.columns["action"];
            column.makeCell = () =>
            {
                var button = new Button { text = "Fix", name = "result-row-fix-button" };
                button.AddToClassList("analysis-row-fix-button");
                button.clicked += () =>
                {
                    // Virtualizētā šūna vienmēr izmanto pašlaik piesaistīto rindu.
                    var row = button.userData as AnalysisResultRow;
                    if (row?.Fix == null || row.Source == null) return;
                    int index = _visibleRows.IndexOf(row);
                    if (index < 0) return;
                    _table.SetSelection(index);
                    FixRequested?.Invoke(row);
                };
                // Tabulas klikšķa apstrāde nedrīkst aizvērt tikko atvērto priekšskatījumu.
                button.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                button.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
                return button;
            };
            column.bindCell = (element, index) =>
            {
                var button = (Button)element;
                var row = _visibleRows[index];
                button.userData = row;
                button.SetEnabled(row.Fix != null && row.Source != null);
                button.tooltip = row.Fix == null
                    ? "No supported fix is available. Select the row to read the recommendation."
                    : $"Apply {row.RuleId}: {row.Resource} after confirmation.";
            };
            column.unbindCell = (element, _) =>
            {
                element.userData = null;
                element.SetEnabled(false);
                element.tooltip = string.Empty;
            };
        }

        public void SetFixState(ISet<string> guids, ISet<string> appliedFixes)
        {
            foreach (var row in _allRows)
            {
                row.CanUndo = row.Source?.Resource != null && guids.Contains(row.Source.Resource.Guid);
                row.HasAppliedFix = row.CanUndo && (row.IsResolved || appliedFixes.Contains(row.RuleId + ":" + row.Source.Resource.Guid));
            }
            _table.RefreshItems();
        }

        private void BindUndoColumn()
        {
            var column = _table.columns["undo"];
            column.makeCell = () =>
            {
                var button = new Button { text = "Undo", name = "result-row-undo-button" };
                button.AddToClassList("analysis-row-fix-button");
                button.clicked += () =>
                {
                    var row = button.userData as AnalysisResultRow;
                    if (row?.CanUndo != true || row.Source?.Resource == null || !_visibleRows.Contains(row)) return;
                    UndoRequested?.Invoke(row.Source.Resource.Guid);
                };
                button.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                button.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
                return button;
            };
            column.bindCell = (element, index) =>
            {
                var row = _visibleRows[index]; element.userData = row; element.SetEnabled(row.CanUndo);
                element.tooltip = row.CanUndo ? "Revert all fixes for this asset file since the last Run Analysis." : "No fixes to revert for this resource in the current analysis session.";
            };
            column.unbindCell = (element, _) => { element.userData = null; element.SetEnabled(false); element.tooltip = string.Empty; };
        }

        private void BindColumn(string name, Func<AnalysisResultRow, string> getText)
        {
            var column = _table.columns[name];
            column.makeCell = () =>
            {
                var label = new Label();
                label.AddToClassList("project-analysis-results__cell");
                return label;
            };
            column.bindCell = (element, index) =>
            {
                // Piesaistām šūnu filtrētajam sarakstam, jo rindu indeksi mainās.
                var label = (Label)element;
                var row = _visibleRows[index];
                string text = getText(row);
                // Pārizmantojot šūnu, atjauno arī parasto teksta krāsu.
                label.EnableInClassList("analysis-result-applied", row.HasAppliedFix);
                label.text = text;
                label.tooltip = text;
            };
            column.unbindCell = (element, _) =>
            {
                element.RemoveFromClassList("analysis-result-applied");
                ((Label)element).text = string.Empty;
                element.tooltip = string.Empty;
            };
        }

        public void SetResults(IEnumerable<AnalysisResultRow> rows, bool hasOutput = true, bool preserveSelection = false)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var newRows = rows.ToList();
            var selected = preserveSelection ? _table.selectedItem as AnalysisResultRow : null;
            // Vecā atlase nedrīkst pāriet uz citu palaišanu.
            _table.SetSelectionWithoutNotify(Array.Empty<int>());
            _selectedFixIds.Clear();
            _allRows.Clear();
            _allRows.AddRange(newRows);
            CacheSearchText();
            _hasOutput = hasOutput;
            ApplyFilters();
            FixSelectionChanged?.Invoke();
            if (selected != null)
            {
                int index = _visibleRows.FindIndex(row => row.RuleId == selected.RuleId &&
                    row.Source?.Resource?.Identity == selected.Source?.Resource?.Identity);
                if (index >= 0) _table.SetSelection(index);
            }
        }

        public void ClearResults()
        {
            SetResults(Array.Empty<AnalysisResultRow>(), false);
            ResultsCleared?.Invoke();
        }

        private void CacheSearchText()
        {
            // Tekstu sagatavo vienreiz jaunai rezultātu kopai, nevis katram meklēšanas taustiņam.
            _searchableText.Clear();
            foreach (var row in _allRows)
                _searchableText[row] = string.Join(" ", row.Resource, row.Path, row.RuleId, row.Finding);
        }

        private void ApplyFilters()
        {
            // Saglabājam atsauci uz atlasīto rindu pirms saraksta pārveidošanas.
            var previousSelection = _table.selectedItem as AnalysisResultRow;
            _table.SetSelectionWithoutNotify(Array.Empty<int>());

            string query = (_search.value ?? string.Empty).Trim();
            _visibleRows.Clear();

            foreach (var row in _allRows)
            {
                // Kategorijas un riska neatbilstības izlaiž pirms teksta meklēšanas.
                if (_category != "All" && row.Category != _category) continue;
                if (_riskFilter.value != "All risks" && row.Risk != _riskFilter.value) continue;
                if (query.Length == 0 || _searchableText[row].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    _visibleRows.Add(row);
            }

            _table.RefreshItems();
            _summary.text = $"{_visibleRows.Count} of {_allRows.Count} findings";
            _clearResultsButton.SetEnabled(_hasOutput);
            _emptyMessage.text = _allRows.Count == 0
                ? "No results to display."
                : "No results match the current filters.";
            _emptyMessage.EnableInClassList("pa-hidden", _visibleRows.Count > 0);

            // Atjaunojam atlasi tikai tad, ja iepriekšējā rinda joprojām ir redzama.
            int previousIndex = _visibleRows.IndexOf(previousSelection);
            if (previousIndex >= 0)
                _table.SetSelectionWithoutNotify(new[] { previousIndex });
            ResultSelected?.Invoke(previousIndex >= 0 ? previousSelection : null);
            ViewChanged?.Invoke();
        }
    }
}
