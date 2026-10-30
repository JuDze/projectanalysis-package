using System;
using System.Collections.Generic;

namespace Judze.ProjectAnalysis.Editor.Analysis.Models
{
    // Importētāja parametri attiecas uz visu datni, bet ģeometrijas dati — uz katru tīklu.
    internal sealed class ModelFacts
    {
        public string AssetPath { get; }
        public string Guid { get; }
        public bool HasModelImporter { get; }
        public bool? ReadWriteEnabled { get; }
        public bool? GenerateMeshLods { get; }
        public int? MaximumMeshLod { get; }
        public bool? DiscardOddLodLevels { get; }
        public IReadOnlyList<MeshFacts> Meshes { get; }
        public string ImportVersion { get; }
        public bool HasPendingImporterChanges { get; }

        public ModelFacts(string assetPath, string guid, bool hasModelImporter,
            bool? readWriteEnabled, bool? generateMeshLods, IEnumerable<MeshFacts> meshes, string importVersion = null, bool hasPendingImporterChanges = false,
            int? maximumMeshLod = null, bool? discardOddLodLevels = null)
        {
            AssetPath = assetPath; Guid = guid; HasModelImporter = hasModelImporter;
            ReadWriteEnabled = readWriteEnabled; GenerateMeshLods = generateMeshLods;
            MaximumMeshLod = maximumMeshLod; DiscardOddLodLevels = discardOddLodLevels;
            ImportVersion = importVersion; HasPendingImporterChanges = hasPendingImporterChanges;
            Meshes = new List<MeshFacts>(meshes ?? Array.Empty<MeshFacts>()).AsReadOnly();
        }
    }
}
