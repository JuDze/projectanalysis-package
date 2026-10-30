using System.Collections.Generic;

namespace Judze.ProjectAnalysis.Editor.Analysis.Rules
{
    internal interface IRule<TFacts, TContext>
    {
        string Id { get; }
        // Izvērtē iegūtos datus, neizmantojot resursu ielādes vai izmaiņu API.
        RuleEvaluation Evaluate(IReadOnlyList<TFacts> facts, TContext context);
    }
}
