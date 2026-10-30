using System;
using Judze.ProjectAnalysis.Editor.Analysis.Scope;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Judze.ProjectAnalysis.Editor.UI
{
    internal sealed class ResourceScopeView
    {
        private readonly DropdownField _mode;
        private readonly ObjectField _folder;
        private readonly Label _status;

        public event Action<AnalysisScopeSelection> CheckRequested;
        public event Action SelectionChanged;

        public ResourceScopeView(VisualElement root)
        {
            _mode = root.Q<DropdownField>("scope-mode");
            _folder = root.Q<ObjectField>("scope-folder");
            _status = root.Q<Label>("scope-status");

            var hint = root.Q<Label>("project-scope-hint");
            var removeButton = root.Q<Button>("remove-scope-button");

            _folder.objectType = typeof(DefaultAsset);

            _folder.allowSceneObjects = false;

            _mode.RegisterValueChangedCallback(_ => RefreshMode());

            _folder.RegisterValueChangedCallback(_ => RefreshSelection());

            removeButton.clicked += () =>
            {
                // Noņem tikai izvēli analīzes laukā; projekta resurss netiek dzēsts.
                if (_mode.index == (int)AnalysisScopeMode.SelectedFolder)
                    _folder.value = null;
            };

            root.Q<Button>("check-scope-button").clicked += () =>
                CheckRequested?.Invoke(GetSelection());

            RefreshMode();

            void RefreshMode()
            {
                // Parāda tikai izvēlētajam režīmam vajadzīgo lauku.
                hint.EnableInClassList("pa-hidden", _mode.index != 0);
                _folder.EnableInClassList("pa-hidden", _mode.index != 1);
                removeButton.EnableInClassList("pa-hidden", _mode.index == 0);

                RefreshSelection();
            }

            void RefreshSelection()
            {
                _status.text = string.Empty;
                SelectionChanged?.Invoke();
                // Poga ir pieejama tikai tad, ja pašreizējā režīmā ir izvēlēts resurss.
                bool hasSelection = _mode.index == (int)AnalysisScopeMode.SelectedFolder && _folder.value != null;
                removeButton.SetEnabled(hasSelection);
            }
        }

        public AnalysisScopeSelection GetSelection()
        {
            var mode = (AnalysisScopeMode)_mode.index;
            // Nolasa pašreizējo izvēli, nemainot projekta resursus.
            string path = mode == AnalysisScopeMode.SelectedFolder
                ? AssetDatabase.GetAssetPath(_folder.value)
                : string.Empty;
            return new AnalysisScopeSelection(mode, path);
        }

        public void ShowSummary(string message)
        {
            _status.text = message;
        }
    }
}