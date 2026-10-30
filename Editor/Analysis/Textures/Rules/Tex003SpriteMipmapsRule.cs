using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Analysis.Textures.Rules
{
    // Vērtē importētāja Sprite (2D and UI) tipu, nevis pieņem visu tekstūras lietojumu.
    // TEX003: Sprite tipa tekstūrām ar mipmaps piedāvā pārskatīt šo līmeņu nepieciešamību.
    // IRule saskarne ļauj noteikumu izpildīt kopīgajā noteikumu dzinējā.
    // internal ierobežo piekļuvi ar šo C# kompilācijas vienību (assembly); sealed nepieļauj mantošanu.
    // Unity resursu datus iepriekš apkopo analizators; šī klase vērtē saņemtos faktus.
    internal sealed class Tex003SpriteMipmapsRule : IRule<TextureFacts, TextureAnalysisContext>
    {
        // Norāda, vai lietotājs ir ieslēdzis noteikumu.
        // readonly lauku vērtības piešķir konstruktorā un vēlāk nemaina.
        private readonly bool _enabled;
        // Pastāvīgs noteikuma identifikators atradnēm, diagnostikai un pārskatiem.
        public string Id => "TEX003";
        // No saņemtajiem iestatījumiem nokopē analīzei nepieciešamās vērtības.
        // Turpmākas iestatījumu objekta izmaiņas šīs instances saglabātās vērtības nemaina.
        public Tex003SpriteMipmapsRule(Tex003Settings settings)
        { _enabled = (settings ?? throw new ArgumentNullException(nameof(settings))).Enabled; }

        // facts satur TextureImporter.textureType un mipmapEnabled vērtības, kuras nolasījis analizators.
        // context satur šīs analīzes kontekstu, piemēram, informāciju par mērķplatformu.
        // Rezultāts satur atradnes un, ja vajadzīgs, diagnostiku par nepabeigtām pārbaudēm.
        // Labojuma piedāvājums vēl nemaina resursu: to piemēro atsevišķā darbībā pēc apstiprinājuma.
        public RuleEvaluation Evaluate(IReadOnlyList<TextureFacts> facts, TextureAnalysisContext context)
        {
            var findings = new List<RuleResult>();
            // Izslēgts noteikums neatgriež ne atradnes, ne diagnostikas ierakstus.
            if (!_enabled) return new RuleEvaluation();
            foreach (var texture in facts)
            {
                // Jāizpildās abiem nosacījumiem: Sprite (2D and UI) tips un ieslēgta mip līmeņu ģenerēšana.
                // Tekstūras tips nepierāda, ka visi tās lietojumi ir ekrāna saskarnē; scēnu lietojums netiek pārbaudīts.
                if (texture.TextureType != TextureImporterType.Sprite || !texture.MipmapsEnabled) continue;
                // Priekšlikums paredz izslēgt Generate Mip Maps; tekstūras saspiešanas formātu tas nemaina.
                var proposal = new TextureMipmapsProposal();
                // Šis ir stāvokļa pārbaudes noteikums ar nemainīgu Low risku, bez skaitliskām robežvērtībām.
                // Brīdinājumi izskaidro attāluma un pasaules telpas Canvas gadījumus, kuros mipmaps var būt vajadzīgi.
                // Lietotājs izvērtē attēla kvalitātes risku pirms atsevišķas labojuma apstiprināšanas.
                findings.Add(new RuleResult(Id, TextureRulePolicy.Resource(texture), RiskLevel.Low,
                    "Sprite/UI texture has mipmaps enabled",
                    "Texture Type is Sprite (2D and UI), and Generate Mip Maps is enabled.",
                    proposal.Summary, warnings: new[] {
                        "Mipmaps can help sprites viewed at a distance and UI on a world-space Canvas. Review all uses before disabling them.",
                        "Disabling mipmaps can cause aliasing or shimmering when the image is reduced. Usage is not verified by this rule.",
                        "Generate Mip Maps is shared across target platforms. The texture format is unchanged."
                    }, proposal: proposal));
            }
            // Atgriež pa vienai atradnei katrai tekstūrai, kas atbilst abiem atlases nosacījumiem.
            return new RuleEvaluation(findings);
        }
    }
}
