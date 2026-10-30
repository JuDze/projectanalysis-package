using Judze.ProjectAnalysis.Editor.Assets;
using System;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Models;
using UnityEditor;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal sealed class UnityModelFixExecutor : IModelFixExecutor
    {
        public ModelImporterSnapshot Capture(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (string.IsNullOrEmpty(path) || importer == null)
                throw new InvalidOperationException("The model importer is no longer available: " + guid);
            var snapshot = new ModelImporterSnapshot
            {
                Guid = guid, Path = path, ImportVersion = UnityAssetVersion.Read(path, importer),
                SettingsJson = EditorJsonUtility.ToJson(importer),
                IsReadable = importer.isReadable, GenerateMeshLods = importer.generateMeshLods
            };
            foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string meshGuid, out long localId))
                    throw new InvalidOperationException("A model mesh has no persistent identity: " + path);
                snapshot.Meshes.Add(new ModelMeshLodSnapshot { Guid = meshGuid, LocalId = localId, Name = mesh.name, LodCount = mesh.lodCount });
            }
            return snapshot;
        }
        public void Validate(ModelAssetFixPlan asset, BuildTarget target)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before applying model changes.");
            if (!AnalysisBuildTarget.IsKnown(target) || UnityBuildTargetReader.Read() != target)
                throw new InvalidOperationException("Build target changed or is Unknown. Run analysis and review changes again.");
            var current = Capture(asset.Guid);
            if (current.Path != asset.Path || current.ImportVersion != asset.Before.ImportVersion)
                throw new InvalidOperationException("Model settings have changed. Run analysis again: " + current.Path);
            var importer = GetImporter(asset);
            if (EditorUtility.IsDirty(importer))
                throw new InvalidOperationException("Apply or revert pending Inspector changes first: " + current.Path);
            UnityAssetEditability.RequireWritable(current.Path);
            if (asset.IsReadable.HasValue && (asset.IsReadable.Value || !current.IsReadable))
                throw new InvalidOperationException("Read/Write no longer matches the reviewed change.");
            if (asset.GenerateMeshLods.HasValue && (!asset.GenerateMeshLods.Value || current.GenerateMeshLods))
                throw new InvalidOperationException("Generate Mesh LODs no longer matches the reviewed change.");
        }
        public void Apply(ModelAssetFixPlan asset)
        {
            var importer = GetImporter(asset);
            if (asset.IsReadable.HasValue) importer.isReadable = asset.IsReadable.Value;
            if (asset.GenerateMeshLods.HasValue) importer.generateMeshLods = asset.GenerateMeshLods.Value;
            // Abas vienas datnes izmaiņas saglabā ar vienu atkārtotu importu.
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Verify(asset, false);
        }
        public void Restore(ModelAssetFixPlan asset)
        {
            var importer = GetImporter(asset);
            if (asset.IsReadable.HasValue) importer.isReadable = asset.Before.IsReadable;
            if (asset.GenerateMeshLods.HasValue) importer.generateMeshLods = asset.Before.GenerateMeshLods;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Verify(asset, true);
        }
        private void Verify(ModelAssetFixPlan asset, bool restoring)
        {
            var actual = Capture(asset.Guid);
            var before = asset.Before;
            bool readable = restoring ? before.IsReadable : asset.IsReadable ?? before.IsReadable;
            bool lods = restoring ? before.GenerateMeshLods : asset.GenerateMeshLods ?? before.GenerateMeshLods;
            if (actual.IsReadable != readable || actual.GenerateMeshLods != lods)
                throw new InvalidOperationException("Imported model settings differ from the expected values: " + asset.Path);
        }
        private static ModelImporter GetImporter(ModelAssetFixPlan asset)
        {
            string path = AssetDatabase.GUIDToAssetPath(asset.Guid);
            if (path != asset.Path) throw new InvalidOperationException("The model path changed during the operation.");
            return AssetImporter.GetAtPath(path) as ModelImporter
                ?? throw new InvalidOperationException("The model importer is no longer available.");
        }
    }
}
