using System;
namespace Judze.ProjectAnalysis.Editor.Settings
{
    [Serializable]
    internal sealed class Mod001Settings : IRuleSettings
    {
        public bool Enabled = true;
        bool IRuleSettings.Enabled => Enabled;
        // Divas robežas nosaka Low, Medium un High; vienādība paliek zemākajā līmenī.
        public long VertexThreshold = 10000;
        public long VertexHighThreshold = 40000;
    }
    [Serializable]
    internal sealed class Mod002Settings : IRuleSettings
    {
        public bool Enabled = true;
        bool IRuleSettings.Enabled => Enabled;
        // Atradni veido pēc pirmās robežas pārsniegšanas; otrā robeža nosaka High.
        public long TriangleThreshold = 25000;
        public long TriangleHighThreshold = 100000;
    }
    [Serializable]
    internal sealed class Mod003Settings : IRuleSettings
    {
        public bool Enabled = true;
        bool IRuleSettings.Enabled => Enabled;
    }
    [Serializable]
    internal sealed class Mod004Settings : IRuleSettings
    {
        public bool Enabled = true;
        bool IRuleSettings.Enabled => Enabled;
        // Atlase un risks izmanto abas MOD002 robežvērtības.
    }
}
