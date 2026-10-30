using System;
using System.Collections.Generic;
using System.IO;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;

namespace Judze.ProjectAnalysis.Editor.Analysis.Models.Rules
{
    internal static class ModelRulePolicy
    {
        public static RuleResource Resource(ModelFacts model, MeshFacts mesh = null) => mesh == null
            ? new RuleResource(model.Guid, model.AssetPath)
            : new RuleResource(mesh.Guid, model.AssetPath, mesh.LocalId, Path.GetFileName(model.AssetPath) + " / " + mesh.Name);

        public static List<string> Warnings(ModelAnalysisContext context)
        {
            var warnings = new List<string>();
            if (context.TargetPlatform != context.ActivePlatform)
                warnings.Add($"Geometry was read from the current {context.ActivePlatform} import.");
            return warnings;
        }
    }
}
