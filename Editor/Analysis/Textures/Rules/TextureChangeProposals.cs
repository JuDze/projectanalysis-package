using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Analysis.Textures.Rules
{
    // Tipizēti priekšlikumi; piemērošanu vēlāk veiks atsevišķs labojumu pārvaldnieks.
    internal sealed class TextureResizeProposal : IChangeProposal
    {
        public int CurrentMaxSize { get; }
        public int ProposedMaxSize { get; }
        public bool PlatformOverridden { get; }
        public string Summary => $"Set Standalone Max Size to {ProposedMaxSize}.";
        public TextureResizeProposal(int currentMaxSize, int proposedMaxSize, bool platformOverridden)
        {
            CurrentMaxSize = currentMaxSize;
            ProposedMaxSize = proposedMaxSize;
            PlatformOverridden = platformOverridden;
        }
    }

    internal sealed class TextureReadWriteProposal : IChangeProposal
    {
        public bool CurrentValue => true;
        public bool ProposedValue => false;
        public string Summary => "Disable texture Read/Write after confirming CPU pixel access is unnecessary.";
    }

    internal sealed class TextureMipmapsProposal : IChangeProposal
    {
        public bool CurrentValue => true;
        public bool ProposedValue => false;
        public string Summary => "Disable Generate Mip Maps after reviewing Sprite/UI usage.";
    }

    internal sealed class TextureStreamingProposal : IChangeProposal
    {
        public bool CurrentValue => false;
        public bool ProposedValue => true;
        public string Summary => "Enable Streaming Mipmaps for this texture after reviewing suitability.";
    }
}
