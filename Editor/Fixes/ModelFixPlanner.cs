using System;
using System.Collections.Generic;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Analysis.Models.Rules;
using Judze.ProjectAnalysis.Editor.Analysis.Rules;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal sealed class ModelFixPlanner
    {
        private readonly ModelFixPreviewService _review;
        private readonly IModelFixExecutor _executor;
        private sealed class Changes { public bool? Readable, Lods; }
        private readonly Dictionary<Type, Action<Changes>> _handlers = new Dictionary<Type, Action<Changes>>
        {
            { typeof(ModelReadWriteProposal), c => c.Readable = false },
            { typeof(ModelLodGenerationProposal), c => c.Lods = true }
        };
        public ModelFixPlanner(ModelFixPreviewService review, IModelFixExecutor executor)
        { _review = review ?? throw new ArgumentNullException(nameof(review)); _executor = executor ?? throw new ArgumentNullException(nameof(executor)); }
        public ModelFixPlan Prepare(AnalysisRunResult run, IEnumerable<RuleResult> selected)
        {
            var preview = _review.Prepare(run, selected);
            var assets = new List<ModelAssetFixPlan>();
            foreach (var item in preview.Assets)
            {
                var before = _executor.Capture(item.Model.Guid);
                if (before.ImportVersion != item.Model.ImportVersion || before.Path != item.Model.AssetPath)
                    throw new InvalidOperationException("Model settings changed while preparing the plan. Run analysis again.");
                var changes = new Changes();
                foreach (var finding in item.Findings)
                {
                    if (!_handlers.TryGetValue(finding.Proposal.GetType(), out var handler))
                        throw new NotSupportedException("This model proposal has no supported fix handler.");
                    handler(changes);
                }
                var asset = new ModelAssetFixPlan(before, item, changes.Readable, changes.Lods);
                _executor.Validate(asset, run.Context.TargetPlatform);
                assets.Add(asset);
            }
            return new ModelFixPlan(run, preview, assets);
        }
        public void ValidateContext(ModelFixPlan plan) => _review.Prepare(plan.Baseline, plan.Assets.SelectMany(a => a.Findings));
        public IEnumerable<IAssetFixOperation> CreateOperations(ModelFixPlan plan) =>
            plan.Assets.Select(asset => new ModelFixOperation(asset, plan.Baseline, _executor, _review));
    }
}
