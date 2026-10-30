using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using System;
using Judze.ProjectAnalysis.Editor.Assets;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal sealed class UnityAudioRevertFactory : IAssetRevertFactory
    {
        private readonly IAudioFixExecutor _reader;
        public string Category => "Audio";
        public UnityAudioRevertFactory(IAudioFixExecutor reader) { _reader = reader; }
        public IAssetFixOperation Create(IReadOnlyList<FixRevertEntry> entries)
        {
            var original = entries[0].Asset.AudioBefore?.Copy() ?? throw new InvalidOperationException("Original audio settings are missing.");
            var expected = original;
            foreach (var entry in entries)
            {
                UnityRevertGuard.Unchanged(Equal(expected, entry.Asset.AudioBefore), original.Path);
                expected = entry.Asset.AudioAfter ?? throw new InvalidOperationException("Actual audio settings are missing.");
            }
            var current = _reader.Capture(original.Guid);
            UnityRevertGuard.Unchanged(Equal(expected, current), original.Path);
            var record = FixRevertRecord.Create(entries); record.AudioBefore = current;
            void Validate()
            {
                UnityRevertGuard.Importer<AudioImporter>(original.Guid, original.Path);
                UnityRevertGuard.Unchanged(Equal(current, _reader.Capture(original.Guid)), original.Path);
            }
            bool platformChanged = original.HasOverride != current.HasOverride || !original.Effective.Matches(current.Effective.ToSettings());
            void Write(AudioImporterSnapshot target)
            {
                var importer = UnityRevertGuard.Importer<AudioImporter>(original.Guid, original.Path, allowPending: true);
                if (platformChanged)
                {
                    if (target.HasOverride)
                    {
                        if (!importer.SetOverrideSampleSettings(AnalysisBuildTarget.StandaloneGroup, target.Effective.ToSettings()))
                            throw new InvalidOperationException("Unity rejected the restored audio settings.");
                    }
                    else if (importer.ContainsSampleSettingsOverride(AnalysisBuildTarget.StandaloneGroup) &&
                        !importer.ClearSampleSettingOverride(AnalysisBuildTarget.StandaloneGroup))
                        throw new InvalidOperationException("The audio override could not be removed.");
                }
                if (original.ForceToMono != current.ForceToMono) importer.forceToMono = target.ForceToMono;
                EditorUtility.SetDirty(importer); importer.SaveAndReimport();
                if (!Equal(target, _reader.Capture(original.Guid))) throw new InvalidOperationException("Audio restoration could not be verified: " + original.Path);
            }
            return new RevertAssetOperation(record, Validate, () => Write(original), () => Write(current), r =>
            {
                r.AudioAfter = _reader.Capture(original.Guid);
                FixRevertRecord.Actual(r, property => Value(r.AudioAfter, property));
            });
        }
        private static bool Equal(AudioImporterSnapshot a, AudioImporterSnapshot b) => a != null && b != null &&
            a.Guid == b.Guid && a.Path == b.Path && a.HasOverride == b.HasOverride && a.ForceToMono == b.ForceToMono &&
            a.Ambisonic == b.Ambisonic && a.LoadInBackground == b.LoadInBackground &&
            a.Default.Matches(b.Default.ToSettings()) && a.Effective.Matches(b.Effective.ToSettings());
        private static string Value(AudioImporterSnapshot s, string property)
        {
            switch (property)
            {
                case "Standalone Override": return UnityRevertGuard.Flag(s.HasOverride);
                case "Force To Mono": return UnityRevertGuard.Flag(s.ForceToMono);
                case "Load Type": return s.Effective.LoadType.ToString();
                case "Compression Format": return s.Effective.CompressionFormat.ToString();
                case "Sample Rate Setting": return s.Effective.SampleRateSetting.ToString();
                case "Vorbis Quality": return s.Effective.CompressionFormat == AudioCompressionFormat.Vorbis ?
                    s.Effective.Quality.ToString("0.#####", CultureInfo.InvariantCulture) : "Not applicable (" + s.Effective.CompressionFormat + ")";
                default: return null;
            }
        }
    }
}
