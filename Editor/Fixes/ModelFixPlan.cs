using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Models;
using Judze.ProjectAnalysis.Editor.Analysis.Models.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal sealed class ModelAssetFixPlan
    {
        private readonly ModelImporterSnapshot _before;
        public ModelImporterSnapshot Before => _before.Copy();
        public string Guid => _before.Guid;
        public string Path => _before.Path;
        public ModelFacts Facts { get; }
        public IReadOnlyList<RuleResult> Findings { get; }
        public bool? IsReadable { get; }
        public bool? GenerateMeshLods { get; }
        public ModelAssetFixPlan(ModelImporterSnapshot before, ModelAssetPreview preview, bool? readable, bool? lods)
        {
            _before = before.Copy(); Facts = preview.Model; Findings = preview.Findings;
            IsReadable = readable; GenerateMeshLods = lods;
        }
    }

    internal sealed class ModelFixPlan
    {
        public AnalysisRunResult Baseline { get; }
        public ModelFixPreview Preview { get; }
        public IReadOnlyList<ModelAssetFixPlan> Assets { get; }
        public ModelFixPlan(AnalysisRunResult baseline, ModelFixPreview preview, IEnumerable<ModelAssetFixPlan> assets)
        { Baseline = baseline; Preview = preview; Assets = assets.ToList().AsReadOnly(); }
    }

    internal sealed class ModelFixOperation : IAssetFixOperation
    {
        private readonly ModelAssetFixPlan _asset;
        private readonly AnalysisRunResult _baseline;
        private readonly IModelFixExecutor _executor;
        private readonly ModelFixPreviewService _review;
        public string Guid => _asset.Guid;
        public string Path => _asset.Path;
        public string Category => "Models";
        public ModelFixOperation(ModelAssetFixPlan asset, AnalysisRunResult baseline, IModelFixExecutor executor, ModelFixPreviewService review)
        { _asset = asset; _baseline = baseline; _executor = executor; _review = review; }
        public void Validate()
        {
            // Iepriekšējās datnes imports var mainīt kopīgo atkarību hash; šeit pārbauda konkrētās datnes faktus un LOD lietojumu.
            _review.ValidateAsset(_baseline, _asset.Findings);
            _executor.Validate(_asset, _baseline.Context.TargetPlatform);
        }
        public void Apply() => _executor.Apply(_asset);
        public void Restore() => _executor.Restore(_asset);
        public FixAssetRecord CreateRecord() => new FixAssetRecord
        {
            Guid = Guid, Path = Path, Category = Category, ModelBefore = _asset.Before,
            RuleIds = _asset.Findings.Select(f => f.RuleId).Distinct().ToList(), Changes = ModelFixChanges.Describe(_asset)
        };
        public void RecordActual(FixAssetRecord record) => ModelFixChanges.RecordActual(record, _asset, _executor.Capture(Guid));
    }

    internal static class ModelFixChanges
    {
        public static List<FixValueChange> Describe(ModelAssetFixPlan asset)
        {
            var changes = new List<FixValueChange>();
            if (asset.IsReadable.HasValue) changes.Add(Change("Read/Write", asset.Before.IsReadable, asset.IsReadable.Value));
            if (asset.GenerateMeshLods.HasValue) changes.Add(Change("Generate Mesh LODs", asset.Before.GenerateMeshLods, asset.GenerateMeshLods.Value));
            return changes;
        }
        public static void RecordActual(FixAssetRecord record, ModelAssetFixPlan asset, ModelImporterSnapshot actual)
        {
            record.ModelAfter = actual;
            foreach (var change in record.Changes)
                change.Actual = Flag(change.Property == "Read/Write" ? actual.IsReadable : actual.GenerateMeshLods);
            record.ModelChecks.Clear();
            if (asset.GenerateMeshLods != true || !actual.GenerateMeshLods) return;
            // Izmanto apstiprinātās atradnes tīklus, nedublējot noteikuma atlases robežvērtību.
            var candidates = new HashSet<(string, long?)>(asset.Findings
                .Where(f => f.Proposal is ModelLodGenerationProposal)
                .SelectMany(f => f.RelatedResources).Select(r => (r.Guid, r.LocalId)));
            // Iestatījuma maiņa un ģenerēšanas rezultāts tiek pārbaudīti atsevišķi.
            foreach (var mesh in asset.Facts.Meshes.Where(m => candidates.Contains((m.Guid, m.LocalId))))
            {
                var imported = actual.Meshes.SingleOrDefault(m => m.Guid == mesh.Guid && m.LocalId == mesh.LocalId);
                record.ModelChecks.Add(imported == null ? mesh.Name + ": LOD result is unknown; the original mesh was not found after import." :
                    imported.LodCount > 1 ? mesh.Name + ": Mesh LOD levels " + mesh.LodCount + " → " + imported.LodCount + ". Review visual quality." :
                    mesh.Name + ": Generate Mesh LODs is enabled, but no additional levels were produced. Review the import result.");
            }
        }
        private static FixValueChange Change(string name, bool before, bool intended) =>
            new FixValueChange { Property = name, Before = Flag(before), Intended = Flag(intended) };
        private static string Flag(bool value) => value ? "Enabled" : "Disabled";
    }
}
