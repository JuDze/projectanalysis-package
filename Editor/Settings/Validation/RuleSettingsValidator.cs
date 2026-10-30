using System;
using System.Collections.Generic;

namespace Judze.ProjectAnalysis.Editor.Settings
{
    // Pārbauda iestatījumu vērtības, nemainot tās un neizmantojot Unity resursu API.
    internal sealed class RuleSettingsValidator
    {
        public IReadOnlyList<SettingsValidationIssue> Validate(RuleSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var issues = new List<SettingsValidationIssue>();
            ValidateTex001(settings.Tex001, issues);
            ValidateTex002(settings.Tex002, issues);
            ValidateTex004(settings.Tex004, issues);
            ValidateMod001(settings.Mod001, issues);
            ValidateMod002(settings.Mod002, settings.Mod004.Enabled, issues);
            ValidateAud002(settings.Aud002, issues);
            ValidateAud003(settings.Aud003, issues);
            // AUD001 RAM mērķis izmanto kopīgās robežas arī tad, ja AUD005 ir izslēgts.
            if (settings.Aud005.Enabled || settings.Aud001.Enabled && settings.Aud001.Goal == AudioOptimizationGoal.ReduceRamUsage)
                ValidatePair("AUD005", nameof(Aud005Settings.RuntimeSizeThresholdBytes), settings.Aud005.RuntimeSizeThresholdBytes,
                    nameof(Aud005Settings.RuntimeSizeHighThresholdBytes), settings.Aud005.RuntimeSizeHighThresholdBytes, issues);
            return issues;
        }

        private static void ValidateTex001(Tex001Settings settings, List<SettingsValidationIssue> issues)
        {
            if (!settings.Enabled) return;
            ValidatePair("TEX001", nameof(Tex001Settings.ResolutionThreshold), settings.ResolutionThreshold,
                nameof(Tex001Settings.ResolutionHighThreshold), settings.ResolutionHighThreshold, issues);
        }

        private static void ValidateMod001(Mod001Settings settings, List<SettingsValidationIssue> issues)
        {
            if (!settings.Enabled) return;
            ValidatePair("MOD001", nameof(Mod001Settings.VertexThreshold), settings.VertexThreshold,
                nameof(Mod001Settings.VertexHighThreshold), settings.VertexHighThreshold, issues);
        }

        private static void ValidateMod002(Mod002Settings settings, bool usedForLod, List<SettingsValidationIssue> issues)
        {
            // Kopīgā robežvērtība ir obligāta arī tad, ja darbojas tikai MOD004.
            if (!settings.Enabled && !usedForLod) return;
            ValidatePair("MOD002", nameof(Mod002Settings.TriangleThreshold), settings.TriangleThreshold,
                nameof(Mod002Settings.TriangleHighThreshold), settings.TriangleHighThreshold, issues);
        }

        private static void ValidateTex002(Tex002Settings settings, List<SettingsValidationIssue> issues)
        {
            if (!settings.Enabled) return;
            ValidatePair("TEX002", nameof(Tex002Settings.CpuDataThresholdMiB), settings.CpuDataThresholdMiB,
                nameof(Tex002Settings.CpuDataHighThresholdMiB), settings.CpuDataHighThresholdMiB, issues);
        }

        private static void ValidateTex004(Tex004Settings settings, List<SettingsValidationIssue> issues)
        {
            if (!settings.Enabled) return;
            ValidatePair("TEX004", nameof(Tex004Settings.MipDataThresholdMiB), settings.MipDataThresholdMiB,
                nameof(Tex004Settings.MipDataHighThresholdMiB), settings.MipDataHighThresholdMiB, issues);
        }

        private static void ValidateAud002(Aud002Settings settings, List<SettingsValidationIssue> issues)
        {
            // Saglabāto kvalitāti vērtē tikai tad, ja noteikums izmanto Vorbis.
            if (!settings.Enabled || settings.TargetFormat != AudioCompressionChoice.Vorbis) return;

            float quality = settings.VorbisQuality;
            if (float.IsNaN(quality) || float.IsInfinity(quality) || quality < 0f || quality > 1f)
                issues.Add(new SettingsValidationIssue("AUD002", nameof(Aud002Settings.VorbisQuality),
                    "Vorbis quality must be a finite number between 0 and 1."));
        }

        private static void ValidateAud003(Aud003Settings settings, List<SettingsValidationIssue> issues)
        {
            if (!settings.Enabled) return;

            ValidatePair("AUD003", nameof(Aud003Settings.SampleRateThresholdHz), settings.SampleRateThresholdHz,
                nameof(Aud003Settings.SampleRateHighThresholdHz), settings.SampleRateHighThresholdHz, issues);
        }

        private static void ValidatePair(string ruleId, string mediumName, long medium,
            string highName, long high, List<SettingsValidationIssue> issues)
        {
            if (medium <= 0)
                issues.Add(new SettingsValidationIssue(ruleId, mediumName, "Enter a value greater than zero."));
            if (high <= 0 || medium > 0 && high <= medium)
                issues.Add(new SettingsValidationIssue(ruleId, highName, "High must be positive and greater than Medium."));
        }

        private static void ValidatePair(string ruleId, string mediumName, double medium,
            string highName, double high, List<SettingsValidationIssue> issues)
        {
            bool validMedium = !double.IsNaN(medium) && !double.IsInfinity(medium) && medium > 0;
            if (!validMedium)
                issues.Add(new SettingsValidationIssue(ruleId, mediumName, "Enter a finite value greater than zero."));
            if (double.IsNaN(high) || double.IsInfinity(high) || high <= 0 || validMedium && high <= medium)
                issues.Add(new SettingsValidationIssue(ruleId, highName, "High must be finite, positive and greater than Medium."));
        }
    }

    // Viena iestatījumu pārbaudes kļūda bez atsauces uz saskarnes elementiem.
    internal sealed class SettingsValidationIssue
    {
        public string RuleId { get; }
        public string PropertyName { get; }
        public string Message { get; }

        public SettingsValidationIssue(string ruleId, string propertyName, string message)
        {
            RuleId = ruleId;
            PropertyName = propertyName;
            Message = message;
        }
    }
}
