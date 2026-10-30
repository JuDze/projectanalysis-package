using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Assets;
using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Judze.ProjectAnalysis.Editor.Analysis.Textures
{
    // Unity API izsaukumi ir nodalīti no noteikumu izvērtēšanas un labojumu piemērošanas.
    internal sealed class UnityTextureAnalyzer : ITextureAnalyzer
    {
        public TextureCollectionResult Collect(IReadOnlyList<string> assetPaths, TextureAnalysisContext context)
        {
            if (assetPaths == null) throw new ArgumentNullException(nameof(assetPaths));
            if (context == null) throw new ArgumentNullException(nameof(context));
            var targetPlatform = context.TargetPlatform;
            if (!Enum.IsDefined(typeof(BuildTarget), targetPlatform) || targetPlatform == BuildTarget.NoTarget)
                throw new ArgumentException("Select a valid target platform.", nameof(targetPlatform));
            if (BuildPipeline.GetBuildTargetGroup(targetPlatform) != AnalysisBuildTarget.StandaloneGroup)
                throw new NotSupportedException("Texture collection currently supports Standalone import settings.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before collecting texture data.");

            var textures = new List<TextureFacts>();
            var diagnostics = new List<RuleDiagnostic>();
            var visitedPaths = new HashSet<string>(StringComparer.Ordinal);
            var visitedGuids = new HashSet<string>(StringComparer.Ordinal);

            foreach (string requestedPath in assetPaths)
            {
                if (string.IsNullOrWhiteSpace(requestedPath))
                {
                    diagnostics.Add(new RuleDiagnostic("COLLECTION", requestedPath, "Skipped an empty asset path."));
                    continue;
                }
                if (!visitedPaths.Add(requestedPath)) continue;

                Texture2D texture = null;
                bool unloadAfterReading = false;
                try
                {
                    if (AssetDatabase.IsValidFolder(requestedPath)) continue;
                    string guid = AssetDatabase.AssetPathToGUID(requestedPath, AssetPathToGUIDOptions.OnlyExistingAssets);
                    if (string.IsNullOrEmpty(guid))
                    {
                        diagnostics.Add(new RuleDiagnostic("COLLECTION", requestedPath, $"Asset is missing or unregistered: {requestedPath}"));
                        continue;
                    }
                    if (!visitedGuids.Add(guid)) continue;
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                    {
                        if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D))
                            diagnostics.Add(new RuleDiagnostic("COLLECTION", path, $"Texture has no TextureImporter: {path}"));
                        continue;
                    }
                    if (importer.textureShape != TextureImporterShape.Texture2D)
                    {
                        diagnostics.Add(new RuleDiagnostic("COLLECTION", path, $"Only imported Texture2D assets are supported: {path}"));
                        continue;
                    }

                    if (!UnityAssetEditability.CanEditSettings(path)) continue;
                    // Nesaglabātās vērtības nedrīkst sajaukt ar iepriekšējā importa datiem.
                    if (EditorUtility.IsDirty(importer))
                    {
                        diagnostics.Add(new RuleDiagnostic("COLLECTION", path,
                            "Apply or revert pending texture importer changes before analysis."));
                        continue;
                    }

                    unloadAfterReading = !AssetDatabase.IsMainAssetAtPathLoaded(path);
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture == null)
                    {
                        diagnostics.Add(new RuleDiagnostic("COLLECTION", path, $"Could not load Texture2D: {path}"));
                        continue;
                    }

                    var platform = importer.GetPlatformTextureSettings(context.ImportSettingsPlatform);
                    var effective = platform.overridden ? platform : importer.GetDefaultPlatformTextureSettings();
                    var sourceSize = ReadOptional(() =>
                    {
                        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                        if (width <= 0 || height <= 0) throw new InvalidOperationException("Source dimensions are unavailable.");
                        return new Vector2Int(width, height);
                    }, path, "source dimensions", diagnostics);
                    long? cpuBytes = null;
                    if (importer.isReadable)
                    {
                        cpuBytes = ReadOptional(() =>
                        {
                            if (!texture.isReadable) throw new InvalidOperationException("CPU data is not readable.");
                            // NativeArray pieder tekstūrai; saglabā tikai tā garumu un neizsauc Dispose.
                            return (long)texture.GetRawTextureData<byte>().Length;
                        }, path, "CPU data size", diagnostics);
                    }
                    var mipBytes = ReadOptional(() => ReadMipChainSize(texture.width, texture.height,
                        texture.graphicsFormat, texture.mipmapCount), path, "mip chain size", diagnostics);

                    textures.Add(new TextureFacts(path, guid, sourceSize?.x, sourceSize?.y,
                        texture.width, texture.height, importer.textureType, platform.overridden,
                        effective.maxTextureSize, importer.isReadable, cpuBytes, importer.mipmapEnabled,
                        texture.mipmapCount, importer.streamingMipmaps, mipBytes, UnityAssetVersion.Read(path, importer)));
                }
                catch (Exception exception)
                {
                    // Viena resursa kļūda nepārtrauc pārējo tekstūru datu iegūšanu.
                    diagnostics.Add(new RuleDiagnostic("COLLECTION", requestedPath, $"Could not collect texture data for {requestedPath}: {exception.Message}"));
                }
                finally
                {
                    // Atbrīvo tikai paša ielādēto tekstūru; jau atvērtie resursi paliek ielādēti.
                    if (unloadAfterReading && texture != null)
                    {
                        try { Resources.UnloadAsset(texture); }
                        catch (Exception exception)
                        {
                            diagnostics.Add(new RuleDiagnostic("COLLECTION", requestedPath, $"Could not unload texture {requestedPath}: {exception.Message}"));
                        }
                    }
                }
            }

            textures.Sort((left, right) => StringComparer.Ordinal.Compare(left.AssetPath, right.AssetPath));
            return new TextureCollectionResult(context, textures, diagnostics);
        }

        private static T? ReadOptional<T>(Func<T> read, string path, string field, List<RuleDiagnostic> diagnostics)
            where T : struct
        {
            try { return read(); }
            catch (Exception exception)
            {
                diagnostics.Add(new RuleDiagnostic("COLLECTION", path, $"Could not read {field} for {path}: {exception.Message}"));
                return null;
            }
        }

        internal static long ReadMipChainSize(int width, int height, GraphicsFormat format, int mipCount)
        {
            if (width <= 0 || height <= 0 || mipCount <= 0 || format == GraphicsFormat.None)
                throw new ArgumentException("Texture dimensions, format and mip count must be known.");

            // Unity aprēķins atgriež uint. Pirms tā pārbauda, vai apjoms ietilpst šajā diapazonā.
            long blockWidth = GraphicsFormatUtility.GetBlockWidth(format);
            long blockHeight = GraphicsFormatUtility.GetBlockHeight(format);
            long blockBytes = GraphicsFormatUtility.GetBlockSize(format);
            if (blockWidth <= 0 || blockHeight <= 0 || blockBytes <= 0)
                throw new NotSupportedException("Texture format size is unavailable.");
            long bytes = 0;
            int mipWidth = width;
            int mipHeight = height;
            for (int mip = 0; mip < mipCount; mip++)
            {
                bytes = checked(bytes + ((mipWidth + blockWidth - 1) / blockWidth) *
                    ((mipHeight + blockHeight - 1) / blockHeight) * blockBytes);
                mipWidth = Math.Max(1, mipWidth / 2);
                mipHeight = Math.Max(1, mipHeight / 2);
            }
            if (bytes > uint.MaxValue)
                throw new NotSupportedException("Mip chain exceeds the size supported by Unity's uint calculation.");

            uint calculated = GraphicsFormatUtility.ComputeMipChainSize(width, height, format, mipCount);
            if (calculated == 0) throw new InvalidOperationException("Unity returned no mip chain size.");
            return calculated;
        }
    }
}
