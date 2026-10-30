using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Assets;
using System;
using System.IO;
using Judze.ProjectAnalysis.Editor.Analysis.Textures;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    // Vienīgā komponente, kas šajā darbplūsmā maina tekstūras importētāju.
    internal sealed class UnityTextureFixExecutor : ITextureFixExecutor
    {
        public TextureAnalysisContext ReadContext(BuildTarget target)
        {
            int quality = QualitySettings.GetQualityLevel();
            return new TextureAnalysisContext(target, EditorUserBuildSettings.activeBuildTarget, AnalysisBuildTarget.StandaloneImporterName,
                quality, QualitySettings.names[quality], QualitySettings.streamingMipmapsActive);
        }

        public TextureImporterSnapshot Capture(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (string.IsNullOrEmpty(path) || importer == null || importer.textureShape != TextureImporterShape.Texture2D)
                throw new InvalidOperationException("The imported Texture2D is no longer available: " + guid);
            return new TextureImporterSnapshot
            {
                Guid = guid, Path = path, ImportVersion = UnityAssetVersion.Read(path, importer),
                PlatformJson = JsonUtility.ToJson(importer.GetPlatformTextureSettings(AnalysisBuildTarget.StandaloneImporterName)),
                DefaultJson = JsonUtility.ToJson(importer.GetDefaultPlatformTextureSettings()),
                IsReadable = importer.isReadable, StreamingMipmaps = importer.streamingMipmaps,
                MipmapsEnabled = importer.mipmapEnabled
            };
        }

        public void Validate(TextureAssetFixPlan asset, TextureAnalysisContext context)
        {
            TextureFixPlanner.RequireContext(context, ReadContext(context.TargetPlatform));
            var current = Capture(asset.Guid);
            if (current.Path != asset.Path || current.ImportVersion != asset.Before.ImportVersion)
                throw new InvalidOperationException("Texture settings have changed. Run analysis again: " + current.Path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(current.Path);
            if (EditorUtility.IsDirty(importer))
                throw new InvalidOperationException("Apply or revert pending Inspector changes first: " + current.Path);
            UnityAssetEditability.RequireWritable(current.Path);
            if (!AnalysisBuildTarget.IsStandalone(context.TargetPlatform))
                throw new NotSupportedException("The current texture fixes require Standalone import settings.");
            if (asset.MaxTextureSize.HasValue || asset.StreamingMipmaps.HasValue)
                if (!context.ImportedDataMatchesTarget)
                    throw new InvalidOperationException("Imported texture data does not match the analysis target.");
            if (asset.MaxTextureSize.HasValue)
            {
                int size = asset.MaxTextureSize.Value;
                if (size < 32 || size > 16384 || (size & (size - 1)) != 0 || size >= current.Effective().maxTextureSize)
                    throw new InvalidOperationException("The proposed Max Size is no longer a valid reduction.");
            }
            if (asset.IsReadable.HasValue && (asset.IsReadable.Value || !current.IsReadable))
                throw new InvalidOperationException("Read/Write no longer matches the reviewed change.");
            if (asset.StreamingMipmaps.HasValue && (!asset.StreamingMipmaps.Value || current.StreamingMipmaps ||
                !importer.mipmapEnabled || !context.GlobalStreamingMipmapsActive))
                throw new InvalidOperationException("Streaming Mipmaps no longer matches the reviewed change.");
            if (asset.MipmapsEnabled.HasValue && (asset.MipmapsEnabled.Value || !current.MipmapsEnabled ||
                importer.textureType != TextureImporterType.Sprite || asset.StreamingMipmaps == true))
                throw new InvalidOperationException("Sprite mipmaps no longer match the reviewed change.");
        }

        public void Apply(TextureAssetFixPlan asset)
        {
            var importer = GetImporter(asset);
            if (asset.ChangesPlatform) importer.SetPlatformTextureSettings(asset.ExpectedPlatform());
            if (asset.IsReadable.HasValue) importer.isReadable = asset.IsReadable.Value;
            if (asset.MipmapsEnabled.HasValue) importer.mipmapEnabled = asset.MipmapsEnabled.Value;
            if (asset.StreamingMipmaps.HasValue) importer.streamingMipmaps = asset.StreamingMipmaps.Value;
            // Visas viena resursa izmaiņas importē kopā, nevis atsevišķi katram noteikumam.
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Verify(asset, false);
        }

        public void Restore(TextureAssetFixPlan asset)
        {
            var importer = GetImporter(asset);
            var before = asset.Before;
            if (asset.ChangesPlatform) importer.SetPlatformTextureSettings(before.Platform());
            if (asset.IsReadable.HasValue) importer.isReadable = before.IsReadable;
            if (asset.MipmapsEnabled.HasValue) importer.mipmapEnabled = before.MipmapsEnabled;
            if (asset.StreamingMipmaps.HasValue) importer.streamingMipmaps = before.StreamingMipmaps;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Verify(asset, true);
        }

        private void Verify(TextureAssetFixPlan asset, bool restoring)
        {
            var actual = Capture(asset.Guid);
            var before = asset.Before;
            string platform = restoring ? before.PlatformJson : JsonUtility.ToJson(asset.ExpectedPlatform());
            bool readable = restoring ? before.IsReadable : asset.IsReadable ?? before.IsReadable;
            bool mipmaps = restoring ? before.MipmapsEnabled : asset.MipmapsEnabled ?? before.MipmapsEnabled;
            bool streaming = restoring ? before.StreamingMipmaps : asset.StreamingMipmaps ?? before.StreamingMipmaps;
            if (actual.PlatformJson != platform || actual.DefaultJson != before.DefaultJson ||
                actual.IsReadable != readable || actual.StreamingMipmaps != streaming || actual.MipmapsEnabled != mipmaps)
                throw new InvalidOperationException("Imported settings differ from the expected values: " + asset.Path);
        }

        private static TextureImporter GetImporter(TextureAssetFixPlan asset)
        {
            string path = AssetDatabase.GUIDToAssetPath(asset.Guid);
            if (path != asset.Path) throw new InvalidOperationException("The texture path changed during the operation.");
            return AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException("The texture importer is no longer available.");
        }

    }
}
