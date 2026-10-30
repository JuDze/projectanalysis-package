using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;

namespace Judze.ProjectAnalysis.Editor.Assets
{
    // Pakotņu kešatmiņu un tikai lasāmus importa iestatījumus nemaina.
    internal static class UnityAssetEditability
    {
        // Savākšanas laikā neiekļauj resursus, kuru importa iestatījumus nevar rediģēt.
        // Pirms labojuma šo pašu nosacījumu pārbauda atkārtoti ar RequireWritable.
        public static bool CanEditSettings(string path)
        {
            try { RequireWritable(path); return true; }
            catch (InvalidOperationException) { return false; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public static void RequireWritable(string path)
        {
            if (!AssetDatabase.IsOpenForEdit(path + ".meta", out string reason, StatusQueryOptions.ForceUpdate))
                throw new InvalidOperationException("Resource settings are not editable: " + path + ". " + reason);
            string physicalPath = Path.GetFullPath(path);
            if (path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                if (package == null || (package.source != PackageSource.Local && package.source != PackageSource.Embedded))
                    throw new InvalidOperationException("This package is read-only: " + path);
                physicalPath = Path.Combine(package.resolvedPath, path.Substring(package.assetPath.Length + 1));
            }
            if (!File.Exists(physicalPath) || !File.Exists(physicalPath + ".meta") ||
                (File.GetAttributes(physicalPath + ".meta") & FileAttributes.ReadOnly) != 0)
                throw new IOException("Resource settings are missing or read-only: " + path);
        }
    }

    // Atcelšanai papildus pārbauda importētāju un ārējas iestatījumu izmaiņas.
    internal static class UnityRevertGuard
    {
        public static T Importer<T>(string guid, string path, bool allowPending = false) where T : AssetImporter
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before reverting fixes.");
            if (AssetDatabase.GUIDToAssetPath(guid) != path) throw new InvalidOperationException("The resource was moved or removed: " + path);
            var importer = AssetImporter.GetAtPath(path) as T;
            if (importer == null) throw new InvalidOperationException("The importer is no longer available: " + path);
            if (!allowPending && EditorUtility.IsDirty(importer)) throw new InvalidOperationException("Apply or revert pending Inspector edits first: " + path);
            UnityAssetEditability.RequireWritable(path);
            return importer;
        }
        public static void Unchanged(bool equal, string path)
        {
            if (!equal) throw new InvalidOperationException("Import settings changed outside these fixes. Revert was stopped to preserve those edits: " + path);
        }
        public static string Flag(bool value) => value ? "Enabled" : "Disabled";
    }
}
