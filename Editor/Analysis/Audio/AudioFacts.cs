using UnityEditor;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio
{
    // Nemainīgs datu momentuzņēmums; noteikumi nesaņem dzīvu importētāja vai AudioClip atsauci.
    internal sealed class AudioFacts
    {
        public string Guid { get; }
        public long LocalId { get; }
        public string AssetPath { get; }
        public string Name { get; }
        public AudioImporterSampleSettings SampleSettings { get; }
        public bool HasOverride { get; }
        public int Frequency { get; }
        public int Channels { get; }
        public int Samples { get; }
        public double LengthSeconds { get; }
        public AudioClipLoadType CurrentLoadType { get; }
        public bool ForceToMono { get; }
        public bool Ambisonic { get; }
        public long? ProfilerSizeBytes { get; }
        public long? ImportedSizeBytes { get; }
        public string SizeReadIssue { get; }
        public string ImportVersion { get; }
        public bool HasPendingImporterChanges { get; }

        public AudioFacts(string guid, long localId, string assetPath, string name,
            AudioImporterSampleSettings sampleSettings, bool hasOverride, int frequency, int channels,
            int samples, double lengthSeconds, AudioClipLoadType currentLoadType,
            bool forceToMono, bool ambisonic, long? profilerSizeBytes = null,
            long? importedSizeBytes = null, string sizeReadIssue = null,
            string importVersion = null, bool hasPendingImporterChanges = false)
        {
            Guid = guid; LocalId = localId; AssetPath = assetPath; Name = name;
            SampleSettings = sampleSettings; HasOverride = hasOverride;
            Frequency = frequency; Channels = channels; Samples = samples; LengthSeconds = lengthSeconds;
            CurrentLoadType = currentLoadType; ForceToMono = forceToMono; Ambisonic = ambisonic;
            ProfilerSizeBytes = profilerSizeBytes; ImportedSizeBytes = importedSizeBytes;
            SizeReadIssue = sizeReadIssue;
            ImportVersion = importVersion; HasPendingImporterChanges = hasPendingImporterChanges;
        }
    }
}
