using System;
using Judze.ProjectAnalysis.Editor.Assets;
using System.Collections.Generic;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal sealed class UnityTextureRevertFactory : IAssetRevertFactory
    {
        private readonly ITextureFixExecutor _reader;
        public string Category => "Textures";
        public UnityTextureRevertFactory(ITextureFixExecutor reader) { _reader = reader; }
        public IAssetFixOperation Create(IReadOnlyList<FixRevertEntry> entries)
        {
            var original = entries[0].Asset.Before?.Copy() ?? throw new InvalidOperationException("Original texture settings are missing.");
            var expected = original;
            foreach (var entry in entries)
            {
                UnityRevertGuard.Unchanged(Equal(expected, entry.Asset.Before), original.Path);
                expected = entry.Asset.After ?? throw new InvalidOperationException("Actual texture settings are missing.");
            }
            var current = _reader.Capture(original.Guid);
            UnityRevertGuard.Unchanged(Equal(expected, current), original.Path);
            var record = FixRevertRecord.Create(entries); record.Before = current;
            void Validate()
            {
                UnityRevertGuard.Importer<TextureImporter>(original.Guid, original.Path);
                UnityRevertGuard.Unchanged(Equal(current, _reader.Capture(original.Guid)), original.Path);
            }
            void Write(TextureImporterSnapshot target)
            {
                var importer = UnityRevertGuard.Importer<TextureImporter>(original.Guid, original.Path, allowPending: true);
                // Maina tikai šajā periodā skartos parametrus; citus importētāja laukus saglabā.
                if (original.PlatformJson != current.PlatformJson) importer.SetPlatformTextureSettings(target.Platform());
                if (original.IsReadable != current.IsReadable) importer.isReadable = target.IsReadable;
                if (original.MipmapsEnabled != current.MipmapsEnabled) importer.mipmapEnabled = target.MipmapsEnabled;
                if (original.StreamingMipmaps != current.StreamingMipmaps) importer.streamingMipmaps = target.StreamingMipmaps;
                EditorUtility.SetDirty(importer); importer.SaveAndReimport();
                if (!Equal(target, _reader.Capture(original.Guid))) throw new InvalidOperationException("Texture restoration could not be verified: " + original.Path);
            }
            return new RevertAssetOperation(record, Validate, () => Write(original), () => Write(current),
                r => TextureFixChanges.RecordActual(r, _reader.Capture(original.Guid)));
        }
        private static bool Equal(TextureImporterSnapshot a, TextureImporterSnapshot b) => a != null && b != null &&
            a.Guid == b.Guid && a.Path == b.Path && a.PlatformJson == b.PlatformJson && a.DefaultJson == b.DefaultJson &&
            a.IsReadable == b.IsReadable && a.MipmapsEnabled == b.MipmapsEnabled && a.StreamingMipmaps == b.StreamingMipmaps;
    }
}
