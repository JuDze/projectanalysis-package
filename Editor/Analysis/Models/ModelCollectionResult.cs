using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Analysis.Models
{
    internal sealed class ModelAnalysisContext
    {
        public BuildTarget TargetPlatform { get; }
        public BuildTarget ActivePlatform { get; }
        public bool LodContextComplete { get; }
        public string LodContextVersion { get; }
        public ModelAnalysisContext(BuildTarget targetPlatform, BuildTarget activePlatform, bool lodContextComplete = true, string lodContextVersion = null)
        { TargetPlatform = targetPlatform; ActivePlatform = activePlatform; LodContextComplete = lodContextComplete; LodContextVersion = lodContextVersion; }
    }

    internal sealed class ModelCollectionResult
    {
        public IReadOnlyList<ModelFacts> Models { get; }
        public IReadOnlyList<RuleDiagnostic> Diagnostics { get; }
        public ModelAnalysisContext Context { get; }
        public int MeshCount => Models.Sum(model => model.Meshes.Count);

        public ModelCollectionResult(IEnumerable<ModelFacts> models, ModelAnalysisContext context,
            IEnumerable<RuleDiagnostic> diagnostics = null)
        {
            Models = new List<ModelFacts>(models ?? Array.Empty<ModelFacts>()).AsReadOnly();
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Diagnostics = new List<RuleDiagnostic>(diagnostics ?? Array.Empty<RuleDiagnostic>()).AsReadOnly();
        }
    }
}
