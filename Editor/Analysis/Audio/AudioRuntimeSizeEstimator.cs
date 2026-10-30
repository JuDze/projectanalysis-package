using System;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio
{
    // Project Auditor 1.0.2 modelis vienai atskaņošanas instancei, nevis izmērīta RAM ekonomija.
    internal static class AudioRuntimeSizeEstimator
    {
        public static bool TryEstimate(AudioFacts clip, out long bytes, out string issue)
        {
            bytes = 0; issue = null;
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            if (!string.IsNullOrEmpty(clip.SizeReadIssue)) { issue = clip.SizeReadIssue; return false; }
            if (!clip.ProfilerSizeBytes.HasValue || clip.ProfilerSizeBytes <= 0)
            { issue = "The Profiler audio object size is unavailable."; return false; }
            if (clip.Samples <= 0 || clip.Channels <= 0 || clip.Frequency <= 0)
            { issue = "Valid sample count, channel count and frequency are required."; return false; }
            if (clip.CurrentLoadType != clip.SampleSettings.loadType)
            { issue = "Current clip Load Type does not match the selected import settings."; return false; }
            try
            {
                checked
                {
                    if (clip.CurrentLoadType == AudioClipLoadType.DecompressOnLoad)
                        bytes = clip.ProfilerSizeBytes.Value + (long)clip.Samples * clip.Channels * sizeof(float);
                    else if (clip.CurrentLoadType == AudioClipLoadType.CompressedInMemory)
                    {
                        if (!clip.ImportedSizeBytes.HasValue || clip.ImportedSizeBytes <= 0)
                        { issue = "The imported audio data size is unavailable."; return false; }
                        // floor(0,4 * frequency * channels * 4), izmantojot veselus skaitļus.
                        long buffer = (long)clip.Frequency * clip.Channels * 8 / 5;
                        bytes = clip.ProfilerSizeBytes.Value + clip.ImportedSizeBytes.Value + buffer;
                    }
                    else { issue = "This estimate is only used for non-streaming clips."; return false; }
                }
                return true;
            }
            catch (OverflowException)
            { issue = "The audio runtime size estimate exceeded the supported numeric range."; return false; }
        }
    }
}
