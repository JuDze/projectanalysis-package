using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Analysis.Textures.Rules
{
    // TEX004: salīdzina tekstūras mip straumēšanas stāvokli ar pašreizējā kvalitātes līmeņa stāvokli.
    // IRule saskarne ļauj noteikumu izpildīt kopīgajā noteikumu dzinējā.
    // internal ierobežo piekļuvi ar šo C# kompilācijas vienību (assembly); sealed nepieļauj mantošanu.
    // Unity resursu datus iepriekš apkopo analizators; šī klase vērtē saņemtos faktus.
    internal sealed class Tex004StreamingRule : IRule<TextureFacts, TextureAnalysisContext>
    {
        // Norāda, vai lietotājs ir ieslēdzis noteikumu.
        // readonly lauku vērtības piešķir konstruktorā un vēlāk nemaina.
        private readonly bool _enabled;
        // Robežvērtības izsaka esošo mip datu apjomu MiB, nevis paredzamo ietaupījumu.
        private readonly double _threshold, _highThreshold;
        // Pastāvīgs noteikuma identifikators atradnēm, diagnostikai un pārskatiem.
        public string Id => "TEX004";
        // No saņemtajiem iestatījumiem nokopē analīzei nepieciešamās vērtības.
        // Turpmākas iestatījumu objekta izmaiņas šīs instances saglabātās vērtības nemaina.
        public Tex004StreamingRule(Tex004Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _enabled = settings.Enabled; _threshold = settings.MipDataThresholdMiB; _highThreshold = settings.MipDataHighThresholdMiB;
            // Robežvērtībām jābūt pozitīvām, galīgām un augošā secībā.
            if (_enabled) RiskThresholds.Validate(_threshold, _highThreshold);
        }
        // facts satur tekstūru mip līmeņus, to datu apjomu un individuālo straumēšanas karogu.
        // context satur šīs analīzes kontekstu, piemēram, informāciju par mērķplatformu.
        // Rezultāts satur atradnes un, ja vajadzīgs, diagnostiku par nepabeigtām pārbaudēm.
        // Labojuma piedāvājums vēl nemaina resursu: to piemēro atsevišķā darbībā pēc apstiprinājuma.
        public RuleEvaluation Evaluate(IReadOnlyList<TextureFacts> facts, TextureAnalysisContext context)
        {
            // Izslēgts noteikums neatgriež ne atradnes, ne diagnostikas ierakstus.
            if (!_enabled) return new RuleEvaluation();
            var findings = new List<RuleResult>();
            var diagnostics = new List<RuleDiagnostic>();
            // Ja globālā straumēšana ir izslēgta, saistītās tekstūras apkopo vienai projekta atradnei.
            var related = new List<RuleResource>();
            // GUID, bet tā trūkuma gadījumā ceļš, novērš vienas tekstūras atkārtotu iekļaušanu.
            // Ordinal nodrošina precīzu identifikatoru salīdzinājumu bez valodas iestatījumu ietekmes.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            // Projekta atradnes risks būs lielākais no zināmajiem atsevišķo tekstūru riskiem.
            // null šeit ir aprēķina sākumstāvoklis, nevis atradnei piešķirams riska līmenis.
            RiskLevel? projectPriority = null;
            // Uzskaita saistītās tekstūras bez izmēra, lai globālajai atradnei pievienotu precizējošu brīdinājumu.
            int unknownSizes = 0;
            foreach (var texture in facts)
            {
                // Vajadzīgi jau esoši samazinātie mip līmeņi; noteikums nepiedāvā ieslēgt pašu mipmaps ģenerēšanu.
                if (!texture.MipmapsEnabled || texture.MipmapCount <= 1) continue;
                // Pirmajā realizācijā piedāvājums paredzēts Default un NormalMap tekstūrām.
                if (texture.TextureType != TextureImporterType.Default && texture.TextureType != TextureImporterType.NormalMap)
                {
                    diagnostics.Add(new RuleDiagnostic(Id, texture.AssetPath, "This texture type requires a separate streaming review."));
                    continue;
                }
                string identity = string.IsNullOrEmpty(texture.Guid) ? texture.AssetPath : texture.Guid;
                if (!seen.Add(identity)) continue;
                // Analizators mip ķēdes apjomu sagatavo ar GraphicsFormatUtility.ComputeMipChainSize.
                // Tas ietver pamatlīmeni un esošos mip līmeņus; Read/Write nav priekšnosacījums šim aprēķinam.
                double? size = context.ImportedDataMatchesTarget ? TextureRulePolicy.MiB(texture.ImportedMipChainBytes) : null;
                // Riska intervāli: līdz pirmajai robežai Low, līdz otrajai Medium, virs otrās High.
                // Vienādība paliek zemākajā intervālā; nezināms izmērs dod null.
                RiskLevel? priority = RiskThresholds.Evaluate(size, _threshold, _highThreshold);
                // Globālo stāvokli analizators nolasa ar QualitySettings.streamingMipmapsActive.
                // Ja tas ir izslēgts, sagatavo kopīgu rekomendāciju; individuālu ieslēgšanas piedāvājumu šajā zarā neveido.
                if (!context.GlobalStreamingMipmapsActive)
                {
                    related.Add(TextureRulePolicy.Resource(texture));
                    if (!size.HasValue) unknownSizes++;
                    if (priority.HasValue && (!projectPriority.HasValue || priority.Value > projectPriority.Value)) projectPriority = priority;
                    continue;
                }
                // Kad globālā straumēšana darbojas, atradne vajadzīga tikai tekstūrai ar izslēgtu individuālo karogu.
                if (texture.StreamingMipmaps) continue;
                // Bez derīga izmēra saglabā diagnostiku, nevis izdomātu risku un labojuma piedāvājumu.
                if (!priority.HasValue)
                {
                    diagnostics.Add(new RuleDiagnostic(Id, texture.AssetPath, context.ImportedDataMatchesTarget
                        ? "Existing mip data size is unavailable; the risk was not evaluated." : TextureRulePolicy.PlatformWarning));
                    continue;
                }
                var warnings = new List<string>
                {
                    "Confirm that streaming suits every use of the texture. Detail can arrive later; usage was not analyzed.",
                    "No memory savings are estimated. Quality Settings are not changed.",
                    "The texture streaming flag is a shared importer setting across target platforms."
                };
                // Piedāvā tikai konkrētās tekstūras streamingMipmaps ieslēgšanu pēc lietotāja apstiprinājuma.
                // QualitySettings vērtības un kvalitātes līmeni šis priekšlikums nemaina.
                var proposal = new TextureStreamingProposal();
                findings.Add(new RuleResult(Id, TextureRulePolicy.Resource(texture), priority.Value,
                    "Texture mip streaming is disabled",
                    $"Existing mip levels: {texture.MipmapCount}. Global streaming is enabled at current Quality Level '{context.QualityLevelName}'.",
                    proposal.Summary, new[] { new RuleMeasurement("Existing mip data", size, "MiB") }, warnings, proposal));
            }
            // Projekta risku nosaka tikai tad, ja ir vismaz viens derīgs tekstūras izmērs.
            if (related.Count > 0 && !projectPriority.HasValue)
                diagnostics.Add(new RuleDiagnostic(Id, null, "Related texture sizes are unavailable; the global streaming risk was not evaluated."));
            // Globālās problēmas atradni izveido, ja vismaz vienai saistītajai tekstūrai varēja noteikt risku.
            if (related.Count > 0 && projectPriority.HasValue)
            {
                // Viena projekta atradne aizstāj vienādus ziņojumus katrai tekstūrai.
                var warnings = new List<string> { "Quality Level and thresholds are unchanged. No savings are estimated." };
                if (unknownSizes > 0) warnings.Add($"{unknownSizes} related texture sizes are unknown; priority uses only known sizes.");
                if (!context.ImportedDataMatchesTarget) warnings.Add(TextureRulePolicy.PlatformWarning);
                // Resursa vietā ir null, jo atradne attiecas uz projektu; tekstūras nodod relatedResources sarakstā.
                // Šim rezultātam nav labojuma priekšlikuma: globālo iestatījumu pārskatīšana paliek lietotāja ziņā.
                findings.Add(new RuleResult(Id, null, projectPriority.Value, "Global mip streaming is disabled",
                    $"Current Quality Level '{context.QualityLevelName}' has global mip streaming disabled. " +
                    $"Related textures with existing mipmaps: {related.Count}.",
                    "Review Project Settings > Quality and texture streaming suitability. No project setting change is proposed automatically.",
                    new[] { new RuleMeasurement("Related textures", related.Count, "count") }, warnings,
                    relatedResources: related));
            }
            // Atgriež gan individuālās vai kopīgo atradni, gan diagnostiku par neizvērtējamiem datiem.
            return new RuleEvaluation(findings, diagnostics);
        }
    }
}
