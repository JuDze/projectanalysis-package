using System;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Parāda projekta kontekstu, neveido iestatījumu melnrakstu un neko nesaglabā.
    internal sealed class AnalysisContextView
    {
        private readonly Label _platformValue;
        private readonly Label _qualityLevelValue;
        public BuildTarget CurrentTarget { get; private set; } = BuildTarget.NoTarget;
        public string TargetLabel => AnalysisBuildTarget.DisplayName(CurrentTarget);

        public AnalysisContextView(VisualElement root, BuildTarget target)
        {
            _platformValue = root.Q<Label>("target-platform-value")
                ?? throw new InvalidOperationException("Missing UI element 'target-platform-value'.");
            _qualityLevelValue = root.Q<Label>("quality-level-value")
                ?? throw new InvalidOperationException("Missing UI element 'quality-level-value'.");
            RefreshTarget(target);
            RefreshQualityLevel();
        }

        public bool RefreshTarget(BuildTarget target)
        {
            target = AnalysisBuildTarget.Normalize(target);
            bool changed = CurrentTarget != target;
            CurrentTarget = target;
            _platformValue.text = TargetLabel;
            return changed;
        }

        public void RefreshQualityLevel()
        {
            // Kvalitātes līmenis paliek informatīvs un nemaina noteikumu robežvērtības.
            int index = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            _qualityLevelValue.text = index >= 0 && index < names.Length ? names[index] : "Unknown";
        }
    }
}
