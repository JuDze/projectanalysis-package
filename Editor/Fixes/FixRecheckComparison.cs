using System;
using System.Linq;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal static class FixRecheckComparison
    {
        public static void Fill(FixRecheckRecord record, TextureAnalysisRunResult before, TextureAnalysisRunResult after)
        {
            // Platformas maiņa padara salīdzinājumu nederīgu arī tad, ja ir cita diagnostika.
            TextureFixPlanner.RequireContext(before.Context, after.Context);
            var checkedGuids = new HashSet<string>(after.Textures.Select(t => t.Guid));
            bool incomplete = before.Textures.Any(t => !checkedGuids.Contains(t.Guid)) ||
                before.Evaluation.Diagnostics.Count > 0 || after.Evaluation.Diagnostics.Count > 0;
            FillResults(record, before.Evaluation.Findings, after.Evaluation.Findings, after.Evaluation.Diagnostics,
                incomplete, "Repeated analysis used the original resolved scope and saved rule settings.");
        }

        public static void FillResults(FixRecheckRecord record, IEnumerable<RuleResult> before, IEnumerable<RuleResult> after,
            IEnumerable<RuleDiagnostic> diagnostics, bool incomplete, string successMessage)
        {
            record.BeforeKeys = before.Select(Key).Distinct(StringComparer.Ordinal).ToList();
            record.AfterKeys = after.Select(Key).Distinct(StringComparer.Ordinal).ToList();
            record.Diagnostics = diagnostics.Select(d => d.RuleId + ": " + d.AssetPath + " " + d.Message).ToList();
            // Atkārtota izmantošana nedrīkst atstāt iepriekšējā salīdzinājuma skaitītājus.
            record.HasCounts = false;
            record.BeforeCount = record.AfterCount = record.Resolved = record.Remaining = record.New = 0;
            if (incomplete)
            {
                record.State = FixRecheckState.Incomplete;
                record.Message = "Diagnostics or missing resources prevent a complete before/after comparison. Review the analysis results.";
                return;
            }
            var oldKeys = new HashSet<string>(record.BeforeKeys, StringComparer.Ordinal);
            var newKeys = new HashSet<string>(record.AfterKeys, StringComparer.Ordinal);
            record.HasCounts = true;
            record.BeforeCount = oldKeys.Count; record.AfterCount = newKeys.Count;
            record.Remaining = oldKeys.Count(newKeys.Contains);
            record.Resolved = oldKeys.Count - record.Remaining;
            record.New = newKeys.Count - record.Remaining;
            record.State = FixRecheckState.ResultsAvailable;
            record.Message = successMessage;
        }
        // localId saglabā atsevišķas viena modeļa tīklu vai audio apakšresursu atradnes.
        private static string Key(RuleResult finding) => finding.RuleId + ":" + (finding.Resource?.Identity ?? "project");
    }
}
