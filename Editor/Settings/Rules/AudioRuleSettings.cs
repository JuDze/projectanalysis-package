using System;

namespace Judze.ProjectAnalysis.Editor.Settings
{
    // AUD001 optimizācijas mērķis.
    internal enum AudioOptimizationGoal
    {
        ReduceRamUsage = 0,
        ReducePlaybackCpuLoad = 1
    }

    // AUD002 piedāvātais saspiešanas formāts.
    internal enum AudioCompressionChoice
    {
        NotSelected = 0,
        ADPCM = 1,
        Vorbis = 2
    }

    // AUD001: lietotājs izvēlas vienu optimizācijas mērķi.
    [Serializable]
    internal sealed class Aud001Settings : IRuleSettings
    {
        public bool Enabled = true;
        bool IRuleSettings.Enabled => Enabled;

        public AudioOptimizationGoal Goal =
            AudioOptimizationGoal.ReduceRamUsage;
    }

    // AUD002: saspiešanas formāts un Vorbis kvalitāte.
    [Serializable]
    internal sealed class Aud002Settings : IRuleSettings
    {
        public bool Enabled = true;
        bool IRuleSettings.Enabled => Enabled;

        public AudioCompressionChoice TargetFormat =
            AudioCompressionChoice.NotSelected;

        // Šo vērtību izmanto tikai tad, ja izvēlēts Vorbis.
        public float VorbisQuality = 0.75f;
    }

    // AUD003: importētās frekvences pārbaudes robežvērtība.
    [Serializable]
    internal sealed class Aud003Settings : IRuleSettings
    {
        public bool Enabled = true;
        bool IRuleSettings.Enabled => Enabled;

        public int SampleRateThresholdHz = 48000;
        public int SampleRateHighThresholdHz = 96000;
    }

    // AUD004: mono pārveides pārbaudei nav papildu parametru.
    [Serializable]
    internal sealed class Aud004Settings : IRuleSettings
    {
        public bool Enabled = true;
        bool IRuleSettings.Enabled => Enabled;
    }



    [Serializable]
    internal sealed class Aud005Settings : IRuleSettings
    {
        public bool Enabled = true;
        bool IRuleSettings.Enabled => Enabled;
        // Abas robežas izmanto arī AUD001 RAM mērķis, neatkarīgi no AUD005 ieslēgšanas.
        public long RuntimeSizeThresholdBytes = 5 * 1024 * 1024;
        public long RuntimeSizeHighThresholdBytes = 20 * 1024 * 1024;
    }
}
