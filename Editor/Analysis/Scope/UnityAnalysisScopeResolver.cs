using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Judze.ProjectAnalysis.Editor.Analysis.Scope
{
    //nodrošina meklēšanu, resursu filtrēšanu, atkārtojumu pārbaudi
    //https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetDatabase.FindAssets.html
    //https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetDatabase.IsValidFolder.html
    internal sealed class UnityAnalysisScopeResolver : IAnalysisScopeResolver
    {
        private const string AssetFilter = "t:Texture2D t:AudioClip t:GameObject t:Mesh";
        public ResolvedAnalysisScope Resolve(AnalysisScopeSelection selection)
        {
            if(selection == null) throw new ArgumentNullException(nameof(selection));
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before checking the scope");
            }

            var assetPaths = new List<string>();
            var messages = new List<string>();
            var assetGuids = new HashSet<string>(StringComparer.Ordinal);

            switch(selection.Mode)
            {
                case AnalysisScopeMode.AllProjectAssets:
                    var folders = new List<string> {"Assets"};
                    foreach (var package in PackageInfo.GetAllRegisteredPackages())
                    {
                        if(AssetDatabase.IsValidFolder(package.assetPath))
                        {
                            folders.Add(package.assetPath);
                        }

                    }
                    AddFolders(folders.ToArray());
                    break;

                case AnalysisScopeMode.SelectedFolder:
                    if(!IsResourcePath(selection.Path) || !AssetDatabase.IsValidFolder(selection.Path))
                    {
                        throw new ArgumentException("Select folder in Assets or package");
                    }

                    AddFolders(new[] {selection.Path});
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(selection.Mode));
            }

            assetPaths.Sort(StringComparer.Ordinal);
            return new ResolvedAnalysisScope(assetPaths, messages);

            void AddFolders(string[] folders)
            {
                foreach(string guid in AssetDatabase.FindAssets(AssetFilter, folders))
                {
                    AddAsset(AssetDatabase.GUIDToAssetPath(guid));
                }
            }

            void AddAsset(string path)
            {
                if(!IsResourcePath(path) || AssetDatabase.IsValidFolder(path))
                {
                    return;
                }
                Type type = AssetDatabase.GetMainAssetTypeAtPath(path);

                if (type != typeof(Texture2D) &&
                    type != typeof(AudioClip) &&
                    type != typeof(GameObject) &&
                    type != typeof(Mesh))
                {
                    return;
                }

                string guid = AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets);
                if(string.IsNullOrEmpty(guid) || !assetGuids.Add(guid))
                {
                    return;
                }
                //vienu datni peivieno tikai vienu reizi
                assetPaths.Add(path);

            }

        }
        private static bool IsResourcePath(string path)
        {
            return !string.IsNullOrEmpty(path) && (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal) ||
            path.StartsWith("Packages/", StringComparison.Ordinal));
        }
    }
}
