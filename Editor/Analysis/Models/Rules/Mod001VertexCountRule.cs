using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Models.Rules
{
    // MOD001: katram savāktajam ģeometrijas tīklam izvērtē virsotņu skaitu.
    // IRule saskarne ļauj noteikumu izpildīt kopīgajā noteikumu dzinējā.
    // internal ierobežo piekļuvi ar šo C# kompilācijas vienību (assembly); sealed nepieļauj mantošanu.
    // Unity resursu datus iepriekš apkopo analizators; šī klase vērtē saņemtos faktus.
    internal sealed class Mod001VertexCountRule : IRule<ModelFacts, ModelAnalysisContext>
    {
        // Norāda, vai lietotājs ir ieslēdzis noteikumu.
        // readonly lauku vērtības piešķir konstruktorā un vēlāk nemaina.
        private readonly bool _enabled;
        // Abas robežvērtības ir virsotņu skaits vienā ģeometrijas tīklā, nevis modeļa datnes kopsumma.
        private readonly long _threshold, _highThreshold;
        // Pastāvīgs noteikuma identifikators atradnēm, diagnostikai un pārskatiem.
        public string Id => "MOD001";
        // No saņemtajiem iestatījumiem nokopē analīzei nepieciešamās vērtības.
        // Turpmākas iestatījumu objekta izmaiņas šīs instances saglabātās vērtības nemaina.
        public Mod001VertexCountRule(Mod001Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _enabled = settings.Enabled; _threshold = settings.VertexThreshold; _highThreshold = settings.VertexHighThreshold;
            // Ieslēgtam noteikumam pārbauda pozitīvas robežvērtības un otrās pārsvaru pār pirmo.
            if (_enabled) RiskThresholds.Validate(_threshold, _highThreshold);
        }
        // facts satur modeļu datnes un to MeshFacts ierakstus; VertexCount analizators nolasa no mesh.vertexCount.
        // context satur šīs analīzes kontekstu, piemēram, informāciju par mērķplatformu.
        // Rezultāts satur atradnes un, ja vajadzīgs, diagnostiku par nepabeigtām pārbaudēm.
        // Labojuma piedāvājums vēl nemaina resursu: to piemēro atsevišķā darbībā pēc apstiprinājuma.
        public RuleEvaluation Evaluate(IReadOnlyList<ModelFacts> facts, ModelAnalysisContext context)
        {
            var findings = new List<RuleResult>();
            // Izslēgts noteikums neatgriež ne atradnes, ne diagnostikas ierakstus.
            if (!_enabled) return new RuleEvaluation();
            // Ārējais cikls izvēlas modeļa datni, bet iekšējais izskata katru tajā savākto ģeometrijas tīklu.
            // Analizators jau nošķīra tīklus pēc GUID un localId; tiem vienā datnē var būt kopīgs GUID.
            foreach (var model in facts)
            foreach (var mesh in model.Meshes)
            {
                // Atšķirībā no MOD002 šeit atradni veido arī zem pirmās robežvērtības.
                // Low ir līdz pirmajai robežai ieskaitot, Medium līdz otrajai ieskaitot, High virs otrās.
                // Vienādība ar pirmo robežvērtību pieder Low līmenim.
                // Resursa identitāte ietver konkrētā apakšresursa localId, lai atšķirtu vienas datnes tīklus.
                // Rekomendācija mainās atkarībā no pirmās robežas pārsniegšanas; risks izmanto abas robežas.
                // Labojuma priekšlikuma nav: automātiska ģeometrijas vienkāršošana šajā noteikumā nenotiek.
                findings.Add(new RuleResult(Id, ModelRulePolicy.Resource(model, mesh),
                    RiskThresholds.Evaluate((long)mesh.VertexCount, _threshold, _highThreshold),
                    "Mesh vertex count", $"{mesh.Name} contains {mesh.VertexCount} vertices.",
                    mesh.VertexCount > _threshold
                        ? "Review the required detail and consider simplifying the source geometry. Vertex count alone does not measure frame time."
                        : "The vertex count is within the configured threshold. Review it in the context of the model's use.",
                    new[] { new RuleMeasurement("Vertices", mesh.VertexCount, "vertices"),
                        new RuleMeasurement("Threshold", _threshold, "vertices") }, ModelRulePolicy.Warnings(context)));
            }
            // Atgriež atsevišķu rezultātu par katru faktos iekļauto ģeometrijas tīklu.
            return new RuleEvaluation(findings);
        }
    }
}
