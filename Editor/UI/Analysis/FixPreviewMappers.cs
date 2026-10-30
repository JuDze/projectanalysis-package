using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System;
using Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Audio;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Models.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Models;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Textures.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Textures;
using Judze.ProjectAnalysis.Editor.Fixes;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Pārveido gatavus priekšlikumus attēlošanas datos, nepārlasot un nemainot resursus.
    internal sealed class TextureFixPreviewMapper
    {
        private const string SharedScope = "Shared importer setting — all target platforms";
        private static readonly Dictionary<Type, Func<RuleResult, TextureAnalysisContext, FixPreviewRow>> _creators =
            new Dictionary<Type, Func<RuleResult, TextureAnalysisContext, FixPreviewRow>>
            {
                { typeof(TextureResizeProposal), CreateResizePreview },
                { typeof(TextureReadWriteProposal), CreateReadWritePreview },
                { typeof(TextureMipmapsProposal), CreateMipmapsPreview },
                { typeof(TextureStreamingProposal), CreateStreamingPreview }
            };

        public FixPreviewRow Create(RuleResult result, TextureAnalysisContext context)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (result.Proposal == null || result.Resource == null || string.IsNullOrEmpty(result.Resource.Path))
                return null;

            return _creators.TryGetValue(result.Proposal.GetType(), out var create)
                ? create(result, context) : null;
        }

        private static FixPreviewRow CreateResizePreview(RuleResult result, TextureAnalysisContext context)
        {
            var proposal = (TextureResizeProposal)result.Proposal;
            var changes = PlatformChanges(proposal.PlatformOverridden, context.ImportSettingsPlatform);
            string current = proposal.CurrentMaxSize.ToString(CultureInfo.InvariantCulture);
            if (!proposal.PlatformOverridden) current += " — inherited from Default";
            changes.Add(new FixPreviewPropertyChange("Max Size", current,
                proposal.ProposedMaxSize.ToString(CultureInfo.InvariantCulture)));
            return CreateRow(result, context, context.ImportSettingsPlatform + " platform settings", changes);
        }

        private static FixPreviewRow CreateMipmapsPreview(RuleResult result, TextureAnalysisContext context)
        {
            return CreateRow(result, context, SharedScope, new[]
            { new FixPreviewPropertyChange("Generate Mip Maps", "Enabled", "Disabled") });
        }

        private static FixPreviewRow CreateReadWritePreview(RuleResult result, TextureAnalysisContext context)
        {
            var proposal = (TextureReadWriteProposal)result.Proposal;
            return CreateRow(result, context, SharedScope, new[]
            {
                new FixPreviewPropertyChange("Read/Write", EnabledLabel(proposal.CurrentValue),
                    EnabledLabel(proposal.ProposedValue))
            });
        }

        private static FixPreviewRow CreateStreamingPreview(RuleResult result, TextureAnalysisContext context)
        {
            var proposal = (TextureStreamingProposal)result.Proposal;
            return CreateRow(result, context, SharedScope, new[]
            {
                new FixPreviewPropertyChange("Streaming Mipmaps", EnabledLabel(proposal.CurrentValue),
                    EnabledLabel(proposal.ProposedValue))
            });
        }

        private static List<FixPreviewPropertyChange> PlatformChanges(bool overridden, string platform)
        {
            var changes = new List<FixPreviewPropertyChange>();
            if (!overridden)
                changes.Add(new FixPreviewPropertyChange(platform + " Override", "Disabled", "Enabled"));
            return changes;
        }

        private static string EnabledLabel(bool value) => value ? "Enabled" : "Disabled";

        private static FixPreviewRow CreateRow(RuleResult result, TextureAnalysisContext context,
            string settingsScope, IEnumerable<FixPreviewPropertyChange> changes)
        {
            var resource = new FixPreviewResource
            {
                // Ceļš ir rezerves identifikators tikai attēlošanai, ja GUID nav pieejams.
                Id = string.IsNullOrEmpty(result.Resource.Guid) ? result.Resource.Path : result.Resource.Guid,
                Name = Path.GetFileName(result.Resource.Path),
                Path = result.Resource.Path
            };
            return new FixPreviewRow
            {
                Id = result.RuleId + ":" + resource.Id,

                Risk = result.Priority.ToString(),
                Resource = resource.Name,
                Path = resource.Path,
                RuleId = result.RuleId,
                RuleName = result.Title,
                ChangeSummary = result.Proposal.Summary,
                Reason = AnalysisResultRowMapper.Describe(result),
                Recommendation = result.Recommendation,
                Consequences = string.Join("\n", result.Warnings.Select(warning => "• " + warning)),
                AnalysisTarget = context.TargetPlatform.ToString(),
                SettingsScope = settingsScope,
                PropertyChanges = new List<FixPreviewPropertyChange>(changes).AsReadOnly(),
                AffectedResources = new[] { resource }
            };
        }

        // Pārbaudītā plāna vērtības izmanto atsevišķi no sākotnējās analīzes priekšlikuma.
        public static FixPreviewRow[] CreateFromPlan(TextureFixPlan plan) => plan.Assets
            .SelectMany(asset => asset.Findings.Select(finding => Create(plan, asset, finding))).ToArray();

        private static FixPreviewRow Create(TextureFixPlan plan, TextureAssetFixPlan asset, RuleResult finding)
        {
            // Vienas kartītes vērtības iegūst no pārbaudītā plāna momentuzņēmuma.
            var single = new TextureAssetFixPlan(asset.Before, new[] { finding },
                finding.Proposal is TextureResizeProposal ? asset.MaxTextureSize : null,
                finding.Proposal is TextureMipmapsProposal ? asset.MipmapsEnabled : null,
                finding.Proposal is TextureReadWriteProposal ? asset.IsReadable : null,
                finding.Proposal is TextureStreamingProposal ? asset.StreamingMipmaps : null);
            var changes = TextureFixChanges.Describe(single).ToArray();
            if (changes.Length == 0) throw new InvalidOperationException("The texture proposal has no planned changes.");
            return new FixPreviewRow
            {
                Id = finding.RuleId + ":" + finding.Resource.Identity,
                Resource = Path.GetFileName(asset.Path), Path = asset.Path,
                Risk = finding.Priority.ToString(), RuleId = finding.RuleId, RuleName = finding.Title,
                ChangeSummary = finding.Proposal.Summary,
                AnalysisTarget = plan.Baseline.Context.TargetPlatform.ToString(),
                SettingsScope = single.ChangesPlatform ? "Standalone platform settings" : "Shared importer setting — all target platforms",
                Reason = AnalysisResultRowMapper.Describe(finding),
                Recommendation = finding.Recommendation,
                Consequences = string.Join("\n", finding.Warnings.Select(w => "• " + w)),
                PropertyChanges = changes.Select(c => new FixPreviewPropertyChange(c.Property, CurrentValue(single, c), c.Intended)).ToArray(),
                AffectedResources = new[] { new FixPreviewResource { Id = asset.Guid, AssetId = asset.Guid,
                    Name = Path.GetFileName(asset.Path), Path = asset.Path } }
            };
        }
        private static string CurrentValue(TextureAssetFixPlan asset, FixValueChange change) =>
            change.Before + (change.Property == "Max Size" && !asset.Before.Platform().overridden ? " — inherited from Default" : "");
    }

    // Kopīga importētāja priekšskatījumā rāda visus datnes tīklus, ne tikai atradnes cēloni.
    internal sealed class ModelFixPreviewMapper
    {
        private readonly Dictionary<Type, Func<ModelFacts, FixPreviewPropertyChange>> _changes =
            new Dictionary<Type, Func<ModelFacts, FixPreviewPropertyChange>>
            {
                { typeof(ModelReadWriteProposal), m => new FixPreviewPropertyChange("Read/Write", Label(m.ReadWriteEnabled), "Disabled") },
                { typeof(ModelLodGenerationProposal), m => new FixPreviewPropertyChange("Generate Mesh LODs", Label(m.GenerateMeshLods), "Enabled") }
            };

        public FixPreviewRow Create(RuleResult finding, ModelCollectionResult collection)
        {
            if (finding?.Proposal == null || !_changes.ContainsKey(finding.Proposal.GetType())) return null;
            var model = collection.Models.SingleOrDefault(m => m.Guid == finding.Resource?.Guid);
            return model == null ? null : Create(finding, model, collection.Context);
        }

        public FixPreviewRow[] Create(ModelFixPreview preview) =>
            preview.Assets.SelectMany(asset => asset.Findings.Select(finding =>
                Create(finding, asset.Model, preview.Context))).ToArray();

        private FixPreviewRow Create(RuleResult finding, ModelFacts model, ModelAnalysisContext context)
        {
            if (!_changes.TryGetValue(finding.Proposal.GetType(), out var describe))
                throw new NotSupportedException("This model proposal has no supported preview.");
            var resources = new List<FixPreviewResource>
            {
                new FixPreviewResource { Id = model.Guid, AssetId = model.Guid,
                    Name = Path.GetFileName(model.AssetPath), Path = model.AssetPath }
            };
            resources.AddRange(model.Meshes.Select(mesh => new FixPreviewResource
            {
                Id = mesh.Guid + ":" + mesh.LocalId, AssetId = model.Guid,
                Name = mesh.Name, Path = model.AssetPath, IsSubAsset = true
            }));
            return new FixPreviewRow
            {
                Id = finding.RuleId + ":" + finding.Resource.Identity,
                Resource = Path.GetFileName(model.AssetPath), Path = model.AssetPath,
                RuleId = finding.RuleId, RuleName = finding.Title,
                Risk = finding.Priority.ToString(),
                ChangeSummary = finding.Proposal.Summary,
                Reason = AnalysisResultRowMapper.Describe(finding),
                Recommendation = finding.Recommendation,
                Consequences = string.Join("\n", finding.Warnings.Select(w => "• " + w)),
                AnalysisTarget = AnalysisBuildTarget.DisplayName(context.TargetPlatform),
                SettingsScope = "Shared importer setting — all target platforms",
                PropertyChanges = new[] { describe(model) }, AffectedResources = resources.AsReadOnly()
            };
        }
        private static string Label(bool? value) => value.HasValue ? (value.Value ? "Enabled" : "Disabled") : "Unknown";
    }

    // Katram noteikuma priekšlikumam veido atsevišķu kartīti, arī vienai un tai pašai datnei.
    internal sealed class AudioFixPreviewMapper
    {
        private readonly Dictionary<Type, Func<IChangeProposal, AudioFacts, IEnumerable<FixPreviewPropertyChange>>> _changes =
            new Dictionary<Type, Func<IChangeProposal, AudioFacts, IEnumerable<FixPreviewPropertyChange>>>
            {
                { typeof(AudioLoadTypeProposal), (p, c) => new[] { new FixPreviewPropertyChange("Load Type",
                    AudioRulePolicy.LoadTypeName(((AudioLoadTypeProposal)p).Current), AudioRulePolicy.LoadTypeName(((AudioLoadTypeProposal)p).Target)) } },
                { typeof(AudioCompressionProposal), CompressionChanges },
                { typeof(AudioSampleRateProposal), (p, c) => new[] { new FixPreviewPropertyChange("Sample Rate Setting",
                    RateName(((AudioSampleRateProposal)p).Current), "Optimize Sample Rate") } },
                { typeof(AudioMonoProposal), (p, c) => new[] { new FixPreviewPropertyChange("Force To Mono", "Disabled", "Enabled") } }
            };

        public FixPreviewRow Create(RuleResult finding, AudioCollectionResult collection)
        {
            if (finding?.Proposal == null || !_changes.ContainsKey(finding.Proposal.GetType())) return null;
            var clip = collection.Clips.SingleOrDefault(c => c.Guid == finding.Resource?.Guid && c.LocalId == finding.Resource.LocalId);
            return clip == null ? null : Create(finding, clip, collection.Context);
        }

        public FixPreviewRow[] Create(AudioFixPreview preview) => preview.Assets
            .SelectMany(asset => asset.Findings.Select(f => Create(f, asset.Clip, preview.Context))).ToArray();

        private FixPreviewRow Create(RuleResult finding, AudioFacts clip, AudioAnalysisContext context)
        {
            bool shared = finding.Proposal is AudioMonoProposal;
            var changes = new List<FixPreviewPropertyChange>();
            if (!shared && !clip.HasOverride)
                changes.Add(new FixPreviewPropertyChange("Standalone Override", "Disabled", "Enabled"));
            changes.AddRange(_changes[finding.Proposal.GetType()](finding.Proposal, clip));
            return new FixPreviewRow
            {
                Id = finding.RuleId + ":" + finding.Resource.Identity,
                Resource = Path.GetFileName(clip.AssetPath), Path = clip.AssetPath,
                RuleId = finding.RuleId, RuleName = finding.Title, Risk = finding.Priority.ToString(),
                ChangeSummary = finding.Proposal.Summary,
                Reason = AnalysisResultRowMapper.Describe(finding),
                Recommendation = finding.Recommendation,
                Consequences = string.Join("\n", finding.Warnings.Select(w => "• " + w)),
                AnalysisTarget = AnalysisBuildTarget.DisplayName(context.TargetPlatform),
                SettingsSource = shared ? "Shared importer setting" : clip.HasOverride ? "Standalone override" : "Default (translated for Standalone)",
                SettingsScope = shared ? "Shared importer setting — all target platforms" :
                    "Standalone platform settings" + (clip.HasOverride ? "" : " — override will be created"),
                PropertyChanges = changes.AsReadOnly(),
                AffectedResources = new[] { new FixPreviewResource { Id = finding.Resource.Identity, AssetId = clip.Guid,
                    Name = Path.GetFileName(clip.AssetPath), Path = clip.AssetPath } }
            };
        }

        private static IEnumerable<FixPreviewPropertyChange> CompressionChanges(IChangeProposal value, AudioFacts clip)
        {
            var proposal = (AudioCompressionProposal)value;
            yield return new FixPreviewPropertyChange("Compression Format", proposal.Current.ToString(), proposal.Target.ToString());
            if (proposal.VorbisQuality.HasValue)
                yield return new FixPreviewPropertyChange("Vorbis Quality",
                    clip.SampleSettings.compressionFormat == AudioCompressionFormat.Vorbis ?
                        clip.SampleSettings.quality.ToString("0.###", CultureInfo.InvariantCulture) : "Not applicable (" + clip.SampleSettings.compressionFormat + ")",
                    proposal.VorbisQuality.Value.ToString("0.###", CultureInfo.InvariantCulture));
        }
        private static string RateName(UnityEditor.AudioSampleRateSetting value) =>
            value == UnityEditor.AudioSampleRateSetting.PreserveSampleRate ? "Preserve Sample Rate" :
            value == UnityEditor.AudioSampleRateSetting.OverrideSampleRate ? "Override Sample Rate" : "Optimize Sample Rate";
    }
}
