using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Models.Rules
{
    // MOD002: atlasa ģeometrijas tīklus, kuru LOD0 trijstūru skaits pārsniedz pirmo robežvērtību.
    // IRule saskarne ļauj noteikumu izpildīt kopīgajā noteikumu dzinējā.
    // internal ierobežo piekļuvi ar šo C# kompilācijas vienību (assembly); sealed nepieļauj mantošanu.
    // Unity resursu datus iepriekš apkopo analizators; šī klase vērtē saņemtos faktus.
    internal sealed class Mod002TriangleCountRule : IRule<ModelFacts, ModelAnalysisContext>
    {
        // Norāda, vai lietotājs ir ieslēdzis noteikumu.
        // readonly lauku vērtības piešķir konstruktorā un vēlāk nemaina.
        private readonly bool _enabled;
        // Abas robežvērtības ir LOD0 trijstūru skaits vienā tīklā; tās izmanto arī MOD004.
        private readonly long _threshold, _highThreshold;
        // Pastāvīgs noteikuma identifikators atradnēm, diagnostikai un pārskatiem.
        public string Id => "MOD002";
        // No saņemtajiem iestatījumiem nokopē analīzei nepieciešamās vērtības.
        // Turpmākas iestatījumu objekta izmaiņas šīs instances saglabātās vērtības nemaina.
        public Mod002TriangleCountRule(Mod002Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _enabled = settings.Enabled; _threshold = settings.TriangleThreshold; _highThreshold = settings.TriangleHighThreshold;
            // Pārbauda pozitīvas, augošas robežvērtības, pirms noteikums sāk darbu.
            if (_enabled) RiskThresholds.Validate(_threshold, _highThreshold);
        }
        // facts satur analizatora sagatavotus MeshFacts; tie paši dati ir pieejami arī citiem modeļu noteikumiem.
        // context satur šīs analīzes kontekstu, piemēram, informāciju par mērķplatformu.
        // Rezultāts satur atradnes un, ja vajadzīgs, diagnostiku par nepabeigtām pārbaudēm.
        // Labojuma piedāvājums vēl nemaina resursu: to piemēro atsevišķā darbībā pēc apstiprinājuma.
        public RuleEvaluation Evaluate(IReadOnlyList<ModelFacts> facts, ModelAnalysisContext context)
        {
            var findings = new List<RuleResult>();
            var diagnostics = new List<RuleDiagnostic>();
            // Izslēgts noteikums neatgriež ne atradnes, ne diagnostikas ierakstus.
            if (!_enabled) return new RuleEvaluation();
            // Katras modeļa datnes ģeometrijas tīklus vērtē atsevišķi, nesummējot visu datni.
            foreach (var model in facts)
            foreach (var mesh in model.Meshes)
            {
                // TriangleCount ir nullable skaitlis: null nozīmē, ka skaitu nevarēja iegūt, nevis nulli trijstūru.
                // Šādu gadījumu apraksta diagnostikā un tam nepiešķir risku.
                if (!mesh.TriangleCount.HasValue)
                {
                    diagnostics.Add(new RuleDiagnostic(Id, model.AssetPath, mesh.Name + ": triangle count is unknown."));
                    continue;
                }
                // TriangleCount analizators iegūst, summējot GetIndexCount(i) / 3 tikai Triangles topoloģijas apakštīkliem.
                // GetTopology(i) atšķir topoloģijas; izmantotais GetIndexCount(i) attiecas uz LOD0.
                // Atradni veido tikai pēc pirmās robežas pārsniegšanas; Low rezultātus šis noteikums neveido.
                if (mesh.TriangleCount.Value <= _threshold) continue;
                var warnings = ModelRulePolicy.Warnings(context);
                // Ja tīklā ir arī līnijas vai cita topoloģija, trijstūru kopsumma šīs primitīvu grupas neietver.
                if (!mesh.TriangleTopologyOnly) warnings.Add("Only triangle-topology submeshes are included; other primitive types are not counted as triangles.");
                // Virs pirmās robežas līdz otrajai ieskaitot piešķir Medium; virs otrās piešķir High.
                // Mērījumi pamato rekomendāciju pārskatīt ģeometriju; automātisks labojums netiek piedāvāts.
                findings.Add(new RuleResult(Id, ModelRulePolicy.Resource(model, mesh), RiskThresholds.Evaluate(mesh.TriangleCount.Value, _threshold, _highThreshold),
                    "Mesh triangle count exceeds threshold", $"{mesh.Name} contains {mesh.TriangleCount.Value} triangles at LOD0.",
                    "Review the source geometry and the required level of detail. The mesh is not simplified automatically.",
                    new[] { new RuleMeasurement("LOD0 triangles", mesh.TriangleCount.Value, "triangles"),
                        new RuleMeasurement("Threshold", _threshold, "triangles") }, warnings));
            }
            // Atgriež atradnes un atsevišķi norāda tīklus, kuriem trūkst derīga trijstūru skaita.
            return new RuleEvaluation(findings, diagnostics);
        }
    }
}
