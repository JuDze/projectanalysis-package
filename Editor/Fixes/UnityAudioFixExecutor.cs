using Judze.ProjectAnalysis.Editor.Assets;
using System;
using Judze.ProjectAnalysis.Editor.Analysis.Audio;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using UnityEditor;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal sealed class UnityAudioFixExecutor : IAudioFixExecutor
    {
        public AudioImporterSnapshot Capture(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (string.IsNullOrEmpty(path) || importer == null || clip == null)
                throw new InvalidOperationException("The audio importer or clip is no longer available: " + guid);
            return new AudioImporterSnapshot
            {
                Guid = guid, Path = path, ImportVersion = UnityAssetVersion.Read(path, importer), SettingsJson = EditorJsonUtility.ToJson(importer),
                Default = AudioSampleSettingsSnapshot.From(importer.defaultSampleSettings),
                Effective = AudioSampleSettingsSnapshot.From(importer.GetOverrideSampleSettings(AnalysisBuildTarget.StandaloneGroup)),
                HasOverride = importer.ContainsSampleSettingsOverride(AnalysisBuildTarget.StandaloneGroup),
                ForceToMono = importer.forceToMono, Ambisonic = importer.ambisonic, LoadInBackground = importer.loadInBackground,
                Frequency = clip.frequency, Channels = clip.channels, Samples = clip.samples
            };
        }
        public void Validate(AudioAssetFixPlan asset, BuildTarget target)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before applying audio changes.");
            var context = new AudioAnalysisContext(target, UnityBuildTargetReader.Read());
            if (!context.SupportsStandalone || !context.HasMatchingImport)
                throw new InvalidOperationException("Build target changed, is unsupported or Unknown. Run analysis again.");
            var current = Capture(asset.Guid);
            if (current.Path != asset.Path || current.ImportVersion != asset.Before.ImportVersion)
                throw new InvalidOperationException("Audio settings have changed. Run analysis again: " + current.Path);
            if (EditorUtility.IsDirty(GetImporter(asset)))
                throw new InvalidOperationException("Apply or revert pending Inspector changes first: " + current.Path);
            UnityAssetEditability.RequireWritable(current.Path);
        }
        public void Apply(AudioAssetFixPlan asset)
        {
            var importer = GetImporter(asset);
            if (asset.ChangesPlatform && !importer.SetOverrideSampleSettings(AnalysisBuildTarget.StandaloneGroup, asset.IntendedSettings))
                throw new InvalidOperationException("Unity rejected the Standalone audio settings: " + asset.Path);
            if (asset.ForceToMono.HasValue) importer.forceToMono = asset.ForceToMono.Value;
            // Visas savietojamās vienas datnes izmaiņas piemēro ar vienu importu.
            EditorUtility.SetDirty(importer); importer.SaveAndReimport();
            Verify(asset, false);
        }
        public void Restore(AudioAssetFixPlan asset)
        {
            var importer = GetImporter(asset); var before = asset.Before;
            if (asset.ChangesPlatform)
            {
                if (before.HasOverride)
                {
                    if (!importer.SetOverrideSampleSettings(AnalysisBuildTarget.StandaloneGroup, before.Effective.ToSettings()))
                        throw new InvalidOperationException("Unity rejected restoration of the original audio override.");
                }
                else if (importer.ContainsSampleSettingsOverride(AnalysisBuildTarget.StandaloneGroup) &&
                    !importer.ClearSampleSettingOverride(AnalysisBuildTarget.StandaloneGroup))
                    throw new InvalidOperationException("The newly created audio override could not be removed.");
            }
            if (asset.ForceToMono.HasValue) importer.forceToMono = before.ForceToMono;
            EditorUtility.SetDirty(importer); importer.SaveAndReimport();
            Verify(asset, true);
        }
        private void Verify(AudioAssetFixPlan asset, bool restoring)
        {
            var actual = Capture(asset.Guid); var before = asset.Before;
            var expected = restoring ? before.Effective.ToSettings() : asset.IntendedSettings;
            bool overridden = restoring ? before.HasOverride : asset.ChangesPlatform || before.HasOverride;
            bool mono = restoring ? before.ForceToMono : asset.ForceToMono ?? before.ForceToMono;
            if (actual.HasOverride != overridden || !actual.Effective.Matches(expected) || actual.ForceToMono != mono ||
                !before.Default.Matches(actual.Default.ToSettings()) || actual.Ambisonic != before.Ambisonic || actual.LoadInBackground != before.LoadInBackground)
                throw new InvalidOperationException("Imported audio settings differ from the expected values: " + asset.Path);
            if (!restoring && asset.ForceToMono == true && actual.Channels != 1)
                throw new InvalidOperationException("Force To Mono was saved, but the imported clip does not have one channel: " + asset.Path);
        }
        private static AudioImporter GetImporter(AudioAssetFixPlan asset)
        {
            string path = AssetDatabase.GUIDToAssetPath(asset.Guid);
            if (path != asset.Path) throw new InvalidOperationException("The audio path changed during the operation.");
            return AssetImporter.GetAtPath(path) as AudioImporter ?? throw new InvalidOperationException("The audio importer is no longer available.");
        }
    }
}
