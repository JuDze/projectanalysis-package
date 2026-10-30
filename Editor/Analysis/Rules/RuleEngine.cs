using System;
using System.Collections.Generic;

namespace Judze.ProjectAnalysis.Editor.Analysis.Rules
{
    // Dzinējs zina tikai noteikumu saskarni; jauniem noteikumiem nav jāmaina šī klase.
    internal sealed class RuleEngine<TFacts, TContext>
    {
        private readonly IReadOnlyList<IRule<TFacts, TContext>> _rules;
        public RuleEngine(IEnumerable<IRule<TFacts, TContext>> rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            var copy = new List<IRule<TFacts, TContext>>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.Id) || !ids.Add(rule.Id))
                    throw new ArgumentException("Rules must have unique, non-empty identifiers.", nameof(rules));
                copy.Add(rule);
            }
            _rules = copy.AsReadOnly();
        }

        public RuleEvaluation Evaluate(IReadOnlyList<TFacts> facts, TContext context)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            if (ReferenceEquals(context, null)) throw new ArgumentNullException(nameof(context));
            var snapshot = new List<TFacts>(facts).AsReadOnly();
            var findings = new List<RuleResult>();
            var diagnostics = new List<RuleDiagnostic>();
            foreach (var rule in _rules)
            {
                try
                {
                    var result = rule.Evaluate(snapshot, context);
                    if (result == null) throw new InvalidOperationException("The rule returned no evaluation.");
                    findings.AddRange(result.Findings);
                    diagnostics.AddRange(result.Diagnostics);
                }
                catch (Exception exception)
                {
                    // Kļūdu reģistrē; pārējie noteikumi var turpināt darbu.
                    diagnostics.Add(new RuleDiagnostic(rule.Id, null, "Rule evaluation failed: " + exception.Message));
                }
            }
            return new RuleEvaluation(findings, diagnostics);
        }
    }
}
