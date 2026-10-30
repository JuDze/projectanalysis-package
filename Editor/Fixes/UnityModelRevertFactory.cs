using System;
using Judze.ProjectAnalysis.Editor.Assets;
using System.Collections.Generic;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal sealed class UnityModelRevertFactory : IAssetRevertFactory
    {
        private readonly IModelFixExecutor _reader;
        public string Category => "Models";
        public UnityModelRevertFactory(IModelFixExecutor reader) { _reader = reader; }
        public IAssetFixOperation Create(IReadOnlyList<FixRevertEntry> entries)
        {
            var original = entries[0].Asset.ModelBefore?.Copy() ?? throw new InvalidOperationException("Original model settings are missing.");
            var expected = original;
            foreach (var entry in entries)
            {
                UnityRevertGuard.Unchanged(Equal(expected, entry.Asset.ModelBefore), original.Path);
                expected = entry.Asset.ModelAfter ?? throw new InvalidOperationException("Actual model settings are missing.");
            }
            var current = _reader.Capture(original.Guid);
            UnityRevertGuard.Unchanged(Equal(expected, current), original.Path);
            var record = FixRevertRecord.Create(entries); record.ModelBefore = current;
            void Validate()
            {
                UnityRevertGuard.Importer<ModelImporter>(original.Guid, original.Path);
                UnityRevertGuard.Unchanged(Equal(current, _reader.Capture(original.Guid)), original.Path);
            }
            void Write(ModelImporterSnapshot target)
            {
                var importer = UnityRevertGuard.Importer<ModelImporter>(original.Guid, original.Path, allowPending: true);
                if (original.IsReadable != current.IsReadable) importer.isReadable = target.IsReadable;
                if (original.GenerateMeshLods != current.GenerateMeshLods) importer.generateMeshLods = target.GenerateMeshLods;
                EditorUtility.SetDirty(importer); importer.SaveAndReimport();
                if (!Equal(target, _reader.Capture(original.Guid))) throw new InvalidOperationException("Model restoration could not be verified: " + original.Path);
            }
            return new RevertAssetOperation(record, Validate, () => Write(original), () => Write(current), r =>
            {
                r.ModelAfter = _reader.Capture(original.Guid);
                FixRevertRecord.Actual(r, property => UnityRevertGuard.Flag(property == "Read/Write" ? r.ModelAfter.IsReadable : r.ModelAfter.GenerateMeshLods));
            });
        }
        private static bool Equal(ModelImporterSnapshot a, ModelImporterSnapshot b) => a != null && b != null &&
            a.Guid == b.Guid && a.Path == b.Path && a.IsReadable == b.IsReadable && a.GenerateMeshLods == b.GenerateMeshLods;
    }
}
