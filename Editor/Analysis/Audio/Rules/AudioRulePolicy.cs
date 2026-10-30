using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules
{
    internal static class AudioRulePolicy
    {
        public static RuleResource Resource(AudioFacts clip) =>
            new RuleResource(clip.Guid, clip.AssetPath, clip.LocalId);

        public static List<string> Warnings(AudioFacts clip, AudioAnalysisContext context)
        {
            var warnings = new List<string>
            {
                clip.HasOverride ? "Uses the Standalone platform override." : "Uses Default settings translated by Unity for Standalone."
            };
            if (!context.HasMatchingImport)
                warnings.Add($"Clip metadata comes from the current {context.ActivePlatform} import, not a separate measurement of the selected {context.TargetPlatform} build.");
            return warnings;
        }

        public static bool RequireMatchingImport(string ruleId, AudioFacts clip, AudioAnalysisContext context,
            List<RuleDiagnostic> diagnostics)
        {
            if (context.HasMatchingImport) return true;
            diagnostics.Add(new RuleDiagnostic(ruleId, clip.AssetPath,
                $"Numeric check was not evaluated for {context.TargetPlatform}: clip data belongs to the current {context.ActivePlatform} import."));
            return false;
        }

        public static string LoadTypeName(AudioClipLoadType value)
        {
            switch (value)
            {
                case AudioClipLoadType.DecompressOnLoad: return "Decompress On Load";
                case AudioClipLoadType.CompressedInMemory: return "Compressed In Memory";
                case AudioClipLoadType.Streaming: return "Streaming";
                default: return value.ToString();
            }
        }
    }
}
