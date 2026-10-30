using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio
{
    internal sealed class UnityAudioAnalyzer : IAudioAnalyzer
    {
        private readonly IAudioImportSizeReader _sizeReader;
        public UnityAudioAnalyzer(IAudioImportSizeReader sizeReader)
        { _sizeReader = sizeReader ?? throw new ArgumentNullException(nameof(sizeReader)); }

        public AudioCollectionResult Collect(IReadOnlyList<string> paths, BuildTarget targetPlatform, bool collectRuntimeSize)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before collecting audio data.");
            var context = new AudioAnalysisContext(targetPlatform, EditorUserBuildSettings.activeBuildTarget);
            var clips = new List<AudioFacts>();
            var diagnostics = new List<RuleDiagnostic>();
            if (!context.SupportsStandalone)
            {
                diagnostics.Add(new RuleDiagnostic("AUDIO_COLLECTION", null, "Audio analysis requires Standalone settings."));
                return new AudioCollectionResult(clips, context, diagnostics);
            }
            var identities = new HashSet<(string, long)>();
            foreach (string path in paths.Where(IsResourcePath).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal))
            {
                AudioClip clip = null;
                bool unloadAfterReading = false;
                try
                {
                    if (AssetDatabase.IsValidFolder(path)) continue;
                    Type mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
                    if (mainType == null)
                    { diagnostics.Add(new RuleDiagnostic("AUDIO_COLLECTION", path, "The selected asset is unavailable.")); continue; }
                    if (mainType != typeof(AudioClip)) continue;
                    if (!UnityAssetEditability.CanEditSettings(path)) continue;
                    unloadAfterReading = !AssetDatabase.IsMainAssetAtPathLoaded(path);
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (clip == null || importer == null)
                    { diagnostics.Add(new RuleDiagnostic("AUDIO_COLLECTION", path, "An imported AudioClip with an AudioImporter is required.")); continue; }
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long localId) || string.IsNullOrEmpty(guid))
                    { diagnostics.Add(new RuleDiagnostic("AUDIO_COLLECTION", path, "The audio resource has no persistent identifier.")); continue; }
                    if (!identities.Add((guid, localId))) continue;

                    // Bez override Unity atgriež platformai pielāgotus noklusējuma iestatījumus.
                    var settings = importer.GetOverrideSampleSettings(AnalysisBuildTarget.StandaloneGroup);
                    bool hasOverride = importer.ContainsSampleSettingsOverride(AnalysisBuildTarget.StandaloneGroup);
                    long? profilerBytes = null, importedBytes = null;
                    string sizeIssue = null;
                    // Apjoma dati vajadzīgi AUD005 un AUD001 RAM mērķim, ja pašreizējais imports atbilst mērķplatformai.
                    if (collectRuntimeSize && context.HasMatchingImport && settings.loadType != AudioClipLoadType.Streaming &&
                        settings.loadType == clip.loadType)
                    {
                        try
                        {
                            long measured = Profiler.GetRuntimeMemorySizeLong(clip);
                            if (measured > 0) profilerBytes = measured;
                            else sizeIssue = "Profiler did not return an available audio object size.";
                            if (settings.loadType == AudioClipLoadType.CompressedInMemory)
                            {
                                if (_sizeReader.TryRead(importer, out long size, out string issue)) importedBytes = size;
                                else sizeIssue = string.Join(" ", new[] { sizeIssue, issue }.Where(s => !string.IsNullOrEmpty(s)));
                            }
                        }
                        catch (Exception exception) { sizeIssue = "Could not read runtime size inputs: " + exception.GetBaseException().Message; }
                    }
                    clips.Add(new AudioFacts(guid, localId, path, clip.name, settings, hasOverride,
                        clip.frequency, clip.channels, clip.samples, clip.length, clip.loadType,
                        importer.forceToMono, importer.ambisonic, profilerBytes, importedBytes, sizeIssue,
                        UnityAssetVersion.Read(path, importer), EditorUtility.IsDirty(importer)));
                }
                catch (Exception exception)
                { diagnostics.Add(new RuleDiagnostic("AUDIO_COLLECTION", path, "Could not collect audio data: " + exception.GetBaseException().Message)); }
                finally
                {
                    // Jau ielādētos vai atvērtos klipus neaiztiek; faktos paliek tikai nolasītās vērtības.
                    if (unloadAfterReading && clip != null)
                    {
                        try { Resources.UnloadAsset(clip); }
                        catch (Exception exception)
                        { diagnostics.Add(new RuleDiagnostic("AUDIO_COLLECTION", path, "Could not unload audio clip: " + exception.Message)); }
                    }
                }
            }
            return new AudioCollectionResult(clips, context, diagnostics);
        }

        private static bool IsResourcePath(string path) => !string.IsNullOrEmpty(path) &&
            (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal));
    }
}
