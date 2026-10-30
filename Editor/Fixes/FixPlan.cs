using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    // Operācija attiecas uz vienu datni; noteikumu izvērtēšana šajā līgumā neietilpst.
    internal interface IAssetFixOperation
    {
        string Guid { get; }
        string Path { get; }
        string Category { get; }
        void Validate();
        FixAssetRecord CreateRecord();
        void Apply();
        void Restore();
        void RecordActual(FixAssetRecord record);
    }

    internal enum FixOperationKind { Apply, Revert }

    internal sealed class FixPlan
    {
        private readonly Action _validateContext;
        public string Id { get; }
        public FixOperationKind Kind { get; }
        public string Notes { get; }
        public AnalysisRunResult Baseline { get; }
        public IReadOnlyList<IAssetFixOperation> Assets { get; }
        public FixPlan(AnalysisRunResult baseline, IEnumerable<IAssetFixOperation> assets,
            Action validateContext = null, string id = null, FixOperationKind kind = FixOperationKind.Apply, string notes = null)
        {
            Baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
            Assets = (assets ?? throw new ArgumentNullException(nameof(assets))).ToList().AsReadOnly();
            if (Assets.Any(a => a == null) || Assets.Select(a => a.Guid).Distinct().Count() != Assets.Count)
                throw new ArgumentException("Each asset must occur exactly once in a fix plan.");
            _validateContext = validateContext;
            Kind = kind; Notes = notes;
            Id = id ?? System.Guid.NewGuid().ToString("N");
        }
        public void ValidateBeforeApply()
        {
            _validateContext?.Invoke();
            foreach (var asset in Assets) asset.Validate();
        }
    }
}
