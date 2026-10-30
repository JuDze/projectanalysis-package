using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Analysis.Textures
{
    // Tikai pašreizējiem TEX noteikumiem un priekšskatījumam nepieciešamie nolasītie dati.
    internal sealed class TextureFacts
    {
        public string AssetPath { get; }
        public string Guid { get; }
        public string ImportVersion { get; }
        public int? SourceWidth { get; }
        public int? SourceHeight { get; }
        public int ImportedWidth { get; }
        public int ImportedHeight { get; }
        public TextureImporterType TextureType { get; }
        public bool PlatformOverridden { get; }
        public int EffectiveMaxTextureSize { get; }
        public bool ReadWriteEnabled { get; }
        // Null nozīmē nezināmu apjomu, nevis izmērītu nulli.
        public long? CpuDataBytes { get; }
        public bool MipmapsEnabled { get; }
        public int MipmapCount { get; }
        public bool StreamingMipmaps { get; }
        // Visu esošo mip līmeņu datu apjoms; tā nav izmērīta VRAM slodze.
        public long? ImportedMipChainBytes { get; }

        public TextureFacts(string assetPath, string guid, int? sourceWidth, int? sourceHeight,
            int importedWidth, int importedHeight, TextureImporterType textureType,
            bool platformOverridden, int effectiveMaxTextureSize, bool readWriteEnabled,
            long? cpuDataBytes, bool mipmapsEnabled, int mipmapCount, bool streamingMipmaps,
            long? importedMipChainBytes, string importVersion = null)
        {
            AssetPath = assetPath;
            Guid = guid;
            ImportVersion = importVersion;
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            ImportedWidth = importedWidth;
            ImportedHeight = importedHeight;
            TextureType = textureType;
            PlatformOverridden = platformOverridden;
            EffectiveMaxTextureSize = effectiveMaxTextureSize;
            ReadWriteEnabled = readWriteEnabled;
            CpuDataBytes = cpuDataBytes;
            MipmapsEnabled = mipmapsEnabled;
            MipmapCount = mipmapCount;
            StreamingMipmaps = streamingMipmaps;
            ImportedMipChainBytes = importedMipChainBytes;
        }
    }
}
