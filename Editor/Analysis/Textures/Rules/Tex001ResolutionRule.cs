using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Textures.Rules
{
    // TEX001: atlasa tekstūras, kuru importētā lielākā dimensija pārsniedz pirmo robežvērtību.
    // IRule saskarne ļauj noteikumu izpildīt kopīgajā noteikumu dzinējā.
    // internal ierobežo piekļuvi ar šo C# kompilācijas vienību (assembly); sealed nepieļauj mantošanu.
    // Unity resursu datus iepriekš apkopo analizators; šī klase vērtē saņemtos faktus.
    internal sealed class Tex001ResolutionRule : IRule<TextureFacts, TextureAnalysisContext>
    {
        // Norāda, vai lietotājs ir ieslēdzis noteikumu.
        // readonly lauku vērtības piešķir konstruktorā un vēlāk nemaina.
        private readonly bool _enabled;
        // Abas robežvērtības ir pikseļos: pirmā sāk Medium, otrā sāk High intervālu.
        // Riska līmenis paaugstinās tikai pēc robežvērtības pārsniegšanas.
        private readonly long _threshold, _highThreshold;
        // Pastāvīgs noteikuma identifikators atradnēm, diagnostikai un pārskatiem.
        public string Id => "TEX001";
        // No saņemtajiem iestatījumiem nokopē analīzei nepieciešamās vērtības.
        // Turpmākas iestatījumu objekta izmaiņas šīs instances saglabātās vērtības nemaina.
        public Tex001ResolutionRule(Tex001Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _enabled = settings.Enabled;
            _threshold = settings.ResolutionThreshold; _highThreshold = settings.ResolutionHighThreshold;
            // Ieslēgtam noteikumam robežvērtībām jābūt pozitīvām, un otrajai jāpārsniedz pirmā.
            if (_enabled) RiskThresholds.Validate(_threshold, _highThreshold);
        }

        // facts satur TextureFacts ierakstus ar importētajiem izmēriem un spēkā esošajiem importa iestatījumiem.
        // context satur šīs analīzes kontekstu, piemēram, informāciju par mērķplatformu.
        // Rezultāts satur atradnes un, ja vajadzīgs, diagnostiku par nepabeigtām pārbaudēm.
        // Labojuma piedāvājums vēl nemaina resursu: to piemēro atsevišķā darbībā pēc apstiprinājuma.
        public RuleEvaluation Evaluate(IReadOnlyList<TextureFacts> facts, TextureAnalysisContext context)
        {
            // Izslēgts noteikums neatgriež ne atradnes, ne diagnostikas ierakstus.
            if (!_enabled) return new RuleEvaluation();
            // Atradnes apraksta izvērtējamos resursus; diagnostika apraksta trūkstošus vai neatbilstošus datus.
            var findings = new List<RuleResult>();
            var diagnostics = new List<RuleDiagnostic>();
            foreach (var texture in facts)
            {
                // Texture2D.width un Texture2D.height analizators jau saglabāja faktos.
                // Citas platformas importa izmērus vai nederīgas dimensijas neizmanto šīs platformas riska noteikšanai.
                if (!context.ImportedDataMatchesTarget || texture.ImportedWidth <= 0 || texture.ImportedHeight <= 0)
                {
                    diagnostics.Add(new RuleDiagnostic(Id, texture.AssetPath,
                        "Imported dimensions are unavailable for the selected target; resolution was not evaluated."));
                    continue;
                }
                // Pārbaude izmanto garāko malu, nevis pikseļu skaitu vai avota datnes izšķirtspēju.
                int dimension = Math.Max(texture.ImportedWidth, texture.ImportedHeight);
                // Pie pirmās robežvērtības un zem tās atradni neveido; šim noteikumam nav Low atradņu.
                if (dimension <= _threshold) continue;
                // Virs pirmās robežas līdz otrajai ieskaitot ir Medium; virs otrās ir High.
                RiskLevel priority = RiskThresholds.Evaluate((long)dimension, _threshold, _highThreshold);
                int? proposedSize = NextSize(dimension);
                var warnings = new List<string> { "Review the loss of visual detail before confirming a change." };
                TextureResizeProposal proposal = null;
                // Piedāvā nākamo mazāko izmēra pakāpi tikai tad, ja tā samazina pašreizējo Max Size.
                // Priekšlikumā saglabā arī to, vai platformai jau bija atsevišķi importa iestatījumi.
                if (proposedSize.HasValue && texture.EffectiveMaxTextureSize > proposedSize.Value)
                    proposal = new TextureResizeProposal(texture.EffectiveMaxTextureSize, proposedSize.Value, texture.PlatformOverridden);
                else warnings.Add("No smaller supported Max Size can be proposed from the collected settings.");

                // RuleResult apvieno resursa identitāti, risku, pamatojumu, mērījumus, brīdinājumus un izvēles labojumu.
                // Ja proposal ir null, atradne paliek apskatāma, bet konkrēts automātisks labojums nav sagatavots.
                findings.Add(new RuleResult(Id, TextureRulePolicy.Resource(texture), priority,
                    "Imported texture resolution exceeds the review threshold",
                    $"Imported size: {texture.ImportedWidth} x {texture.ImportedHeight}; largest dimension: {dimension} px. " +
                    $"Review threshold: {_threshold} px (strictly greater).",
                    proposal?.Summary ?? "Review texture resolution and its effective import settings.",
                    new[] { new RuleMeasurement("Imported width", texture.ImportedWidth, "px"),
                        new RuleMeasurement("Imported height", texture.ImportedHeight, "px"),
                        new RuleMeasurement("Largest imported dimension", dimension, "px") }, warnings, proposal));
            }
            // Abus sarakstus nodod analīzes procesam; resursa importēšana šeit nenotiek.
            return new RuleEvaluation(findings, diagnostics);
        }

        // Atgriež lielāko atbalstīto pakāpi, kas ir stingri mazāka par faktisko dimensiju.
        // Piemēram, 8192 dod 4096, bet 3000 dod 2048; pie 32 vai mazāk nav nākamās atbalstītās pakāpes.
        // int? ļauj atgriezt null, ja mazāku izmēru nevar piedāvāt.
        private static int? NextSize(int dimension)
        {
            // Izvēlas nākamo mazāko atbalstīto Max Size pēc faktiskā izmēra.
            int? result = null;
            for (int size = 32; size <= 16384 && size < dimension; size *= 2) result = size;
            return result;
        }
    }
}
