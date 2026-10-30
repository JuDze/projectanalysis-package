using System;
using System.Collections.Generic;

namespace Judze.ProjectAnalysis.Editor.Analysis.Rules
{
    // Priekšlikums ir dati; tas nepiemēro izmaiņas un neaizstāj lietotāja apstiprinājumu.
    internal interface IChangeProposal { string Summary { get; } }

    internal sealed class RuleResource
    {
        public string Guid { get; }
        public string Path { get; }
        public long? LocalId { get; }
        public string DisplayName { get; }
        public string Identity => LocalId.HasValue ? Guid + ":" + LocalId.Value : Guid;
        public RuleResource(string guid, string path, long? localId = null, string displayName = null)
        { Guid = guid; Path = path; LocalId = localId; DisplayName = displayName; }
    }

    internal sealed class RuleMeasurement
    {
        public string Name { get; }
        public double? Value { get; }
        public string Unit { get; }
        public RuleMeasurement(string name, double? value, string unit)
        { Name = name; Value = value; Unit = unit; }
    }

    // Nemainīga atradne ar datiem atskaitei un turpmākam labojuma priekšskatījumam.
    internal sealed class RuleResult
    {
        public string RuleId { get; }
        public RuleResource Resource { get; }
        public bool IsProjectFinding => Resource == null;
        public RiskLevel Priority { get; }
        public string Title { get; }
        public string Description { get; }
        public string Recommendation { get; }
        public IReadOnlyList<RuleMeasurement> Measurements { get; }
        public IReadOnlyList<string> Warnings { get; }
        public IReadOnlyList<RuleResource> RelatedResources { get; }
        public IChangeProposal Proposal { get; }

        public RuleResult(string ruleId, RuleResource resource, RiskLevel priority,
            string title, string description, string recommendation,
            IEnumerable<RuleMeasurement> measurements = null, IEnumerable<string> warnings = null,
            IChangeProposal proposal = null, IEnumerable<RuleResource> relatedResources = null)
        {
            // Atradnei jābūt noteiktam riskam; datu trūkums paliek diagnostikā.
            if (!Enum.IsDefined(typeof(RiskLevel), priority))
                throw new ArgumentOutOfRangeException(nameof(priority));
            RuleId = ruleId; Resource = resource; Priority = priority;
            Title = title; Description = description; Recommendation = recommendation;
            Measurements = new List<RuleMeasurement>(measurements ?? Array.Empty<RuleMeasurement>()).AsReadOnly();
            Warnings = new List<string>(warnings ?? Array.Empty<string>()).AsReadOnly();
            RelatedResources = new List<RuleResource>(relatedResources ?? Array.Empty<RuleResource>()).AsReadOnly();
            Proposal = proposal;
        }
    }
}
