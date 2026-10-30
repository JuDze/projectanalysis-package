using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio
{
    internal sealed class AudioCollectionResult
    {
        public IReadOnlyList<AudioFacts> Clips { get; }
        public AudioAnalysisContext Context { get; }
        public IReadOnlyList<RuleDiagnostic> Diagnostics { get; }
        public AudioCollectionResult(IEnumerable<AudioFacts> clips, AudioAnalysisContext context,
            IEnumerable<RuleDiagnostic> diagnostics = null)
        {
            Clips = new List<AudioFacts>(clips ?? Array.Empty<AudioFacts>()).AsReadOnly();
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Diagnostics = new List<RuleDiagnostic>(diagnostics ?? Array.Empty<RuleDiagnostic>()).AsReadOnly();
        }
    }
}
