using System;
namespace Judze.ProjectAnalysis.Editor.Settings
{
    [Serializable]
    internal sealed class Tex001Settings : IRuleSettings
    {
        public bool Enabled = true;
        bool IRuleSettings.Enabled => Enabled;
        public long ResolutionThreshold = 4096;
        public long ResolutionHighThreshold = 8192;
    }
    [Serializable]
    internal sealed class Tex002Settings : IRuleSettings
    {
        public bool Enabled = true;
        bool IRuleSettings.Enabled => Enabled;
        public double CpuDataThresholdMiB = 8;
        public double CpuDataHighThresholdMiB = 32;
    }
    [Serializable]
    internal sealed class Tex003Settings : IRuleSettings
    {
        public bool Enabled = true;
        bool IRuleSettings.Enabled => Enabled;
    }
    [Serializable]
    internal sealed class Tex004Settings : IRuleSettings
    {
        public bool Enabled = true;
        bool IRuleSettings.Enabled => Enabled;
        public double MipDataThresholdMiB = 8;
        public double MipDataHighThresholdMiB = 32;
    }
}
