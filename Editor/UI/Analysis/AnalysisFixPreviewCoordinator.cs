using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;
using Judze.ProjectAnalysis.Editor.Fixes;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Vispirms sagatavo visu grupu plānus; apstiprinājums attiecas uz visu kopu.
    internal sealed class AnalysisFixPreviewCoordinator
    {
        private readonly TextureFixPlanner _textures;
        private readonly ModelFixPlanner _models;
        private readonly AudioFixPlanner _audio;
        private readonly ModelFixPreviewMapper _mapper = new ModelFixPreviewMapper();
        public AnalysisFixPreviewCoordinator(TextureFixPlanner textures, ModelFixPlanner models, AudioFixPlanner audio = null)
        {
            _textures = textures ?? throw new ArgumentNullException(nameof(textures));
            _models = models ?? throw new ArgumentNullException(nameof(models));
            _audio = audio;
        }
        public AnalysisFixPreview PrepareAvailable(AnalysisRunResult run, IEnumerable<RuleResult> selected, string savedSettingsJson)
        {
            var findings = selected.Distinct().ToArray();
            // Savietojamu kopu sagatavo vienā reizē; pa datnēm sadala tikai kļūdas gadījumā.
            try { return Prepare(run, findings, savedSettingsJson); }
            catch (Exception) { }
            var parts = new List<AnalysisFixPreview>();
            var skipped = new List<string>();
            foreach (var group in findings.GroupBy(f => f.Resource?.Guid ?? "project"))
            {
                try { parts.Add(Prepare(run, group, savedSettingsJson)); }
                catch (Exception error) { skipped.Add((group.First().Resource?.Path ?? "Project") + ": " + error.Message); }
            }
            string notes = skipped.Count == 0 ? null : "Skipped resources:\n" + string.Join("\n", skipped);
            if (parts.Count == 0) return new AnalysisFixPreview(Array.Empty<FixPreviewRow>(), null, notes ?? "No applicable fixes are selected.");
            var operations = parts.SelectMany(p => p.Plan.Assets).OrderBy(a => a.Category == "Models" ? 0 : 1).ToArray();
            var plan = new FixPlan(run, operations, () => { foreach (var part in parts) part.Plan.ValidateBeforeApply(); }, notes: notes);
            return new AnalysisFixPreview(parts.SelectMany(p => p.Rows), plan);
        }

        public AnalysisFixPreview Prepare(AnalysisRunResult run, IEnumerable<RuleResult> selected, string savedSettingsJson)
        {
            if (run == null || string.IsNullOrEmpty(run.Context.SettingsJson) || run.Context.SettingsJson != savedSettingsJson)
                throw new InvalidOperationException("Saved settings changed. Run analysis again before preparing fixes.");
            var findings = selected?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(selected));
            if (findings.Length == 0) throw new InvalidOperationException("Select at least one proposal.");
            var textures = findings.Where(f => run.TextureRun.Evaluation.Findings.Contains(f)).ToArray();
            var models = findings.Where(f => run.ModelEvaluation.Findings.Contains(f)).ToArray();
            var audio = findings.Where(f => run.AudioEvaluation.Findings.Contains(f)).ToArray();
            if (textures.Length + models.Length + audio.Length != findings.Length)
                throw new InvalidOperationException("The selection contains an unavailable preview or a result from another analysis.");
            var rows = new List<FixPreviewRow>();
            AudioFixPlan audioPlan = null;
            if (audio.Length > 0)
                audioPlan = (_audio ?? throw new InvalidOperationException("Audio preview is unavailable."))
                    .Prepare(run, audio);
            ModelFixPlan modelPlan = null;
            if (models.Length > 0)
            {
                modelPlan = _models.Prepare(run, models);
                rows.AddRange(_mapper.Create(modelPlan.Preview));
            }
            TextureFixPlan texturePlan = null;
            if (textures.Length > 0)
            {
                texturePlan = _textures.Prepare(run.TextureRun, textures);
                rows.InsertRange(0, TextureFixPreviewMapper.CreateFromPlan(texturePlan));
            }
            if (audioPlan != null) rows.AddRange(new AudioFixPreviewMapper().Create(audioPlan.Preview));
            // Modeļus izpilda pirms tekstūrām, kas var būt to importa atkarības.
            var operations = new List<IAssetFixOperation>();
            if (modelPlan != null) operations.AddRange(_models.CreateOperations(modelPlan));
            if (texturePlan != null) operations.AddRange(_textures.CreateOperations(texturePlan));
            if (audioPlan != null) operations.AddRange(_audio.CreateOperations(audioPlan));
            var plan = new FixPlan(run, operations, () =>
            {
                if (modelPlan != null) _models.ValidateContext(modelPlan);
                if (audioPlan != null) _audio.ValidateContext(audioPlan);
            });
            return new AnalysisFixPreview(rows, plan);
        }
    }
}
