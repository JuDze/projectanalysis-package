using System;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;

namespace Judze.ProjectAnalysis.Editor.Analysis.Textures.Rules
{
    // Tikai kopīgās mērvienības un robežvērtību darbības; noteikumu nosacījumi paliek to klasēs.
    internal static class TextureRulePolicy
    {
        public const double BytesPerMiB = 1048576d;
        public static double? MiB(long? bytes) => bytes.HasValue && bytes.Value >= 0
            ? bytes.Value / BytesPerMiB : (double?)null;
        public static RuleResource Resource(TextureFacts facts) => new RuleResource(facts.Guid, facts.AssetPath);
        public const string PlatformWarning =
            "Imported data belongs to the active platform, not the selected target. Target size and risk cannot be evaluated.";
    }
}
