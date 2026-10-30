using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;

namespace Judze.ProjectAnalysis.Editor.Analysis.Textures
{
    // Atgriež faktus un lasīšanas problēmas; tie vēl nav noteikumu rezultāti.
    internal sealed class TextureCollectionResult
    {
        public TextureAnalysisContext Context { get; }
        public IReadOnlyList<TextureFacts> Textures { get; }
        public IReadOnlyList<RuleDiagnostic> Diagnostics { get; }

        public TextureCollectionResult(TextureAnalysisContext context,
            IEnumerable<TextureFacts> textures, IEnumerable<RuleDiagnostic> diagnostics)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Textures = new List<TextureFacts>(textures).AsReadOnly();
            Diagnostics = new List<RuleDiagnostic>(diagnostics ?? Array.Empty<RuleDiagnostic>()).AsReadOnly();
        }
    }
}
