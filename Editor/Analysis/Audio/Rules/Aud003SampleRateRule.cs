using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules
{
    // AUD003: izvērtē paaugstinātu audio diskretizācijas frekvenci un atbalstītos gadījumos piedāvā Optimize Sample Rate.
    // IRule saskarne ļauj noteikumu izpildīt kopīgajā noteikumu dzinējā.
    // internal ierobežo piekļuvi ar šo C# kompilācijas vienību (assembly); sealed nepieļauj mantošanu.
    // Unity resursu datus iepriekš apkopo analizators; šī klase vērtē saņemtos faktus.
    internal sealed class Aud003SampleRateRule : IRule<AudioFacts, AudioAnalysisContext>
    {
        // Norāda, vai lietotājs ir ieslēdzis noteikumu.
        // readonly lauku vērtības piešķir konstruktorā un vēlāk nemaina.
        private readonly bool _enabled;
        // Robežvērtības ir hercos un nosaka risku; tās nenosaka importēšanai obligātu rezultāta frekvenci.
        private readonly int _threshold, _highThreshold;
        // Pastāvīgs noteikuma identifikators atradnēm, diagnostikai un pārskatiem.
        public string Id => "AUD003";
        // No saņemtajiem iestatījumiem nokopē analīzei nepieciešamās vērtības.
        // Turpmākas iestatījumu objekta izmaiņas šīs instances saglabātās vērtības nemaina.
        public Aud003SampleRateRule(Aud003Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _enabled = settings.Enabled; _threshold = settings.SampleRateThresholdHz; _highThreshold = settings.SampleRateHighThresholdHz;
            // Pārbauda pozitīvas un augošas frekvences robežvērtības.
            if (_enabled) RiskThresholds.Validate(_threshold, _highThreshold);
        }

        // facts satur no AudioClip.frequency iegūto frekvenci un efektīvos audio importa iestatījumus.
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
                // Clip frekvenci drīkst attiecināt uz mērķplatformu tikai tad, ja konteksts apstiprina atbilstošu importu.
                if (!AudioRulePolicy.RequireMatchingImport(Id, clip, context, diagnostics)) continue;
                // Nederīga frekvence dod diagnostiku; to nesalīdzina ar sliekšņiem kā reālu mērījumu.
                if (clip.Frequency <= 0)
                { diagnostics.Add(new RuleDiagnostic(Id, clip.AssetPath, "The imported clip frequency is unavailable.")); continue; }
                // Atradni veido tikai pēc pirmās robežas pārsniegšanas, tādēļ Low atradnes šeit netiek veidotas.
                if (clip.Frequency <= _threshold) continue;
                var settings = clip.SampleSettings;
                // Automātisko optimizācijas piedāvājumu šajā noteikumā atbalsta tikai PCM un ADPCM.
                bool supportsOptimize = settings.compressionFormat == AudioCompressionFormat.PCM || settings.compressionFormat == AudioCompressionFormat.ADPCM;
                // Režīma maiņai jābūt iespējamai: pašreiz ir PreserveSampleRate vai OverrideSampleRate.
                // Ja OptimizeSampleRate jau darbojas, to atkārtoti ieslēgt nepiedāvā.
                bool changeMode = settings.sampleRateSetting == AudioSampleRateSetting.PreserveSampleRate ||
                    settings.sampleRateSetting == AudioSampleRateSetting.OverrideSampleRate;
                var resource = AudioRulePolicy.Resource(clip);
                var warnings = AudioRulePolicy.Warnings(clip, context);
                // Piedāvājums pārslēdz režīmu; tas neiestata sampleRateOverride vienādu ar riska robežvērtību.
                // Pēc piemērošanas frekvenci izvēlas Unity, analizējot skaņas saturu.
                warnings.Add("Unity chooses the optimized frequency from the audio content. The threshold is not a promised output frequency; listen for changes in sound quality.");
                // Arī bez piemērojama labojuma frekvences pārsniegums paliek atradne ar manuālu rekomendāciju.
                string recommendation = !supportsOptimize ? "Review the clip's import settings manually; automatic sample-rate optimization is offered here only for PCM and ADPCM."
                    : !changeMode ? "Review the import result: no new Optimize Sample Rate change is available for the current mode."
                    : "Consider Optimize Sample Rate and review the resulting frequency and sound quality.";
                // Risks ir Medium līdz otrajai robežai ieskaitot un High virs tās.
                // AudioSampleRateProposal izveido tikai tad, ja atbalstīts gan formāts, gan pašreizējais režīms.
                findings.Add(new RuleResult(Id, resource, RiskThresholds.Evaluate((long)clip.Frequency, _threshold, _highThreshold), "Audio sample rate exceeds the threshold",
                    $"Imported frequency: {clip.Frequency} Hz. Standalone sample-rate mode: {settings.sampleRateSetting}.", recommendation,
                    new[] { new RuleMeasurement("Imported frequency", clip.Frequency, "Hz"), new RuleMeasurement("Threshold", _threshold, "Hz") },
                    warnings, supportsOptimize && changeMode ? new AudioSampleRateProposal(resource, settings.sampleRateSetting) : null));
            }
            // Atgriež frekvences atradnes un diagnostiku par nederīgiem vai citas platformas datiem.
            return new RuleEvaluation(findings, diagnostics);
        }
    }
}
