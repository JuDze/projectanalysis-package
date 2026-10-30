using System;
using System.Collections.Generic;

namespace Judze.ProjectAnalysis.Editor.Analysis.Models
{
    // Viena ģeometrijas tīkla nemainīgi dati, nevis Unity objekta atsauce.
    internal sealed class MeshFacts
    {
        public string Name { get; }
        public string Guid { get; }
        public long LocalId { get; }
        public int VertexCount { get; }
        public long? TriangleCount { get; }
        public bool TriangleTopologyOnly { get; }
        public int LodCount { get; }
        public IReadOnlyList<string> LodGroupPaths { get; }
        public bool HasKnownLodGroup => LodGroupPaths.Count > 0;

        public MeshFacts(string name, string guid, long localId, int vertexCount,
            long? triangleCount, bool triangleTopologyOnly, int lodCount,
            IEnumerable<string> lodGroupPaths = null)
        {
            Name = name; Guid = guid; LocalId = localId; VertexCount = vertexCount;
            TriangleCount = triangleCount; TriangleTopologyOnly = triangleTopologyOnly;
            LodCount = lodCount;
            LodGroupPaths = new List<string>(lodGroupPaths ?? Array.Empty<string>()).AsReadOnly();
        }
    }
}
