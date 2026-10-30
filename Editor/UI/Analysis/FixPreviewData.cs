using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Fixes;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Priekšskatījuma attēlošanas dati nav izpildāms labojuma plāns.
    internal sealed class FixPreviewRow
    {
        public string Id { get; set; }
        public string Risk { get; set; }
        public string Resource { get; set; }
        public string Path { get; set; }
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string ChangeSummary { get; set; }
        public string Reason { get; set; }
        public string Recommendation { get; set; }
        public string Consequences { get; set; }

        // Saglabā pabeigtās analīzes kontekstu, nevis pašreizējās formas melnrakstu.
        public string AnalysisTarget { get; set; }
        public string SettingsScope { get; set; }
        public string SettingsSource { get; set; }

        // Vienā priekšlikumā var būt vairākas saistītas parametru izmaiņas.
        public IReadOnlyList<FixPreviewPropertyChange> PropertyChanges { get; set; } =
            Array.Empty<FixPreviewPropertyChange>();
        public IReadOnlyList<FixPreviewResource> AffectedResources { get; set; } =
            Array.Empty<FixPreviewResource>();
    }

    // Viena parametra pašreizējā un piedāvātā vērtība priekšskatījumā.
    internal sealed class FixPreviewPropertyChange
    {
        public string PropertyName { get; }
        public string CurrentValue { get; }
        public string ProposedValue { get; }

        public FixPreviewPropertyChange(string propertyName, string currentValue, string proposedValue)
        {
            PropertyName = propertyName;
            CurrentValue = currentValue;
            ProposedValue = proposedValue;
        }
    }

    // Apraksta maināmo datni; Id izmanto analīzē iegūto GUID.
    internal sealed class FixPreviewResource
    {
        public string Id { get; set; }
        // Mesh identitāte atšķiras no tās datnes GUID.
        public string AssetId { get; set; }
        public bool IsSubAsset { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
    }

    // Priekšskatījums un apstiprinājums attiecas uz visu pārbaudīto izmaiņu kopu.
    internal sealed class AnalysisFixPreview
    {
        public IReadOnlyList<FixPreviewRow> Rows { get; }
        public FixPlan Plan { get; }
        public string ApplyUnavailableReason { get; }
        public AnalysisFixPreview(IEnumerable<FixPreviewRow> rows, FixPlan plan, string applyUnavailableReason = null)
        {
            Rows = new List<FixPreviewRow>(rows ?? throw new ArgumentNullException(nameof(rows))).AsReadOnly();
            Plan = plan;
            ApplyUnavailableReason = applyUnavailableReason;
        }
    }
}
