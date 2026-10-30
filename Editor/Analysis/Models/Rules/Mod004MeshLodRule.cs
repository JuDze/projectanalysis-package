using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Models.Rules
{
    // MOD004: atrod detalizētus tīklus bez konstatētiem LOD un izvērtē Mesh LOD ģenerēšanas piedāvājumu.
    // IRule saskarne ļauj noteikumu izpildīt kopīgajā noteikumu dzinējā.
    // internal ierobežo piekļuvi ar šo C# kompilācijas vienību (assembly); sealed nepieļauj mantošanu.
    // Unity resursu datus iepriekš apkopo analizators; šī klase vērtē saņemtos faktus.
    internal sealed class Mod004MeshLodRule : IRule<ModelFacts, ModelAnalysisContext>
    {
        // Unity tehniskais minimums ir neatkarīgs no lietotāja atlases robežvērtības.
        private const int MinimumGenerationTriangleCount = 256;
        // Norāda, vai lietotājs ir ieslēdzis noteikumu.
        // readonly lauku vērtības piešķir konstruktorā un vēlāk nemaina.
        private readonly bool _enabled;
        // Abas trijstūru robežvērtības saņem no MOD002 iestatījumiem; atsevišķas MOD004 robežas nav.
        private readonly long _triangleThreshold, _highTriangleThreshold;
        // Pastāvīgs noteikuma identifikators atradnēm, diagnostikai un pārskatiem.
        public string Id => "MOD004";
        // No saņemtajiem iestatījumiem nokopē analīzei nepieciešamās vērtības.
        // Turpmākas iestatījumu objekta izmaiņas šīs instances saglabātās vērtības nemaina.
        public Mod004MeshLodRule(Mod004Settings settings, long triangleThreshold, long highTriangleThreshold)
        {
            _enabled = (settings ?? throw new ArgumentNullException(nameof(settings))).Enabled;
            if (_enabled) RiskThresholds.Validate(triangleThreshold, highTriangleThreshold);
            // Reģistrs nodod MOD002 vērtības kopiju; noteikums nav atkarīgs no MOD002 izpildes.
            _triangleThreshold = triangleThreshold;
            _highTriangleThreshold = highTriangleThreshold;
        }

        // facts satur tīklu LOD0 trijstūrus, Mesh LOD skaitu, zināmos LODGroup lietojumus un importētāja ierobežojumus.
        // context satur šīs analīzes kontekstu, piemēram, informāciju par mērķplatformu.
        // Rezultāts satur atradnes un, ja vajadzīgs, diagnostiku par nepabeigtām pārbaudēm.
        // Labojuma piedāvājums vēl nemaina resursu: to piemēro atsevišķā darbībā pēc apstiprinājuma.
        public RuleEvaluation Evaluate(IReadOnlyList<ModelFacts> facts, ModelAnalysisContext context)
        {
            var findings = new List<RuleResult>();
            var diagnostics = new List<RuleDiagnostic>();
            // Izslēgts noteikums neatgriež ne atradnes, ne diagnostikas ierakstus.
            if (!_enabled) return new RuleEvaluation();
            foreach (var model in facts)
            {
                // Par trūkstošu trijstūru skaitu izveido diagnostiku; citi šīs datnes tīkli joprojām var būt kandidāti.
                foreach (var mesh in model.Meshes.Where(mesh => !mesh.TriangleCount.HasValue))
                    diagnostics.Add(new RuleDiagnostic(Id, model.AssetPath, mesh.Name + ": LOD review requires a known triangle count."));

                // Atlasei jāizpildās visiem nosacījumiem: zināms skaits virs pirmās MOD002 robežas,
                // nav papildu Mesh LOD līmeņu un analizators nav atradis tīklu konfigurētā LODGroup.
                // LodCount nāk no mesh.lodCount; LODGroup saistības analizators apkopo izvēlētajos modeļos un sagatavēs.
                // ToArray saglabā atlasītos tīklus, jo tālāk šo kopu izmanto vairākās pārbaudēs.
                var candidates = model.Meshes.Where(mesh => mesh.TriangleCount.HasValue &&
                    mesh.TriangleCount.Value > _triangleThreshold &&
                    mesh.LodCount <= 1 && !mesh.HasKnownLodGroup).ToArray();
                if (candidates.Length == 0) continue;

                // Piedāvājumam vajadzīgs vismaz viens kandidāts ar tikai trijstūru topoloģiju un vismaz 256 trijstūriem.
                // Any pārbauda vismaz viena elementa atbilstību; pārējiem neatbilstošajiem kandidātiem pievieno brīdinājumus.
                bool supported = candidates.Any(mesh => mesh.TriangleTopologyOnly &&
                    mesh.TriangleCount.Value >= MinimumGenerationTriangleCount);
                // Pārskata visus datnes tīklus, ne tikai kandidātus, jo ģenerēšanas slēdzis ietekmē visu importētāju.
                bool conflict = model.Meshes.Any(mesh => mesh.HasKnownLodGroup);
                // Ierobežojumu pārbaudei jābūt zināmam gan maksimālajam līmenim, gan nepāra līmeņu atmešanas iestatījumam.
                bool limitsKnown = model.MaximumMeshLod.HasValue && model.DiscardOddLodLevels.HasValue;
                // Ja nepāra līmeņus atmet, pirmais saglabājamais vienkāršotais līmenis ir LOD2.
                int firstSimplifiedLevel = model.DiscardOddLodLevels == true ? 2 : 1;
                // Negatīvs MaximumMeshLod nozīmē, ka līmeņu indekss nav ierobežots.
                // Citādi robežai jāļauj saglabāt vismaz LOD1 vai, atmetot nepāra līmeņus, LOD2.
                bool limitsAllowGeneration = limitsKnown &&
                    (model.MaximumMeshLod.Value < 0 || model.MaximumMeshLod.Value >= firstSimplifiedLevel);
                // Zināmais LODGroup lietojums neaptver scēnas un izpildes laikā veidotus objektus.
                // Brīdinājumos norāda nepilnīgu pārbaudi, visas datnes ietekmi un iespējamas kvalitātes izmaiņas.
                var warnings = ModelRulePolicy.Warnings(context);
                warnings.Add("LODGroup usage is checked only in the selected model and prefab assets. Scenes and runtime-created uses are not inspected.");
                warnings.Add("Generation affects the model file. Review visual artifacts, especially skinned or blend-shape deformation. No frame-time or memory saving is guaranteed.");
                warnings.Add("Some rendering paths, including static batching and ordinary GPU instancing, use LOD0 instead of automatic Mesh LOD selection.");
                if (!context.LodContextComplete) warnings.Add("Some selected hierarchies could not be inspected; the LODGroup check is incomplete.");
                if (conflict) warnings.Add("Another mesh in this file already participates in a known LODGroup. Enabling generation for the whole importer may conflict with it.");
                if (candidates.Any(mesh => !mesh.TriangleTopologyOnly))
                    warnings.Add("Meshes with non-triangle topology cannot use Mesh LOD generation.");
                if (candidates.Any(mesh => mesh.TriangleCount.Value < MinimumGenerationTriangleCount))
                    warnings.Add("Meshes below 256 triangles do not meet Unity's technical minimum for Mesh LOD generation.");

                // Sākumā paredz tikai rekomendāciju; priekšlikumu piešķir vienīgi pēc visu atļaujošo nosacījumu pārbaudes.
                IChangeProposal proposal = null;
                string recommendation;
                string title = "Review mesh LOD generation";
                // Šīs pārbaudes ir sakārtotas prioritārā secībā: pirmais šķērslis nosaka galveno rekomendāciju.
                // Bez importētāja, atbalstīta tīkla vai derīgiem ierobežojumiem automātisku ieslēgšanu nepiedāvā.
                if (!model.HasModelImporter)
                    recommendation = "This mesh asset has no ModelImporter. Review LOD authoring in its source or creation workflow.";
                else if (!supported)
                    recommendation = "Review the source geometry. Automatic Mesh LOD generation requires triangle topology and at least 256 triangles per mesh.";
                else if (!limitsKnown)
                    recommendation = "The generation limits are unknown. Review Maximum Levels and Discard Odd Levels in the model importer before enabling generation.";
                else if (!limitsAllowGeneration)
                {
                    title = "Generation limits prevent simplified Mesh LODs";
                    recommendation = "Review Maximum Levels and Discard Odd Levels: the current combination leaves no simplified level. " +
                        "Allow LOD" + firstSimplifiedLevel + " or higher, or remove the level limit, then run analysis again. Generation limits are not changed automatically.";
                }
                // Ja ģenerēšana jau ir ieslēgta, to atkārtoti ieslēgt nav jēgas; jāizvērtē esošais importa rezultāts.
                else if (model.GenerateMeshLods == true)
                {
                    title = "No additional Mesh LOD levels were generated";
                    recommendation = "Generate Mesh LODs is already enabled. Review the affected meshes and import result.";
                }
                // Zināms LODGroup citam tīklam vai nepilnīgi nolasītas hierarhijas bloķē kopīgā iestatījuma piedāvājumu.
                else if (conflict || !context.LodContextComplete)
                    recommendation = "Review existing LODGroup usage before considering a shared importer change.";
                // Tikai skaidri izslēgtam Generate Mesh LODs pēc iepriekšējām pārbaudēm sagatavo ieslēgšanas priekšlikumu.
                // Maximum Levels un Discard Odd Levels netiek mainīti.
                else if (model.GenerateMeshLods == false)
                {
                    recommendation = "Consider enabling Generate Mesh LODs for eligible meshes, then inspect the imported result and visual quality. Maximum Levels and Discard Odd Levels remain unchanged.";
                    proposal = new ModelLodGenerationProposal();
                }
                else recommendation = "The generation setting is unknown. Review the model importer.";

                // Aprakstā uzrāda izmantoto robežu un kandidātu nosaukumus, lai lietotājs saprastu atlases pamatojumu.
                // InvariantCulture nodrošina vienādu skaitļa pierakstu neatkarīgi no redaktora valodas iestatījumiem.
                string description = "Meshes above the MOD002 threshold of " +
                    _triangleThreshold.ToString(CultureInfo.InvariantCulture) +
                    " LOD0 triangles without additional Mesh LODs or a matching LODGroup in the inspected assets: " +
                    string.Join(", ", candidates.Select(mesh => mesh.Name)) + ".";
                if (model.HasModelImporter)
                    description += " Maximum generated LOD: " +
                        (!model.MaximumMeshLod.HasValue ? "Unknown" : model.MaximumMeshLod.Value < 0 ? "No limit" : "LOD" + model.MaximumMeshLod.Value) +
                        "; Discard Odd Levels: " + (!model.DiscardOddLodLevels.HasValue ? "Unknown" : model.DiscardOddLodLevels.Value ? "On" : "Off") + ".";
                // Viena datne saņem vienu priekšlikumu, pat ja tai ir vairāki kandidāti.
                // Risks balstās lielākajā kandidāta trijstūru skaitā, nevis visu tīklu summā.
                // Pārsniegta pirmā robeža dod Medium, bet otrā dod High; vienādība ar otro paliek Medium.
                // Select sagatavo atsevišķus mērījumus un saistītos tīklus vienas datnes atradnei.
                findings.Add(new RuleResult(Id, ModelRulePolicy.Resource(model),
                    RiskThresholds.Evaluate(candidates.Max(mesh => mesh.TriangleCount.Value), _triangleThreshold, _highTriangleThreshold),
                    title, description, recommendation,
                    candidates.Select(mesh => new RuleMeasurement(mesh.Name + " LOD0 triangles", mesh.TriangleCount, "triangles")),
                    warnings, proposal, candidates.Select(mesh => ModelRulePolicy.Resource(model, mesh))));
            }
            // Atradne var saturēt tikai rekomendāciju vai arī apstiprināmu labojuma priekšlikumu.
            return new RuleEvaluation(findings, diagnostics);
        }
    }
}
