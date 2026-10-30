using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    // Nemaināms vienas tekstūras plāns; null nozīmē, ka parametrs nav izvēlēts.
    internal sealed class TextureAssetFixPlan
    {
        private readonly TextureImporterSnapshot _before;
        public TextureImporterSnapshot Before => _before.Copy();
        public string Guid => _before.Guid;
        public string Path => _before.Path;
        public IReadOnlyList<RuleResult> Findings { get; }
        public int? MaxTextureSize { get; }
        public bool? MipmapsEnabled { get; }
        public bool? IsReadable { get; }
        public bool? StreamingMipmaps { get; }
        public bool ChangesPlatform => MaxTextureSize.HasValue;

        public TextureAssetFixPlan(TextureImporterSnapshot before, IEnumerable<RuleResult> findings,
            int? maxSize, bool? mipmaps, bool? readable, bool? streaming)
        {
            _before = before.Copy();
            Findings = findings.ToList().AsReadOnly();
            MaxTextureSize = maxSize; MipmapsEnabled = mipmaps; IsReadable = readable; StreamingMipmaps = streaming;
        }

        public TextureImporterPlatformSettings ExpectedPlatform()
        {
            var result = _before.Platform();
            if (!ChangesPlatform) return result;
            if (!result.overridden) _before.Defaults().CopyTo(result);
            result.name = AnalysisBuildTarget.StandaloneImporterName;
            result.overridden = true;
            if (MaxTextureSize.HasValue) result.maxTextureSize = MaxTextureSize.Value;
            return result;
        }
    }

    internal sealed class TextureFixPlan
    {
        public string Id { get; } = System.Guid.NewGuid().ToString("N");
        public TextureAnalysisRunResult Baseline { get; }
        public IReadOnlyList<TextureAssetFixPlan> Assets { get; }
        public TextureFixPlan(TextureAnalysisRunResult baseline, IEnumerable<TextureAssetFixPlan> assets)
        { Baseline = baseline; Assets = assets.ToList().AsReadOnly(); }
    }

    // Adapteris saglabā esošo TEX izpildītāju, bet kopīgā vadība nezina resursa tipu.
    internal sealed class TextureFixOperation : IAssetFixOperation
    {
        private readonly TextureAssetFixPlan _asset;
        private readonly TextureAnalysisRunResult _baseline;
        private readonly ITextureFixExecutor _executor;
        public string Guid => _asset.Guid;
        public string Path => _asset.Path;
        public string Category => "Textures";
        public TextureFixOperation(TextureAssetFixPlan asset, TextureAnalysisRunResult baseline, ITextureFixExecutor executor)
        { _asset = asset; _baseline = baseline; _executor = executor; }
        public void Validate() => _executor.Validate(_asset, _baseline.Context);
        public void Apply() => _executor.Apply(_asset);
        public void Restore() => _executor.Restore(_asset);
        public void RecordActual(FixAssetRecord record) => TextureFixChanges.RecordActual(record, _executor.Capture(Guid));
        public FixAssetRecord CreateRecord() => new FixAssetRecord
        {
            Guid = Guid, Path = Path, Category = Category, Before = _asset.Before,
            RuleIds = _asset.Findings.Select(f => f.RuleId).Distinct().ToList(), Changes = TextureFixChanges.Describe(_asset)
        };
    }

    internal static class TextureFixChanges
    {
        public static List<FixValueChange> Describe(TextureAssetFixPlan asset)
        {
            var before = asset.Before;
            var effective = before.Effective();
            var result = new List<FixValueChange>();
            if (asset.ChangesPlatform && !before.Platform().overridden) Add("Standalone Override", "Disabled", "Enabled");
            if (asset.MaxTextureSize.HasValue) Add("Max Size", effective.maxTextureSize.ToString(), asset.MaxTextureSize.Value.ToString());
            if (asset.MipmapsEnabled.HasValue) Add("Generate Mip Maps", Flag(before.MipmapsEnabled), Flag(asset.MipmapsEnabled.Value));
            if (asset.IsReadable.HasValue) Add("Read/Write", Flag(before.IsReadable), Flag(asset.IsReadable.Value));
            if (asset.StreamingMipmaps.HasValue) Add("Streaming Mipmaps", Flag(before.StreamingMipmaps), Flag(asset.StreamingMipmaps.Value));
            return result;
            void Add(string property, string current, string intended) => result.Add(new FixValueChange
                { Property = property, Before = current, Intended = intended });
        }

        public static void RecordActual(FixAssetRecord record, TextureImporterSnapshot after)
        {
            record.After = after;
            foreach (var change in record.Changes)
            {
                // Šī izvēle formatē labojumu datus; resursu izmaiņas nekad neizpilda pēc teksta.
                switch (change.Property)
                {
                    case "Standalone Override": change.Actual = Flag(after.Platform().overridden); break;
                    case "Max Size": change.Actual = after.Effective().maxTextureSize.ToString(); break;
                    case "Generate Mip Maps": change.Actual = Flag(after.MipmapsEnabled); break;
                    case "Read/Write": change.Actual = Flag(after.IsReadable); break;
                    case "Streaming Mipmaps": change.Actual = Flag(after.StreamingMipmaps); break;
                }
            }
        }
        private static string Flag(bool value) => value ? "Enabled" : "Disabled";
    }
}
