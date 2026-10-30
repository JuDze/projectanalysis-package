using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal interface IAssetRevertFactory
    {
        string Category { get; }
        IAssetFixOperation Create(IReadOnlyList<FixRevertEntry> entries);
    }

    // Atcelšanas plāns izmanto to pašu apstiprināto izmaiņu vadītāju un stāvokļa glabātāju.
    internal sealed class FixRevertPlanner
    {
        private readonly IReadOnlyDictionary<string, IAssetRevertFactory> _factories;
        public FixRevertPlanner(IEnumerable<IAssetRevertFactory> factories) => _factories = factories.ToDictionary(f => f.Category);
        public FixPlan Prepare(AnalysisRunResult baseline, IReadOnlyList<FixRevertEntry> pending, string guid = null)
        {
            var groups = pending.Where(e => guid == null || e.Asset.Guid == guid).GroupBy(e => e.Asset.Guid).ToArray();
            if (groups.Length == 0) throw new InvalidOperationException("There are no fixes to revert since the last Run Analysis.");
            var assets = groups.Select(group =>
            {
                var entries = group.ToArray();
                string category = entries[0].Asset.Category ?? "Textures";
                if (!_factories.TryGetValue(category, out var factory)) throw new NotSupportedException("Revert is unavailable for " + category);
                return factory.Create(entries);
            }).ToArray();
            return new FixPlan(baseline, assets, kind: FixOperationKind.Revert);
        }
    }

    // Deleģē tikai viena resursa atjaunošanu; Unity API paliek konkrētajā rūpnīcā.
    internal sealed class RevertAssetOperation : IAssetFixOperation
    {
        private readonly FixAssetRecord _record;
        private readonly Action _validate, _apply, _recover;
        private readonly Action<FixAssetRecord> _capture;
        public string Guid => _record.Guid;
        public string Path => _record.Path;
        public string Category => _record.Category;
        public RevertAssetOperation(FixAssetRecord record, Action validate, Action apply, Action recover, Action<FixAssetRecord> capture)
        { _record = record; _validate = validate; _apply = apply; _recover = recover; _capture = capture; }
        public void Validate() => _validate();
        public void Apply() => _apply();
        public void Restore() => _recover();
        public void RecordActual(FixAssetRecord record) => _capture(record);
        public FixAssetRecord CreateRecord() => JsonUtility.FromJson<FixAssetRecord>(JsonUtility.ToJson(_record));
    }

    internal static class FixRevertRecord
    {
        public static FixAssetRecord Create(IReadOnlyList<FixRevertEntry> entries)
        {
            var first = entries[0].Asset;
            return new FixAssetRecord
            {
                Guid = first.Guid, Path = first.Path, Category = first.Category ?? "Textures",
                RuleIds = entries.SelectMany(e => e.Asset.RuleIds).Distinct().ToList(),
                RevertsOperationIds = entries.Select(e => e.Operation.Id).Distinct().ToList(),
                Changes = entries.SelectMany(e => e.Asset.Changes).GroupBy(c => c.Property).Select(g => new FixValueChange
                    { Property = g.Key, Before = g.Last().Actual, Intended = g.First().Before }).ToList()
            };
        }
        public static void Actual(FixAssetRecord record, Func<string, string> read)
        { foreach (var change in record.Changes) change.Actual = read(change.Property); }
    }

    internal sealed class FixRevertEntry
    {
        public FixOperationRecord Operation { get; }
        public FixAssetRecord Asset { get; }
        public FixRevertEntry(FixOperationRecord operation, FixAssetRecord asset) { Operation = operation; Asset = asset; }
    }

    internal static class PendingFixes
    {
        public static IReadOnlyList<FixRevertEntry> Pending(IEnumerable<FixOperationRecord> operations, string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return Array.Empty<FixRevertEntry>();
            var records = operations.Where(o => o.SessionId == sessionId).OrderBy(o => o.StartedUtc, StringComparer.Ordinal).ThenBy(o => o.Id).ToArray();
            // Atcelšana tiek uzskaitīta katrai datnei atsevišķi, arī daļēji pabeigtā operācijā.
            var reverted = new HashSet<string>(records.Where(o => o.Action == "Revert fixes")
                .SelectMany(o => o.Assets.Where(a => a.State == FixAssetStatus.Completed)
                    .SelectMany(a => (a.RevertsOperationIds ?? new List<string>()).Select(id => id + ":" + a.Guid))));
            return records.Where(o => o.Action != "Revert fixes")
                .SelectMany(o => o.Assets.Where(a => a.State == FixAssetStatus.Completed && !reverted.Contains(o.Id + ":" + a.Guid))
                    .Select(a => new FixRevertEntry(o, a))).ToList().AsReadOnly();
        }
    }
}
