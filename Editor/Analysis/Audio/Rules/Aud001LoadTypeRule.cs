using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules
{
    // AUD001: piedāvā mainīt audio ielādes režīmu atbilstoši lietotāja mērķim samazināt RAM vai atskaņošanas CPU slodzi.
    // IRule saskarne ļauj noteikumu izpildīt kopīgajā noteikumu dzinējā.
    // internal ierobežo piekļuvi ar šo C# kompilācijas vienību (assembly); sealed nepieļauj mantošanu.
    // Unity resursu datus iepriekš apkopo analizators; šī klase vērtē saņemtos faktus.
    internal sealed class Aud001LoadTypeRule : IRule<AudioFacts, AudioAnalysisContext>
    {
        // Norāda, vai lietotājs ir ieslēdzis noteikumu.
        // readonly lauku vērtības piešķir konstruktorā un vēlāk nemaina.
        private readonly bool _enabled;
        // Lietotāja izvēlētais mērķis nosaka gan režīma maiņas virzienu, gan riska noteikšanas principu.
        private readonly AudioOptimizationGoal _goal;
        // RAM riska robežvērtības baitos saņem no AUD005 iestatījumiem; CPU mērķim tās neizmanto.
        private readonly long _mediumBytes, _highBytes;
        // Pastāvīgs noteikuma identifikators atradnēm, diagnostikai un pārskatiem.
        public string Id => "AUD001";
        // No saņemtajiem iestatījumiem nokopē analīzei nepieciešamās vērtības.
        // Turpmākas iestatījumu objekta izmaiņas šīs instances saglabātās vērtības nemaina.
        public Aud001LoadTypeRule(Aud001Settings settings, long mediumBytes, long highBytes)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _enabled = settings.Enabled; _goal = settings.Goal;
            _mediumBytes = mediumBytes; _highBytes = highBytes;
            // Robežvērtības validē tikai RAM mērķim, jo tikai šajā zarā tās piedalās aprēķinā.
            if (_enabled && _goal == AudioOptimizationGoal.ReduceRamUsage)
                RiskThresholds.Validate(_mediumBytes, _highBytes);
            // Enum pārbaude nepieļauj neatbalstītu izvēli, piemēram, kļūdaini izveidotos iestatījumos.
            if (_enabled && !Enum.IsDefined(typeof(AudioOptimizationGoal), _goal))
                throw new ArgumentOutOfRangeException(nameof(settings.Goal));
        }

        // facts satur AudioFacts ar analizatora nolasītiem audio importa iestatījumiem un klipa īpašībām.
        // context satur šīs analīzes kontekstu, piemēram, informāciju par mērķplatformu.
        // Rezultāts satur atradnes un, ja vajadzīgs, diagnostiku par nepabeigtām pārbaudēm.
        // Labojuma piedāvājums vēl nemaina resursu: to piemēro atsevišķā darbībā pēc apstiprinājuma.
        public RuleEvaluation Evaluate(IReadOnlyList<AudioFacts> facts, AudioAnalysisContext context)
        {
            var findings = new List<RuleResult>();
            var diagnostics = new List<RuleDiagnostic>();
            // Izslēgts noteikums neatgriež ne atradnes, ne diagnostikas ierakstus.
            if (!_enabled) return new RuleEvaluation();
            // RAM mērķis: DecompressOnLoad -> CompressedInMemory; CPU mērķis: pretējais virziens.
            // source ir vajadzīgais pašreizējais režīms, bet target ir piedāvātais režīms.
            bool reduceRam = _goal == AudioOptimizationGoal.ReduceRamUsage;
            var source = reduceRam ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
            var target = reduceRam ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            foreach (var clip in facts)
            {
                // Šī AudioImporterSampleSettings struktūra jau iegūta ar GetOverrideSampleSettings platformai Standalone.
                // Šeit lasa tās laukus, nevis atkārtoti vēršas pie Unity importētāja.
                var settings = clip.SampleSettings;
                // Pārbauda tikai izvēlētā virziena sākuma režīmu un Vorbis vai ADPCM formātu.
                // PCM un citi formāti, kā arī Streaming šajā priekšlikumā netiek iekļauti.
                if (settings.loadType != source ||
                    (settings.compressionFormat != AudioCompressionFormat.Vorbis && settings.compressionFormat != AudioCompressionFormat.ADPCM)) continue;
                // Brīdinājumos paskaidro kompromisu: mazāka RAM var prasīt vairāk CPU darba un otrādi.
                var warnings = AudioRulePolicy.Warnings(clip, context);
                warnings.Add(reduceRam
                    ? "Playback decompression may increase CPU load. Confirm that direct sample access through AudioClip.GetData is not required."
                    : "Decompression during loading may increase loading work and RAM usage.");
                // CPU mērķim vērtē kodeku, nevis klipa kopējo datu apjomu.
                // CPU mērķim ADPCM piešķir Low, bet Vorbis Medium; tā nav izmērīta procesora noslodze.
                // RAM mērķim šo sākotnējo vērtību zemāk aizstāj ar apjoma robežvērtību rezultātu.
                RiskLevel priority = settings.compressionFormat == AudioCompressionFormat.ADPCM ? RiskLevel.Low : RiskLevel.Medium;
                var measurements = new List<RuleMeasurement>();
                if (reduceRam)
                {
                    // RAM riska aprēķinam vajadzīgs atbilstošās platformas izmēra novērtējums.
                    if (!AudioRulePolicy.RequireMatchingImport(Id, clip, context, diagnostics)) continue;
                    // Palīgmetode izmanto Project Auditor 1.0.2 modeli, neatskaņojot audio.
                    // bool norāda aprēķina izdošanos; out bytes saņem novērtējumu baitos, out issue saņem neveiksmes iemeslu.
                    // Neveiksmīgs aprēķins dod diagnostiku un izlaiž atradni, nevis pieņem nulles patēriņu.
                    if (!AudioRuntimeSizeEstimator.TryEstimate(clip, out long bytes, out string issue))
                    {
                        diagnostics.Add(new RuleDiagnostic(Id, clip.AssetPath, issue));
                        continue;
                    }
                    // Low ir līdz pirmajai robežai ieskaitot, Medium līdz otrajai ieskaitot, High virs otrās.
                    // Šajā noteikumā arī Low novērtējums rada atradni; novērtējums neizsaka garantētu ietaupījumu.
                    priority = RiskThresholds.Evaluate(bytes, _mediumBytes, _highBytes);
                    measurements.Add(new RuleMeasurement("Medium threshold", _mediumBytes, "bytes"));
                    measurements.Add(new RuleMeasurement("High threshold", _highBytes, "bytes"));

                }
                var resource = AudioRulePolicy.Resource(clip);
                // AudioLoadTypeProposal saglabā resursu, sākuma režīmu un mērķa režīmu priekšskatījumam.
                // Maināmais iestatījums ir tikai Load Type; apstiprināto izmaiņu vēlāk piemēro labojumu izpildītājs.
                findings.Add(new RuleResult(Id, resource, priority, "Review audio Load Type",
                    $"Standalone format: {settings.compressionFormat}. Load Type: {AudioRulePolicy.LoadTypeName(source)}. Selected goal: {(reduceRam ? "Reduce RAM usage" : "Reduce playback CPU load")}.",
                    "Review the selected audio files before changing Load Type.",
                    measurements: measurements, warnings: warnings, proposal: new AudioLoadTypeProposal(resource, source, target)));
            }
            // Atgriež visas atlasīto klipu atradnes un neizdevušos skaitlisko pārbaužu diagnostiku.
            return new RuleEvaluation(findings, diagnostics);
        }
    }
}
