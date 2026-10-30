using System;
using UnityEngine.UIElements;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Audio formu izvēles maina tikai saskarnes tekstus un lauku redzamību.
    // Šeit netiek analizēti resursi, saglabāti iestatījumi vai piemēroti labojumi.
    internal static class AudioRuleSettingsView
    {
        public static void BindLoadType(VisualElement root)
        {
            var goal = RuleFieldBinding.Require<DropdownField>(root, "aud001-optimization-goal");
            var direction = RuleFieldBinding.Require<Label>(root, "aud001-direction-hint");
            var warning = RuleFieldBinding.Require<Label>(root, "aud001-warning-hint");

            void Refresh()
            {
                bool reduceRam = goal.index == 0;
                direction.text = reduceRam
                    ? "Decompress On Load → Compressed In Memory"
                    : "Compressed In Memory → Decompress On Load";
                warning.text = reduceRam
                    ? "Playback decompression can increase CPU load. Confirm that direct sample access through AudioClip.GetData is not required."
                    : "Decompressed audio uses more RAM. Decompression moves to loading; this does not promise less total CPU work.";
            }

            goal.RegisterValueChangedCallback(_ => Refresh());
            Refresh();
        }

        public static void BindCompression(VisualElement root)
        {
            var format = RuleFieldBinding.Require<DropdownField>(root, "aud002-target-format");
            var vorbisOptions = RuleFieldBinding.Require<VisualElement>(root, "aud002-vorbis-options");
            var hint = RuleFieldBinding.Require<Label>(root, "aud002-format-hint");

            void Refresh()
            {
                bool vorbis = format.value == "Vorbis";
                // ADPCM neizmanto kvalitātes parametru; Vorbis melnraksta vērtību saglabā.
                vorbisOptions.EnableInClassList("pa-hidden", !vorbis);
                vorbisOptions.SetEnabled(vorbis);
                hint.text = vorbis
                    ? "PCM → Vorbis. Choose quality from 0 to 1 and review sound quality and decoding CPU cost."
                    : format.value == "ADPCM"
                        ? "PCM → ADPCM. No quality parameter applies. Review the sound and decoding cost."
                        : "Select ADPCM or Vorbis to review the format options.";
            }

            format.RegisterValueChangedCallback(_ => Refresh());
            Refresh();
        }

    }
}
