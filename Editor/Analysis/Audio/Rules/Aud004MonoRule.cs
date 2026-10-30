using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules
{
    // AUD004: daudzkanālu audio bez Force To Mono un Ambisonic piedāvā izvērtēt pārveidi uz mono.
    // IRule saskarne ļauj noteikumu izpildīt kopīgajā noteikumu dzinējā.
    // internal ierobežo piekļuvi ar šo C# kompilācijas vienību (assembly); sealed nepieļauj mantošanu.
    // Unity resursu datus iepriekš apkopo analizators; šī klase vērtē saņemtos faktus.
    internal sealed class Aud004MonoRule : IRule<AudioFacts, AudioAnalysisContext>
    {
        // Norāda, vai lietotājs ir ieslēdzis noteikumu.
        // readonly lauku vērtības piešķir konstruktorā un vēlāk nemaina.
        private readonly bool _enabled;
        // Pastāvīgs noteikuma identifikators atradnēm, diagnostikai un pārskatiem.
        public string Id => "AUD004";
        // No saņemtajiem iestatījumiem nokopē analīzei nepieciešamās vērtības.
        // Turpmākas iestatījumu objekta izmaiņas šīs instances saglabātās vērtības nemaina.
        public Aud004MonoRule(Aud004Settings settings)
        { _enabled = (settings ?? throw new ArgumentNullException(nameof(settings))).Enabled; }

        // facts satur importer.forceToMono, importer.ambisonic un clip.channels vērtības no analizatora.
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
                // Jau ieslēgtam Force To Mono nepiedāvā to pašu izmaiņu vēlreiz.
                // Ambisonic nozīmē īpašu telpiskās skaņas kodējumu; šos resursus šis noteikums nepārveido.
                if (clip.ForceToMono || clip.Ambisonic) continue;
                // Nederīgs kanālu skaits nav pierādījums mono vai stereo formātam, tāpēc izveido diagnostiku.
                if (clip.Channels <= 0)
                { diagnostics.Add(new RuleDiagnostic(Id, clip.AssetPath, "The imported channel count is unavailable.")); continue; }
                // Atradnei nepieciešami vismaz divi kanāli; vienkanāla skaņai pārveide nav vajadzīga.
                if (clip.Channels <= 1) continue;
                // Noteikums nepārbauda AudioSource, scēnu atsauces vai atskaņošanas scenārijus.
                // Mono piemērotību nevar secināt tikai no kanālu skaita; lietotājs izvērtē kanālu nošķiršanas nepieciešamību.
                var warnings = AudioRulePolicy.Warnings(clip, context);
                warnings.Add("Multiple channels do not prove that mono is suitable. This check does not inspect AudioSource, scene references or runtime usage.");
                warnings.Add("Mono conversion removes channel separation and affects all uses of this clip across platforms. User review and listening are required.");
                var resource = AudioRulePolicy.Resource(clip);
                // Šai pārbaudei ir nemainīgs Low risks un nav skaitlisku robežvērtību.
                // AudioMonoProposal paredz ieslēgt kopīgo importētāja Force To Mono iestatījumu pēc apstiprinājuma.
                // Tas ietekmē visus klipa lietojumus un platformas, tāpēc brīdinājumā prasīta rezultāta noklausīšanās.
                findings.Add(new RuleResult(Id, resource, RiskLevel.Low, "Review multichannel audio for mono conversion",
                    "Force To Mono: Off. Ambisonic: Off.", "Consider mono only when channel separation is unnecessary for this clip.",
                    new[] { new RuleMeasurement("Imported channels", clip.Channels, "channels") }, warnings, new AudioMonoProposal(resource)));
            }
            // Atgriež atlasītos daudzkanālu klipus un diagnostiku par nenolasāmu kanālu skaitu.
            return new RuleEvaluation(findings, diagnostics);
        }
    }
}
