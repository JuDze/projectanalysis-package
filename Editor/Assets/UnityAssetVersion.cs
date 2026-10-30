using UnityEditor;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Assets
{
    // Salīdzina importa ievadi un vēl nesaglabātos importētāja parametrus.
    internal static class UnityAssetVersion
    {
        public static string Read(string path, AssetImporter importer) =>
            AssetDatabase.GetAssetDependencyHash(path) + ":" +
            Hash128.Compute(importer == null ? string.Empty : EditorJsonUtility.ToJson(importer));
    }
}
