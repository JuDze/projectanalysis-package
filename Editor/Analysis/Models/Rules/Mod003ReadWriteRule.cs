using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Models.Rules
{
    // MOD003: modeļa importētājam ar ieslēgtu Read/Write sagatavo izslēgšanas piedāvājumu.
    // IRule saskarne ļauj noteikumu izpildīt kopīgajā noteikumu dzinējā.
    // internal ierobežo piekļuvi ar šo C# kompilācijas vienību (assembly); sealed nepieļauj mantošanu.
    // Unity resursu datus iepriekš apkopo analizators; šī klase vērtē saņemtos faktus.
    internal sealed class Mod003ReadWriteRule : IRule<ModelFacts, ModelAnalysisContext>
    {
        // Norāda, vai lietotājs ir ieslēdzis noteikumu.
        // readonly lauku vērtības piešķir konstruktorā un vēlāk nemaina.
        private readonly bool _enabled;
        // Pastāvīgs noteikuma identifikators atradnēm, diagnostikai un pārskatiem.
        public string Id => "MOD003";
        // No saņemtajiem iestatījumiem nokopē analīzei nepieciešamās vērtības.
        // Turpmākas iestatījumu objekta izmaiņas šīs instances saglabātās vērtības nemaina.
        public Mod003ReadWriteRule(Mod003Settings settings)
        { _enabled = (settings ?? throw new ArgumentNullException(nameof(settings))).Enabled; }

        // facts satur modeļa datnes, to tīklus un analizatora nolasīto ModelImporter.isReadable stāvokli.
        // context satur šīs analīzes kontekstu, piemēram, informāciju par mērķplatformu.
        // Rezultāts satur atradnes un, ja vajadzīgs, diagnostiku par nepabeigtām pārbaudēm.
        // Labojuma piedāvājums vēl nemaina resursu: to piemēro atsevišķā darbībā pēc apstiprinājuma.
        public RuleEvaluation Evaluate(IReadOnlyList<ModelFacts> facts, ModelAnalysisContext context)
        {
            var findings = new List<RuleResult>();
            // Izslēgts noteikums neatgriež ne atradnes, ne diagnostikas ierakstus.
            if (!_enabled) return new RuleEvaluation();
            foreach (var model in facts)
            {
                // Nepieciešams ModelImporter un skaidri nolasīts ieslēgts iestatījums.
                // ReadWriteEnabled var būt null; salīdzinājums ar true neiekļauj ne nezināmu, ne izslēgtu stāvokli.
                if (!model.HasModelImporter || model.ReadWriteEnabled != true) continue;
                // Noteikums neanalizē skriptu vajadzību pēc CPU datiem, tādēļ to pārskata lietotājs.
                // Iestatījums ir kopīgs visiem šīs datnes ģeometrijas tīkliem un mērķplatformām.
                var warnings = ModelRulePolicy.Warnings(context);
                warnings.Add("Scripts and Unity operations may require readable mesh data. This check does not prove that CPU access is unnecessary.");
                warnings.Add("The importer setting affects every mesh and use of this model, across target platforms.");
                // Viens kopīgs importētāja iestatījums rada vienu atradni datnei.
                // Risks vienmēr ir Medium; atmiņas apjomu un skaitliskas robežvērtības šeit neizmanto.
                // ModelReadWriteProposal apraksta izmaiņu; tas neizslēdz Read/Write šīs pārbaudes laikā.
                // Ar Select izveido saistīto apakšresursu sarakstu, lai priekšskatījumā varētu norādīt ietekmētos tīklus.
                findings.Add(new RuleResult(Id, ModelRulePolicy.Resource(model), RiskLevel.Medium,
                    "Mesh Read/Write is enabled", "The model importer keeps mesh data accessible to the CPU.",
                    "Consider disabling Read/Write only after reviewing CPU access requirements for every mesh in this file.",
                    new[] { new RuleMeasurement("Meshes in this file", model.Meshes.Count, "meshes") }, warnings,
                    new ModelReadWriteProposal(), model.Meshes.Select(mesh => ModelRulePolicy.Resource(model, mesh))));
            }
            // Atgriež vienu rezultātu katrai atbilstošajai modeļa datnei, nevis atsevišķu labojumu katram tīklam.
            return new RuleEvaluation(findings);
        }
    }
}
