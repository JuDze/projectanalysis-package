using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Attēlo vienas vai vairāku izmaiņu priekšskatījumu Analysis detaļu panelī.
    // Šim skatam nav piekļuves resursu maiņai vai labojumu stāvokļa saglabāšanai.
    internal sealed class FixPreviewView
    {
        private readonly ScrollView _scroll;
        private readonly VisualElement _resultDetails;
        private readonly VisualElement _preview;
        private readonly VisualElement _changes;
        private readonly VisualTreeAsset _changeTemplate;
        private readonly VisualTreeAsset _propertyChangeTemplate;
        private readonly Label _summary;
        private readonly Label _status;
        public FixPreviewView(VisualElement root, VisualTreeAsset changeTemplate, VisualTreeAsset propertyChangeTemplate)
        {
            _changeTemplate = changeTemplate ?? throw new ArgumentNullException(nameof(changeTemplate));
            _propertyChangeTemplate = propertyChangeTemplate ?? throw new ArgumentNullException(nameof(propertyChangeTemplate));
            _scroll = root.Q<ScrollView>("details-panel");
            _resultDetails = root.Q("result-details-content");
            _preview = root.Q("fix-preview-panel");
            _changes = root.Q("fix-preview-changes");
            _summary = root.Q<Label>("fix-preview-summary");
            _status = root.Q<Label>("fix-preview-status");
            root.Q<Button>("close-fix-preview-button").clicked += Hide;

            Hide();
        }

        public void Show(IEnumerable<FixPreviewRow> fixes, string applyUnavailableReason = null)
        {
            if (fixes == null) throw new ArgumentNullException(nameof(fixes));
            var rows = fixes.ToList();
            if (rows.Count == 0)
            {
                Hide();
                return;
            }

            // Apstiprinājums ir dialogā; šis panelis tikai attēlo izmaiņu detaļas.
            _status.text = applyUnavailableReason ??
                "Use Fix or Fix selected to apply changes after confirmation.";
            _changes.Clear();
            foreach (var row in rows)
            {
                var card = _changeTemplate.CloneTree();
                card.Q<Label>("fix-resource").text = row.Resource;
                card.Q<Label>("fix-path").text = row.Path;
                card.Q<Label>("fix-rule").text = $"{row.RuleId} — {row.RuleName}";
                card.Q<Label>("fix-risk").text = "Risk: " + row.Risk;
                card.Q<Label>("fix-recommendation").text = row.Recommendation;
                card.Q<Label>("fix-change-summary").text = row.ChangeSummary;
                card.Q<Label>("fix-analysis-target").text = "Analysis target: " + row.AnalysisTarget;
                card.Q<Label>("fix-settings-scope").text = "Settings scope: " + row.SettingsScope;
                var source = card.Q<Label>("fix-settings-source");
                source.text = "Current settings source: " + row.SettingsSource;
                source.EnableInClassList("pa-hidden", string.IsNullOrEmpty(row.SettingsSource));
                var properties = card.Q<VisualElement>("fix-property-changes");
                foreach (var change in row.PropertyChanges)
                {
                    // Katram parametram izveido atsevišķu salīdzinājuma rindu.
                    var item = _propertyChangeTemplate.CloneTree();
                    item.Q<Label>("fix-property").text = change.PropertyName;
                    item.Q<Label>("fix-current").text = change.CurrentValue;
                    item.Q<Label>("fix-proposed").text = change.ProposedValue;
                    properties.Add(item);
                }
                card.Q<Label>("fix-reason").text = row.Reason;
                card.Q<Label>("fix-affected").text = string.Join(
                    "\n", row.AffectedResources.Select(resource => resource.IsSubAsset
                        ? "    Mesh: " + resource.Name : resource.Path));
                card.Q<Label>("fix-consequences").text = row.Consequences;
                _changes.Add(card);
            }

            int resources = rows.SelectMany(row => row.AffectedResources)
                .Select(resource => resource.AssetId ?? resource.Id).Distinct(StringComparer.Ordinal).Count();
            _summary.text = $"Proposals: {rows.Count} · Asset files: {resources}";
            _resultDetails.AddToClassList("pa-hidden");
            _preview.RemoveFromClassList("pa-hidden");
            _scroll.scrollOffset = Vector2.zero;
        }

        public void Hide()
        {
            _changes.Clear();
            _preview.AddToClassList("pa-hidden");
            _resultDetails.RemoveFromClassList("pa-hidden");
            _scroll.scrollOffset = Vector2.zero;
        }

    }
}
