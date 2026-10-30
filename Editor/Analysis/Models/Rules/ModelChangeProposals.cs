using Judze.ProjectAnalysis.Editor.Analysis.Rules;

namespace Judze.ProjectAnalysis.Editor.Analysis.Models.Rules
{
    // Priekšlikumi ir tikai dati; šajā posmā modeļu izmaiņas netiek piemērotas.
    internal sealed class ModelReadWriteProposal : IChangeProposal
    {
        public string Summary => "Read/Write On → Off for the entire model file, after confirming that all its meshes can operate without CPU access.";
    }
    internal sealed class ModelLodGenerationProposal : IChangeProposal
    {
        public string Summary => "Generate Mesh LODs Off → On for the model file. Review all affected meshes and their uses before applying.";
    }
}
