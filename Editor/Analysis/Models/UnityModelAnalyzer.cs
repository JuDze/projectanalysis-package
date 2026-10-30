using Judze.ProjectAnalysis.Editor.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using UnityEditor;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Analysis.Models
{
    // Nolasa tikai izvēlētos resursus; neatver scēnas un nemaina importēšanas iestatījumus.
    internal sealed class UnityModelAnalyzer : IModelAnalyzer
    {
        public ModelCollectionResult Collect(IReadOnlyList<string> assetPaths, BuildTarget targetPlatform, bool collectLodContext = true)
        {
            if (assetPaths == null) throw new ArgumentNullException(nameof(assetPaths));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before analyzing models.");

            var diagnostics = new List<RuleDiagnostic>();
            var paths = assetPaths.Where(IsResourcePath).Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var lodUses = new Dictionary<(string, long), HashSet<string>>();
            bool contextComplete = collectLodContext;
            var hierarchyVersions = new List<string>();

            // Prefab un modeļu hierarhijas dod tikai LOD kontekstu; ārējos Mesh tvērumam nepievieno.
            foreach (string path in collectLodContext ? paths : Array.Empty<string>())
            {
                try
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(GameObject)) continue;
                    hierarchyVersions.Add(path + ":" + UnityAssetVersion.Read(path, AssetImporter.GetAtPath(path)));
                    var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (root == null) throw new InvalidOperationException("Could not load the model or prefab hierarchy.");
                    if (EditorUtility.IsDirty(root))
                        throw new InvalidOperationException("Save or revert pending hierarchy changes before analysis.");
                    foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
                    foreach (var lod in group.GetLODs())
                    foreach (var renderer in lod.renderers ?? Array.Empty<Renderer>())
                    {
                        if (renderer == null) continue;
                        Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                            : renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                        if (mesh == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long id)) continue;
                        var key = (guid, id);
                        if (!lodUses.TryGetValue(key, out var uses)) lodUses[key] = uses = new HashSet<string>();
                        uses.Add(path);
                    }
                }
                catch (Exception exception)
                {
                    contextComplete = false;
                    diagnostics.Add(new RuleDiagnostic("MODEL_COLLECTION", path, "LODGroup context could not be read: " + exception.Message));
                }
            }

            var models = new List<ModelFacts>();
            var seen = new HashSet<(string, long)>();
            foreach (string path in paths)
            {
                try
                {
                    Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                    if (type == null) throw new InvalidOperationException("The asset is missing or could not be loaded.");
                    if (type != typeof(GameObject) && type != typeof(Mesh)) continue;
                    // LOD lietojuma konteksts augstāk paliek pieejams arī no tikai lasāmiem prefab.
                    if (!UnityAssetEditability.CanEditSettings(path)) continue;
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    var meshes = new List<MeshFacts>();
                    foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                    {
                        try
                        {
                            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId))
                                throw new InvalidOperationException("The mesh has no persistent GUID and local file identifier.");
                            if (!seen.Add((guid, localId))) continue;
                            long? triangles = 0;
                            bool triangleOnly = mesh.subMeshCount > 0;
                            try
                            {
                                for (int i = 0; i < mesh.subMeshCount; i++)
                                {
                                    if (mesh.GetTopology(i) != MeshTopology.Triangles)
                                    { triangleOnly = false; continue; }
                                    // Bez papildu LOD argumenta Unity atgriež LOD0 indeksu skaitu.
                                    uint indices = mesh.GetIndexCount(i);
                                    if (indices % 3 != 0) throw new InvalidOperationException("Triangle index count is not divisible by three.");
                                    triangles += indices / 3;
                                }
                            }
                            catch (Exception exception)
                            {
                                triangles = null; triangleOnly = false;
                                diagnostics.Add(new RuleDiagnostic("MODEL_COLLECTION", path,
                                    mesh.name + ": triangle count is unknown. " + exception.Message));
                            }
                            lodUses.TryGetValue((guid, localId), out var uses);
                            meshes.Add(new MeshFacts(mesh.name, guid, localId, mesh.vertexCount,
                                triangles, triangleOnly, mesh.lodCount, uses?.OrderBy(value => value, StringComparer.Ordinal)));
                        }
                        catch (Exception exception)
                        {
                            diagnostics.Add(new RuleDiagnostic("MODEL_COLLECTION", path, mesh.name + ": " + exception.Message));
                        }
                    }
                    if (meshes.Count > 0)
                        models.Add(new ModelFacts(path, AssetDatabase.AssetPathToGUID(path), importer != null,
                            importer == null ? (bool?)null : importer.isReadable,
                            importer == null ? (bool?)null : importer.generateMeshLods,
                            meshes.OrderBy(mesh => mesh.LocalId),
                            UnityAssetVersion.Read(path, importer), importer != null && EditorUtility.IsDirty(importer),
                            importer == null ? (int?)null : importer.maximumMeshLod,
                            // Noteikums saņem vienkāršus faktus un neizmanto Unity importētāju.
                            importer == null ? (bool?)null :
                                (importer.meshLodGenerationFlags & MeshLodUtility.LodGenerationFlags.DiscardOddLevels) != 0));
                }
                catch (Exception exception)
                {
                    diagnostics.Add(new RuleDiagnostic("MODEL_COLLECTION", path, "Model data could not be read: " + exception.Message));
                }
            }
            return new ModelCollectionResult(models,
                new ModelAnalysisContext(targetPlatform, EditorUserBuildSettings.activeBuildTarget, contextComplete,
                    Hash128.Compute(string.Join("\n", hierarchyVersions)).ToString()), diagnostics);
        }

        private static bool IsResourcePath(string path) => !string.IsNullOrEmpty(path) &&
            (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal)) &&
            !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && !AssetDatabase.IsValidFolder(path);
    }
}
