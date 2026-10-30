using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal sealed class AudioAssetFixPlan
    {
        private readonly AudioImporterSnapshot _before;
        public AudioImporterSnapshot Before => _before.Copy();
        public string Guid => _before.Guid;
        public string Path => _before.Path;
        public IReadOnlyList<RuleResult> Findings { get; }
        public AudioImporterSampleSettings IntendedSettings { get; }
        public bool ChangesPlatform { get; }
        public bool? ForceToMono { get; }
        public AudioAssetFixPlan(AudioImporterSnapshot before, AudioAssetPreview preview,
            AudioImporterSampleSettings intended, bool platform, bool? mono)
        { _before = before.Copy(); Findings = preview.Findings; IntendedSettings = intended; ChangesPlatform = platform; ForceToMono = mono; }
    }
    internal sealed class AudioFixPlan
    {
        public AnalysisRunResult Baseline { get; }
        public AudioFixPreview Preview { get; }
        public IReadOnlyList<AudioAssetFixPlan> Assets { get; }
        public AudioFixPlan(AnalysisRunResult baseline, AudioFixPreview preview, IEnumerable<AudioAssetFixPlan> assets)
        { Baseline = baseline; Preview = preview; Assets = assets.ToList().AsReadOnly(); }
    }
    internal sealed class AudioFixOperation : IAssetFixOperation
    {
        private readonly AudioAssetFixPlan _asset;
        private readonly AnalysisRunResult _baseline;
        private readonly IAudioFixExecutor _executor;
        private readonly AudioFixPreviewService _review;
        public string Guid => _asset.Guid;
        public string Path => _asset.Path;
        public string Category => "Audio";
        public AudioFixOperation(AudioAssetFixPlan asset, AnalysisRunResult baseline, IAudioFixExecutor executor, AudioFixPreviewService review)
        { _asset = asset; _baseline = baseline; _executor = executor; _review = review; }
        public void Validate()
        {
            _review.Prepare(_baseline, _asset.Findings);
            _executor.Validate(_asset, _baseline.Context.TargetPlatform);
        }
        public void Apply() => _executor.Apply(_asset);
        public void Restore() => _executor.Restore(_asset);
        public FixAssetRecord CreateRecord() => new FixAssetRecord
        {
            Guid = Guid, Path = Path, Category = Category, AudioBefore = _asset.Before,
            RuleIds = _asset.Findings.Select(f => f.RuleId).Distinct().ToList(), Changes = AudioFixChanges.Describe(_asset)
        };
        public void RecordActual(FixAssetRecord record) => AudioFixChanges.RecordActual(record, _asset, _executor.Capture(Guid));
    }
    internal static class AudioFixChanges
    {
        public static List<FixValueChange> Describe(AudioAssetFixPlan asset)
        {
            var before = asset.Before; var target = asset.IntendedSettings;
            var result = new List<FixValueChange>();
            if (asset.ChangesPlatform && !before.HasOverride) Add("Standalone Override", "Disabled", "Enabled");
            if (target.loadType != before.Effective.LoadType) Add("Load Type", before.Effective.LoadType.ToString(), target.loadType.ToString());
            if (target.compressionFormat != before.Effective.CompressionFormat)
            {
                Add("Compression Format", before.Effective.CompressionFormat.ToString(), target.compressionFormat.ToString());
                if (target.compressionFormat == UnityEngine.AudioCompressionFormat.Vorbis)
                    Add("Vorbis Quality", "Not applicable (PCM)", Number(target.quality));
            }
            if (target.sampleRateSetting != before.Effective.SampleRateSetting)
                Add("Sample Rate Setting", before.Effective.SampleRateSetting.ToString(), target.sampleRateSetting.ToString());
            if (asset.ForceToMono.HasValue) Add("Force To Mono", Flag(before.ForceToMono), Flag(asset.ForceToMono.Value));
            return result;
            void Add(string name, string current, string intended) => result.Add(new FixValueChange { Property = name, Before = current, Intended = intended });
        }
        public static void RecordActual(FixAssetRecord record, AudioAssetFixPlan asset, AudioImporterSnapshot actual)
        {
            record.AudioAfter = actual;
            foreach (var change in record.Changes)
                switch (change.Property)
                {
                    case "Standalone Override": change.Actual = Flag(actual.HasOverride); break;
                    case "Load Type": change.Actual = actual.Effective.LoadType.ToString(); break;
                    case "Compression Format": change.Actual = actual.Effective.CompressionFormat.ToString(); break;
                    case "Vorbis Quality": change.Actual = actual.Effective.CompressionFormat == UnityEngine.AudioCompressionFormat.Vorbis ? Number(actual.Effective.Quality) : "Not applicable (" + actual.Effective.CompressionFormat + ")"; break;
                    case "Sample Rate Setting": change.Actual = actual.Effective.SampleRateSetting.ToString(); break;
                    case "Force To Mono": change.Actual = Flag(actual.ForceToMono); break;
                }
            // Faktisko importa rezultātu rāda atsevišķi no sekmīgas iestatījuma saglabāšanas.
            record.AudioChecks.Clear();
            record.AudioChecks.Add("Imported frequency: " + asset.Before.Frequency + " → " + actual.Frequency + " Hz; channels: " + asset.Before.Channels + " → " + actual.Channels + ".");
            if (asset.IntendedSettings.sampleRateSetting == AudioSampleRateSetting.OptimizeSampleRate &&
                asset.Before.Effective.SampleRateSetting != AudioSampleRateSetting.OptimizeSampleRate && actual.Frequency == asset.Before.Frequency)
                record.AudioChecks.Add("Optimize Sample Rate did not change the imported frequency. Review the repeated analysis.");
            record.AudioChecks.Add("Importer settings were checked. Listen to the clip and verify its use in the project; quality and runtime performance were not measured.");
        }
        private static string Flag(bool value) => value ? "Enabled" : "Disabled";
        private static string Number(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);
    }
}
