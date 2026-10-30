using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules
{
    // AUD005: atlasa lielus audio klipus bez Streaming un sagatavo straumēšanas ieslēgšanas piedāvājumu.
    // IRule saskarne ļauj noteikumu izpildīt kopīgajā noteikumu dzinējā.
    // internal ierobežo piekļuvi ar šo C# kompilācijas vienību (assembly); sealed nepieļauj mantošanu.
    // Unity resursu datus iepriekš apkopo analizators; šī klase vērtē saņemtos faktus.
    internal sealed class Aud005StreamingRule : IRule<AudioFacts, AudioAnalysisContext>
    {
        // Norāda, vai lietotājs ir ieslēdzis noteikumu.
        // readonly lauku vērtības piešķir konstruktorā un vēlāk nemaina.
        private readonly bool _enabled;
        // Abas robežvērtības ir baitos, tādēļ tās tieši salīdzina ar bytes saņemto apjoma novērtējumu.
        private readonly long _threshold, _highThreshold;
        // Pastāvīgs noteikuma identifikators atradnēm, diagnostikai un pārskatiem.
        public string Id => "AUD005";
        // No saņemtajiem iestatījumiem nokopē analīzei nepieciešamās vērtības.
        // Turpmākas iestatījumu objekta izmaiņas šīs instances saglabātās vērtības nemaina.
        public Aud005StreamingRule(Aud005Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _enabled = settings.Enabled; _threshold = settings.RuntimeSizeThresholdBytes; _highThreshold = settings.RuntimeSizeHighThresholdBytes;
            // Robežvērtībām jābūt pozitīvām, un High robežai jābūt lielākai par Medium robežu.
            if (_enabled) RiskThresholds.Validate(_threshold, _highThreshold);
        }

        // facts satur importa režīmu, klipa parametrus un analizatora iegūtus datus atmiņas novērtējumam.
        // context satur šīs analīzes kontekstu, piemēram, informāciju par mērķplatformu.
        // Rezultāts satur atradnes un, ja vajadzīgs, diagnostiku par nepabeigtām pārbaudēm.
        // Labojuma piedāvājums vēl nemaina resursu: to piemēro atsevišķā darbībā pēc apstiprinājuma.
        public RuleEvaluation Evaluate(IReadOnlyList<AudioFacts> facts, AudioAnalysisContext context)
        {
            var findings = new List<RuleResult>(); var diagnostics = new List<RuleDiagnostic>();
            // Izslēgts noteikums neatgriež ne atradnes, ne diagnostikas ierakstus.
            if (!_enabled) return new RuleEvaluation();
            foreach (var clip in facts)
            {
                // Ja straumēšana jau ir ieslēgta, šī noteikuma ieteiktā izmaiņa nav vajadzīga.
                if (clip.SampleSettings.loadType == AudioClipLoadType.Streaming) continue;
                // Skaitliskā novērtējuma dati jāsasaista ar atbilstošu mērķplatformas importu.
                // Citas platformas mērījumus neizmanto šīs platformas riska piešķiršanai.
                if (!AudioRulePolicy.RequireMatchingImport(Id, clip, context, diagnostics)) continue;
                // Pakotnes palīgmetode aprēķinu veic analīzes laikā, neatskaņojot klipu.
                // Tā izmanto Project Auditor 1.0.2 modeli vienai vienlaicīgai atskaņošanas instancei:
                // DecompressOnLoad: ProfilerSizeBytes + Samples * Channels * sizeof(float);
                // CompressedInMemory: ProfilerSizeBytes + ImportedSizeBytes + floor(0,4 * Frequency * Channels * 4).
                // ProfilerSizeBytes analizators nolasa ar Profiler.GetRuntimeMemorySizeLong(clip).
                // ImportedSizeBytes atsevišķs datu lasītājs iegūst no AudioImporter iekšējās compSize īpašības.
                // 0,4 sekundes ir modeļa bufera ilgums, nevis izmērīts buferis; 4 baiti ir float izmērs.
                // Šī summa ir izvēlētās metodes novērtējums, nevis apstiprināts precīzs RAM mērījums.
                // No Profiler metodes apraksta vien nevar pierādīt, ka visi summas locekļi savstarpēji nepārklājas.
                // TryEstimate atgriež izdošanos; out bytes saņem rezultātu, bet out issue apraksta neveiksmi.
                // Ja aprēķins neizdodas, izveido diagnostiku un klipam šajā noteikumā neveido atradni.
                if (!AudioRuntimeSizeEstimator.TryEstimate(clip, out long bytes, out string issue))
                { diagnostics.Add(new RuleDiagnostic(Id, clip.AssetPath, issue)); continue; }
                // Atlase prasa pirmās robežas stingru pārsniegšanu; vienādība un mazāks novērtējums atradni nerada.
                if (bytes <= _threshold) continue;
                // Brīdinājumi atšķir aprēķina modeli no faktiskās RAM un garantēta ietaupījuma.
                // Streaming izvēle var radīt papildu diska, buferizācijas un dekodēšanas darbu.
                var warnings = AudioRulePolicy.Warnings(clip, context);
                warnings.Add("Project Auditor 1.0.2 estimate for one playback instance, not measured build RAM or guaranteed savings. The float-based calculation does not identify the imported PCM bit depth.");
                warnings.Add("Streaming introduces storage reads, buffering and decoding work. Review playback requirements and direct AudioClip.GetData access before changing Load Type.");
                var resource = AudioRulePolicy.Resource(clip);
                // Līdz otrajai robežai ieskaitot risks ir Medium; virs tās High.
                // Mērījumos saglabā novērtējumu un pieņēmumu par vienu atskaņošanas instanci.
                // AudioLoadTypeProposal sagatavo tikai pašreizējā Load Type nomaiņu uz Streaming.
                // Apstiprinātā labojuma izpildītājs vēlāk piemēro importa iestatījumus un veic atkārtotu importēšanu.
                findings.Add(new RuleResult(Id, resource, RiskThresholds.Evaluate(bytes, _threshold, _highThreshold), "Large audio clip is not streaming",
                    $"Load Type: {AudioRulePolicy.LoadTypeName(clip.SampleSettings.loadType)}. The estimated runtime size exceeds the configured threshold.",
                    "Consider Streaming and review storage access, playback latency and decoding load.",
                    new[] { new RuleMeasurement("Estimated runtime size (Project Auditor 1.0.2)", bytes, "bytes"),
                        new RuleMeasurement("Threshold", _threshold, "bytes"), new RuleMeasurement("Playback instances assumed", 1, "instance") },
                    warnings, new AudioLoadTypeProposal(resource, clip.SampleSettings.loadType, AudioClipLoadType.Streaming)));
            }
            // Atgriež lielo klipu atradnes un aprēķinu diagnostiku; šeit netiek sākta audio atskaņošana vai straumēšana.
            return new RuleEvaluation(findings, diagnostics);
        }
    }
}
