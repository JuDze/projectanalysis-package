using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Textures.Rules
{
    // TEX002: izvērtē ieslēgtu tekstūras Read/Write un sagatavo tā izslēgšanas piedāvājumu.
    // IRule saskarne ļauj noteikumu izpildīt kopīgajā noteikumu dzinējā.
    // internal ierobežo piekļuvi ar šo C# kompilācijas vienību (assembly); sealed nepieļauj mantošanu.
    // Unity resursu datus iepriekš apkopo analizators; šī klase vērtē saņemtos faktus.
    internal sealed class Tex002ReadWriteRule : IRule<TextureFacts, TextureAnalysisContext>
    {
        // Norāda, vai lietotājs ir ieslēdzis noteikumu.
        // readonly lauku vērtības piešķir konstruktorā un vēlāk nemaina.
        private readonly bool _enabled;
        // Abas CPU datu apjoma robežvērtības ir MiB; viens MiB ir 1 048 576 baiti.
        private readonly double _threshold, _highThreshold;
        // Pastāvīgs noteikuma identifikators atradnēm, diagnostikai un pārskatiem.
        public string Id => "TEX002";
        // No saņemtajiem iestatījumiem nokopē analīzei nepieciešamās vērtības.
        // Turpmākas iestatījumu objekta izmaiņas šīs instances saglabātās vērtības nemaina.
        public Tex002ReadWriteRule(Tex002Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _enabled = settings.Enabled; _threshold = settings.CpuDataThresholdMiB; _highThreshold = settings.CpuDataHighThresholdMiB;
            // Pārbauda pozitīvas, galīgas robežvērtības un to augošo secību.
            if (_enabled) RiskThresholds.Validate(_threshold, _highThreshold);
        }
        // facts satur analizatora nolasīto Read/Write stāvokli un CPU pieejamo tekstūras datu apjomu.
        // context satur šīs analīzes kontekstu, piemēram, informāciju par mērķplatformu.
        // Rezultāts satur atradnes un, ja vajadzīgs, diagnostiku par nepabeigtām pārbaudēm.
        // Labojuma piedāvājums vēl nemaina resursu: to piemēro atsevišķā darbībā pēc apstiprinājuma.
        public RuleEvaluation Evaluate(IReadOnlyList<TextureFacts> facts, TextureAnalysisContext context)
        {
            // Izslēgts noteikums neatgriež ne atradnes, ne diagnostikas ierakstus.
            if (!_enabled) return new RuleEvaluation();
            var findings = new List<RuleResult>();
            var diagnostics = new List<RuleDiagnostic>();
            foreach (var texture in facts)
            {
                // Atradnes kandidāts ir tikai resurss, kuram analizators nolasījis importer.isReadable == true.
                if (!texture.ReadWriteEnabled) continue;
                // CpuDataBytes iegūti ar texture.GetRawTextureData<byte>().Length un ietver esošos mip līmeņus.
                // Baitus pārvērš MiB; null nozīmē, ka izmērs nav izmantojams šīs platformas novērtēšanai.
                double? size = context.ImportedDataMatchesTarget ? TextureRulePolicy.MiB(texture.CpuDataBytes) : null;
                // Low: līdz pirmajai robežai ieskaitot; Medium: virs pirmās līdz otrajai ieskaitot; High: virs otrās.
                // Atšķirībā no TEX001 arī Low līmenī saglabā atradni un Read/Write izslēgšanas piedāvājumu.
                var priority = RiskThresholds.Evaluate(size, _threshold, _highThreshold);
                // Bez izmēra nevar noteikt risku; neizveido atradni vai labojuma piedāvājumu.
                if (!priority.HasValue)
                {
                    diagnostics.Add(new RuleDiagnostic(Id, texture.AssetPath, context.ImportedDataMatchesTarget
                        ? "CPU data size is unavailable; the risk was not evaluated." : TextureRulePolicy.PlatformWarning));
                    continue;
                }
                // Pikseļu datu apjoms nenosaka, vai skriptiem šie dati ir vajadzīgi.
                // Brīdinājums prasa lietotājam izvērtēt CPU piekļuvi visos lietojumos un platformās.
                var warnings = new List<string>
                {
                    "Confirm that scripts do not need to read or modify CPU pixel data. Usage is not proven by this check.",
                    "Read/Write is a shared importer setting; changing it can affect all target platforms."
                };
                // Nulle ir zināms izmērs, tādēļ piedāvājums paliek pieejams ar atsevišķu brīdinājumu.
                if (size == 0) warnings.Add("CPU data size is zero; review the setting without a memory reduction claim.");
                // Izveido tikai darbības aprakstu Read/Write: On -> Off; šeit neiestata importer.isReadable.
                var proposal = new TextureReadWriteProposal();
                findings.Add(new RuleResult(Id, TextureRulePolicy.Resource(texture),
                    priority.Value, "Texture Read/Write is enabled",
                    "A CPU-accessible copy is enabled. Size includes the existing mip levels when available.",
                    proposal.Summary, new[] { new RuleMeasurement("CPU-accessible texture data", size, "MiB") },
                    warnings, proposal));
            }
            // Atgriež sagatavotās atradnes un atsevišķus pārbaudes diagnostikas ierakstus.
            return new RuleEvaluation(findings, diagnostics);
        }
    }
}
