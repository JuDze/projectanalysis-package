using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Settings;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio.Rules
{
    // AUD002: PCM resursiem piedāvā lietotāja izvēlētu ADPCM vai Vorbis saspiešanu.
    // IRule saskarne ļauj noteikumu izpildīt kopīgajā noteikumu dzinējā.
    // internal ierobežo piekļuvi ar šo C# kompilācijas vienību (assembly); sealed nepieļauj mantošanu.
    // Unity resursu datus iepriekš apkopo analizators; šī klase vērtē saņemtos faktus.
    internal sealed class Aud002CompressionRule : IRule<AudioFacts, AudioAnalysisContext>
    {
        // Norāda, vai lietotājs ir ieslēdzis noteikumu.
        // readonly lauku vērtības piešķir konstruktorā un vēlāk nemaina.
        private readonly bool _enabled;
        // Mērķa formātu izvēlas lietotājs; NotSelected nozīmē, ka konkrēts labojums vēl nav izvēlēts.
        private readonly AudioCompressionChoice _format;
        // Vorbis kvalitāte ir vērtība no 0 līdz 1; ADPCM priekšlikumam šo parametru nepiemēro.
        private readonly float _quality;
        // Pastāvīgs noteikuma identifikators atradnēm, diagnostikai un pārskatiem.
        public string Id => "AUD002";
        // No saņemtajiem iestatījumiem nokopē analīzei nepieciešamās vērtības.
        // Turpmākas iestatījumu objekta izmaiņas šīs instances saglabātās vērtības nemaina.
        public Aud002CompressionRule(Aud002Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _enabled = settings.Enabled; _format = settings.TargetFormat; _quality = settings.VorbisQuality;
            // Noraida nezināmu izvēles enum vērtību, lai nejauši nepiedāvātu citu formātu.
            if (_enabled && !Enum.IsDefined(typeof(AudioCompressionChoice), _format))
                throw new ArgumentOutOfRangeException(nameof(settings.TargetFormat));
            // Vorbis kvalitātei jābūt galīgam skaitlim atļautajā intervālā; NaN un bezgalība nav derīgas vērtības.
            if (_enabled && _format == AudioCompressionChoice.Vorbis &&
                (float.IsNaN(_quality) || float.IsInfinity(_quality) || _quality < 0 || _quality > 1))
                throw new ArgumentOutOfRangeException(nameof(settings.VorbisQuality));
        }

        // facts satur efektīvos Standalone audio importa iestatījumus; noteikums izmanto compressionFormat lauku.
        // context satur šīs analīzes kontekstu, piemēram, informāciju par mērķplatformu.
        // Rezultāts satur atradnes un, ja vajadzīgs, diagnostiku par nepabeigtām pārbaudēm.
        // Labojuma piedāvājums vēl nemaina resursu: to piemēro atsevišķā darbībā pēc apstiprinājuma.
        public RuleEvaluation Evaluate(IReadOnlyList<AudioFacts> facts, AudioAnalysisContext context)
        {
            var findings = new List<RuleResult>();
            // Izslēgts noteikums neatgriež ne atradnes, ne diagnostikas ierakstus.
            if (!_enabled) return new RuleEvaluation();
            foreach (var clip in facts)
            {
                // Atlasa tikai PCM; šis noteikums neveic jau saspiestu formātu savstarpēju nomaiņu.
                if (clip.SampleSettings.compressionFormat != AudioCompressionFormat.PCM) continue;
                var resource = AudioRulePolicy.Resource(clip);
                // Atradni var parādīt arī bez izvēlēta mērķa formāta; null nozīmē, ka konkrēts labojums nav sagatavots.
                IChangeProposal proposal = null;
                // Ja formāts ir izvēlēts, sagatavo PCM -> ADPCM vai PCM -> Vorbis pāreju.
                // Vorbis saņem _quality; ADPCM saņem null, jo tam šāds kvalitātes parametrs šeit nav piemērojams.
                // Skaņas saturu neklasificē automātiski: piemērotību izvērtē lietotājs.
                if (_format != AudioCompressionChoice.NotSelected)
                    proposal = new AudioCompressionProposal(resource, AudioCompressionFormat.PCM,
                        _format == AudioCompressionChoice.ADPCM ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.Vorbis,
                        _format == AudioCompressionChoice.Vorbis ? _quality : (float?)null);
                // Saspiešana var mainīt kvalitāti, bet RAM un CPU ietekme ir atkarīga arī no ielādes režīma.
                var warnings = AudioRulePolicy.Warnings(clip, context);
                warnings.Add("Compression may reduce sound quality. Decoding costs and RAM usage depend on Load Type; no resulting size or savings are guaranteed.");
                // PCM konstatējumam vienmēr piešķir Low; skaitlisku robežvērtību un ietaupījuma aprēķina nav.
                // Rekomendācija atšķiras atkarībā no tā, vai lietotājs jau ir izvēlējies saspiešanas formātu.
                findings.Add(new RuleResult(Id, resource, RiskLevel.Low, "Audio uses PCM compression settings",
                    $"Standalone Compression Format: PCM. Load Type: {AudioRulePolicy.LoadTypeName(clip.SampleSettings.loadType)}.",
                    proposal == null ? "Choose ADPCM or Vorbis in Rules to prepare a specific compression proposal."
                        : "Review the chosen compression format and listen to the result when a change is applied.",
                    warnings: warnings, proposal: proposal));
            }
            // Atgriež PCM resursu atradnes; pats noteikums importa iestatījumus nesaglabā.
            return new RuleEvaluation(findings);
        }
    }
}
