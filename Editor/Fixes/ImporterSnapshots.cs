using System.Collections.Generic;
using System.Linq;
using System;
using UnityEditor;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    // Platformas JSON saglabā arī Automatic, izslēgtu override un pārējos platformas laukus.
    [Serializable]
    internal sealed class TextureImporterSnapshot
    {
        public string Guid;
        public string Path;
        public string ImportVersion;
        public string PlatformJson;
        public string DefaultJson;
        public bool IsReadable;
        public bool MipmapsEnabled;
        public bool StreamingMipmaps;

        public TextureImporterPlatformSettings Platform() =>
            JsonUtility.FromJson<TextureImporterPlatformSettings>(PlatformJson);
        public TextureImporterPlatformSettings Defaults() =>
            JsonUtility.FromJson<TextureImporterPlatformSettings>(DefaultJson);
        public TextureImporterPlatformSettings Effective()
        {
            var platform = Platform();
            return platform.overridden ? platform : Defaults();
        }
        // Lauki ir vērtību tipi vai nemaināmas virknes; JSON starpkopija nav vajadzīga.
        public TextureImporterSnapshot Copy() => (TextureImporterSnapshot)MemberwiseClone();
    }

    [Serializable]
    internal sealed class ModelImporterSnapshot
    {
        public string Guid;
        public string Path;
        public string ImportVersion;
        public string SettingsJson;
        public bool IsReadable;
        public bool GenerateMeshLods;
        public List<ModelMeshLodSnapshot> Meshes = new List<ModelMeshLodSnapshot>();
        public ModelImporterSnapshot Copy()
        {
            var copy = (ModelImporterSnapshot)MemberwiseClone();
            // Saraksts un tā elementi nedrīkst būt kopīgi ar apstiprinātā plāna datiem.
            copy.Meshes = Meshes?.Select(mesh => mesh?.Copy()).ToList();
            return copy;
        }
    }
    [Serializable]
    internal sealed class ModelMeshLodSnapshot
    {
        public string Guid;
        public long LocalId;
        public string Name;
        public int LodCount;
        public ModelMeshLodSnapshot Copy() => (ModelMeshLodSnapshot)MemberwiseClone();
    }

    // Atsevišķi saglabā noklusējuma un platformas vērtības, lai varētu atjaunot arī override neesamību.
    [Serializable]
    internal sealed class AudioImporterSnapshot
    {
        public string Guid, Path, ImportVersion, SettingsJson;
        public AudioSampleSettingsSnapshot Default, Effective;
        public bool HasOverride, ForceToMono, Ambisonic, LoadInBackground;
        public int Frequency, Channels, Samples;
        public AudioImporterSnapshot Copy()
        {
            var copy = (AudioImporterSnapshot)MemberwiseClone();
            copy.Default = Default?.Copy();
            copy.Effective = Effective?.Copy();
            return copy;
        }
    }

    [Serializable]
    internal sealed class AudioSampleSettingsSnapshot
    {
        public AudioClipLoadType LoadType;
        public AudioCompressionFormat CompressionFormat;
        public AudioSampleRateSetting SampleRateSetting;
        public uint SampleRateOverride;
        public float Quality;
        public bool PreloadAudioData;
        public AudioSampleSettingsSnapshot Copy() => (AudioSampleSettingsSnapshot)MemberwiseClone();
        public static AudioSampleSettingsSnapshot From(AudioImporterSampleSettings value) => new AudioSampleSettingsSnapshot
        {
            LoadType = value.loadType, CompressionFormat = value.compressionFormat, Quality = value.quality,
            SampleRateSetting = value.sampleRateSetting, SampleRateOverride = value.sampleRateOverride,
            PreloadAudioData = value.preloadAudioData
        };
        public AudioImporterSampleSettings ToSettings() => new AudioImporterSampleSettings
        {
            loadType = LoadType, compressionFormat = CompressionFormat, quality = Quality,
            sampleRateSetting = SampleRateSetting, sampleRateOverride = SampleRateOverride, preloadAudioData = PreloadAudioData
        };
        public bool Matches(AudioImporterSampleSettings value) => LoadType == value.loadType &&
            CompressionFormat == value.compressionFormat && SampleRateSetting == value.sampleRateSetting &&
            SampleRateOverride == value.sampleRateOverride && PreloadAudioData == value.preloadAudioData &&
            Math.Abs(Quality - value.quality) < 0.00001f;
    }
}
